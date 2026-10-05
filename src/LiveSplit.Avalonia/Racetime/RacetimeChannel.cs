using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options;
using LiveSplit.TimeFormatters;
using LiveSplit.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSplit.Racetime;

public enum ChannelState
{
    Connecting,
    SigningIn,
    Connected,
    Reconnecting,
    Closed
}

/// <summary>
/// A connection to a racetime.gg race room. Keeps the race data and chat up to date, keeps the
/// timer in sync with the race (starting it on the countdown, finishing or resetting it when the
/// entrant finishes or forfeits), relays the runner's splits and shows other entrants' splits as
/// "[Race] name" comparisons. Ported from the Windows LiveSplit.Racetime component.
/// </summary>
/// <remarks>
/// Everything that touches the timer runs on the thread that called <see cref="RunAsync"/>
/// (the UI thread in the app), because the receive loop resumes on its synchronization context.
/// </remarks>
public sealed class RacetimeChannel : IDisposable
{
    private static readonly int[] ReconnectDelays = [0, 5, 5, 10, 10, 10, 15];
    private const int MaxMessageSize = 4 * 1024 * 1024;

    private readonly LiveSplitState state;
    private readonly ITimerModel model;
    private readonly RacetimeSettings settings;
    private readonly RacetimeAuthenticator authenticator;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly HashSet<string> seenMessageIds = [];

    private ClientWebSocket socket;
    private string lastMessageId;
    private bool forfeiting;
    private bool disposed;

    public RacetimeChannel(LiveSplitState state, ITimerModel model, RacetimeSettings settings, RacetimeAuthenticator authenticator, string raceId)
    {
        this.state = state;
        this.model = model;
        this.settings = settings;
        this.authenticator = authenticator;
        RaceId = raceId;

        state.OnSplit += State_OnSplit;
        state.OnUndoSplit += State_OnUndoSplit;
        state.OnReset += State_OnReset;
        state.OnPause += State_OnPause;
    }

    public string RaceId { get; }
    public Race Race { get; private set; }
    public ChannelState State { get; private set; } = ChannelState.Connecting;
    public string StatusText { get; private set; } = "Connecting...";
    public string UserId => authenticator.Identity?.Id;
    public RacetimeUser User => authenticator.Identity;
    public Entrant Me => Race?.FindEntrant(UserId);
    public UserStatus PersonalStatus => GetStatus(Race);

    /// <summary>
    /// Sends a raw message over the socket. Replaced by tests.
    /// </summary>
    internal Func<string, Task> Sender { get; set; }

    public event EventHandler RaceChanged;
    public event EventHandler StateChanged;
    public event EventHandler<ChatMessage> MessageReceived;
    public event EventHandler<string> MessageDeleted;
    public event EventHandler<string> UserPurged;

    private ITimerModel InternalModel => model is DoubleTapPrevention prevention ? prevention.InternalModel : model;

    #region Connection

