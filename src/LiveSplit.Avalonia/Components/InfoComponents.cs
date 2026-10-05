using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.TimeFormatters;
using System;
using System.Linq;
using System.Xml;

namespace LiveSplit.UI.Components;

#region Previous Segment

public class PreviousSegmentSettings : InfoSettings
{
    [Setting(Options = ["{Comparisons}"])]
    public string Comparison { get; set; } = "Current Comparison";
    public TimeAccuracy DeltaAccuracy { get; set; } = TimeAccuracy.Tenths;
    public bool DropDecimals { get; set; } = true;
    public bool ShowPossibleTimeSave { get; set; }
    public TimeAccuracy TimeSaveAccuracy { get; set; } = TimeAccuracy.Tenths;

    public override void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        TextColor = SettingsHelper.ParseColor(element["TextColor"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        ParseBackground(element);
        DeltaAccuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["DeltaAccuracy"]);
        DropDecimals = SettingsHelper.ParseBool(element["DropDecimals"]);
        Comparison = SettingsHelper.ParseString(element["Comparison"]);
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"], false);
        ShowPossibleTimeSave = SettingsHelper.ParseBool(element["ShowPossibleTimeSave"], false);
        TimeSaveAccuracy = SettingsHelper.ParseEnum(element["TimeSaveAccuracy"], TimeAccuracy.Tenths);
    }

    protected override int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.6") ^
            SettingsHelper.CreateSetting(document, parent, "TextColor", TextColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
            SettingsHelper.CreateSetting(document, parent, "DeltaAccuracy", DeltaAccuracy) ^
            SettingsHelper.CreateSetting(document, parent, "DropDecimals", DropDecimals) ^
            SettingsHelper.CreateSetting(document, parent, "Comparison", Comparison) ^
            SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
            SettingsHelper.CreateSetting(document, parent, "ShowPossibleTimeSave", ShowPossibleTimeSave) ^
            SettingsHelper.CreateSetting(document, parent, "TimeSaveAccuracy", TimeSaveAccuracy);
    }
}

[GlobalFontConsumer(GlobalFont.TimesFont | GlobalFont.TextFont)]
public class PreviousSegment : InfoComponentBase<PreviousSegmentSettings>
{
    protected DeltaTimeFormatter DeltaFormatter { get; }
    protected PossibleTimeSaveFormatter TimeSaveFormatter { get; }
    private string previousNameText;

    public PreviousSegment(LiveSplitState state)
        : this(state, new DeltaTimeFormatter()) { }

    private PreviousSegment(LiveSplitState state, DeltaTimeFormatter formatter)
        : base(state, new PreviousSegmentSettings(), new InfoTimeComponent(null, null, formatter))
    {
        DeltaFormatter = formatter;
        TimeSaveFormatter = new PossibleTimeSaveFormatter();
        TrackComparisonRenames(() => Settings.Comparison, x => Settings.Comparison = x);
    }

    public override string ComponentName
        => "Previous Segment" + (Settings.Comparison == "Current Comparison"
            ? ""
            : " (" + CompositeComparisons.GetShortComparisonName(Settings.Comparison) + ")");

    public static TimeSpan? GetPossibleTimeSave(LiveSplitState state, int splitIndex, string comparison)
    {
        TimeSpan prevTime = TimeSpan.Zero;
        TimeSpan? bestSegments = state.Run[splitIndex].BestSegmentTime[state.CurrentTimingMethod];

        while (splitIndex > 0 && bestSegments != null)
        {
            TimeSpan? splitTime = state.Run[splitIndex - 1].Comparisons[comparison][state.CurrentTimingMethod];
            if (splitTime != null)
            {
                prevTime = splitTime.Value;
                break;
            }

            splitIndex--;
            bestSegments += state.Run[splitIndex].BestSegmentTime[state.CurrentTimingMethod];
        }

        TimeSpan? time = state.Run[splitIndex].Comparisons[comparison][state.CurrentTimingMethod] - prevTime - bestSegments;
        return time < TimeSpan.Zero ? TimeSpan.Zero : time;
    }

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        string comparison = ResolveComparison(state, Settings.Comparison);
        string suffix = ComparisonSuffix(Settings.Comparison, comparison);
        string componentName = "Previous Segment" + suffix;
        InternalComponent.LongestString = componentName;
        InternalComponent.InformationName = componentName;

