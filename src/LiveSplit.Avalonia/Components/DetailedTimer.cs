using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.TimeFormatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Color = System.Drawing.Color;
using Font = LiveSplit.Drawing.Font;
using FontStyle = LiveSplit.Drawing.FontStyle;
using GraphicsUnit = LiveSplit.Drawing.GraphicsUnit;

namespace LiveSplit.UI.Components;

public class DetailedTimerSettings : ComponentSettings
{
    [Setting("Height", Minimum = 20, Maximum = 500)]
    public float Height { get; set; } = 75;
    [Setting("Width", Minimum = 20, Maximum = 1000)]
    public float Width { get; set; } = 200;
    [Setting("Segment Timer Size (%)", Minimum = 10, Maximum = 90)]
    public float SegmentTimerSizeRatio { get; set; } = 40;

    [Setting("Timing Method", Options = ["Current Timing Method", "Real Time", "Game Time"])]
    public string TimingMethod { get; set; } = "Current Timing Method";
    [Setting(Options = ["{Comparisons}", "None"])]
    public string Comparison { get; set; } = "Current Comparison";
    [Setting(Options = ["{Comparisons}", "None"])]
    public string Comparison2 { get; set; } = "Best Segments";
    public bool HideComparison { get; set; }

    [Setting("Timer Digits Format", Options = ["1", "00:01", "0:00:01", "00:00:01"])]
    public string DigitsFormat { get; set; } = "1";
    [Setting("Timer Accuracy", Options = ["", ".2", ".23", ".234"])]
    public string Accuracy { get; set; } = ".23";
    [Setting("Timer Decimals Size", Minimum = 10, Maximum = 50)]
    public float DecimalsSize { get; set; } = 35f;
    public bool TimerShowGradient { get; set; } = true;
    public bool OverrideTimerColors { get; set; }
    public Color TimerColor { get; set; } = Color.FromArgb(170, 170, 170);

    [Setting("Segment Timer Digits Format", Options = ["1", "00:01", "0:00:01", "00:00:01"])]
    public string SegmentDigitsFormat { get; set; } = "1";
    [Setting("Segment Timer Accuracy", Options = ["", ".2", ".23", ".234"])]
    public string SegmentAccuracy { get; set; } = ".23";
    [Setting("Segment Timer Decimals Size", Minimum = 10, Maximum = 50)]
    public float SegmentTimerDecimalsSize { get; set; } = 35f;
    public bool SegmentTimerShowGradient { get; set; } = true;
    public Color SegmentTimerColor { get; set; } = Color.FromArgb(170, 170, 170);

    public TimeAccuracy SegmentTimesAccuracy { get; set; } = TimeAccuracy.Hundredths;
    public Color SegmentLabelsColor { get; set; } = Color.FromArgb(255, 255, 255);
    public Color SegmentTimesColor { get; set; } = Color.FromArgb(255, 255, 255);
    public Font SegmentLabelsFont { get; set; } = new("Segoe UI", 13, FontStyle.Regular, GraphicsUnit.Pixel);
    public Font SegmentTimesFont { get; set; } = new("Segoe UI", 13, FontStyle.Bold, GraphicsUnit.Pixel);

    public bool ShowSplitName { get; set; }
    public Color SplitNameColor { get; set; } = Color.FromArgb(255, 255, 255);
    public Font SplitNameFont { get; set; } = new("Segoe UI", 15, FontStyle.Regular, GraphicsUnit.Pixel);

    public bool DisplayIcon { get; set; }
    [Setting("Icon Size", Minimum = 0, Maximum = 256)]
    public float IconSize { get; set; } = 40f;

    public DeltasGradientType BackgroundGradient { get; set; } = DeltasGradientType.Plain;
    public Color BackgroundColor { get; set; } = Color.Transparent;
    public Color BackgroundColor2 { get; set; } = Color.Transparent;

    [Hidden]
    public GeneralTimeFormatter SegmentTimesFormatter { get; } = new()
    {
        NullFormat = NullFormat.Dash,
        Accuracy = TimeAccuracy.Hundredths
    };

    private string TimerFormat
    {
        get => DigitsFormat + Accuracy;
        set => (DigitsFormat, Accuracy) = SplitFormat(value);
    }

    private string SegmentTimerFormat
    {
        get => SegmentDigitsFormat + SegmentAccuracy;
        set => (SegmentDigitsFormat, SegmentAccuracy) = SplitFormat(value);
    }

