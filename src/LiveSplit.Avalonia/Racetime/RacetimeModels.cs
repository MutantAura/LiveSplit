using LiveSplit.Model;
using LiveSplit.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace LiveSplit.Racetime;

public enum RaceState
{
    Unknown,
    Open,
    OpenInviteOnly,
    Starting,
    Started,
    Ended,
    Cancelled
}

public enum UserStatus
{
    Unknown,
    NotInRace,
    Requested,
    Invited,
    Declined,
    NotReady,
    Ready,
    Racing,
    Finished,
    Forfeit,
    Disqualified
}

/// <summary>
/// A user as described by racetime.gg's user summaries (race entrants, chat authors, userinfo).
/// </summary>
public sealed class RacetimeUser
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string FullName { get; init; }
    public string Flair { get; init; }
    public string TwitchName { get; init; }
    public bool CanModerate { get; init; }

    public string DisplayName => string.IsNullOrEmpty(FullName) ? Name : FullName;

    public static RacetimeUser FromJson(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new RacetimeUser
        {
            Id = e.Str("id"),
            Name = e.Str("name") ?? "",
            FullName = e.Str("full_name"),
            Flair = e.Str("flair"),
            TwitchName = e.Str("twitch_name"),
            CanModerate = e.Bool("can_moderate")
        };
    }
}

public sealed class Entrant
{
    public RacetimeUser User { get; init; }
    public string StatusValue { get; init; }
    public string StatusText { get; init; }
    public int? Place { get; init; }
    public string PlaceOrdinal { get; init; }
    public TimeSpan? FinishTime { get; init; }
    public string Comment { get; init; }
    public bool StreamLive { get; init; }
    public IReadOnlyList<string> Actions { get; init; } = [];

    public UserStatus Status => StatusValue switch
    {
        "requested" => UserStatus.Requested,
        "invited" => UserStatus.Invited,
        "declined" => UserStatus.Declined,
        "not_ready" => UserStatus.NotReady,
        "ready" => UserStatus.Ready,
        "in_progress" => UserStatus.Racing,
        "done" => UserStatus.Finished,
        "dnf" => UserStatus.Forfeit,
        "dq" => UserStatus.Disqualified,
        _ => UserStatus.Unknown
    };

    public static Entrant FromJson(JsonElement e)
    {
        JsonElement status = e.Obj("status");
        return new Entrant
        {
            User = RacetimeUser.FromJson(e.Obj("user")),
            StatusValue = status.Str("value"),
            StatusText = status.Str("verbose_value"),
            Place = e.Int("place"),
            PlaceOrdinal = e.Str("place_ordinal"),
            FinishTime = e.Duration("finish_time"),
            Comment = e.Str("comment"),
            StreamLive = e.Bool("stream_live"),
            Actions = e.Strings("actions")
        };
    }
}

/// <summary>
/// A race, parsed from the race data sent over the race WebSocket or listed by /races/data.
/// </summary>
public sealed class Race : IRaceInfo
{
    public string Id { get; init; }
    public string Url { get; init; }
    public string StatusValue { get; init; }
    public string StatusText { get; init; }
    public string GameName { get; init; }
    public string GameId { get; init; }
    public string Goal { get; init; }
    public string Info { get; init; }
    public int Version { get; init; }
    public int NumEntrants { get; init; }
    public int Finishes { get; init; }
    public int Forfeits { get; init; }
    public DateTime? StartedAt { get; init; }
    public TimeSpan StartDelay { get; init; }
    public bool ChatRestricted { get; init; }
    public bool AllowComments { get; init; }
    public bool StreamingRequired { get; init; }
    public IReadOnlyList<Entrant> Entrants { get; init; } = [];

    public string Slug => Id?[(Id.IndexOf('/') + 1)..];

    public RaceState State => StatusValue switch
    {
        "open" => RaceState.Open,
        "invitational" => RaceState.OpenInviteOnly,
        "pending" => RaceState.Starting,
        "in_progress" => RaceState.Started,
        "finished" => RaceState.Ended,
        "cancelled" or "partitioned" => RaceState.Cancelled,
        _ => RaceState.Unknown
    };

    public bool IsPreparing => State is RaceState.Open or RaceState.OpenInviteOnly;

    public Entrant FindEntrant(string userId)
    {
        return userId == null ? null : Entrants.FirstOrDefault(x => x.User?.Id == userId);
    }

