using LiveSplit.Options;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;

namespace LiveSplit.Model;

public enum AutoSplitterType
{
    Component,
    Script,
    AutoSplittingRuntimeScript
}

public class AutoSplittingRuntimeInfo
{
    public string URL { get; set; }
    public string Description { get; set; }
    public string Website { get; set; }
}

/// <summary>
/// Placeholder for LiveSplit.Core's AutoSplitter. Auto splitters are WinForms components (ASL
/// scripts read game memory through Win32 APIs), so they cannot be activated in this front end
/// yet. The run's auto splitter settings are still preserved when splits are saved.
/// </summary>
public class AutoSplitter : ICloneable
{
    public string Description { get; set; }
    public IEnumerable<string> Games { get; set; } = [];
    public bool IsActivated => Component != null;
    public List<string> URLs { get; set; } = [];
    public AutoSplitterType Type { get; set; }
    public bool ShowInLayoutEditor { get; set; }
    public IComponent Component { get; set; }
    public IComponentFactory Factory { get; set; }
    public string Website { get; set; }
    public AutoSplittingRuntimeInfo AutoSplittingRuntime { get; set; }

    /// <summary>
    /// Creates a placeholder that carries a run's auto splitter settings through a save, since
    /// the run saver only writes them for an active auto splitter. Returns null if there are none.
    /// </summary>
    public static AutoSplitter PreserveSettings(System.Xml.XmlElement settings)
    {
        if (settings == null || !settings.HasChildNodes)
        {
            return null;
        }

        var component = new UnavailableComponent("Auto Splitter");
        component.SetSettings(settings);
        return new AutoSplitter
        {
            Description = "Auto splitter settings (preserved, not active)",
            Component = component
        };
    }

    public void Activate(LiveSplitState state)
    {
        Log.Warning("Auto splitters are not supported by the cross-platform version of LiveSplit yet.");
    }

    public void Deactivate()
    {
        Component?.Dispose();
        Component = null;
    }

    public AutoSplitter Clone()
    {
        return new AutoSplitter()
        {
            Description = Description,
            Games = [.. Games],
            URLs = [.. URLs],
            Type = Type,
            ShowInLayoutEditor = ShowInLayoutEditor,
            Component = Component,
            Factory = Factory,
            Website = Website,
            AutoSplittingRuntime = AutoSplittingRuntime
        };
    }

    object ICloneable.Clone()
    {
        return Clone();
    }
}
