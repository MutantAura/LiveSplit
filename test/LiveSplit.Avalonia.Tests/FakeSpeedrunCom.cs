using LiveSplit.Web;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSplit.Tests;

/// <summary>
/// Serves canned speedrun.com responses and records the requested URLs.
/// </summary>
internal sealed class FakeSpeedrunCom : HttpMessageHandler
{
    public List<string> Requests { get; } = [];
    public SpeedrunComApi Api => new(new HttpClient(this));
    public string Leaderboard { get; set; } = TwoWayTie;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string url = request.RequestUri!.ToString();
        Requests.Add(url);
        string path = url[SpeedrunComApi.BaseUrl.Length..];

        string json = path switch
        {
            _ when path.StartsWith("games?name=") => Games,
            _ when path.EndsWith("/categories") => Categories,
            _ when path.EndsWith("/variables") => Variables,
            _ when path.StartsWith("leaderboards/") => Leaderboard,
            _ => null
        };

        return Task.FromResult(json == null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }

    private const string Games = """
        {"data": [
          {"id": "other", "names": {"international": "Celeste 64"}, "ruleset": {"default-time": "realtime"}},
          {"id": "g1", "names": {"international": "Celeste", "japanese": null, "twitch": "Celeste"},
           "ruleset": {"default-time": "ingame"},
           "platforms": {"data": [{"id": "pc", "name": "PC"}, {"id": "sw", "name": "Nintendo Switch"}]},
           "regions": {"data": [{"id": "eu", "name": "EUR / PAL"}]}}
        ]}
        """;

    private const string Categories = """
        {"data": [
          {"id": "lvl", "name": "Any%", "type": "per-level", "players": {"type": "exactly", "value": 1}},
          {"id": "c1", "name": "Any%", "type": "per-game", "players": {"type": "exactly", "value": 1}}
        ]}
        """;

    private const string Variables = """
        {"data": [
          {"id": "v1", "name": "Version", "category": null, "scope": {"type": "global"}, "is-subcategory": false,
           "values": {"values": {"x1": {"label": "1.4"}, "x2": {"label": "1.3"}}}},
          {"id": "v2", "name": "Seeded", "category": "c1", "scope": {"type": "full-game"}, "is-subcategory": true,
           "values": {"values": {"s1": {"label": "Seeded"}, "s2": {"label": "Set Seed"}}, "default": "s2"}},
          {"id": "v3", "name": "Other Category", "category": "zz", "scope": {"type": "full-game"}, "is-subcategory": true,
           "values": {"values": {"o1": {"label": "Seeded"}}}},
          {"id": "v4", "name": "Level Only", "category": null, "scope": {"type": "single-level"}, "is-subcategory": true,
           "values": {"values": {"l1": {"label": "Seeded"}}}}
        ]}
        """;

    public const string TwoWayTie = """
        {"data": {"runs": [
          {"place": 1, "run": {"times": {"primary": "PT24M48.843S", "primary_t": 1488.843, "realtime": null, "realtime_t": 0,
            "realtime_noloads": null, "realtime_noloads_t": 0, "ingame": "PT24M48.843S", "ingame_t": 1488.843},
            "players": [{"rel": "user", "id": "u1"}]}},
          {"place": 1, "run": {"times": {"primary": "PT24M48.843S", "primary_t": 1488.843, "realtime": "PT25M", "realtime_t": 1500,
            "realtime_noloads": null, "realtime_noloads_t": 0, "ingame": "PT24M48.843S", "ingame_t": 1488.843},
            "players": [{"rel": "guest", "name": "SomeGuest"}]}},
          {"place": 3, "run": {"times": {"primary": "PT30M", "primary_t": 1800}, "players": [{"rel": "user", "id": "u2"}]}}
        ],
        "players": {"data": [
          {"rel": "user", "id": "u1", "names": {"international": "secureaccount"}},
          {"rel": "user", "id": "u2", "names": {"international": "slower"}}
        ]}}}
        """;

    public const string SingleRecord = """
        {"data": {"runs": [
          {"place": 1, "run": {"times": {"primary": "PT24M48S", "primary_t": 1488, "ingame": "PT24M48S", "ingame_t": 1488},
            "players": [{"rel": "user", "id": "u1"}]}}
        ],
        "players": {"data": [{"rel": "user", "id": "u1", "names": {"international": "secureaccount"}}]}}}
        """;
}
