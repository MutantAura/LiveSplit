using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSplit.Web;

/// <summary>
/// speedrun.com timing methods, as named by the API.
/// </summary>
public enum SpeedrunComTiming
{
    RealTime,
    RealTimeWithoutLoads,
    GameTime
}

public sealed record SrcGame(
    string Id,
    string Name,
    SpeedrunComTiming DefaultTiming,
    IReadOnlyDictionary<string, string> PlatformIds,
    IReadOnlyDictionary<string, string> RegionIds);

public sealed record SrcCategory(string Id, string Name, bool IsPerGame, int Players);

public sealed record SrcVariable(
    string Id,
    string Name,
    string CategoryId,
    bool IsFullGame,
    bool IsSubcategory,
    IReadOnlyDictionary<string, string> ValueIdsByLabel,
    string DefaultValueLabel = null);

/// <summary>
/// A run at the top of a leaderboard. Times are null when the leaderboard doesn't track them.
/// </summary>
public sealed record SrcRecord(
    TimeSpan? Primary,
    TimeSpan? RealTime,
    TimeSpan? RealTimeWithoutLoads,
    TimeSpan? GameTime,
    IReadOnlyList<string> Players);

public sealed class SrcLeaderboardFilter
{
    public string PlatformId { get; init; }
    public string RegionId { get; init; }
    public bool? Emulators { get; init; }
    public SpeedrunComTiming? Timing { get; init; }
    public IReadOnlyDictionary<string, string> VariableValues { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// A small client for the parts of the speedrun.com REST API (v1) that LiveSplit uses. It reads
/// the JSON with <see cref="JsonDocument"/>, which keeps it Native AOT compatible, unlike the
/// SpeedrunComSharp library used by the Windows version.
/// </summary>
public sealed class SpeedrunComApi(HttpClient http)
{
    public const string BaseUrl = "https://www.speedrun.com/api/v1/";

    public static SpeedrunComApi Shared { get; } = new(CreateHttpClient());

    /// <summary>
    /// Finds a game by its exact name (international, Japanese or Twitch name, ignoring case),
    /// with its platforms and regions.
    /// </summary>
    public async Task<SrcGame> FindGameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        using JsonDocument document = await GetAsync($"games?name={Uri.EscapeDataString(name)}&max=50&embed=platforms,regions", cancellationToken);
        JsonElement match = document.RootElement.Obj("data").EnumerateArrayOrEmpty()
            .FirstOrDefault(game =>
            {
                JsonElement names = game.Obj("names");
                return new[] { names.Str("international"), names.Str("japanese"), names.Str("twitch") }
                    .Any(x => string.Equals(x?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
            });

        return match.ValueKind == JsonValueKind.Object ? ParseGame(match) : null;
    }

    public async Task<IReadOnlyList<SrcCategory>> GetCategoriesAsync(string gameId, CancellationToken cancellationToken = default)
    {
        using JsonDocument document = await GetAsync($"games/{Uri.EscapeDataString(gameId)}/categories", cancellationToken);
        return [.. document.RootElement.Obj("data").EnumerateArrayOrEmpty().Select(category => new SrcCategory(
            category.Str("id"),
            category.Str("name"),
            category.Str("type") == "per-game",
            category.Obj("players").Int("value") ?? 1))];
    }

    public async Task<IReadOnlyList<SrcVariable>> GetVariablesAsync(string gameId, CancellationToken cancellationToken = default)
    {
        using JsonDocument document = await GetAsync($"games/{Uri.EscapeDataString(gameId)}/variables", cancellationToken);
        return [.. document.RootElement.Obj("data").EnumerateArrayOrEmpty().Select(variable =>
        {
            string scope = variable.Obj("scope").Str("type");
            JsonElement values = variable.Obj("values").Obj("values");
            string defaultId = variable.Obj("values").Str("default");
            string defaultLabel = null;
            var valueIds = new Dictionary<string, string>();
            if (values.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty value in values.EnumerateObject())
                {
                    string label = value.Value.Str("label");
                    if (label != null && valueIds.TryAdd(label, value.Name) && value.Name == defaultId)
                    {
                        defaultLabel = label;
                    }
                }
            }

            return new SrcVariable(
                variable.Str("id"),
                variable.Str("name"),
                variable.Str("category"),
                scope is "global" or "full-game",
                variable.Bool("is-subcategory"),
                valueIds,
                defaultLabel);
        })];
    }

    /// <summary>
    /// Returns the first place runs (several when tied) of a full game leaderboard.
    /// </summary>
    public async Task<IReadOnlyList<SrcRecord>> GetWorldRecordsAsync(string gameId, string categoryId, SrcLeaderboardFilter filter, CancellationToken cancellationToken = default)
    {
        var query = new StringBuilder($"leaderboards/{Uri.EscapeDataString(gameId)}/category/{Uri.EscapeDataString(categoryId)}?top=1&embed=players");
        if (filter.PlatformId != null)
        {
            query.Append("&platform=").Append(Uri.EscapeDataString(filter.PlatformId));
        }

        if (filter.RegionId != null)
        {
            query.Append("&region=").Append(Uri.EscapeDataString(filter.RegionId));
        }

        if (filter.Emulators is bool emulators)
        {
            query.Append("&emulators=").Append(emulators ? "true" : "false");
        }

        if (filter.Timing is SpeedrunComTiming timing)
        {
            query.Append("&timing=").Append(ToApiName(timing));
        }

        foreach ((string variableId, string valueId) in filter.VariableValues)
        {
            query.Append("&var-").Append(Uri.EscapeDataString(variableId)).Append('=').Append(Uri.EscapeDataString(valueId));
        }

        using JsonDocument document = await GetAsync(query.ToString(), cancellationToken);
        return ParseWorldRecords(document.RootElement.Obj("data"));
    }

    internal static IReadOnlyList<SrcRecord> ParseWorldRecords(JsonElement leaderboard)
    {
        var userNames = new Dictionary<string, string>();
        foreach (JsonElement player in leaderboard.Obj("players").Obj("data").EnumerateArrayOrEmpty())
        {
            if (player.Str("rel") == "user" && player.Str("id") is string id)
            {
                userNames[id] = player.Obj("names").Str("international") ?? id;
            }
        }

        return [.. leaderboard.Obj("runs").EnumerateArrayOrEmpty()
            .Where(x => x.Int("place") == 1)
            .Select(x =>
            {
                JsonElement run = x.Obj("run");
                JsonElement times = run.Obj("times");
                return new SrcRecord(
                    ParseTime(times, "primary"),
                    ParseTime(times, "realtime"),
                    ParseTime(times, "realtime_noloads"),
                    ParseTime(times, "ingame"),
                    [.. run.Obj("players").EnumerateArrayOrEmpty().Select(player => player.Str("rel") == "user"
                        ? userNames.GetValueOrDefault(player.Str("id") ?? "", player.Str("id"))
                        : player.Str("name"))
                        .Where(name => name != null)]);
            })];
    }

    private static SrcGame ParseGame(JsonElement game)
    {
        static IReadOnlyDictionary<string, string> IdsByName(JsonElement list)
        {
            var ids = new Dictionary<string, string>();
            foreach (JsonElement item in list.Obj("data").EnumerateArrayOrEmpty())
            {
                if (item.Str("name") is string name && item.Str("id") is string id)
                {
                    ids.TryAdd(name, id);
                }
            }

            return ids;
        }

        return new SrcGame(
            game.Str("id"),
            game.Obj("names").Str("international"),
            FromApiName(game.Obj("ruleset").Str("default-time")) ?? SpeedrunComTiming.RealTime,
            IdsByName(game.Obj("platforms")),
            IdsByName(game.Obj("regions")));
    }

    /// <summary>
    /// Reads a time; the API sends a null duration string (and 0 seconds) when a time isn't tracked.
    /// </summary>
    private static TimeSpan? ParseTime(JsonElement times, string name)
    {
        if (times.Str(name) == null)
        {
            return null;
        }

        JsonElement seconds = times.Obj(name + "_t");
        return seconds.ValueKind == JsonValueKind.Number ? TimeSpan.FromSeconds(seconds.GetDouble()) : null;
    }

    public static string ToApiName(SpeedrunComTiming timing)
    {
        return timing switch
        {
            SpeedrunComTiming.RealTimeWithoutLoads => "realtime_noloads",
            SpeedrunComTiming.GameTime => "ingame",
            _ => "realtime"
        };
    }

    public static SpeedrunComTiming? FromApiName(string name)
    {
        return name switch
        {
            "realtime" => SpeedrunComTiming.RealTime,
            "realtime_noloads" => SpeedrunComTiming.RealTimeWithoutLoads,
            "ingame" => SpeedrunComTiming.GameTime,
            _ => null
        };
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(BaseUrl + path, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using System.IO.Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        string version = typeof(SpeedrunComApi).Assembly.GetName().Version?.ToString(3) ?? "0";
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"LiveSplit/{version}");
        return client;
    }
}
