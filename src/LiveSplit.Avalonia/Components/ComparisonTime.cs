using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.TimeFormatters;
using System;
using System.Xml;

namespace LiveSplit.UI.Components;

public enum TimeType
{
    FinalTime,
    SplitTime,
    SegmentTime,
}

public class ComparisonTimeSettings : InfoTimeSettings
{
    [Setting(Options = ["{Comparisons}"])]
    public string Comparison { get; set; } = "Current Comparison";
    [Setting("Timing Method", Options = ["Current Timing Method", "Real Time", "Game Time"])]
    public string TimingMethod { get; set; } = "Current Timing Method";
    public TimeType Type { get; set; } = TimeType.FinalTime;
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
        TimingMethod = SettingsHelper.ParseString(element["TimingMethod"], "Current Timing Method");
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"], false);
        Type = SettingsHelper.ParseEnum<TimeType>(element["Type"]);
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
            SettingsHelper.CreateSetting(document, parent, "TimingMethod", TimingMethod) ^
            SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
            SettingsHelper.CreateSetting(document, parent, "Type", Type);
    }
}

[GlobalFontConsumer(GlobalFont.TimesFont | GlobalFont.TextFont)]
public class ComparisonTime : InfoComponentBase<ComparisonTimeSettings>
{
    private readonly SplitTimeFormatter formatter;
    private string previousInformationName;

    public ComparisonTime(LiveSplitState state)
        : this(state, new SplitTimeFormatter(TimeAccuracy.Seconds)) { }

    private ComparisonTime(LiveSplitState state, SplitTimeFormatter formatter)
        : base(state, new ComparisonTimeSettings(), new InfoTimeComponent(null, null, formatter))
    {
        this.formatter = formatter;
        TrackComparisonRenames(() => Settings.Comparison, x => Settings.Comparison = x);
    }

    public override string ComponentName
    {
        get
        {
            bool isComparisonOverride = Settings.Comparison != "Current Comparison";
            bool isTimingMethodOverride = Settings.TimingMethod != "Current Timing Method";
            return isComparisonOverride && isTimingMethodOverride ? $"Comparison Time ({Settings.Comparison}, {Settings.TimingMethod})"
                : isComparisonOverride ? $"Comparison Time ({Settings.Comparison})"
                : isTimingMethodOverride ? $"Comparison Time ({Settings.TimingMethod})"
                : "Comparison Time";
        }
    }

    protected override void PrepareDraw(LiveSplitState state)
    {
        base.PrepareDraw(state);
        formatter.Accuracy = Settings.Accuracy;
    }

    private void SetAlternateText(string comparison)
    {
        string shortName = comparison switch
        {
            Run.PersonalBestComparisonName => "PB",
            AverageSegmentsComparisonGenerator.ComparisonName => AverageSegmentsComparisonGenerator.ShortComparisonName,
            BestSegmentsComparisonGenerator.ComparisonName => BestSegmentsComparisonGenerator.ShortComparisonName,
            LatestRunComparisonGenerator.ComparisonName => LatestRunComparisonGenerator.ShortComparisonName,
            MedianSegmentsComparisonGenerator.ComparisonName => MedianSegmentsComparisonGenerator.ShortComparisonName,
            PercentileComparisonGenerator.ComparisonName => PercentileComparisonGenerator.ShortComparisonName,
            WorstSegmentsComparisonGenerator.ComparisonName => WorstSegmentsComparisonGenerator.ShortComparisonName,
            _ => null
        };

        if (shortName != null)
        {
            InternalComponent.AlternateNameText = [shortName];
        }
    }

    private TimeSpan? GetTimeValue(LiveSplitState state, string comparison, TimingMethod timingMethod)
    {
        if (Settings.Type == TimeType.FinalTime)
        {
            return state.Run[^1].Comparisons[comparison][timingMethod];
        }

        if (state.CurrentPhase == TimerPhase.NotRunning)
        {
            return null;
        }

        TimeSpan? currentSplitComparisonTime = state.CurrentPhase == TimerPhase.Ended
            ? state.Run[^1].Comparisons[comparison][timingMethod]
            : state.Run[state.CurrentSplitIndex].Comparisons[comparison][timingMethod];

        if (Settings.Type == TimeType.SplitTime)
        {
            return currentSplitComparisonTime;
        }

        int previousSplitIndex = state.CurrentPhase == TimerPhase.Ended ? state.Run.Count - 2 : state.CurrentSplitIndex - 1;
        TimeSpan? previousSplitComparisonTime = previousSplitIndex < 0
            ? TimeSpan.Zero
            : state.Run[previousSplitIndex].Comparisons[comparison][timingMethod];

        return currentSplitComparisonTime - previousSplitComparisonTime;
    }

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        string comparison = ResolveComparison(state, Settings.Comparison);

        TimingMethod timingMethod = Settings.TimingMethod switch
        {
            "Real Time" => TimingMethod.RealTime,
            "Game Time" => TimingMethod.GameTime,
            _ => state.CurrentTimingMethod
        };

        InternalComponent.InformationName = InternalComponent.LongestString = Settings.TimingMethod != "Current Timing Method"
            ? $"{comparison} ({Settings.TimingMethod})"
            : comparison;

        if (InternalComponent.InformationName != previousInformationName)
        {
            SetAlternateText(comparison);
            previousInformationName = InternalComponent.InformationName;
        }

        formatter.Accuracy = Settings.Accuracy;
        ((InfoTimeComponent)InternalComponent).TimeValue = GetTimeValue(state, comparison, timingMethod);
        InternalComponent.Update(invalidator, state, width, height, mode);
    }
}