    public static Race FromJson(JsonElement e)
    {
        JsonElement status = e.Obj("status");
        JsonElement category = e.Obj("category");
        JsonElement entrants = e.Obj("entrants");

        return new Race
        {
            Id = e.Str("name"),
            Url = e.Str("url"),
            StatusValue = status.Str("value"),
            StatusText = status.Str("verbose_value"),
            GameName = category.Str("name"),
            GameId = category.Str("slug"),
            Goal = e.Obj("goal").Str("name"),
            Info = e.Str("info"),
            Version = e.Int("version") ?? 0,
            NumEntrants = e.Int("entrants_count") ?? 0,
            Finishes = e.Int("entrants_count_finished") ?? 0,
            Forfeits = e.Int("entrants_count_inactive") ?? 0,
            StartedAt = e.Date("started_at"),
            StartDelay = e.Duration("start_delay") ?? TimeSpan.Zero,
            ChatRestricted = e.Bool("chat_restricted"),
            AllowComments = e.Bool("allow_comments"),
            StreamingRequired = e.Bool("streaming_required"),
            Entrants = entrants.ValueKind == JsonValueKind.Array
                ? [.. entrants.EnumerateArray().Select(Entrant.FromJson)]
                : []
        };
    }

    int IRaceInfo.State => IsPreparing ? 1 : (State == RaceState.Started ? 3 : 42);

    int IRaceInfo.Starttime => StartedAt is DateTime start ? (int)(start - DateTime.UnixEpoch).TotalSeconds : 0;

    IEnumerable<string> IRaceInfo.LiveStreams => Entrants
        .Where(x => x.Status == UserStatus.Racing && !string.IsNullOrEmpty(x.User?.TwitchName))
        .Select(x => x.User.TwitchName);

    bool IRaceInfo.IsParticipant(string username)
    {
        return true;
    }
}

public enum ChatMessageKind
{
    User,
    Bot,
    System,
    Error,
    LiveSplit
}

public sealed class ChatMessage
{
    public string Id { get; init; }
    public ChatMessageKind Kind { get; init; }
    public RacetimeUser User { get; init; }
    public string BotName { get; init; }
    public DateTime PostedAt { get; init; }
    public string Text { get; init; }
    public bool Highlight { get; init; }
    public bool IsPinned { get; init; }
    public bool IsDirect { get; init; }

    public string Author => Kind switch
    {
        ChatMessageKind.User => User?.DisplayName ?? "Anonymous",
        ChatMessageKind.Bot => BotName ?? "Bot",
        ChatMessageKind.LiveSplit => "LiveSplit",
        _ => null
    };

    public static ChatMessage FromJson(JsonElement e)
    {
        RacetimeUser user = RacetimeUser.FromJson(e.Obj("user"));
        ChatMessageKind kind = e.Bool("is_system") ? ChatMessageKind.System
            : e.Bool("is_bot") ? ChatMessageKind.Bot
            : ChatMessageKind.User;

        return new ChatMessage
        {
            Id = e.Str("id"),
            Kind = kind,
            User = user,
            BotName = e.Str("bot"),
            PostedAt = e.Date("posted_at")?.ToLocalTime() ?? DateTime.Now,
            Text = e.Str("message_plain") ?? e.Str("message") ?? "",
            Highlight = e.Bool("highlight"),
            IsPinned = e.Bool("is_pinned"),
            IsDirect = e.Bool("is_dm")
        };
    }

    public static ChatMessage Local(string text, ChatMessageKind kind = ChatMessageKind.LiveSplit, bool highlight = false)
    {
        return new ChatMessage
        {
            Kind = kind,
            PostedAt = DateTime.Now,
            Text = text,
            Highlight = highlight
        };
    }
}

/// <summary>
/// A split relayed by another entrant ("race.split").
/// </summary>
public sealed class SplitUpdate
{
    public string UserId { get; init; }
    public string SplitName { get; init; }
    public TimeSpan? SplitTime { get; init; }
    public bool IsUndo { get; init; }
    public bool IsFinish { get; init; }

    public static SplitUpdate FromJson(JsonElement e)
    {
        string time = e.Str("split_time");
        TimeSpan? splitTime = null;
        if (!string.IsNullOrEmpty(time) && time != "-")
        {
            try
            {
                splitTime = TimeSpanParser.Parse(time);
            }
            catch (FormatException) { }
        }

        return new SplitUpdate
        {
            UserId = e.Str("user_id"),
            SplitName = e.Str("split_name") ?? "",
            SplitTime = splitTime,
            IsUndo = e.Bool("is_undo"),
            IsFinish = e.Bool("is_finish")
        };
    }
}