    private static (string Digits, string Accuracy) SplitFormat(string value)
    {
        int decimalIndex = value.IndexOf('.');
        return decimalIndex < 0 ? (value, "") : (value[..decimalIndex], value[decimalIndex..]);
    }

    private static string AccuracyString(TimeAccuracy accuracy)
    {
        return accuracy switch
        {
            TimeAccuracy.Seconds => "",
            TimeAccuracy.Tenths => ".2",
            TimeAccuracy.Hundredths => ".23",
            TimeAccuracy.Milliseconds => ".234",
            _ => ".23",
        };
    }

    public override void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);
        Height = SettingsHelper.ParseFloat(element["Height"]);
        Width = SettingsHelper.ParseFloat(element["Width"]);
        SegmentTimerSizeRatio = SettingsHelper.ParseFloat(element["SegmentTimerSizeRatio"]);
        TimerShowGradient = SettingsHelper.ParseBool(element["TimerShowGradient"]);
        SegmentTimerShowGradient = SettingsHelper.ParseBool(element["SegmentTimerShowGradient"]);
        TimerColor = SettingsHelper.ParseColor(element["TimerColor"]);
        SegmentTimerColor = SettingsHelper.ParseColor(element["SegmentTimerColor"]);
        SegmentLabelsColor = SettingsHelper.ParseColor(element["SegmentLabelsColor"]);
        SegmentTimesColor = SettingsHelper.ParseColor(element["SegmentTimesColor"]);
        TimingMethod = SettingsHelper.ParseString(element["TimingMethod"], "Current Timing Method");
        DecimalsSize = SettingsHelper.ParseFloat(element["DecimalsSize"], 35f);
        SegmentTimerDecimalsSize = SettingsHelper.ParseFloat(element["SegmentTimerDecimalsSize"], 35f);
        DisplayIcon = SettingsHelper.ParseBool(element["DisplayIcon"], false);
        IconSize = SettingsHelper.ParseFloat(element["IconSize"], 40f);
        ShowSplitName = SettingsHelper.ParseBool(element["ShowSplitName"], false);
        SplitNameColor = SettingsHelper.ParseColor(element["SplitNameColor"], Color.FromArgb(255, 255, 255));
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.Transparent);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.Transparent);
        BackgroundGradient = SettingsHelper.ParseEnum(element["BackgroundGradient"], DeltasGradientType.Plain);
        Comparison = SettingsHelper.ParseString(element["Comparison"], "Current Comparison");
        Comparison2 = SettingsHelper.ParseString(element["Comparison2"], "Best Segments");
        HideComparison = SettingsHelper.ParseBool(element["HideComparison"], false);
        SegmentTimesAccuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["SegmentTimesAccuracy"]);
        SegmentTimesFormatter.Accuracy = SegmentTimesAccuracy;

        if (version >= new Version(1, 3))
        {
            OverrideTimerColors = SettingsHelper.ParseBool(element["OverrideTimerColors"]);
            SegmentLabelsFont = SettingsHelper.GetFontFromElement(element["SegmentLabelsFont"]) ?? SegmentLabelsFont;
            SegmentTimesFont = SettingsHelper.GetFontFromElement(element["SegmentTimesFont"]) ?? SegmentTimesFont;
            SplitNameFont = SettingsHelper.GetFontFromElement(element["SplitNameFont"]) ?? SplitNameFont;
        }
        else
        {
            OverrideTimerColors = !SettingsHelper.ParseBool(element["TimerUseSplitColors"]);
        }

        if (version >= new Version(1, 5))
        {
            TimerFormat = element["TimerFormat"].InnerText;
            SegmentTimerFormat = element["SegmentTimerFormat"].InnerText;
        }
        else
        {
            DigitsFormat = "1";
            SegmentDigitsFormat = "1";
            Accuracy = AccuracyString(SettingsHelper.ParseEnum<TimeAccuracy>(element["TimerAccuracy"]));
            SegmentAccuracy = AccuracyString(SettingsHelper.ParseEnum<TimeAccuracy>(element["SegmentTimerAccuracy"]));
        }
    }

    public override void OnSettingChanged(string propertyName)
    {
        SegmentTimesFormatter.Accuracy = SegmentTimesAccuracy;
        base.OnSettingChanged(propertyName);
    }

    protected override int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.5") ^
            SettingsHelper.CreateSetting(document, parent, "Height", Height) ^
            SettingsHelper.CreateSetting(document, parent, "Width", Width) ^
            SettingsHelper.CreateSetting(document, parent, "SegmentTimerSizeRatio", SegmentTimerSizeRatio) ^
            SettingsHelper.CreateSetting(document, parent, "TimerShowGradient", TimerShowGradient) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTimerColors", OverrideTimerColors) ^
            SettingsHelper.CreateSetting(document, parent, "SegmentTimerShowGradient", SegmentTimerShowGradient) ^
            SettingsHelper.CreateSetting(document, parent, "TimerFormat", TimerFormat) ^
            SettingsHelper.CreateSetting(document, parent, "SegmentTimerFormat", SegmentTimerFormat) ^
            SettingsHelper.CreateSetting(document, parent, "SegmentTimesAccuracy", SegmentTimesAccuracy) ^
            SettingsHelper.CreateSetting(document, parent, "TimerColor", TimerColor) ^
            SettingsHelper.CreateSetting(document, parent, "SegmentTimerColor", SegmentTimerColor) ^
            SettingsHelper.CreateSetting(document, parent, "SegmentLabelsColor", SegmentLabelsColor) ^
            SettingsHelper.CreateSetting(document, parent, "SegmentTimesColor", SegmentTimesColor) ^
            SettingsHelper.CreateSetting(document, parent, "SegmentLabelsFont", SegmentLabelsFont) ^
            SettingsHelper.CreateSetting(document, parent, "SegmentTimesFont", SegmentTimesFont) ^
            SettingsHelper.CreateSetting(document, parent, "SplitNameFont", SplitNameFont) ^
            SettingsHelper.CreateSetting(document, parent, "DisplayIcon", DisplayIcon) ^
            SettingsHelper.CreateSetting(document, parent, "IconSize", IconSize) ^
            SettingsHelper.CreateSetting(document, parent, "ShowSplitName", ShowSplitName) ^
            SettingsHelper.CreateSetting(document, parent, "SplitNameColor", SplitNameColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
            SettingsHelper.CreateSetting(document, parent, "Comparison", Comparison) ^
            SettingsHelper.CreateSetting(document, parent, "Comparison2", Comparison2) ^
            SettingsHelper.CreateSetting(document, parent, "HideComparison", HideComparison) ^
            SettingsHelper.CreateSetting(document, parent, "TimingMethod", TimingMethod) ^
            SettingsHelper.CreateSetting(document, parent, "DecimalsSize", DecimalsSize) ^
            SettingsHelper.CreateSetting(document, parent, "SegmentTimerDecimalsSize", SegmentTimerDecimalsSize);
    }
}

