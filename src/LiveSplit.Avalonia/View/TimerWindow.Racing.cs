using Avalonia.Controls;
using Avalonia.Threading;
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Racetime;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MessageBox = LiveSplit.UI.MessageBox;

namespace LiveSplit.View;

/// <summary>
/// Race provider integration: the "Races" menus and the race room window.
/// </summary>
public partial class TimerWindow
{
    private readonly Dictionary<string, RaceProviderAPI> raceProviders = [];
    private readonly Dictionary<RaceProviderAPI, MenuItem> raceMenuItems = [];
    private RaceRoomWindow raceRoom;

    private void InitRaceProviders()
    {
        foreach ((string name, IRaceProviderFactory factory) in ComponentManager.RaceProviderFactories)
        {
            RaceProviderAPI provider = factory.Create(Model, GetRaceProviderSettings(name));
            provider.RacesRefreshedCallback = api => Dispatcher.UIThread.Post(() => RacesRefreshed(api));
            provider.JoinRace = (model, raceId) => _ = JoinRace(provider, raceId);
            raceProviders[name] = provider;
        }
    }

    private RaceProviderSettings GetRaceProviderSettings(string name)
    {
        return Settings.RaceProvider.FirstOrDefault(x => x.Name == name);
    }

    private IEnumerable<object> BuildRaceMenus()
    {
        raceMenuItems.Clear();
        foreach ((string name, RaceProviderAPI provider) in raceProviders)
        {
            // The settings object is replaced when the settings dialog is cancelled.
            provider.Settings = GetRaceProviderSettings(name) ?? provider.Settings;
            if (provider.Settings?.Enabled == false)
            {
                continue;
            }

            var item = new MenuItem { Header = $"{provider.ProviderName} Races", ItemsSource = BuildRaceItems(provider) };
            raceMenuItems[provider] = item;
            provider.RefreshRacesListAsync();
            yield return item;
        }
    }

    private void RacesRefreshed(RaceProviderAPI provider)
    {
        if (raceMenuItems.TryGetValue(provider, out MenuItem item))
        {
            item.ItemsSource = BuildRaceItems(provider);
        }
    }

    private List<object> BuildRaceItems(RaceProviderAPI provider)
    {
        var items = new List<object>();
        IRaceInfo[] races = provider.GetRaces()?.ToArray() ?? [];

        foreach (IRaceInfo race in races.Where(x => x.State == 1))
        {
            string entrants = race.NumEntrants == 1 ? "1 Entrant" : $"{race.NumEntrants} Entrants";
            string raceId = race.Id;
            items.Add(Item($"{ShortenGameAndGoal(race)} ({entrants})", () => provider.JoinRace?.Invoke(Model, raceId)));
        }

        if (items.Count > 0)
        {
            items.Add(new Separator());
        }

        var formatter = new RegularTimeFormatter();
        foreach (IRaceInfo race in races.Where(x => x.State == 3))
        {
            TimeSpan elapsed = DateTime.UtcNow - DateTime.UnixEpoch.AddSeconds(race.Starttime);
            string time = formatter.Format(elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed);
            string raceId = race.Id;
            items.Add(Item($"[{time}] {ShortenGameAndGoal(race)} ({race.Finishes}/{race.NumEntrants - race.Forfeits} Finished)",
                () => provider.JoinRace?.Invoke(Model, raceId)));
        }

        if (items.Count > 0 && items[^1] is not Separator)
        {
            items.Add(new Separator());
        }

        items.Add(Item("New Race...", () => provider.CreateRace?.Invoke(Model)));
        return items;
    }

    private static string ShortenGameAndGoal(IRaceInfo race)
    {
        const int maxLength = 60;
        string text = string.IsNullOrEmpty(race.Goal) ? race.GameName : $"{race.GameName} - {race.Goal}";
        text = text.Replace("\r", "").Replace("\n", " ");
        if (text.Length > maxLength)
        {
            text = text[..(maxLength - 3)] + "...";
        }

        return text.Replace("_", "__");
    }

    private async Task JoinRace(RaceProviderAPI provider, string raceId)
    {
        if (raceRoom != null)
        {
            if (raceRoom.Channel.RaceId == raceId)
            {
                raceRoom.Activate();
                return;
            }

            DialogResult leave = await MessageBox.Show(this,
                $"You are already in the race room {raceRoom.Channel.RaceId}. Leave it and open {raceId}?",
                "Race Room", MessageBoxButtons.YesNo);
            if (leave != DialogResult.Yes || !await raceRoom.TryCloseAsync())
            {
                return;
            }
        }

        if (provider is not RacetimeAPI racetime || provider.Settings is not RacetimeSettings settings)
        {
            return;
        }

        var channel = new RacetimeChannel(CurrentState, Model, settings, racetime.Authenticator, raceId);
        var window = new RaceRoomWindow(channel) { Topmost = Layout.Settings.AlwaysOnTop };
        window.Closed += (s, e) =>
        {
            if (raceRoom == window)
            {
                raceRoom = null;
            }

            InvalidationRequired = true;
        };
        raceRoom = window;
        window.Show();
    }

    /// <summary>
    /// Closes the race room before the timer closes. Returns false if the user cancelled.
    /// </summary>
    private Task<bool> CloseRaceRoom()
    {
        return raceRoom?.TryCloseAsync() ?? Task.FromResult(true);
    }
}
