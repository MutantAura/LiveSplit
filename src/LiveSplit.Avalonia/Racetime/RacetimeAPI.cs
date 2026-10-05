using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace LiveSplit.Racetime;

/// <summary>
/// The racetime.gg race provider: lists the current races for the "Races" menu and owns the
/// sign-in shared by all race rooms. Joining a race is handled by the front end through
/// <see cref="RaceProviderAPI.JoinRace"/>, which opens a race room window.
/// </summary>
public sealed class RacetimeAPI : RaceProviderAPI
{
    public static RacetimeAPI Instance => field ??= new();

    internal static HttpClient Http { get; } = CreateHttpClient();

    private IReadOnlyList<Race> races = [];

    private RacetimeAPI()
    {
        Authenticator = new RacetimeAuthenticator(Http, RacetimeTokenStore.Default, UrlLauncher.Open);
        CreateRace = _ => UrlLauncher.Open(RacetimeConfig.WebRoot);
    }

    public RacetimeAuthenticator Authenticator { get; }

    public override string ProviderName => "racetime.gg";

    public override string Username => Authenticator.Identity?.Name;

    public override IEnumerable<IRaceInfo> GetRaces()
    {
        return races;
    }

    public override void RefreshRacesListAsync()
    {
        _ = RefreshRacesList();
    }

    private async Task RefreshRacesList()
    {
        try
        {
            string json = await Http.GetStringAsync(RacetimeConfig.WebRoot + RacetimeConfig.RacesEndpoint);
            races = ParseRaces(json);
            RacesRefreshedCallback?.Invoke(this);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Log.Warning($"Could not refresh the racetime.gg race list: {ex.Message}");
        }
    }

    internal static IReadOnlyList<Race> ParseRaces(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement list = document.RootElement.Obj("races");
        return list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Select(Race.FromJson)]
            : [];
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LiveSplit");
        return client;
    }
}

public sealed class RacetimeFactory : IRaceProviderFactory
{
    public RaceProviderAPI Create(ITimerModel model, RaceProviderSettings settings)
    {
        RacetimeAPI.Instance.Settings = settings;
        return RacetimeAPI.Instance;
    }

    public RaceProviderSettings CreateSettings()
    {
        return new RacetimeSettings();
    }
}