        DeltaFormatter.Accuracy = Settings.DeltaAccuracy;
        DeltaFormatter.DropDecimals = Settings.DropDecimals;
        TimeSaveFormatter.Accuracy = Settings.TimeSaveAccuracy;

        TimeSpan? timeChange = null;
        TimeSpan? timeSave = null;
        TimeSpan? liveSegment = LiveSplitStateHelper.CheckLiveDelta(state, false, comparison, state.CurrentTimingMethod);
        if (state.CurrentPhase != TimerPhase.NotRunning)
        {
            if (liveSegment != null)
            {
                timeChange = liveSegment;
                timeSave = GetPossibleTimeSave(state, state.CurrentSplitIndex, comparison);
                InternalComponent.InformationName = "Live Segment" + suffix;
            }
            else if (state.CurrentSplitIndex > 0)
            {
                timeChange = LiveSplitStateHelper.GetPreviousSegmentDelta(state, state.CurrentSplitIndex - 1, comparison, state.CurrentTimingMethod);
                timeSave = GetPossibleTimeSave(state, state.CurrentSplitIndex - 1, comparison);
            }

            if (timeChange != null)
            {
                InternalComponent.ValueLabel.ForeColor = liveSegment != null
                    ? LiveSplitStateHelper.GetSplitColor(state, timeChange, state.CurrentSplitIndex, false, false, comparison, state.CurrentTimingMethod).Value
                    : LiveSplitStateHelper.GetSplitColor(state, timeChange.Value, state.CurrentSplitIndex - 1, false, true, comparison, state.CurrentTimingMethod).Value;
            }
            else
            {
                System.Drawing.Color? color = LiveSplitStateHelper.GetSplitColor(state, null, state.CurrentSplitIndex - 1, true, true, comparison, state.CurrentTimingMethod)
                    ?? (Settings.OverrideTextColor ? Settings.TextColor : state.LayoutSettings.TextColor);
                InternalComponent.ValueLabel.ForeColor = color.Value;
            }
        }
        else
        {
            InternalComponent.ValueLabel.ForeColor = Settings.OverrideTextColor ? Settings.TextColor : state.LayoutSettings.TextColor;
        }

        if (InternalComponent.InformationName != previousNameText)
        {
            InternalComponent.AlternateNameText.Clear();
            if (liveSegment != null)
            {
                InternalComponent.AlternateNameText.Add("Live Segment");
                InternalComponent.AlternateNameText.Add("Live Seg.");
            }
            else
            {
                InternalComponent.AlternateNameText.Add("Previous Segment");
                InternalComponent.AlternateNameText.Add("Prev. Segment");
                InternalComponent.AlternateNameText.Add("Prev. Seg.");
            }

            previousNameText = InternalComponent.InformationName;
        }

        InternalComponent.InformationValue = DeltaFormatter.Format(timeChange)
            + (Settings.ShowPossibleTimeSave ? " / " + TimeSaveFormatter.Format(timeSave) : "");

        InternalComponent.Update(invalidator, state, width, height, mode);
    }
}

#endregion

#region Sum of Best

public class SumOfBestSettings : InfoTimeSettings
{
    public TimeAccuracy Accuracy { get; set; } = TimeAccuracy.Seconds;

    public override void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        TextColor = SettingsHelper.ParseColor(element["TextColor"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        TimeColor = SettingsHelper.ParseColor(element["TimeColor"]);
        OverrideTimeColor = SettingsHelper.ParseBool(element["OverrideTimeColor"]);
        Accuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["Accuracy"]);
        ParseBackground(element);
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"], false);
    }

    protected override int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.4") ^
            SettingsHelper.CreateSetting(document, parent, "TextColor", TextColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
            SettingsHelper.CreateSetting(document, parent, "TimeColor", TimeColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTimeColor", OverrideTimeColor) ^
            SettingsHelper.CreateSetting(document, parent, "Accuracy", Accuracy) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
            SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows);
    }
}

[GlobalFontConsumer(GlobalFont.TimesFont | GlobalFont.TextFont)]
public class SumOfBestComponent : InfoComponentBase<SumOfBestSettings>
{
    private readonly SplitTimeFormatter formatter;
    private bool previousCalculationMode;
    private TimingMethod previousTimingMethod;

    public TimeSpan? SumOfBestValue { get; set; }

