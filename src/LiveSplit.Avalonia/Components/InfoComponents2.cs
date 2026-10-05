using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.TimeFormatters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;
using Color = System.Drawing.Color;
using Font = LiveSplit.Drawing.Font;

namespace LiveSplit.UI.Components;

#region Delta

public class DeltaSettings : InfoSettings
{
    [Setting(Options = ["{Comparisons}"])]
    public string Comparison { get; set; } = "Current Comparison";
    public TimeAccuracy Accuracy { get; set; } = TimeAccuracy.Tenths;
    public bool DropDecimals { get; set; } = true;
    public bool OverrideText { get; set; }
    public bool DifferentialText { get; set; }
    public string CustomText { get; set; } = "";
    public string CustomTextAhead { get; set; } = "";

    public override void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        TextColor = SettingsHelper.ParseColor(element["TextColor"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        Accuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["Accuracy"]);
        ParseBackground(element);
        Comparison = SettingsHelper.ParseString(element["Comparison"]);
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"]);
        DropDecimals = SettingsHelper.ParseBool(element["DropDecimals"]);
        OverrideText = SettingsHelper.ParseBool(element["OverrideText"]);
        DifferentialText = SettingsHelper.ParseBool(element["DifferentialText"]);
        CustomText = SettingsHelper.ParseString(element["CustomText"]);
        CustomTextAhead = SettingsHelper.ParseString(element["CustomTextAhead"]);
    }

    protected override int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.4") ^
            SettingsHelper.CreateSetting(document, parent, "TextColor", TextColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
            SettingsHelper.CreateSetting(document, parent, "Accuracy", Accuracy) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
            SettingsHelper.CreateSetting(document, parent, "Comparison", Comparison) ^
            SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
            SettingsHelper.CreateSetting(document, parent, "DropDecimals", DropDecimals) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideText", OverrideText) ^
            SettingsHelper.CreateSetting(document, parent, "DifferentialText", DifferentialText) ^
            SettingsHelper.CreateSetting(document, parent, "CustomText", CustomText) ^
            SettingsHelper.CreateSetting(document, parent, "CustomTextAhead", CustomTextAhead);
    }
}

[GlobalFontConsumer(GlobalFont.TimesFont | GlobalFont.TextFont)]
public class DeltaComponent : InfoComponentBase<DeltaSettings>
{
    private readonly GeneralTimeFormatter formatter;

    public DeltaComponent(LiveSplitState state)
        : this(state, new GeneralTimeFormatter { NullFormat = NullFormat.Dash, ShowPlus = true }) { }

    private DeltaComponent(LiveSplitState state, GeneralTimeFormatter formatter)
        : base(state, new DeltaSettings(), new InfoTimeComponent(null, null, formatter))
    {
        this.formatter = formatter;
        formatter.Accuracy = Settings.Accuracy;
        formatter.DropDecimals = Settings.DropDecimals;
        TrackComparisonRenames(() => Settings.Comparison, x => Settings.Comparison = x);
    }

    public override string ComponentName
        => "Delta" + (Settings.Comparison == "Current Comparison"
            ? ""
            : " (" + CompositeComparisons.GetShortComparisonName(Settings.Comparison) + ")");

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        string comparison = ResolveComparison(state, Settings.Comparison);
        string comparisonName = comparison.StartsWith("[Race] ") ? comparison[7..] : comparison;

        formatter.Accuracy = Settings.Accuracy;
        formatter.DropDecimals = Settings.DropDecimals;

        var timeComponent = (InfoTimeComponent)InternalComponent;
        bool useLiveDelta = false;
        if (state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused)
        {
            TimeSpan? delta = LiveSplitStateHelper.GetLastDelta(state, state.CurrentSplitIndex, comparison, state.CurrentTimingMethod);
            TimeSpan? liveDelta = state.CurrentTime[state.CurrentTimingMethod] - state.CurrentSplit.Comparisons[comparison][state.CurrentTimingMethod];
            if (liveDelta > delta || (delta == null && liveDelta > TimeSpan.Zero))
            {
                delta = liveDelta;
                useLiveDelta = true;
            }

            timeComponent.TimeValue = delta;
        }
        else
        {
            timeComponent.TimeValue = state.CurrentPhase == TimerPhase.Ended
                ? state.Run[^1].SplitTime[state.CurrentTimingMethod] - state.Run[^1].Comparisons[comparison][state.CurrentTimingMethod]
                : null;
        }