public class SegmentTimer : Timer
{
    public override TimeSpan? GetTime(LiveSplitState state, TimingMethod method)
    {
        TimeSpan? lastSplit = TimeSpan.Zero;
        int runEndedDelay = state.CurrentPhase == TimerPhase.Ended ? 1 : 0;
        if (state.CurrentSplitIndex > 0 + runEndedDelay)
        {
            lastSplit = state.Run[state.CurrentSplitIndex - 1 - runEndedDelay].SplitTime[method];
        }

        return state.CurrentPhase == TimerPhase.NotRunning
            ? state.Run.Offset
            : state.CurrentTime[method] - lastSplit;
    }
}

[GlobalFontConsumer(GlobalFont.TimerFont)]
public partial class DetailedTimer : IComponent, ISettingsHashCodeProvider
{
    public Timer InternalComponent { get; set; }
    public SegmentTimer SegmentTimer { get; set; }

    public SimpleLabel LabelSegment { get; set; }
    public SimpleLabel LabelBest { get; set; }
    public SimpleLabel SegmentTime { get; set; }
    public SimpleLabel BestSegmentTime { get; set; }
    public SimpleLabel SplitName { get; set; }

    public DetailedTimerSettings Settings { get; set; }
    public GraphicsCache Cache { get; set; }

    public string Comparison { get; set; }
    public string Comparison2 { get; set; }
    public string ComparisonName { get; set; }
    public string ComparisonName2 { get; set; }
    public bool HideComparison { get; set; }

    protected int IconWidth { get; set; }

    private readonly LiveSplitState state;

    public float PaddingTop => 0f;
    public float PaddingLeft => 7f;
    public float PaddingBottom => 0f;
    public float PaddingRight => 7f;

    public float VerticalHeight => Settings.Height;
    public float HorizontalWidth => Settings.Width;
    public float MinimumWidth => 20;
    public float MinimumHeight => 20;

    public IDictionary<string, Action> ContextMenuControls => null;

