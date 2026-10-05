using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LiveSplit.Model;
using LiveSplit.TimeFormatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Color = System.Drawing.Color;
using Image = LiveSplit.Drawing.Image;

namespace LiveSplit.UI.Components;

[GlobalFontConsumer(GlobalFont.TimesFont)]
public class SplitsComponent : IComponent, ISettingsHashCodeProvider
{
    public ComponentRendererComponent InternalComponent { get; protected set; }

    public float PaddingTop => InternalComponent.PaddingTop;
    public float PaddingLeft => InternalComponent.PaddingLeft;
    public float PaddingBottom => InternalComponent.PaddingBottom;
    public float PaddingRight => InternalComponent.PaddingRight;

    protected IList<IComponent> Components { get; set; }
    protected IList<SplitComponent> SplitComponents { get; set; }

    protected SplitsSettings Settings { get; set; }

    protected SimpleLabel MeasureTimeLabel { get; set; }
    protected SimpleLabel MeasureDeltaLabel { get; set; }
    protected SimpleLabel MeasureCharLabel { get; set; }

    protected TimeAccuracy CurrentAccuracy { get; set; }
    protected TimeAccuracy CurrentDeltaAccuracy { get; set; }
    protected bool CurrentDropDecimals { get; set; }

    protected ITimeFormatter TimeFormatter { get; set; }
    protected ITimeFormatter DeltaTimeFormatter { get; set; }

    private Dictionary<Image, Image> ShadowImages { get; } = [];

    private int visualSplitCount;
    private int settingsSplitCount;

    protected bool PreviousShowLabels { get; set; }

    protected int ScrollOffset { get; set; }
    protected int LastSplitSeparatorIndex { get; set; }

    protected LiveSplitState CurrentState { get; set; }
    protected LiveSplitState OldState { get; set; }
    protected LayoutMode OldLayoutMode { get; set; }
    protected Color OldShadowsColor { get; set; }

    protected IEnumerable<ColumnData> ColumnsList => Settings.ColumnsList;
    protected List<(int exLength, float exWidth, float width)> ColumnWidths { get; set; }

    public string ComponentName => "Splits";

    public float VerticalHeight => InternalComponent.VerticalHeight;
    public float MinimumWidth => InternalComponent.MinimumWidth;
    public float HorizontalWidth => InternalComponent.HorizontalWidth;
    public float MinimumHeight => InternalComponent.MinimumHeight;

    public IDictionary<string, Action> ContextMenuControls => null;

    public SplitsComponent(LiveSplitState state)
    {
        CurrentState = state;
        Settings = new SplitsSettings();
        InternalComponent = new ComponentRendererComponent();
        MeasureTimeLabel = new SimpleLabel();
        MeasureDeltaLabel = new SimpleLabel();
        MeasureCharLabel = new SimpleLabel();
        CurrentAccuracy = Settings.SplitTimesAccuracy;
        CurrentDeltaAccuracy = Settings.DeltasAccuracy;
        CurrentDropDecimals = Settings.DropDecimals;
        TimeFormatter = new SplitTimeFormatter(CurrentAccuracy);
        DeltaTimeFormatter = new DeltaSplitTimeFormatter(CurrentDeltaAccuracy, CurrentDropDecimals);
        visualSplitCount = Settings.VisualSplitCount;
        settingsSplitCount = Settings.VisualSplitCount;
        Settings.SplitLayoutChanged += (s, e) => RebuildVisualSplits();
        ColumnWidths = [.. Settings.ColumnsList.Select(_ => (0, 0f, 0f))];
        ScrollOffset = 0;
        RebuildVisualSplits();
        state.ComparisonRenamed += State_ComparisonRenamed;
    }

    private void State_ComparisonRenamed(object sender, EventArgs e)
    {
        var args = (RenameEventArgs)e;
        foreach (ColumnData column in ColumnsList)
        {
            if (column.Comparison == args.OldName)
            {
                column.Comparison = args.NewName;
                ((LiveSplitState)sender).Layout.HasChanged = true;
            }
        }
    }