    public SumOfBestComponent(LiveSplitState state)
        : this(state, new SplitTimeFormatter()) { }

    private SumOfBestComponent(LiveSplitState state, SplitTimeFormatter formatter)
        : base(state, new SumOfBestSettings(), new InfoTimeComponent("Sum of Best Segments", null, formatter)
        {
            AlternateNameText = ["Sum of Best", "SoB"]
        })
    {
        this.formatter = formatter;
        state.OnSplit += OnRunChanged;
        state.OnUndoSplit += OnRunChanged;
        state.OnReset += OnReset;
        state.RunManuallyModified += OnRunChanged;
        UpdateSumOfBestValue(state);
    }

    private void OnRunChanged(object sender, EventArgs e)
    {
        UpdateSumOfBestValue(CurrentState);
    }

    private void OnReset(object sender, TimerPhase e)
    {
        UpdateSumOfBestValue(CurrentState);
    }

    private void UpdateSumOfBestValue(LiveSplitState state)
    {
        SumOfBestValue = SumOfBest.CalculateSumOfBest(state.Run, state.Settings.SimpleSumOfBest, true, state.CurrentTimingMethod);
        previousCalculationMode = state.Settings.SimpleSumOfBest;
        previousTimingMethod = state.CurrentTimingMethod;
    }

    public override string ComponentName => "Sum of Best";

    protected override void PrepareDraw(LiveSplitState state)
    {
        base.PrepareDraw(state);
        formatter.Accuracy = Settings.Accuracy;
    }

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        if (previousCalculationMode != state.Settings.SimpleSumOfBest || previousTimingMethod != state.CurrentTimingMethod)
        {
            UpdateSumOfBestValue(state);
        }

        formatter.Accuracy = Settings.Accuracy;
        ((InfoTimeComponent)InternalComponent).TimeValue = SumOfBestValue;
        InternalComponent.Update(invalidator, state, width, height, mode);
    }

    public override void Dispose()
    {
        CurrentState.OnSplit -= OnRunChanged;
        CurrentState.OnUndoSplit -= OnRunChanged;
        CurrentState.OnReset -= OnReset;
        CurrentState.RunManuallyModified -= OnRunChanged;
    }
}

#endregion

#region Possible Time Save

public class PossibleTimeSaveSettings : InfoTimeSettings
{
    [Setting(Options = ["{Comparisons}"])]
    public string Comparison { get; set; } = "Current Comparison";
    public bool TotalTimeSave { get; set; }
    public TimeAccuracy Accuracy { get; set; } = TimeAccuracy.Hundredths;
    public bool DropDecimals { get; set; }

    public override void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        TextColor = SettingsHelper.ParseColor(element["TextColor"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        TimeColor = SettingsHelper.ParseColor(element["TimeColor"]);
        OverrideTimeColor = SettingsHelper.ParseBool(element["OverrideTimeColor"]);
        Accuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["Accuracy"]);
        DropDecimals = SettingsHelper.ParseBool(element["DropDecimals"], false);
        ParseBackground(element);
        Comparison = SettingsHelper.ParseString(element["Comparison"]);
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"], false);
        TotalTimeSave = SettingsHelper.ParseBool(element["TotalTimeSave"], false);
    }

    protected override int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.5") ^
            SettingsHelper.CreateSetting(document, parent, "TextColor", TextColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
            SettingsHelper.CreateSetting(document, parent, "TimeColor", TimeColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTimeColor", OverrideTimeColor) ^
            SettingsHelper.CreateSetting(document, parent, "Accuracy", Accuracy) ^
            SettingsHelper.CreateSetting(document, parent, "DropDecimals", DropDecimals) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
            SettingsHelper.CreateSetting(document, parent, "Comparison", Comparison) ^
            SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
            SettingsHelper.CreateSetting(document, parent, "TotalTimeSave", TotalTimeSave);
    }
}

[GlobalFontConsumer(GlobalFont.TimesFont | GlobalFont.TextFont)]
public class PossibleTimeSave : InfoComponentBase<PossibleTimeSaveSettings>
{
    private readonly PossibleTimeSaveFormatter formatter;

    public PossibleTimeSave(LiveSplitState state)
        : this(state, new PossibleTimeSaveFormatter()) { }