        string text = comparisonName;
        if (Settings.OverrideText)
        {
            InternalComponent.AlternateNameText.Clear();
            if (Settings.DifferentialText)
            {
                text = timeComponent.TimeValue < TimeSpan.Zero ? Settings.CustomTextAhead : Settings.CustomText;
                InternalComponent.LongestString = Settings.CustomText.Length < Settings.CustomTextAhead.Length ? Settings.CustomTextAhead : Settings.CustomText;
            }
            else
            {
                text = Settings.CustomText;
                InternalComponent.LongestString = text;
            }
        }
        else
        {
            InternalComponent.LongestString = text;
            if (InternalComponent.InformationName != text)
            {
                InternalComponent.AlternateNameText.Clear();
                InternalComponent.AlternateNameText.Add(CompositeComparisons.GetShortComparisonName(comparison));
            }
        }

        InternalComponent.InformationName = text;

        Color? color = LiveSplitStateHelper.GetSplitColor(state, timeComponent.TimeValue, state.CurrentSplitIndex - (useLiveDelta ? 0 : 1), true, false, comparison, state.CurrentTimingMethod)
            ?? (Settings.OverrideTextColor ? Settings.TextColor : state.LayoutSettings.TextColor);
        InternalComponent.ValueLabel.ForeColor = color.Value;

        InternalComponent.Update(invalidator, state, width, height, mode);
    }
}

#endregion

#region Current Comparison

public class CurrentComparisonSettings : InfoTimeSettings
{
    public override void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        TextColor = SettingsHelper.ParseColor(element["TextColor"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        TimeColor = SettingsHelper.ParseColor(element["TimeColor"]);
        OverrideTimeColor = SettingsHelper.ParseBool(element["OverrideTimeColor"]);
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
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
            SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows);
    }
}

[GlobalFontConsumer(GlobalFont.TextFont)]
public class CurrentComparison : InfoComponentBase<CurrentComparisonSettings>
{
    public CurrentComparison(LiveSplitState state)
        : base(state, new CurrentComparisonSettings(), new InfoTextComponent("Comparing Against", "")
        {
            AlternateNameText = ["Comparison", "Comp."]
        })
    { }

    public override string ComponentName => "Current Comparison";

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        InternalComponent.LongestString = InternalComponent.InformationName;
        InternalComponent.InformationValue = state.CurrentComparison;
        InternalComponent.Update(invalidator, state, width, height, mode);
    }
}

#endregion

#region Total Playtime

