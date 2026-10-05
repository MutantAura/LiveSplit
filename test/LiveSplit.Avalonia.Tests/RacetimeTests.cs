using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.Options.SettingsSavers;
using LiveSplit.Racetime;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.View;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using Xunit;

namespace LiveSplit.Tests;

public class RacetimeModelTests
{
    [Fact]
    public void ParsesRaceList()
    {
        IReadOnlyList<Race> races = RacetimeAPI.ParseRaces("""
            {"races": [
              {"name": "smw/clever-yoshi-1234", "status": {"value": "open", "verbose_value": "Open"},
               "url": "/smw/clever-yoshi-1234", "goal": {"name": "Any%", "custom": false}, "info": "",
               "entrants_count": 3, "entrants_count_finished": 0, "entrants_count_inactive": 0,
               "opened_at": "2026-10-05T10:00:00.000Z", "started_at": null,
               "category": {"name": "Super Mario World", "slug": "smw"}},
              {"name": "oot/fast-link-0001", "status": {"value": "in_progress", "verbose_value": "In progress"},
               "goal": {"name": "Beat the game", "custom": true},
               "entrants_count": 5, "entrants_count_finished": 2, "entrants_count_inactive": 1,
               "started_at": "2026-10-05T09:00:00+00:00",
               "category": {"name": "Ocarina of Time", "slug": "oot"}}
            ]}
            """);

        Assert.Equal(2, races.Count);

        IRaceInfo open = races[0];
        Assert.Equal("smw/clever-yoshi-1234", open.Id);
        Assert.Equal("Super Mario World", open.GameName);
        Assert.Equal("Any%", open.Goal);
        Assert.Equal(1, open.State);
        Assert.Equal(3, open.NumEntrants);

        IRaceInfo running = races[1];
        Assert.Equal(3, running.State);
        Assert.Equal(2, running.Finishes);
        Assert.Equal(1, running.Forfeits);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), running.Starttime);
    }

    [Fact]
    public void ParsesRaceDetails()
    {
        Race race = RacetimeTestData.Race("in_progress", ("me", "Me", "done", "P0DT01H02M03.450000S"), ("other", "Other", "in_progress", null));

        Assert.Equal(RaceState.Started, race.State);
        Assert.Equal(TimeSpan.FromSeconds(15), race.StartDelay);
        Assert.Equal("race", race.Slug);

        Entrant me = race.FindEntrant("me");
        Assert.Equal(UserStatus.Finished, me.Status);
        Assert.Equal(new TimeSpan(0, 1, 2, 3, 450), me.FinishTime);
        Assert.Equal("1st", me.PlaceOrdinal);
        Assert.Equal(UserStatus.Racing, race.FindEntrant("other").Status);
        Assert.Contains("undone", me.Actions);
    }

    [Fact]
    public void CreatesCommands()
    {
        string json = RacetimeChannel.CreateCommand("split", ("split", "Level \"1\""), ("time", "1:02.50"), ("is_finish", false));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.Equal("split", root.GetProperty("action").GetString());
        Assert.Equal("Level \"1\"", root.GetProperty("data").GetProperty("split").GetString());
        Assert.False(root.GetProperty("data").GetProperty("is_finish").GetBoolean());

        Assert.Equal("""{"action":"ready"}""", RacetimeChannel.CreateCommand("ready"));
    }

    [Fact]
    public void ParsesOAuthRedirect()
    {
        Dictionary<string, string> query = RacetimeAuthenticator.ParseRequestLine("GET /?code=abc-123&state=x_y%3D HTTP/1.1");
        Assert.Equal("abc-123", query["code"]);
        Assert.Equal("x_y=", query["state"]);

        Assert.Empty(RacetimeAuthenticator.ParseRequestLine("GET /favicon.ico HTTP/1.1"));
        Assert.Empty(RacetimeAuthenticator.ParseRequestLine(""));
    }

    [Fact]
    public void SettingsRoundTripWithPluginName()
    {
        var document = new XmlDocument();
        XmlElement element = new RacetimeSettings { Enabled = false, LoadChatHistory = false }.ToXml(document);
        Assert.Equal(RacetimeSettings.PluginName, element.GetAttribute("name"));

        var loaded = new RacetimeSettings();
        loaded.FromXml(element, new Version(1, 8, 30));
        Assert.False(loaded.Enabled);
        Assert.False(loaded.LoadChatHistory);
    }

    [Fact]
    public void UnknownRaceProvidersKeepTheirSettings()
    {
        var document = new XmlDocument();
        document.LoadXml("""<Plugin name="Other.dll" enabled="False"><Setting>42</Setting></Plugin>""");

        var settings = new UnloadedRaceProviderSettings();
        settings.FromXml(document.DocumentElement, new Version(1, 8, 30));
        XmlElement saved = ((RaceProviderSettings)settings.Clone()).ToXml(new XmlDocument());

        Assert.Equal("Other.dll", saved.GetAttribute("name"));
        Assert.Equal("False", saved.GetAttribute("enabled"));
        Assert.Equal("<Setting>42</Setting>", saved.InnerXml);
    }

    [Fact]
    public void TokenStoreRoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"racetime-{Guid.NewGuid()}.json");
        try
        {
            new RacetimeTokenStore(path).Set("access", "refresh");

            var store = new RacetimeTokenStore(path);
            Assert.Equal("access", store.AccessToken);
            Assert.Equal("refresh", store.RefreshToken);

            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            }

            store.Clear();
            Assert.False(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class RacetimeChannelTests
{
    private readonly LiveSplitState state;
    private readonly ITimerModel model;
    private readonly RacetimeChannel channel;
    private readonly List<JsonElement> sent = [];

    public RacetimeChannelTests()
    {
        IRun run = new Run(new StandardComparisonGeneratorsFactory());
        run.AddSegment("Level 1");
        run.AddSegment("Level 2");
        run.AddSegment("Level 3");
        run.Offset = TimeSpan.FromSeconds(-1);

        ISettings settings = new StandardSettingsFactory().Create();
        state = new LiveSplitState(run, null, null, null, settings) { CurrentHotkeyProfile = settings.HotkeyProfiles.First().Key };
        model = new DoubleTapPrevention(new TimerModel()) { CurrentState = state };

        var authenticator = new RacetimeAuthenticator(new HttpClient(), new RacetimeTokenStore(Path.Combine(Path.GetTempPath(), $"racetime-{Guid.NewGuid()}.json")), _ => false)
        {
            Identity = new RacetimeUser { Id = "me", Name = "Me" }
        };

        channel = new RacetimeChannel(state, model, new RacetimeSettings(), authenticator, "game/race")
        {
            Sender = json =>
            {
                sent.Add(JsonDocument.Parse(json).RootElement.Clone());
                return Task.CompletedTask;
            }
        };
    }

    private IEnumerable<string> SentActions => sent.Select(x => x.GetProperty("action").GetString());

    private void Receive(string raceStatus, string myStatus, DateTime? serverTime = null, int version = 1)
    {
        channel.HandleMessage(RacetimeTestData.RaceMessage(raceStatus, version, serverTime, ("me", "Me", myStatus, null), ("other", "Other", "in_progress", null)));
    }

    [Fact]
    public void StartsTimerWithCountdownWhenRaceStarts()
    {
        DateTime now = DateTime.UtcNow;
        Receive("open", "ready", now);
        Assert.Equal(TimerPhase.NotRunning, state.CurrentPhase);

        Receive("pending", "ready", now, version: 2);

        Assert.Equal(TimerPhase.Running, state.CurrentPhase);
        TimeSpan time = state.CurrentTime.RealTime.Value;
        Assert.InRange(time.TotalSeconds, -15.5, -14);

        // The splits' own offset is left as it was.
        Assert.Equal(TimeSpan.FromSeconds(-1), state.Run.Offset);
    }

    [Fact]
    public void JoiningARunningRaceStartsTimerAtElapsedTime()
    {
        DateTime serverNow = RacetimeTestData.StartedAt.AddMinutes(2);
        Receive("in_progress", "in_progress", serverNow);

        Assert.Equal(TimerPhase.Running, state.CurrentPhase);
        Assert.InRange(state.CurrentTime.RealTime.Value.TotalSeconds, 119, 122);
    }

    [Fact]
    public void RelaysSplitsAndFinish()
    {
        Receive("in_progress", "in_progress", RacetimeTestData.StartedAt.AddSeconds(10));
        sent.Clear();

        model.Split();
        JsonElement split = Assert.Single(sent);
        Assert.Equal("split", split.GetProperty("action").GetString());
        Assert.Equal("Level 1", split.GetProperty("data").GetProperty("split").GetString());
        Assert.False(split.GetProperty("data").GetProperty("is_finish").GetBoolean());


        model.CurrentState.Settings.HotkeyProfiles.First().Value.DoubleTapPrevention = false;
        model.Split();
        model.Split();

        Assert.Equal(TimerPhase.Ended, state.CurrentPhase);
        Assert.Equal(new[] { "split", "split", "split", "done" }, SentActions);
        Assert.True(sent[2].GetProperty("data").GetProperty("is_finish").GetBoolean());
    }

    [Fact]
    public void ResetWhileRacingForfeits()
    {
        Receive("in_progress", "in_progress", RacetimeTestData.StartedAt.AddSeconds(10));
        sent.Clear();

        model.Reset();

        Assert.Equal(new[] { "forfeit" }, SentActions);
    }

    [Fact]
    public async Task ForfeitResetsTimerAndSendsOnce()
    {
        Receive("in_progress", "in_progress", RacetimeTestData.StartedAt.AddSeconds(10));
        sent.Clear();

        await channel.Forfeit();

        Assert.Equal(TimerPhase.NotRunning, state.CurrentPhase);
        Assert.Equal(new[] { "forfeit" }, SentActions);
    }

    [Fact]
    public void FinishingOnRacetimeEndsTimer()
    {
        Receive("in_progress", "in_progress", RacetimeTestData.StartedAt.AddSeconds(10));
        Receive("in_progress", "done", RacetimeTestData.StartedAt.AddSeconds(20), version: 2);

        Assert.Equal(TimerPhase.Ended, state.CurrentPhase);
        Assert.DoesNotContain("done", SentActions);
    }

    [Fact]
    public void PausingIsNotAllowedWhileRacing()
    {
        Receive("in_progress", "in_progress", RacetimeTestData.StartedAt.AddSeconds(10));

        model.Pause();

        Assert.Equal(TimerPhase.Running, state.CurrentPhase);
    }

    [Fact]
    public void IgnoresOutdatedRaceData()
    {
        Receive("in_progress", "in_progress", RacetimeTestData.StartedAt.AddSeconds(10), version: 5);
        Receive("open", "ready", version: 4);

        Assert.Equal(RaceState.Started, channel.Race.State);
    }

    [Fact]
    public void OtherEntrantsSplitsBecomeComparisons()
    {
        Receive("in_progress", "in_progress", RacetimeTestData.StartedAt.AddSeconds(10));
        const string comparison = "[Race] Other";
        Assert.Contains(comparison, state.Run.Comparisons);
        Assert.DoesNotContain("[Race] Me", state.Run.Comparisons);

        channel.HandleMessage("""{"type":"race.split","split":{"split_name":"level 2","split_time":"1:02.50","is_undo":false,"is_finish":false,"user_id":"other"}}""");
        Assert.Equal(new TimeSpan(0, 0, 1, 2, 500), state.Run[1].Comparisons[comparison].RealTime);

        channel.HandleMessage("""{"type":"race.split","split":{"split_name":"level 2","split_time":"-","is_undo":true,"is_finish":false,"user_id":"other"}}""");
        Assert.Null(state.Run[1].Comparisons[comparison].RealTime);

        channel.HandleMessage("""{"type":"race.split","split":{"split_name":"whatever","split_time":"5:00.00","is_undo":false,"is_finish":true,"user_id":"other"}}""");
        Assert.Equal(TimeSpan.FromMinutes(5), state.Run[^1].Comparisons[comparison].RealTime);

        state.CurrentComparison = comparison;
        channel.Dispose();
        Assert.DoesNotContain(comparison, state.Run.Comparisons);
        Assert.Equal(Run.PersonalBestComparisonName, state.CurrentComparison);
        Assert.Null(state.Run[^1].Comparisons[comparison].RealTime);
    }

    [Fact]
    public void ReportsChatModerationAndErrors()
    {
        var received = new List<ChatMessage>();
        var deleted = new List<string>();
        channel.MessageReceived += (s, m) => received.Add(m);
        channel.MessageDeleted += (s, id) => deleted.Add(id);

        string message = """{"id":"m1","user":{"id":"other","name":"Other","full_name":"Other#1"},"posted_at":"2026-10-05T10:00:00Z","message":"<b>hi</b>","message_plain":"hi","is_system":false,"is_bot":false}""";
        channel.HandleMessage($$"""{"type":"chat.history","messages":[{{message}}]}""");
        channel.HandleMessage($$"""{"type":"chat.message","message":{{message}}}""");
        channel.HandleMessage("""{"type":"chat.delete","delete":{"id":"m1"}}""");
        channel.HandleMessage("""{"type":"error","errors":["You cannot .ready at this time."]}""");

        Assert.Equal(2, received.Count);
        Assert.Equal("hi", received[0].Text);
        Assert.Equal("Other#1", received[0].Author);
        Assert.Equal(ChatMessageKind.Error, received[1].Kind);
        Assert.Equal(new[] { "m1" }, deleted);
    }
}

public class RaceRoomWindowTests
{
    [AvaloniaFact]
    public void ShowsRaceEntrantsAndChat()
    {
        IRun run = new Run(new StandardComparisonGeneratorsFactory());
        run.AddSegment("Level 1");
        ISettings settings = new StandardSettingsFactory().Create();
        var state = new LiveSplitState(run, null, null, null, settings);
        var model = new TimerModel { CurrentState = state };

        var authenticator = new RacetimeAuthenticator(new HttpClient(), new RacetimeTokenStore(Path.Combine(Path.GetTempPath(), $"racetime-{Guid.NewGuid()}.json")), _ => false)
        {
            Identity = new RacetimeUser { Id = "me", Name = "Me" }
        };
        var sent = new List<string>();
        var channel = new RacetimeChannel(state, model, new RacetimeSettings(), authenticator, "game/race")
        {
            Sender = json =>
            {
                sent.Add(json);
                return Task.CompletedTask;
            }
        };
        var window = new RaceRoomWindow(channel);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        channel.HandleMessage(RacetimeTestData.RaceMessage("open", 1, null, ("me", "Me", "not_ready", null), ("other", "Other", "ready", null)));
        channel.HandleMessage("""{"type":"chat.message","message":{"id":"m0","user":null,"is_system":true,"message_plain":"Other is ready! (1 remaining)","highlight":false}}""");
        channel.HandleMessage("""{"type":"chat.message","message":{"id":"m1","user":{"id":"other","name":"Other"},"message_plain":"glhf"}}""");
        channel.HandleMessage("""{"type":"error","errors":["You cannot .done at this time."]}""");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Any% [Game] - race", window.Title);
        List<string> texts = [.. window.GetLogicalDescendants().OfType<TextBlock>().Select(x => x.Text)];
        Assert.Contains("Other", texts);
        Assert.Contains(window.GetLogicalDescendants().OfType<SelectableTextBlock>(), x => x.Inlines.Text.Contains("glhf"));

        List<Button> actions = [.. window.GetLogicalDescendants().OfType<Button>().Where(x => x.Content is "Ready" or "Leave Race")];
        Assert.Equal(2, actions.Count);

        for (int i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        using (WriteableBitmap frame = window.CaptureRenderedFrame())
        {
            frame.Save(Path.Combine(Fixtures.ScreenshotDirectory, "race-room.png"));
        }

        actions.First(x => x.Content is "Ready").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("""{"action":"ready"}""", Assert.Single(sent));

        window.Close();
    }
}

internal static class RacetimeTestData
{
    public static DateTime StartedAt { get; } = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    public static Race Race(string status, params (string Id, string Name, string Status, string FinishTime)[] entrants)
    {
        using JsonDocument document = JsonDocument.Parse(RaceMessage(status, 1, null, entrants));
        return Racetime.Race.FromJson(document.RootElement.GetProperty("race"));
    }

    public static string RaceMessage(string status, int version, DateTime? serverTime, params (string Id, string Name, string Status, string FinishTime)[] entrants)
    {
        // During the countdown the race's start time lies in the future.
        DateTime? startedAt = status switch
        {
            "pending" => (serverTime ?? DateTime.UtcNow).AddSeconds(15),
            "in_progress" or "finished" => StartedAt,
            _ => null
        };

        string entrantJson = string.Join(",", entrants.Select((e, i) => $$"""
            {"user": {"id": "{{e.Id}}", "name": "{{e.Name}}", "full_name": "{{e.Name}}"},
             "status": {"value": "{{e.Status}}", "verbose_value": "{{e.Status}}"},
             "finish_time": {{(e.FinishTime == null ? "null" : $"\"{e.FinishTime}\"")}},
             "place": {{(e.Status == "done" ? i + 1 : "null")}},
             "place_ordinal": {{(e.Status == "done" ? "\"1st\"" : "null")}},
             "comment": null, "stream_live": false,
             "actions": {{(e.Status == "done" ? "[\"undone\"]" : e.Status == "not_ready" ? "[\"ready\", \"leave\"]" : "[]")}}}
            """));

        return $$"""
            {"type": "race.data",
             "date": {{(serverTime == null ? "null" : $"\"{serverTime:O}\"")}},
             "race": {"version": {{version}}, "name": "game/race", "url": "/game/race",
               "status": {"value": "{{status}}", "verbose_value": "{{status}}"},
               "category": {"name": "Game", "slug": "game"},
               "goal": {"name": "Any%", "custom": false}, "info": "",
               "entrants_count": {{entrants.Length}}, "entrants_count_finished": 0, "entrants_count_inactive": 0,
               "start_delay": "P0DT00H00M15S",
               "started_at": {{(startedAt == null ? "null" : $"\"{startedAt:O}\"")}},
               "entrants": [{{entrantJson}}]
             }
            }
            """;
    }
}