    [GeneratedRegex(@"^{(.+)}\s*(.+)$")]
    private static partial Regex SubsplitRegex();

    public DetailedTimer(LiveSplitState state)
    {
        this.state = state;
        InternalComponent = new Timer();
        SegmentTimer = new SegmentTimer();
        Settings = new DetailedTimerSettings();
        IconWidth = 0;
        Cache = new GraphicsCache();
        LabelSegment = new SimpleLabel();
        LabelBest = new SimpleLabel();
        SegmentTime = new SimpleLabel();
        BestSegmentTime = new SimpleLabel();
        SplitName = new SimpleLabel();
        state.ComparisonRenamed += State_ComparisonRenamed;
    }

    private void State_ComparisonRenamed(object sender, EventArgs e)
    {
        var args = (RenameEventArgs)e;
        if (Settings.Comparison == args.OldName)
        {
            Settings.Comparison = args.NewName;
            ((LiveSplitState)sender).Layout.HasChanged = true;
        }

        if (Settings.Comparison2 == args.OldName)
        {
            Settings.Comparison2 = args.NewName;
            ((LiveSplitState)sender).Layout.HasChanged = true;
        }
    }

    private void StyleLabel(SimpleLabel label, LiveSplitState state, Font font, Color color)
    {
        label.Font = font;
        label.HorizontalAlignment = StringAlignment.Near;
        label.VerticalAlignment = StringAlignment.Center;
        label.ForeColor = color;
        label.HasShadow = state.LayoutSettings.DropShadows;
        label.ShadowColor = state.LayoutSettings.ShadowsColor;
        label.OutlineColor = state.LayoutSettings.TextOutlineColor;
    }