    private void RebuildVisualSplits()
    {
        Components = [];
        SplitComponents = [];
        InternalComponent.VisibleComponents = Components;

        int totalSplits = Settings.ShowBlankSplits ? Math.Max(Settings.VisualSplitCount, visualSplitCount) : visualSplitCount;

        if (Settings.ShowColumnLabels && CurrentState.Layout?.Mode == LayoutMode.Vertical)
        {
            Components.Add(new LabelsComponent(Settings, ColumnsList, ColumnWidths));
            Components.Add(new SeparatorComponent());
        }

        for (int i = 0; i < totalSplits; ++i)
        {
            if (i == totalSplits - 1 && i > 0)
            {
                LastSplitSeparatorIndex = Components.Count;
                if (Settings.AlwaysShowLastSplit && Settings.SeparatorLastSplit)
                {
                    Components.Add(new SeparatorComponent());
                }
                else if (Settings.ShowThinSeparators)
                {
                    Components.Add(new ThinSeparatorComponent());
                }
            }

            var splitComponent = new SplitComponent(Settings, ColumnsList, ColumnWidths);
            Components.Add(splitComponent);
            if (i < visualSplitCount - 1 || i == (Settings.LockLastSplit ? totalSplits - 1 : visualSplitCount - 1))
            {
                SplitComponents.Add(splitComponent);
            }

            if (Settings.ShowThinSeparators && i < totalSplits - 2)
            {
                Components.Add(new ThinSeparatorComponent());
            }
        }
    }

    private void Prepare(LiveSplitState state)
    {
        if (state != OldState)
        {
            state.OnScrollDown += (s, e) => ScrollOffset++;
            state.OnScrollUp += (s, e) => ScrollOffset--;
            state.OnStart += (s, e) => ScrollOffset = 0;
            state.OnReset += (s, e) => ScrollOffset = 0;
            state.OnSplit += (s, e) => ScrollOffset = 0;
            state.OnSkipSplit += (s, e) => ScrollOffset = 0;
            state.OnUndoSplit += (s, e) => ScrollOffset = 0;
            OldState = state;
        }

        if (Settings.SplitTimesAccuracy != CurrentAccuracy)
        {
            TimeFormatter = new SplitTimeFormatter(Settings.SplitTimesAccuracy);
            CurrentAccuracy = Settings.SplitTimesAccuracy;
        }

        if (Settings.DeltasAccuracy != CurrentDeltaAccuracy || Settings.DropDecimals != CurrentDropDecimals)
        {
            DeltaTimeFormatter = new DeltaSplitTimeFormatter(Settings.DeltasAccuracy, Settings.DropDecimals);
            CurrentDeltaAccuracy = Settings.DeltasAccuracy;
            CurrentDropDecimals = Settings.DropDecimals;
        }

        int previousSplitCount = visualSplitCount;
        visualSplitCount = Math.Min(state.Run.Count, Settings.VisualSplitCount);
        if (previousSplitCount != visualSplitCount
            || (Settings.ShowBlankSplits && settingsSplitCount != Settings.VisualSplitCount)
            || Settings.ShowColumnLabels != PreviousShowLabels
            || (Settings.ShowColumnLabels && state.Layout.Mode != OldLayoutMode))
        {
            PreviousShowLabels = Settings.ShowColumnLabels;
            OldLayoutMode = state.Layout.Mode;
            RebuildVisualSplits();
        }

        settingsSplitCount = Settings.VisualSplitCount;

        int skipCount = GetSkipCount(state);

        if (OldShadowsColor != state.LayoutSettings.ShadowsColor)
        {
            ShadowImages.Clear();
        }

        foreach (ISegment split in state.Run)
        {
            if (split.Icon != null && !ShadowImages.ContainsKey(split.Icon))
            {
                ShadowImages[split.Icon] = IconShadow.Generate(split.Icon, state.LayoutSettings.ShadowsColor);
            }
        }

        bool iconsNotBlank = state.Run.Any(x => x.Icon != null);
        foreach (SplitComponent split in SplitComponents)
        {
            split.DisplayIcon = iconsNotBlank && Settings.DisplayIcons;
            split.ShadowImage = split.Split?.Icon != null ? ShadowImages.GetValueOrDefault(split.Split.Icon) : null;
        }

        OldShadowsColor = state.LayoutSettings.ShadowsColor;

        for (int index = 0; index < Components.Count; index++)
        {
            IComponent component = Components[index];
            if (component is SeparatorComponent separator)
            {
                if (state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused)
                {
                    if (index + 1 < Components.Count && Components[index + 1] is SplitComponent next && next.Split == state.CurrentSplit)
                    {
                        separator.LockToBottom = true;
                    }
                    else if (index > 0 && Components[index - 1] is SplitComponent previous && previous.Split == state.CurrentSplit)
                    {
                        separator.LockToBottom = false;
                    }
                }

                if (Settings.AlwaysShowLastSplit && Settings.SeparatorLastSplit && index == LastSplitSeparatorIndex)
                {
                    if (skipCount >= state.Run.Count - visualSplitCount)
                    {
                        separator.DisplayedSize = Settings.ShowThinSeparators ? 1f : 0f;
                        separator.UseSeparatorColor = false;
                    }
                    else
                    {
                        separator.DisplayedSize = 2f;
                        separator.UseSeparatorColor = true;
                    }
                }
            }
            else if (component is ThinSeparatorComponent thinSeparator)
            {
                if (state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused)
                {
                    if (index + 1 < Components.Count && Components[index + 1] is SplitComponent next && next.Split == state.CurrentSplit)
                    {
                        thinSeparator.LockToBottom = true;
                    }
                    else if (index > 0 && Components[index - 1] is SplitComponent previous && previous.Split == state.CurrentSplit)
                    {
                        thinSeparator.LockToBottom = false;
                    }
                }
            }
        }
    }