    private PossibleTimeSave(LiveSplitState state, PossibleTimeSaveFormatter formatter)
        : base(state, new PossibleTimeSaveSettings(), new InfoTimeComponent(null, null, formatter))
    {
        this.formatter = formatter;
        TrackComparisonRenames(() => Settings.Comparison, x => Settings.Comparison = x);
    }

    public override string ComponentName
        => (Settings.TotalTimeSave ? "Total " : "") + "Possible Time Save"
            + (Settings.Comparison == "Current Comparison"
                ? ""
                : " (" + CompositeComparisons.GetShortComparisonName(Settings.Comparison) + ")");

    protected override void PrepareDraw(LiveSplitState state)
    {
        base.PrepareDraw(state);
        formatter.Accuracy = Settings.Accuracy;
        formatter.DropDecimals = Settings.DropDecimals;
    }

    public static TimeSpan? GetPossibleTimeSave(LiveSplitState state, ISegment segment, string comparison, bool live = false)
    {
        int splitIndex = state.Run.IndexOf(segment);
        TimeSpan prevTime = TimeSpan.Zero;
        TimeSpan? bestSegments = state.Run[splitIndex].BestSegmentTime[state.CurrentTimingMethod];

        while (splitIndex > 0 && bestSegments != null)
        {
            TimeSpan? splitTime = state.Run[splitIndex - 1].Comparisons[comparison][state.CurrentTimingMethod];
            if (splitTime != null)
            {
                prevTime = splitTime.Value;
                break;
            }

            splitIndex--;
            bestSegments += state.Run[splitIndex].BestSegmentTime[state.CurrentTimingMethod];
        }

        TimeSpan? time = segment.Comparisons[comparison][state.CurrentTimingMethod] - prevTime - bestSegments;

        if (live && splitIndex == state.CurrentSplitIndex)
        {
            TimeSpan? segmentDelta = TimeSpan.Zero - LiveSplitStateHelper.GetLiveSegmentDelta(state, state.Run.IndexOf(segment), comparison, state.CurrentTimingMethod);
            if (segmentDelta < time)
            {
                time = segmentDelta;
            }
        }

        return time < TimeSpan.Zero ? TimeSpan.Zero : time;
    }

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        string comparison = ResolveComparison(state, Settings.Comparison);
        string componentName = (Settings.TotalTimeSave ? "Total " : "") + "Possible Time Save" + ComparisonSuffix(Settings.Comparison, comparison);

        if (InternalComponent.InformationName != componentName)
        {
            InternalComponent.AlternateNameText.Clear();
            if (componentName.Contains("Total"))
            {
                InternalComponent.AlternateNameText.Add("Total Possible Time Save");
            }

            InternalComponent.AlternateNameText.Add("Possible Time Save");
            InternalComponent.AlternateNameText.Add("Poss. Time Save");
            InternalComponent.AlternateNameText.Add("Time Save");
        }

        InternalComponent.LongestString = componentName;
        InternalComponent.InformationName = componentName;
        formatter.Accuracy = Settings.Accuracy;
        formatter.DropDecimals = Settings.DropDecimals;

        var timeComponent = (InfoTimeComponent)InternalComponent;
        if (Settings.TotalTimeSave)
        {
            timeComponent.TimeValue = state.CurrentPhase == TimerPhase.Ended
                ? TimeSpan.Zero
                : state.Run
                    .Skip(Math.Max(state.CurrentSplitIndex, 0))
                    .Select(x => GetPossibleTimeSave(state, x, comparison, true))
                    .Where(x => x.HasValue)
                    .Aggregate((TimeSpan?)TimeSpan.Zero, (a, b) => a + b);
        }
        else
        {
            timeComponent.TimeValue = state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused
                ? GetPossibleTimeSave(state, state.CurrentSplit, comparison)
                : null;
        }

        InternalComponent.Update(invalidator, state, width, height, mode);
    }
}

#endregion

#region Run Prediction

public class RunPredictionSettings : InfoTimeSettings
{
    [Setting(Options = ["{Comparisons}"])]
    public string Comparison { get; set; } = "Current Comparison";
    public TimeAccuracy Accuracy { get; set; } = TimeAccuracy.Seconds;

    public override void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        TextColor = SettingsHelper.ParseColor(element["TextColor"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        TimeColor = SettingsHelper.ParseColor(element["TimeColor"]);
        OverrideTimeColor = SettingsHelper.ParseBool(element["OverrideTimeColor"]);
        Accuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["Accuracy"]);
        ParseBackground(element);
        Comparison = SettingsHelper.ParseString(element["Comparison"]);
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"], false);
    }