    /// <summary>
    /// Signs in, connects and keeps the connection alive (reconnecting when it drops) until the
    /// channel is disposed or signing in fails.
    /// </summary>
    public async Task RunAsync()
    {
        if (Sender != null)
        {
            // Tests supply messages themselves; there is no socket to run.
            SetState(ChannelState.Connected, "Connected");
            return;
        }

        int failures = 0;
        bool connectedBefore = false;
        CancellationToken token = lifetime.Token;

        while (!token.IsCancellationRequested && !disposed)
        {
            SetState(ChannelState.SigningIn, authenticator.HasStoredLogin
                ? "Signing in to racetime.gg..."
                : "Waiting for you to sign in to racetime.gg in your web browser...");

            AuthResult auth = await authenticator.AuthorizeAsync(token);
            if (token.IsCancellationRequested)
            {
                break;
            }

            if (auth != AuthResult.Success)
            {
                AddLocalMessage(authenticator.Error ?? "Signing in to racetime.gg was cancelled.", important: true);
                SetState(ChannelState.Closed, "Not signed in.");
                return;
            }

            if (!connectedBefore)
            {
                AddLocalMessage($"Signed in as {authenticator.Identity.DisplayName}.");
            }

            SetState(connectedBefore ? ChannelState.Reconnecting : ChannelState.Connecting, "Connecting to the race room...");

            bool connected = false;
            using (var ws = new ClientWebSocket())
            {
                ws.Options.SetRequestHeader("Authorization", $"Bearer {authenticator.AccessToken}");
                ws.Options.CollectHttpResponseDetails = true;
                try
                {
                    await ws.ConnectAsync(new Uri(RacetimeConfig.RaceSocketUrl(RaceId)), token);
                    connected = true;
                }
                catch (WebSocketException) when (ws.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    AddLocalMessage("This race room does not exist.", important: true);
                    SetState(ChannelState.Closed, "Race not found.");
                    return;
                }
                catch (WebSocketException) when (ws.HttpStatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    authenticator.InvalidateAccessToken();
                }
                catch (Exception ex) when (ex is WebSocketException or System.Net.Http.HttpRequestException)
                {
                    Log.Warning($"racetime.gg connection failed: {ex.Message}");
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (connected)
                {
                    socket = ws;
                    failures = 0;
                    SetState(ChannelState.Connected, "Connected");
                    AddLocalMessage(connectedBefore ? "Reconnected." : $"Joined race room {Race?.Slug ?? RaceId}.");

                    await SendCommand("getrace");
                    if (connectedBefore && lastMessageId != null)
                    {
                        await SendCommand("gethistory", ("last_message", lastMessageId));
                    }
                    else if (!connectedBefore && settings.LoadChatHistory)
                    {
                        await SendCommand("gethistory");
                    }

                    connectedBefore = true;
                    await ReceiveLoop(ws, token);
                    socket = null;
                }
            }

            if (token.IsCancellationRequested || disposed)
            {
                break;
            }

            int delay = ReconnectDelays[Math.Min(failures++, ReconnectDelays.Length - 1)];
            if (connected || failures > 1)
            {
                AddLocalMessage(delay > 0 ? $"Disconnected. Reconnecting in {delay}s..." : "Disconnected. Reconnecting...");
            }

            SetState(ChannelState.Reconnecting, "Reconnecting...");
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delay), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        SetState(ChannelState.Closed, "Disconnected");
    }

    private async Task ReceiveLoop(ClientWebSocket ws, CancellationToken token)
    {
        byte[] buffer = new byte[16 * 1024];
        using var message = new MemoryStream();

        while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(buffer, token);
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
            {
                return;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return;
            }

            message.Write(buffer, 0, result.Count);
            if (message.Length > MaxMessageSize)
            {
                Log.Warning("Dropped an oversized message from racetime.gg.");
                message.SetLength(0);
                continue;
            }

            if (result.EndOfMessage)
            {
                string json = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                message.SetLength(0);
                try
                {
                    HandleMessage(json);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
        }
    }

    private async Task Send(string json)
    {
        if (Sender != null)
        {
            await Sender(json);
            return;
        }

        ClientWebSocket ws = socket;
        if (ws?.State != WebSocketState.Open)
        {
            AddLocalMessage("Not connected to the race room; your action was not sent.", important: true);
            return;
        }

        await sendLock.WaitAsync();
        try
        {
            await ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, lifetime.Token);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            Log.Warning($"Could not send to racetime.gg: {ex.Message}");
        }
        finally
        {
            sendLock.Release();
        }
    }

    internal static string CreateCommand(string action, params (string Key, object Value)[] data)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("action", action);
            if (data.Length > 0)
            {
                writer.WriteStartObject("data");
                foreach ((string key, object value) in data)
                {
                    switch (value)
                    {
                        case bool b:
                            writer.WriteBoolean(key, b);
                            break;
                        case null:
                            writer.WriteNull(key);
                            break;
                        default:
                            writer.WriteString(key, value.ToString());
                            break;
                    }
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public Task SendCommand(string action, params (string Key, object Value)[] data)
    {
        return Send(CreateCommand(action, data));
    }

    /// <summary>
    /// Posts a chat message. Messages starting with "." are race commands (.ready, .done, ...),
    /// which racetime.gg interprets itself.
    /// </summary>
    public Task SendChatMessage(string text)
    {
        text = text?.Trim();
        return string.IsNullOrEmpty(text)
            ? Task.CompletedTask
            : SendCommand("message", ("message", text), ("guid", Guid.NewGuid().ToString()));
    }

    private void SetState(ChannelState newState, string text)
    {
        State = newState;
        StatusText = text;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddLocalMessage(string text, bool important = false)
    {
        MessageReceived?.Invoke(this, ChatMessage.Local(text, important ? ChatMessageKind.Error : ChatMessageKind.LiveSplit, important));
    }

    #endregion

    #region Incoming messages

    internal void HandleMessage(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        DateTime? serverTime = root.Date("date");

        switch (root.Str("type"))
        {
            case "race.data":
                UpdateRace(Race.FromJson(root.Obj("race")), serverTime);
                break;
            case "race.split":
                UpdateSplitComparison(SplitUpdate.FromJson(root.Obj("split")));
                break;
            case "chat.message":
            case "chat.pin":
                AddChatMessage(ChatMessage.FromJson(root.Obj("message")));
                break;
            case "chat.history":
                JsonElement messages = root.Obj("messages");
                if (messages.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement message in messages.EnumerateArray())
                    {
                        AddChatMessage(ChatMessage.FromJson(message));
                    }
                }

                break;
            case "chat.dm":
                RacetimeUser from = RacetimeUser.FromJson(root.Obj("from_user"));
                string sender = from?.DisplayName ?? root.Str("from_bot") ?? "racetime.gg";
                MessageReceived?.Invoke(this, new ChatMessage
                {
                    Kind = ChatMessageKind.User,
                    User = from,
                    BotName = root.Str("from_bot"),
                    PostedAt = DateTime.Now,
                    Text = $"(direct message from {sender}) {root.Str("message")}",
                    Highlight = true,
                    IsDirect = true
                });
                break;
            case "chat.delete":
                string id = root.Obj("delete").Str("id");
                if (id != null)
                {
                    MessageDeleted?.Invoke(this, id);
                }

                break;
            case "chat.purge":
                string userId = root.Obj("purge").Obj("user").Str("id");
                if (userId != null)
                {
                    UserPurged?.Invoke(this, userId);
                }

                break;
            case "error":
                JsonElement errors = root.Obj("errors");
                IEnumerable<string> lines = errors.ValueKind == JsonValueKind.Array
                    ? errors.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText())
                    : [];
                AddLocalMessage(string.Join(" ", lines), important: true);
                break;
        }
    }

    private void AddChatMessage(ChatMessage message)
    {
        if (message.Id != null)
        {
            if (!seenMessageIds.Add(message.Id))
            {
                return;
            }

            lastMessageId = message.Id;
        }

        MessageReceived?.Invoke(this, message);
    }

    private void UpdateRace(Race race, DateTime? serverTime)
    {
        if (race.Id == null || (Race != null && race.Version < Race.Version))
        {
            return;
        }

        Race previous = Race;
        Race = race;

        UpdateRaceComparisons(race);
        SyncTimer(previous, race, serverTime);
        RaceChanged?.Invoke(this, EventArgs.Empty);
    }

    private UserStatus GetStatus(Race race)
    {
        if (race == null || UserId == null)
        {
            return UserStatus.Unknown;
        }

        return race.FindEntrant(UserId)?.Status ?? UserStatus.NotInRace;
    }

    /// <summary>
    /// Brings the timer in line with the race when the race or the runner's status changes.
    /// </summary>
    private void SyncTimer(Race previous, Race race, DateTime? serverTime)
    {
        RaceState oldState = previous?.State ?? RaceState.Unknown;
        UserStatus oldStatus = GetStatus(previous);
        RaceState newState = race.State;
        UserStatus newStatus = GetStatus(race);

        if (oldState == newState && oldStatus == newStatus)
        {
            return;
        }

        ITimerModel timer = InternalModel;
        TimerPhase phase = state.CurrentPhase;
        bool running = phase is TimerPhase.Running or TimerPhase.Paused;

        if ((newState == RaceState.Starting && newStatus == UserStatus.Ready)
            || (newState == RaceState.Started && newStatus == UserStatus.Racing))
        {
            if (phase == TimerPhase.NotRunning)
            {
                // Started at the race's start time; negative during the countdown.
                TimeSpan elapsed = race.StartedAt is DateTime startedAt
                    ? (serverTime ?? DateTime.UtcNow) - startedAt
                    : -race.StartDelay;
                StartTimerAt(timer, elapsed);
            }
            else if (phase == TimerPhase.Ended && oldStatus == UserStatus.Finished)
            {
                // The finish was undone on racetime.gg.
                timer.UndoSplit();
            }
            else if (phase == TimerPhase.Paused)
            {
                timer.Pause();
            }
        }
        else if (newStatus == UserStatus.Finished && running)
        {
            // Finished on racetime.gg (e.g. ".done" or the Done button): end the timer.
            if (phase == TimerPhase.Paused)
            {
                timer.Pause();
            }

            while (state.CurrentSplitIndex < state.Run.Count - 1)
            {
                timer.SkipSplit();
            }

            timer.Split();
        }
        else if (running && newStatus is UserStatus.Forfeit or UserStatus.Disqualified)
        {
            timer.Reset();
        }
        else if (newState == RaceState.Cancelled && oldState == RaceState.Starting && running)
        {
            // Cancelled during the countdown: discard the attempt.
            timer.Reset(false);
        }
    }

    private void StartTimerAt(ITimerModel timer, TimeSpan elapsed)
    {
        // The timer starts at the run's offset. Use the race's elapsed time for this start
        // only, so that the offset saved in the splits is left unchanged.
        TimeSpan offset = state.Run.Offset;
        state.Run.Offset = elapsed;
        try
        {
            timer.Start();
        }
        finally
        {
            state.Run.Offset = offset;
        }
    }

    #endregion

    #region Race comparisons

    private void UpdateRaceComparisons(Race race)
    {
        foreach (Entrant entrant in race.Entrants)
        {
            if (entrant.User?.Id == null || entrant.User.Id == UserId)
            {
                continue;
            }

            string name = entrant.User.DisplayName;
            string comparisonName = RacetimeComparisonGenerator.GetRaceComparisonName(name);
            if (state.Run.ComparisonGenerators.All(x => x.Name != comparisonName))
            {
                CompositeComparisons.AddShortComparisonName(comparisonName, name);
                state.Run.ComparisonGenerators.Add(new RacetimeComparisonGenerator(comparisonName));
            }
        }
    }

    private void UpdateSplitComparison(SplitUpdate split)
    {
        if (split.UserId == null || split.UserId == UserId || state.Run.Count == 0)
        {
            return;
        }

        RacetimeUser user = Race?.Entrants.FirstOrDefault(x => x.User?.Id == split.UserId)?.User;
        if (user == null)
        {
            return;
        }

        string comparisonName = RacetimeComparisonGenerator.GetRaceComparisonName(user.DisplayName);
        bool Matches(ISegment segment)
        {
            return segment.Name.Trim().Equals(split.SplitName, StringComparison.OrdinalIgnoreCase);
        }

        ISegment target = split.IsFinish
            ? state.Run[^1]
            : split.IsUndo
                ? state.Run.LastOrDefault(x => Matches(x) && x.Comparisons[comparisonName][TimingMethod.RealTime] != null)
                : state.Run.FirstOrDefault(x => Matches(x) && x.Comparisons[comparisonName][TimingMethod.RealTime] == null);

        if (target != null)
        {
            var time = new Time(target.Comparisons[comparisonName]);
            time[TimingMethod.RealTime] = split.IsUndo ? null : split.SplitTime;
            target.Comparisons[comparisonName] = time;
        }
    }

    public void RemoveRaceComparisons()
    {
        if (RacetimeComparisonGenerator.IsRaceComparison(state.CurrentComparison))
        {
            state.CurrentComparison = Run.PersonalBestComparisonName;
        }

        for (int i = state.Run.ComparisonGenerators.Count - 1; i >= 0; i--)
        {
            if (RacetimeComparisonGenerator.IsRaceComparison(state.Run.ComparisonGenerators[i].Name))
            {
                state.Run.ComparisonGenerators.RemoveAt(i);
            }
        }

        foreach (ISegment segment in state.Run)
        {
            foreach (string name in segment.Comparisons.Keys.Where(RacetimeComparisonGenerator.IsRaceComparison).ToList())
            {
                segment.Comparisons[name] = default;
            }
        }
    }

    #endregion

    #region Timer events

    private void State_OnSplit(object sender, EventArgs e)
    {
        if (PersonalStatus != UserStatus.Racing || state.CurrentSplitIndex <= 0)
        {
            return;
        }

        ISegment split = state.Run[state.CurrentSplitIndex - 1];
        bool isFinish = state.CurrentSplitIndex >= state.Run.Count;
        string time = new RegularTimeFormatter(TimeAccuracy.Hundredths).Format(split.SplitTime.RealTime);
        _ = SendCommand("split", ("split", split.Name), ("time", time), ("is_finish", isFinish));

        if (isFinish)
        {
            _ = SendCommand("done");
        }
    }

    private void State_OnUndoSplit(object sender, EventArgs e)
    {
        if (PersonalStatus == UserStatus.Finished && state.CurrentSplitIndex == state.Run.Count - 1)
        {
            // Undoing the final split takes back the finish.
            _ = SendCommand("undone");
        }
        else if (PersonalStatus == UserStatus.Racing && state.CurrentSplit != null)
        {
            _ = SendCommand("split", ("split", state.CurrentSplit.Name), ("time", "-"));
        }
    }

    private void State_OnReset(object sender, TimerPhase e)
    {
        if (PersonalStatus == UserStatus.Racing && !forfeiting)
        {
            _ = SendCommand("forfeit");
        }
    }

    private void State_OnPause(object sender, EventArgs e)
    {
        // Races can't be paused.
        if (state.CurrentPhase == TimerPhase.Paused
            && (PersonalStatus == UserStatus.Racing || (PersonalStatus == UserStatus.Ready && Race?.State == RaceState.Starting)))
        {
            InternalModel.Pause();
        }
    }

    #endregion

    #region Race actions

    /// <summary>
    /// Forfeits the race and resets the timer if it is running, so the attempt is recorded like
    /// any other reset. Completes once the forfeit has been sent.
    /// </summary>
    public async Task Forfeit()
    {
        if (state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused)
        {
            forfeiting = true;
            try
            {
                InternalModel.Reset();
            }
            finally
            {
                forfeiting = false;
            }
        }

        await SendCommand("forfeit");
    }

    #endregion

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        state.OnSplit -= State_OnSplit;
        state.OnUndoSplit -= State_OnUndoSplit;
        state.OnReset -= State_OnReset;
        state.OnPause -= State_OnPause;

        RemoveRaceComparisons();

        // Say goodbye before tearing the connection down; cancelling aborts the socket.
        ClientWebSocket ws = socket;
        if (ws?.State == WebSocketState.Open)
        {
            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            _ = ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token)
                .ContinueWith(t =>
                {
                    _ = t.Exception;
                    timeout.Dispose();
                    lifetime.Cancel();
                }, TaskScheduler.Default);
        }
        else
        {
            lifetime.Cancel();
        }
    }
}

/// <summary>
/// Placeholder generator for another entrant's splits. The times are filled in directly by
/// <see cref="RacetimeChannel"/> as splits come in.
/// </summary>
internal sealed class RacetimeComparisonGenerator(string name) : IComparisonGenerator
{
    private const string Prefix = "[Race] ";

    public IRun Run { get; set; }
    public string Name { get; } = name;

    public void Generate(ISettings settings) { }

    public static string GetRaceComparisonName(string user)
    {
        return Prefix + user;
    }

    public static bool IsRaceComparison(string comparison)
    {
        return comparison?.StartsWith(Prefix) == true;
    }
}
