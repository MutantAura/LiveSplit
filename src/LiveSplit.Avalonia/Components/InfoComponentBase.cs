using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Color = System.Drawing.Color;

namespace LiveSplit.UI.Components;

/// <summary>
/// Settings shared by the "name ... value" information components.
/// </summary>
public abstract class InfoSettings : ComponentSettings
{
    public bool OverrideTextColor { get; set; }
    public Color TextColor { get; set; } = Color.FromArgb(255, 255, 255);
    public GradientType BackgroundGradient { get; set; } = GradientType.Plain;
    public Color BackgroundColor { get; set; } = Color.Transparent;
    public Color BackgroundColor2 { get; set; } = Color.Transparent;
    public bool Display2Rows { get; set; }

    protected void ParseBackground(XmlElement element)
    {
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"]);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"]);
        BackgroundGradient = Enum.TryParse(SettingsHelper.ParseString(element["BackgroundGradient"]), out GradientType gradient)
            ? gradient
            : GradientType.Plain;
    }
}

/// <summary>
/// Information settings that also have an overridable color for the value.
/// </summary>
public abstract class InfoTimeSettings : InfoSettings
{
    public bool OverrideTimeColor { get; set; }
    public Color TimeColor { get; set; } = Color.FromArgb(255, 255, 255);
}

/// <summary>
/// Common implementation of the information components (Previous Segment, Sum of Best, ...):
/// an <see cref="InfoTextComponent"/> on top of an optional gradient background.
/// </summary>
public abstract class InfoComponentBase<TSettings> : IComponent, ISettingsHashCodeProvider where TSettings : InfoSettings
{
    protected InfoTextComponent InternalComponent { get; set; }
    public TSettings Settings { get; protected set; }
    protected LiveSplitState CurrentState { get; }

    public float PaddingTop => InternalComponent.PaddingTop;
    public float PaddingLeft => InternalComponent.PaddingLeft;
    public float PaddingBottom => InternalComponent.PaddingBottom;
    public float PaddingRight => InternalComponent.PaddingRight;

    public float VerticalHeight => InternalComponent.VerticalHeight;
    public float MinimumWidth => InternalComponent.MinimumWidth;
    public float HorizontalWidth => InternalComponent.HorizontalWidth;
    public float MinimumHeight => InternalComponent.MinimumHeight;

    public virtual IDictionary<string, Action> ContextMenuControls => null;

    public abstract string ComponentName { get; }

    protected InfoComponentBase(LiveSplitState state, TSettings settings, InfoTextComponent internalComponent)
    {
        CurrentState = state;
        Settings = settings;
        InternalComponent = internalComponent;
    }

    /// <summary>
    /// Renames the comparison stored in <paramref name="getComparison"/> when the run's comparison is renamed.
    /// </summary>
    protected void TrackComparisonRenames(Func<string> getComparison, Action<string> setComparison)
    {
        CurrentState.ComparisonRenamed += (sender, e) =>
        {
            var args = (RenameEventArgs)e;
            if (getComparison() == args.OldName)
            {
                setComparison(args.NewName);
                ((LiveSplitState)sender).Layout.HasChanged = true;
            }
        };
    }

    /// <summary>
    /// Resolves the "Current Comparison" placeholder and falls back to the current comparison
    /// when the configured one does not exist in the run.
    /// </summary>
    protected static string ResolveComparison(LiveSplitState state, string comparison)
    {
        comparison = comparison == "Current Comparison" ? state.CurrentComparison : comparison;
        return state.Run.Comparisons.Contains(comparison) ? comparison : state.CurrentComparison;
    }

    protected static string ComparisonSuffix(string configuredComparison, string resolvedComparison)
    {
        return configuredComparison == "Current Comparison"
            ? ""
            : " (" + CompositeComparisons.GetShortComparisonName(resolvedComparison) + ")";
    }

    protected virtual void PrepareDraw(LiveSplitState state)
    {
        InternalComponent.DisplayTwoRows = Settings.Display2Rows;
        InternalComponent.NameLabel.HasShadow
            = InternalComponent.ValueLabel.HasShadow
            = state.LayoutSettings.DropShadows;
        InternalComponent.NameLabel.ForeColor = Settings.OverrideTextColor ? Settings.TextColor : state.LayoutSettings.TextColor;
        if (Settings is InfoTimeSettings timeSettings)
        {
            InternalComponent.ValueLabel.ForeColor = timeSettings.OverrideTimeColor ? timeSettings.TimeColor : state.LayoutSettings.TextColor;
        }
    }

    private void DrawBackground(DrawingContext g, float width, float height)
    {
        g.FillGradient(
            Settings.BackgroundColor,
            Settings.BackgroundGradient == GradientType.Plain ? Settings.BackgroundColor : Settings.BackgroundColor2,
            Settings.BackgroundGradient == GradientType.Horizontal,
            width, height);
    }

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion)
    {
        DrawBackground(g, width, VerticalHeight);
        PrepareDraw(state);
        InternalComponent.DrawVertical(g, state, width, clipRegion);
    }

    public void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion)
    {
        DrawBackground(g, HorizontalWidth, height);
        PrepareDraw(state);
        InternalComponent.DrawHorizontal(g, state, height, clipRegion);
    }

    public virtual Control GetSettingsControl(LayoutMode mode)
    {
        return SettingsEditor.Create(Settings, CurrentState);
    }

    public void SetSettings(XmlNode settings)
    {
        Settings.SetSettings(settings);
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        return Settings.GetSettings(document);
    }

    public abstract void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode);

    public virtual void Dispose() { }

    public int GetSettingsHashCode()
    {
        return Settings.GetSettingsHashCode();
    }
}

/// <summary>
/// Factory for a component whose constructor takes the state.
/// </summary>
public sealed class SimpleComponentFactory(string name, string description, ComponentCategory category, Func<LiveSplitState, IComponent> create) : IComponentFactory
{
    public string ComponentName => name;
    public string Description => description;
    public ComponentCategory Category => category;

    public IComponent Create(LiveSplitState state)
    {
        return create(state);
    }
}