    public void DrawGeneral(DrawingContext g, LiveSplitState state, float width, float height)
    {
        Timer.DrawBackground(g, InternalComponent.TimerColor, Settings.BackgroundColor, Settings.BackgroundColor2, width, height, Settings.BackgroundGradient);

        int lastSplitOffset = state.CurrentSplitIndex == state.Run.Count ? -1 : 0;

        float originalDrawSize = Math.Min(Settings.IconSize, width - 14);
        Drawing.Image icon = state.CurrentSplitIndex >= 0 ? state.Run[state.CurrentSplitIndex + lastSplitOffset].Icon : null;
        if (Settings.DisplayIcon && icon != null && icon.Width > 0 && icon.Height > 0)
        {
            float drawWidth = originalDrawSize;
            float drawHeight = originalDrawSize;
            if (icon.Width > icon.Height)
            {
                drawHeight *= icon.Height / (float)icon.Width;
            }
            else
            {
                drawWidth *= icon.Width / (float)icon.Height;
            }

            g.DrawImage(
                icon,
                7 + ((originalDrawSize - drawWidth) / 2),
                ((height - originalDrawSize) / 2.0f) + ((originalDrawSize - drawHeight) / 2),
                drawWidth,
                drawHeight);

            IconWidth = (int)(originalDrawSize + 7.5f);
        }
        else
        {
            IconWidth = 0;
        }

        InternalComponent.Settings.ShowGradient = Settings.TimerShowGradient;
        InternalComponent.Settings.OverrideSplitColors = Settings.OverrideTimerColors;
        InternalComponent.Settings.TimerColor = Settings.TimerColor;
        InternalComponent.Settings.DigitsFormat = Settings.DigitsFormat;
        InternalComponent.Settings.Accuracy = Settings.Accuracy;
        InternalComponent.Settings.DecimalsSize = Settings.DecimalsSize;

        SegmentTimer.Settings.ShowGradient = Settings.SegmentTimerShowGradient;
        SegmentTimer.Settings.OverrideSplitColors = true;
        SegmentTimer.Settings.TimerColor = Settings.SegmentTimerColor;
        SegmentTimer.Settings.DigitsFormat = Settings.SegmentDigitsFormat;
        SegmentTimer.Settings.Accuracy = Settings.SegmentAccuracy;
        SegmentTimer.Settings.DecimalsSize = Settings.SegmentTimerDecimalsSize;

        if (state.CurrentSplitIndex < 0)
        {
            return;
        }

        StyleLabel(LabelSegment, state, Settings.SegmentLabelsFont, Settings.SegmentLabelsColor);
        LabelSegment.X = 5 + IconWidth;
        LabelSegment.Y = height * ((100f - Settings.SegmentTimerSizeRatio) / 100f);
        LabelSegment.Width = width - SegmentTimer.ActualWidth - 5 - IconWidth;
        LabelSegment.Height = height * (Settings.SegmentTimerSizeRatio / 200f) * (!HideComparison ? 1f : 2f);
        if (Comparison != "None")
        {
            LabelSegment.Draw(g);
        }

        StyleLabel(LabelBest, state, Settings.SegmentLabelsFont, Settings.SegmentLabelsColor);
        LabelBest.X = 5 + IconWidth;
        LabelBest.Y = height * ((100f - (Settings.SegmentTimerSizeRatio / 2f)) / 100f);
        LabelBest.Width = width - SegmentTimer.ActualWidth - 5 - IconWidth;
        LabelBest.Height = height * (Settings.SegmentTimerSizeRatio / 200f);
        if (!HideComparison)
        {
            LabelBest.Draw(g);
        }

        float offset = Math.Max(LabelSegment.ActualWidth, HideComparison ? 0 : LabelBest.ActualWidth) + 10;

        if (Comparison != "None")
        {
            StyleLabel(SegmentTime, state, Settings.SegmentTimesFont, Settings.SegmentTimesColor);
            SegmentTime.X = offset + IconWidth;
            SegmentTime.Y = height * ((100f - Settings.SegmentTimerSizeRatio) / 100f);
            SegmentTime.Width = width - SegmentTimer.ActualWidth - offset - IconWidth;
            SegmentTime.Height = height * (Settings.SegmentTimerSizeRatio / 200f) * (!HideComparison ? 1f : 2f);
            SegmentTime.IsMonospaced = true;
            SegmentTime.Draw(g);
        }

        if (!HideComparison)
        {
            StyleLabel(BestSegmentTime, state, Settings.SegmentTimesFont, Settings.SegmentTimesColor);
            BestSegmentTime.X = offset + IconWidth;
            BestSegmentTime.Y = height * ((100f - (Settings.SegmentTimerSizeRatio / 2f)) / 100f);
            BestSegmentTime.Width = width - SegmentTimer.ActualWidth - offset - IconWidth;
            BestSegmentTime.Height = height * (Settings.SegmentTimerSizeRatio / 200f);
            BestSegmentTime.IsMonospaced = true;
            BestSegmentTime.Draw(g);
        }

        StyleLabel(SplitName, state, Settings.SplitNameFont, Settings.SplitNameColor);
        SplitName.X = IconWidth + 5;
        SplitName.Y = 0;
        SplitName.Width = width - InternalComponent.ActualWidth - IconWidth - 5;
        SplitName.Height = height * ((100f - Settings.SegmentTimerSizeRatio) / 100f);
        if (Settings.ShowSplitName)
        {
            SplitName.Draw(g);
        }
    }

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion)
    {
        DrawGeneral(g, state, width, VerticalHeight);

        float mainHeight = VerticalHeight * ((100f - Settings.SegmentTimerSizeRatio) / 100f);
        InternalComponent.Settings.TimerHeight = mainHeight;
        InternalComponent.DrawVertical(g, state, width, clipRegion);

        using (g.PushTransform(Matrix.CreateTranslation(0, mainHeight)))
        {
            SegmentTimer.Settings.TimerHeight = VerticalHeight * (Settings.SegmentTimerSizeRatio / 100f);
            SegmentTimer.DrawVertical(g, state, width, clipRegion);
        }
    }

    public void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion)
    {
        DrawGeneral(g, state, HorizontalWidth, height);

        float mainHeight = height * ((100f - Settings.SegmentTimerSizeRatio) / 100f);
        InternalComponent.Settings.TimerWidth = HorizontalWidth;
        InternalComponent.DrawHorizontal(g, state, mainHeight, clipRegion);

        using (g.PushTransform(Matrix.CreateTranslation(0, mainHeight)))
        {
            SegmentTimer.Settings.TimerWidth = HorizontalWidth;
            SegmentTimer.DrawHorizontal(g, state, height * (Settings.SegmentTimerSizeRatio / 100f), clipRegion);
        }
    }

    public string ComponentName => "Detailed Timer";

    public Control GetSettingsControl(LayoutMode mode)
    {
        return SettingsEditor.Create(Settings, state);
    }

    public void SetSettings(XmlNode settings)
    {
        Settings.SetSettings(settings);
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        return Settings.GetSettings(document);
    }

    private static TimeSpan? GetSegmentTime(LiveSplitState state, string comparison, int lastSplitOffset, TimingMethod timingMethod)
    {
        if (comparison == BestSegmentsComparisonGenerator.ComparisonName)
        {
            return state.Run[state.CurrentSplitIndex + lastSplitOffset].BestSegmentTime[timingMethod];
        }

        if (state.CurrentSplitIndex == 0 || (state.CurrentSplitIndex == 1 && lastSplitOffset == -1))
        {
            return state.Run[0].Comparisons[comparison][timingMethod];
        }

        return state.CurrentSplitIndex > 0
            ? state.Run[state.CurrentSplitIndex + lastSplitOffset].Comparisons[comparison][timingMethod]
                - state.Run[state.CurrentSplitIndex - 1 + lastSplitOffset].Comparisons[comparison][timingMethod]
            : null;
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        int lastSplitOffset = state.CurrentSplitIndex == state.Run.Count ? -1 : 0;

        GeneralTimeFormatter formatter = Settings.SegmentTimesFormatter;
        formatter.Accuracy = Settings.SegmentTimesAccuracy;

        TimingMethod timingMethod = Settings.TimingMethod switch
        {
            "Real Time" => TimingMethod.RealTime,
            "Game Time" => TimingMethod.GameTime,
            _ => state.CurrentTimingMethod
        };

        if (state.CurrentSplitIndex >= 0)
        {
            Comparison = Settings.Comparison == "Current Comparison" ? state.CurrentComparison : Settings.Comparison;
            Comparison2 = Settings.Comparison2 == "Current Comparison" ? state.CurrentComparison : Settings.Comparison2;
            HideComparison = Settings.HideComparison;

            if (HideComparison || !state.Run.Comparisons.Contains(Comparison2) || Comparison2 == "None")
            {
                HideComparison = true;
                if (!state.Run.Comparisons.Contains(Comparison) || Comparison == "None")
                {
                    Comparison = state.CurrentComparison;
                }
            }
            else if (!state.Run.Comparisons.Contains(Comparison) || Comparison == "None")
            {
                HideComparison = true;
                Comparison = Comparison2;
            }
            else if (Comparison == Comparison2)
            {
                HideComparison = true;
            }

            ComparisonName = CompositeComparisons.GetShortComparisonName(Comparison);
            ComparisonName2 = CompositeComparisons.GetShortComparisonName(Comparison2);

            LabelSegment.Text = ComparisonName + ":";
            LabelBest.Text = ComparisonName2 + ":";

            if (Comparison != "None")
            {
                SegmentTime.Text = formatter.Format(GetSegmentTime(state, Comparison, lastSplitOffset, timingMethod));
            }

            if (!HideComparison)
            {
                BestSegmentTime.Text = formatter.Format(GetSegmentTime(state, Comparison2, lastSplitOffset, timingMethod));
            }

            string name = state.Run[state.CurrentSplitIndex + lastSplitOffset].Name;
            bool isSubsplit = name.StartsWith('-') && state.CurrentSplitIndex + lastSplitOffset < state.Run.Count - 1;
            if (isSubsplit)
            {
                SplitName.Text = name[1..];
            }
            else
            {
                Match match = SubsplitRegex().Match(name);
                SplitName.Text = match.Success ? match.Groups[2].Value : name;
            }
        }

        SegmentTimer.Settings.TimingMethod = Settings.TimingMethod;
        InternalComponent.Settings.TimingMethod = Settings.TimingMethod;
        SegmentTimer.Update(null, state, width, height, mode);
        InternalComponent.Update(null, state, width, height, mode);

        Cache.Restart();
        Cache["SplitIcon"] = state.CurrentSplitIndex >= 0 ? state.Run[state.CurrentSplitIndex + lastSplitOffset].Icon : null;
        Cache["SplitName"] = SplitName.Text;
        Cache["LabelSegment"] = LabelSegment.Text;
        Cache["LabelBest"] = LabelBest.Text;
        Cache["SegmentTime"] = SegmentTime.Text;
        Cache["BestSegmentTime"] = BestSegmentTime.Text;
        Cache["SegmentTimerText"] = SegmentTimer.BigTextLabel.Text + SegmentTimer.SmallTextLabel.Text;
        Cache["InternalComponentText"] = InternalComponent.BigTextLabel.Text + InternalComponent.SmallTextLabel.Text;
        Cache["TimerColor"] = InternalComponent.TimerColor.ToArgb();

        if (invalidator != null && Cache.HasChanged)
        {
            invalidator.Invalidate(0, 0, width, height);
        }
    }

    public void Dispose() { }

    public int GetSettingsHashCode()
    {
        return Settings.GetSettingsHashCode();
    }
}
