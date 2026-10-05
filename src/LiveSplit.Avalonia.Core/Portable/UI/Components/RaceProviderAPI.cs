using LiveSplit.Model;
using LiveSplit.Options;
using System.Collections.Generic;

namespace LiveSplit.UI.Components;

/// <summary>
/// Portable copy of LiveSplit.Core's IRaceProviderFactory. Factories are registered in
/// <see cref="ComponentManager.RaceProviderFactories"/> under the file name of the Windows plugin
/// they replace (e.g. "LiveSplit.Racetime.dll"), so existing settings resolve to them.
/// </summary>
public interface IRaceProviderFactory
{
    RaceProviderAPI Create(ITimerModel model, RaceProviderSettings settings);
    RaceProviderSettings CreateSettings();
}

/// <summary>
/// Portable copy of LiveSplit.Core's RaceProviderAPI.
/// </summary>
public abstract class RaceProviderAPI
{
    public abstract IEnumerable<IRaceInfo> GetRaces();
    public RacesRefreshedCallback RacesRefreshedCallback;
    public JoinRaceDelegate JoinRace;
    public CreateRaceDelegate CreateRace;
    public abstract void RefreshRacesListAsync();
    public abstract string ProviderName { get; }
    public abstract string Username { get; }
    public RaceProviderSettings Settings { get; set; }
}

public delegate void RacesRefreshedCallback(RaceProviderAPI api);
public delegate void JoinRaceDelegate(ITimerModel model, string raceId);
public delegate void CreateRaceDelegate(ITimerModel model);