public class DaysTimeFormatter : ITimeFormatter
{
    public string Format(TimeSpan? time)
    {
        if (!time.HasValue)
        {
            return "0";
        }

        var builder = new StringBuilder();
        if (time.Value.TotalDays >= 1)
        {
            builder.Append((int)time.Value.TotalDays).Append("d ");
        }

        builder.Append(time.Value.ToString(time.Value.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}

public class TotalPlaytimeSettings : InfoTimeSettings
{
    public bool ShowTotalHours { get; set; }

    public override void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        TextColor = SettingsHelper.ParseColor(element["TextColor"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        TimeColor = SettingsHelper.ParseColor(element["TimeColor"]);
        OverrideTimeColor = SettingsHelper.ParseBool(element["OverrideTimeColor"]);
        ParseBackground(element);
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"]);
        ShowTotalHours = SettingsHelper.ParseBool(element["ShowTotalHours"], false);
    }

    protected override int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.6") ^
            SettingsHelper.CreateSetting(document, parent, "TextColor", TextColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
            SettingsHelper.CreateSetting(document, parent, "TimeColor", TimeColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTimeColor", OverrideTimeColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
            SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
            SettingsHelper.CreateSetting(document, parent, "ShowTotalHours", ShowTotalHours);
    }
}

[GlobalFontConsumer(GlobalFont.TimesFont | GlobalFont.TextFont)]
public class TotalPlaytimeComponent : InfoComponentBase<TotalPlaytimeSettings>
{
    private readonly ITimeFormatter hoursTimeFormatter = new RegularTimeFormatter();
    private readonly ITimeFormatter daysTimeFormatter = new DaysTimeFormatter();
    private TimerPhase lastPhase;
    private int lastAttemptCount;
    private IRun lastRun;

    public TotalPlaytimeComponent(LiveSplitState state)
        : base(state, new TotalPlaytimeSettings(), new InfoTimeComponent("Total Playtime", TimeSpan.Zero, new DaysTimeFormatter()))
    { }

    public override string ComponentName => "Total Playtime";

    public static TimeSpan CalculateTotalPlaytime(LiveSplitState state)
    {
        TimeSpan totalPlaytime = TimeSpan.Zero;

        foreach (Attempt attempt in state.Run.AttemptHistory)
        {
            TimeSpan? duration = attempt.Duration;
            if (duration.HasValue)
            {
                // Either >= 1.6.0 or a finished run
                totalPlaytime += duration.Value - (attempt.PauseTime ?? TimeSpan.Zero);
            }
            else
            {
                // Must be < 1.6.0 and a reset: sum the segments of that attempt
                foreach (ISegment segment in state.Run)
                {
                    if (segment.SegmentHistory.TryGetValue(attempt.Index, out Time segmentHistoryElement) && segmentHistoryElement.RealTime.HasValue)
                    {
                        totalPlaytime += segmentHistoryElement.RealTime.Value;
                    }
                }
            }
        }

        totalPlaytime += state.CurrentAttemptDuration - (state.PauseTime ?? TimeSpan.Zero);

        return totalPlaytime;
    }

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        var timeComponent = (InfoTimeComponent)InternalComponent;
        timeComponent.Formatter = Settings.ShowTotalHours ? hoursTimeFormatter : daysTimeFormatter;

        if (lastAttemptCount != state.Run.AttemptHistory.Count
            || lastPhase != state.CurrentPhase
            || lastRun != state.Run
            || state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused)
        {
            timeComponent.TimeValue = CalculateTotalPlaytime(state);

            lastAttemptCount = state.Run.AttemptHistory.Count;
            lastPhase = state.CurrentPhase;
            lastRun = state.Run;
        }

        InternalComponent.Update(invalidator, state, width, height, mode);
    }
}

#endregion

#region Text

public class TextComponentSettings : InfoTimeSettings
{
    [Setting("Left Text")]
    public string Text1 { get; set; } = "Text";
    [Setting("Right Text")]
    public string Text2 { get; set; } = "";
    [Setting("Right Text Is A Custom Variable")]
    public bool CustomVariable { get; set; }
    [Hidden] public bool OverrideFont1 { get; set; }
    [Hidden] public Font Font1 { get; set; }
    [Hidden] public bool OverrideFont2 { get; set; }
    [Hidden] public Font Font2 { get; set; }

    public override void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        TextColor = SettingsHelper.ParseColor(element["TextColor"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        TimeColor = SettingsHelper.ParseColor(element["TimeColor"]);
        OverrideTimeColor = SettingsHelper.ParseBool(element["OverrideTimeColor"]);
        ParseBackground(element);
        Text1 = SettingsHelper.ParseString(element["Text1"]);
        Text2 = SettingsHelper.ParseString(element["Text2"]);
        Font1 = SettingsHelper.GetFontFromElement(element["Font1"]);
        Font2 = SettingsHelper.GetFontFromElement(element["Font2"]);
        OverrideFont1 = SettingsHelper.ParseBool(element["OverrideFont1"]);
        OverrideFont2 = SettingsHelper.ParseBool(element["OverrideFont2"]);
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"], false);
        CustomVariable = SettingsHelper.ParseBool(element["CustomVariable"], false);
    }

    protected override int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.5") ^
            SettingsHelper.CreateSetting(document, parent, "TextColor", TextColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
            SettingsHelper.CreateSetting(document, parent, "TimeColor", TimeColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTimeColor", OverrideTimeColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
            SettingsHelper.CreateSetting(document, parent, "Text1", Text1) ^
            SettingsHelper.CreateSetting(document, parent, "Text2", Text2) ^
            SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
            SettingsHelper.CreateSetting(document, parent, "CustomVariable", CustomVariable);
    }
}

public class TextTextComponent(TextComponentSettings settings) : InfoTextComponent("", "")
{
    public TextComponentSettings Settings { get; set; } = settings;

    public override void PrepareDraw(LiveSplitState state, LayoutMode mode)
    {
        NameMeasureLabel.Font = Settings.OverrideFont1 && Settings.Font1 != null ? Settings.Font1 : state.LayoutSettings.TextFont;
        ValueLabel.Font = Settings.OverrideFont2 && Settings.Font2 != null ? Settings.Font2 : state.LayoutSettings.TextFont;
        NameLabel.Font = Settings.OverrideFont1 && Settings.Font1 != null ? Settings.Font1 : state.LayoutSettings.TextFont;
    }
}

[GlobalFontConsumer(GlobalFont.TextFont)]
public class TextComponent : InfoComponentBase<TextComponentSettings>, IFontOverridesMigration
{
    public TextComponent(LiveSplitState state)
        : this(state, new TextComponentSettings()) { }

    private TextComponent(LiveSplitState state, TextComponentSettings settings)
        : base(state, settings, new TextTextComponent(settings))
    { }

    public override string ComponentName => string.Join(" ", Settings.Text1, Settings.Text2);

    protected override void PrepareDraw(LiveSplitState state)
    {
        base.PrepareDraw(state);
        LayoutMode mode = state.Layout?.Mode ?? LayoutMode.Vertical;

        if (string.IsNullOrEmpty(Settings.Text1) || string.IsNullOrEmpty(Settings.Text2))
        {
            InternalComponent.NameLabel.HorizontalAlignment = StringAlignment.Center;
            InternalComponent.ValueLabel.HorizontalAlignment = StringAlignment.Center;
            InternalComponent.NameLabel.VerticalAlignment = StringAlignment.Center;
            InternalComponent.ValueLabel.VerticalAlignment = StringAlignment.Center;
        }
        else
        {
            InternalComponent.NameLabel.HorizontalAlignment = StringAlignment.Near;
            InternalComponent.ValueLabel.HorizontalAlignment = StringAlignment.Far;
            InternalComponent.NameLabel.VerticalAlignment =
                mode == LayoutMode.Horizontal || Settings.Display2Rows ? StringAlignment.Near : StringAlignment.Center;
            InternalComponent.ValueLabel.VerticalAlignment =
                mode == LayoutMode.Horizontal || Settings.Display2Rows ? StringAlignment.Far : StringAlignment.Center;
        }
    }

    public void MigrateFontOverrides(Options.FontOverrides overrides)
    {
        if (Settings.OverrideFont1 && Settings.Font1 != null)
        {
            overrides.OverrideTextFont = true;
            overrides.TextFont = (Font)Settings.Font1.Clone();
            Settings.OverrideFont1 = false;
        }
    }

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        string text1 = Settings.Text1 ?? "";
        string text2Value = Settings.CustomVariable
            ? (state.Run.Metadata.CustomVariableValue(Settings.Text2) ?? TimeFormatConstants.DASH)
            : Settings.Text2 ?? "";

        InternalComponent.InformationName = text1;
        InternalComponent.InformationValue = text2Value;
        InternalComponent.LongestString = text1.Length > text2Value.Length ? text1 : text2Value;

        InternalComponent.Update(invalidator, state, width, height, mode);
    }
}

#endregion

#region Blank Space

public class BlankSpaceSettings : ComponentSettings
{
    [Setting("Height", Minimum = 1, Maximum = 1000)]
    public float SpaceHeight { get; set; } = 100;
    [Setting("Width", Minimum = 1, Maximum = 1000)]
    public float SpaceWidth { get; set; } = 100;
    public GradientType BackgroundGradient { get; set; } = GradientType.Plain;
    public Color BackgroundColor { get; set; } = Color.Transparent;
    public Color BackgroundColor2 { get; set; } = Color.Transparent;

    public override void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        SpaceHeight = SettingsHelper.ParseFloat(element["SpaceHeight"]);
        SpaceWidth = SettingsHelper.ParseFloat(element["SpaceWidth"]);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.Transparent);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.Transparent);
        BackgroundGradient = Enum.Parse<GradientType>(SettingsHelper.ParseString(element["BackgroundGradient"], GradientType.Plain.ToString()));
    }

    protected override int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.7") ^
            SettingsHelper.CreateSetting(document, parent, "SpaceHeight", SpaceHeight) ^
            SettingsHelper.CreateSetting(document, parent, "SpaceWidth", SpaceWidth) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient);
    }
}

