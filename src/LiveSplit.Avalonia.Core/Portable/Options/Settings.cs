using LiveSplit.Model;
using LiveSplit.Model.Input;
using LiveSplit.Web.SRL;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.Options;

/// <summary>
/// Portable copy of LiveSplit.Core's Settings. Hotkeys are registered on the cross-platform
/// <see cref="CompositeHook"/>; "deactivate hotkeys for other programs" is not supported because
/// it relies on Win32 RegisterHotKey.
/// </summary>
public class Settings : ISettings
{
    public const int DPI_AWARENESS_OS_MIN_VERSION = 6;
    public IDictionary<string, HotkeyProfile> HotkeyProfiles { get; set; }
    public IList<RecentSplitsFile> RecentSplits { get; set; }
    public IList<string> RecentLayouts { get; set; }
    public string LastComparison { get; set; }
    public int HcpHistorySize { get; set; }
    public int HcpNBestRuns { get; set; }
    public bool WarnOnReset { get; set; }
    public bool AgreedToSRLRules { get; set; }
    public bool SimpleSumOfBest { get; set; }
    public int RefreshRate { get; set; }
    public int ServerPort { get; set; }
    public ServerStartupType ServerStartup { get; set; }
    public ServerStateType ServerState { get; set; }
    public IRaceViewer RaceViewer { get; set; }
    public IList<RaceProviderSettings> RaceProvider { get; set; }
    public IList<string> ActiveAutoSplitters { get; set; }
    public IDictionary<string, bool> ComparisonGeneratorStates { get; set; }
    public bool EnableDPIAwareness { get; set; }
    public string UILanguage { get; set; }

    // Deprecated properties
    public KeyOrButton SplitKey
    {
        get => HotkeyProfiles.First().Value.SplitKey;
        set => HotkeyProfiles.First().Value.SplitKey = value;
    }
    public KeyOrButton ResetKey
    {
        get => HotkeyProfiles.First().Value.ResetKey;
        set => HotkeyProfiles.First().Value.ResetKey = value;
    }
    public KeyOrButton SkipKey
    {
        get => HotkeyProfiles.First().Value.SkipKey;
        set => HotkeyProfiles.First().Value.SkipKey = value;
    }
    public KeyOrButton UndoKey
    {
        get => HotkeyProfiles.First().Value.UndoKey;
        set => HotkeyProfiles.First().Value.UndoKey = value;
    }
    public KeyOrButton PauseKey
    {
        get => HotkeyProfiles.First().Value.PauseKey;
        set => HotkeyProfiles.First().Value.PauseKey = value;
    }
    public KeyOrButton ToggleGlobalHotkeys
    {
        get => HotkeyProfiles.First().Value.ToggleGlobalHotkeys;
        set => HotkeyProfiles.First().Value.ToggleGlobalHotkeys = value;
    }
    public KeyOrButton SwitchComparisonPrevious
    {
        get => HotkeyProfiles.First().Value.SwitchComparisonPrevious;
        set => HotkeyProfiles.First().Value.SwitchComparisonPrevious = value;
    }
    public KeyOrButton SwitchComparisonNext
    {
        get => HotkeyProfiles.First().Value.SwitchComparisonNext;
        set => HotkeyProfiles.First().Value.SwitchComparisonNext = value;
    }
    public float HotkeyDelay
    {
        get => HotkeyProfiles.First().Value.HotkeyDelay;
        set => HotkeyProfiles.First().Value.HotkeyDelay = value;
    }
    public bool GlobalHotkeysEnabled
    {
        get => HotkeyProfiles.First().Value.GlobalHotkeysEnabled;
        set => HotkeyProfiles.First().Value.GlobalHotkeysEnabled = value;
    }
    public bool DeactivateHotkeysForOtherPrograms
    {
        get => HotkeyProfiles.First().Value.DeactivateHotkeysForOtherPrograms;
        set => HotkeyProfiles.First().Value.DeactivateHotkeysForOtherPrograms = value;
    }
    public bool DoubleTapPrevention
    {
        get => HotkeyProfiles.First().Value.DoubleTapPrevention;
        set => HotkeyProfiles.First().Value.DoubleTapPrevention = value;
    }

    public Settings()
    {
        RecentSplits = [];
        RecentLayouts = [];
        ActiveAutoSplitters = [];
        RaceProvider = [];
        ComparisonGeneratorStates = new Dictionary<string, bool>();
    }

    public object Clone()
    {
        return new Settings()
        {
            HotkeyProfiles = HotkeyProfiles.ToDictionary(x => x.Key, x => (HotkeyProfile)x.Value.Clone()),
            WarnOnReset = WarnOnReset,
            RecentSplits = [.. RecentSplits],
            RecentLayouts = [.. RecentLayouts],
            LastComparison = LastComparison,
            RaceViewer = RaceViewer,
            RaceProvider = [.. RaceProvider.Select(x => (RaceProviderSettings)x.Clone())],
            AgreedToSRLRules = AgreedToSRLRules,
            SimpleSumOfBest = SimpleSumOfBest,
            RefreshRate = RefreshRate,
            ServerPort = ServerPort,
            ServerStartup = ServerStartup,
            ServerState = ServerState,
            ActiveAutoSplitters = [.. ActiveAutoSplitters],
            ComparisonGeneratorStates = new Dictionary<string, bool>(ComparisonGeneratorStates),
            EnableDPIAwareness = EnableDPIAwareness,
            UILanguage = UILanguage,
            HcpHistorySize = HcpHistorySize,
            HcpNBestRuns = HcpNBestRuns
        };
    }

    public void RegisterHotkeys(CompositeHook hook, string hotkeyProfileName)
    {
        try
        {
            UnregisterAllHotkeys(hook);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        if (!HotkeyProfiles.TryGetValue(hotkeyProfileName, out HotkeyProfile hotkeyProfile))
        {
            return;
        }

        KeyOrButton[] keys =
        [
            hotkeyProfile.SplitKey,
            hotkeyProfile.ResetKey,
            hotkeyProfile.SkipKey,
            hotkeyProfile.UndoKey,
            hotkeyProfile.PauseKey,
            hotkeyProfile.ToggleGlobalHotkeys,
            hotkeyProfile.SwitchComparisonPrevious,
            hotkeyProfile.SwitchComparisonNext
        ];

        foreach (KeyOrButton key in keys.Where(x => x != null))
        {
            try
            {
                hook.RegisterHotKey(key);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        hook.AllowGamepads = hotkeyProfile.AllowGamepadsAsHotkeys;
    }

    public void AddToRecentSplits(string path, IRun run, TimingMethod lastTimingMethod, string lastHotkeyProfile)
    {
        RecentSplitsFile foundRecentSplitsFile = RecentSplits.FirstOrDefault(x => x.Path == path);
        if (foundRecentSplitsFile.Path != null)
        {
            RecentSplits.Remove(foundRecentSplitsFile);
        }

        var recentSplitsFile = new RecentSplitsFile(path, run, lastTimingMethod, lastHotkeyProfile);

        RecentSplits.Add(recentSplitsFile);

        while (RecentSplits.Count > 50)
        {
            RecentSplits.RemoveAt(0);
        }
    }

    public void AddToRecentLayouts(string path)
    {
        if (RecentLayouts.Contains(path))
        {
            RecentLayouts.Remove(path);
        }

        RecentLayouts.Add(path);
        while (RecentLayouts.Count > 10)
        {
            RecentLayouts.RemoveAt(0);
        }
    }

    public void UnregisterAllHotkeys(CompositeHook hook)
    {
        hook.UnregisterAllHotkeys();
    }
}