    protected override int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.4") ^
            SettingsHelper.CreateSetting(document, parent, "TextColor", TextColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
            SettingsHelper.CreateSetting(document, parent, "TimeColor", TimeColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTimeColor", OverrideTimeColor) ^
            SettingsHelper.CreateSetting(document, parent, "Accuracy", Accuracy) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
            SettingsHelper.CreateSetting(document, parent, "Comparison", Comparison) ^
            SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows);
    }
}

[GlobalFontConsumer(GlobalFont.TimesFont | GlobalFont.TextFont)]
public class RunPrediction : InfoComponentBase<RunPredictionSettings>
{
    private readonly SplitTimeFormatter formatter;
    private string previousInformationName;

    public RunPrediction(LiveSplitState state)
        : this(state, new SplitTimeFormatter(TimeAccuracy.Seconds)) { }

    private RunPrediction(LiveSplitState state, SplitTimeFormatter formatter)
        : base(state, new RunPredictionSettings(), new InfoTimeComponent(null, null, formatter))
    {
        this.formatter = formatter;
        TrackComparisonRenames(() => Settings.Comparison, x => Settings.Comparison = x);
    }

    public override string ComponentName => GetDisplayedName(Settings.Comparison);

    protected override void PrepareDraw(LiveSplitState state)
    {
        base.PrepareDraw(state);
        formatter.Accuracy = Settings.Accuracy;
    }

    protected static string GetDisplayedName(string comparison)
    {
        return comparison switch
        {
            "Current Comparison" => "Current Pace",
            Run.PersonalBestComparisonName => "Current Pace",
            BestSegmentsComparisonGenerator.ComparisonName => "Best Possible Time",
            WorstSegmentsComparisonGenerator.ComparisonName => "Worst Possible Time",
            AverageSegmentsComparisonGenerator.ComparisonName => "Predicted Time",
            _ => "Current Pace (" + CompositeComparisons.GetShortComparisonName(comparison) + ")",
        };
    }

    protected void SetAlternateText(string comparison)
    {
        InternalComponent.AlternateNameText = comparison switch
        {
            "Current Comparison" or Run.PersonalBestComparisonName => ["Cur. Pace", "Pace"],
            BestSegmentsComparisonGenerator.ComparisonName => ["Best Poss. Time", "Best Time", "BPT"],
            WorstSegmentsComparisonGenerator.ComparisonName => ["Worst Poss. Time", "Worst Time"],
            AverageSegmentsComparisonGenerator.ComparisonName => ["Pred. Time"],
            _ => ["Current Pace", "Cur. Pace", "Pace"],
        };
    }

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        string comparison = ResolveComparison(state, Settings.Comparison);

        InternalComponent.InformationName = InternalComponent.LongestString = GetDisplayedName(comparison);
        if (InternalComponent.InformationName != previousInformationName)
        {
            SetAlternateText(comparison);
            previousInformationName = InternalComponent.InformationName;
        }

        formatter.Accuracy = Settings.Accuracy;
        var timeComponent = (InfoTimeComponent)InternalComponent;
        if (InternalComponent.InformationName.StartsWith("Current Pace") && state.CurrentPhase == TimerPhase.NotRunning)
        {
            timeComponent.TimeValue = null;
        }
        else if (state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused)
        {
            TimeSpan? delta = LiveSplitStateHelper.GetLastDelta(state, state.CurrentSplitIndex, comparison, state.CurrentTimingMethod) ?? TimeSpan.Zero;
            TimeSpan? liveDelta = state.CurrentTime[state.CurrentTimingMethod] - state.CurrentSplit.Comparisons[comparison][state.CurrentTimingMethod];
            if (liveDelta > delta)
            {
                delta = liveDelta;
            }

            timeComponent.TimeValue = delta + state.Run[^1].Comparisons[comparison][state.CurrentTimingMethod];
        }
        else
        {
            timeComponent.TimeValue = state.CurrentPhase == TimerPhase.Ended
                ? state.Run[^1].SplitTime[state.CurrentTimingMethod]
                : state.Run[^1].Comparisons[comparison][state.CurrentTimingMethod];
        }

        InternalComponent.Update(invalidator, state, width, height, mode);
    }
}

#endregion