public class BlankSpace : IComponent, ISettingsHashCodeProvider
{
    public BlankSpaceSettings Settings { get; set; } = new();

    public string ComponentName => "Blank Space";

    public float VerticalHeight => Settings.SpaceHeight;
    public float MinimumWidth => 20;
    public float HorizontalWidth => Settings.SpaceWidth;
    public float MinimumHeight => 20;

    public float PaddingTop => 0f;
    public float PaddingLeft => 0f;
    public float PaddingBottom => 0f;
    public float PaddingRight => 0f;

    public IDictionary<string, Action> ContextMenuControls => null;

    private void DrawGeneral(DrawingContext g, float width, float height)
    {
        g.FillGradient(
            Settings.BackgroundColor,
            Settings.BackgroundGradient == GradientType.Plain ? Settings.BackgroundColor : Settings.BackgroundColor2,
            Settings.BackgroundGradient == GradientType.Horizontal,
            width, height);
    }

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion)
    {
        DrawGeneral(g, width, VerticalHeight);
    }

    public void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion)
    {
        DrawGeneral(g, HorizontalWidth, height);
    }

    public Control GetSettingsControl(LayoutMode mode)
    {
        return SettingsEditor.Create(Settings);
    }

    public void SetSettings(XmlNode settings)
    {
        Settings.SetSettings(settings);
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        return Settings.GetSettings(document);
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }

    public void Dispose() { }

    public int GetSettingsHashCode()
    {
        return Settings.GetSettingsHashCode();
    }
}

#endregion