    private int GetSkipCount(LiveSplitState state)
    {
        int skipCount = Math.Min(
            Math.Max(
                0,
                state.CurrentSplitIndex - (visualSplitCount - 2 - Settings.SplitPreviewCount + (Settings.AlwaysShowLastSplit ? 0 : 1))),
            state.Run.Count - visualSplitCount);
        ScrollOffset = Math.Min(Math.Max(ScrollOffset, -skipCount), state.Run.Count - skipCount - visualSplitCount);
        return skipCount + ScrollOffset;
    }

    private void DrawBackground(DrawingContext g, float width, float height)
    {
        if (Settings.BackgroundGradient != ExtendedGradientType.Alternating)
        {
            g.FillGradient(
                Settings.BackgroundColor,
                Settings.BackgroundGradient == ExtendedGradientType.Plain ? Settings.BackgroundColor : Settings.BackgroundColor2,
                Settings.BackgroundGradient == ExtendedGradientType.Horizontal,
                width, height);
        }
    }

    private void SetMeasureLabels(LiveSplitState state)
    {
        MeasureTimeLabel.Text = TimeFormatter.Format(new TimeSpan(24, 0, 0));
        MeasureDeltaLabel.Text = DeltaTimeFormatter.Format(new TimeSpan(0, 9, 0, 0));
        MeasureCharLabel.Text = "W";

        MeasureTimeLabel.Font = state.LayoutSettings.TimesFont;
        MeasureTimeLabel.IsMonospaced = true;
        MeasureDeltaLabel.Font = state.LayoutSettings.TimesFont;
        MeasureDeltaLabel.IsMonospaced = true;
        MeasureCharLabel.Font = state.LayoutSettings.TimesFont;
        MeasureCharLabel.IsMonospaced = true;

        MeasureTimeLabel.SetActualWidth();
        MeasureDeltaLabel.SetActualWidth();
        MeasureCharLabel.SetActualWidth();
    }

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion)
    {
        Prepare(state);
        DrawBackground(g, width, VerticalHeight);
        SetMeasureLabels(state);
        InternalComponent.DrawVertical(g, state, width, clipRegion);
    }

    public void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion)
    {
        Prepare(state);
        DrawBackground(g, HorizontalWidth, height);
        SetMeasureLabels(state);
        InternalComponent.DrawHorizontal(g, state, height, clipRegion);
    }

    public Control GetSettingsControl(LayoutMode mode)
    {
        return SplitsSettingsEditor.Create(Settings, CurrentState);
    }

    public void SetSettings(XmlNode settings)
    {
        Settings.SetSettings(settings);
        RebuildVisualSplits();
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        return Settings.GetSettings(document);
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        int skipCount = GetSkipCount(state);

        if (SplitComponents.Count >= visualSplitCount && visualSplitCount > 0)
        {
            int i = 0;
            foreach (ISegment split in state.Run.Skip(skipCount).Take(visualSplitCount - 1 + (Settings.AlwaysShowLastSplit ? 0 : 1)))
            {
                SplitComponents[i].Split = split;
                i++;
            }

            if (Settings.AlwaysShowLastSplit)
            {
                SplitComponents[i].Split = state.Run[^1];
            }
        }

        CalculateColumnWidths(state.Run);

        if (invalidator != null)
        {
            InternalComponent.Update(invalidator, state, width, height, mode);
        }
    }

    private void CalculateColumnWidths(IRun run)
    {
        if (ColumnsList == null)
        {
            return;
        }

        int columnCount = Settings.ColumnsList.Count;
        while (ColumnWidths.Count < columnCount)
        {
            ColumnWidths.Add((0, 0f, 0f));
        }

        var longestTime = new TimeSpan(9, 0, 0);
        var longestDelta = new TimeSpan(0, 0, 59, 0);
        foreach (ISegment split in run.Reverse())
        {
            if (split.SplitTime.RealTime is TimeSpan splitRealTime && longestTime < splitRealTime)
            {
                longestTime = splitRealTime;
            }

            foreach (KeyValuePair<string, Time> kv in split.Comparisons)
            {
                if (kv.Value.RealTime is TimeSpan cmpRealTime && longestTime < cmpRealTime)
                {
                    longestTime = cmpRealTime;
                }

                if (split.SplitTime.RealTime - kv.Value.RealTime is TimeSpan deltaRealTime)
                {
                    if (longestDelta < deltaRealTime)
                    {
                        longestDelta = deltaRealTime;
                    }
                    else if (longestDelta < (-deltaRealTime))
                    {
                        longestDelta = -deltaRealTime;
                    }
                }
            }
        }

        int timeLength = TimeFormatter.Format(longestTime).Length;
        int deltaLength = DeltaTimeFormatter.Format(longestDelta).Length;

        float timeCharWidth = MeasureTimeLabel.Text?.Length > 0 ? MeasureTimeLabel.ActualWidth / MeasureTimeLabel.Text.Length : MeasureCharLabel.ActualWidth;
        float timeWidth = Math.Max(MeasureTimeLabel.ActualWidth, timeCharWidth * (timeLength + 1));
        float deltaWidth = Math.Max(MeasureDeltaLabel.ActualWidth, timeCharWidth * (deltaLength + 1));

        for (int i = 0; i < columnCount; i++)
        {
            ColumnData column = Settings.ColumnsList[i];

            float labelWidth = 0f;
            if (column.Type is ColumnType.DeltaorSplitTime or ColumnType.SegmentDeltaorSegmentTime)
            {
                labelWidth = Math.Max(deltaWidth, timeWidth);
            }
            else if (column.Type is ColumnType.Delta or ColumnType.SegmentDelta)
            {
                labelWidth = deltaWidth;
            }
            else if (column.Type is ColumnType.SplitTime or ColumnType.SegmentTime)
            {
                labelWidth = timeWidth;
            }
            else if (column.Type is ColumnType.CustomVariable)
            {
                int longestLength = run.Metadata.CustomVariableValue(column.Name)?.Length ?? 0;
                foreach (ISegment split in run)
                {
                    if (split.CustomVariableValues.TryGetValue(column.Name, out string value) && !string.IsNullOrEmpty(value))
                    {
                        longestLength = Math.Max(longestLength, value.Length);
                    }
                }

                float exCharWidth = ColumnWidths[i].exLength > 0 ? ColumnWidths[i].exWidth / ColumnWidths[i].exLength : MeasureCharLabel.ActualWidth;
                labelWidth = exCharWidth * (longestLength + 1);
            }

            ColumnWidths[i] = (ColumnWidths[i].exLength, ColumnWidths[i].exWidth, labelWidth);
        }

        while (ColumnWidths.Count > columnCount)
        {
            ColumnWidths.RemoveAt(ColumnWidths.Count - 1);
        }
    }

    public void Dispose() { }

    public int GetSettingsHashCode()
    {
        return Settings.GetSettingsHashCode();
    }
}

public class SplitsComponentFactory : IComponentFactory
{
    public string ComponentName => "Splits";
    public string Description => "Displays a list of split times and deltas in relation to a comparison.";
    public ComponentCategory Category => ComponentCategory.List;

    public IComponent Create(LiveSplitState state)
    {
        return new SplitsComponent(state);
    }
}
