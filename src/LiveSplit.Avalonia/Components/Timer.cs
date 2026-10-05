using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LiveSplit.Model;
using LiveSplit.TimeFormatters;
using System;
using System.Collections.Generic;
using System.Xml;
using Color = System.Drawing.Color;
using Font = LiveSplit.Drawing.Font;
using GraphicsUnit = LiveSplit.Drawing.GraphicsUnit;

namespace LiveSplit.UI.Components;

public enum DeltasGradientType
{
    Plain, Vertical, Horizontal, PlainWithDeltaColor, VerticalWithDeltaColor, HorizontalWithDeltaColor
}

public class TimerSettings : ComponentSettings
{
    [Setting("Height", Minimum = 20, Maximum = 500)]
    public float TimerHeight { get; set; } = 50;
    [Setting("Width", Minimum = 50, Maximum = 1000)]
    public float TimerWidth { get; set; } = 225;
    [Setting("Decimals Size", Minimum = 10, Maximum = 50)]
    public float DecimalsSize { get; set; } = 35f;

    [Setting("Digits Format", Options = ["1", "00:01", "0:00:01", "00:00:01"])]
    public string DigitsFormat { get; set; } = "1";
    [Setting("Accuracy", Options = ["", ".2", ".23", ".234"])]
    public string Accuracy { get; set; } = ".23";

    [Setting("Timing Method", Options = ["Current Timing Method", "Real Time", "Game Time"])]
    public string TimingMethod { get; set; } = "Current Timing Method";

    public bool OverrideSplitColors { get; set; }
    public Color TimerColor { get; set; } = Color.FromArgb(170, 170, 170);
    public bool CenterTimer { get; set; }
    public bool ShowGradient { get; set; } = true;

    public DeltasGradientType BackgroundGradient { get; set; } = DeltasGradientType.Plain;
    public Color BackgroundColor { get; set; } = Color.Transparent;
    public Color BackgroundColor2 { get; set; } = Color.Transparent;

    private string TimerFormat
    {
        get => DigitsFormat + Accuracy;
        set
        {
            int decimalIndex = value.IndexOf('.');
            if (decimalIndex < 0)
            {
                DigitsFormat = value;
                Accuracy = "";
            }
            else
            {
                DigitsFormat = value[..decimalIndex];
                Accuracy = value[decimalIndex..];
            }
        }
    }

    public override void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);
        TimerHeight = SettingsHelper.ParseFloat(element["TimerHeight"]);
        TimerWidth = SettingsHelper.ParseFloat(element["TimerWidth"]);
        ShowGradient = SettingsHelper.ParseBool(element["ShowGradient"], true);
        TimerColor = SettingsHelper.ParseColor(element["TimerColor"], Color.FromArgb(170, 170, 170));
        DecimalsSize = SettingsHelper.ParseFloat(element["DecimalsSize"], 35f);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.Transparent);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.Transparent);
        BackgroundGradient = SettingsHelper.ParseEnum(element["BackgroundGradient"], DeltasGradientType.Plain);
        CenterTimer = SettingsHelper.ParseBool(element["CenterTimer"], false);
        TimingMethod = SettingsHelper.ParseString(element["TimingMethod"], "Current Timing Method");

        OverrideSplitColors = version >= new Version(1, 3)
            ? SettingsHelper.ParseBool(element["OverrideSplitColors"])
            : !SettingsHelper.ParseBool(element["UseSplitColors"], true);

        if (version >= new Version(1, 2))
        {
            if (version >= new Version(1, 5))
            {
                TimerFormat = SettingsHelper.ParseString(element["TimerFormat"]);
            }
            else
            {
                TimeAccuracy accuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["TimerAccuracy"]);
                DigitsFormat = "1";
                Accuracy = accuracy switch
                {
                    TimeAccuracy.Hundredths => ".23",
                    TimeAccuracy.Tenths => ".2",
                    _ => "",
                };
            }
        }
        else
        {
            DigitsFormat = "1";
            Accuracy = ".23";
        }
    }

    protected override int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.5") ^
            SettingsHelper.CreateSetting(document, parent, "TimerHeight", TimerHeight) ^
            SettingsHelper.CreateSetting(document, parent, "TimerWidth", TimerWidth) ^
            SettingsHelper.CreateSetting(document, parent, "TimerFormat", TimerFormat) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideSplitColors", OverrideSplitColors) ^
            SettingsHelper.CreateSetting(document, parent, "ShowGradient", ShowGradient) ^
            SettingsHelper.CreateSetting(document, parent, "TimerColor", TimerColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
            SettingsHelper.CreateSetting(document, parent, "CenterTimer", CenterTimer) ^
            SettingsHelper.CreateSetting(document, parent, "TimingMethod", TimingMethod) ^
            SettingsHelper.CreateSetting(document, parent, "DecimalsSize", DecimalsSize);
    }
}

[GlobalFontConsumer(GlobalFont.TimerFont)]
public class Timer : IComponent, ISettingsHashCodeProvider
{
    public SimpleLabel BigTextLabel { get; set; }
    public SimpleLabel SmallTextLabel { get; set; }
    protected SimpleLabel BigMeasureLabel { get; set; }
    protected GeneralTimeFormatter Formatter { get; set; }

    protected Font TimerDecimalPlacesFont { get; set; }
    protected Font TimerFont { get; set; }
    protected float PreviousDecimalsSize { get; set; }

    public Color TimerColor = Color.Transparent;

    public GraphicsCache Cache { get; set; }

    public TimerSettings Settings { get; set; }

    public float ActualWidth { get; set; }

    public virtual string ComponentName => "Timer";

    public float VerticalHeight => Settings.TimerHeight;
    public float MinimumWidth => 20;
    public float HorizontalWidth => Settings.TimerWidth;
    public float MinimumHeight => 20;

    public float PaddingTop => 0f;
    public float PaddingLeft => 7f;
    public float PaddingBottom => 0f;
    public float PaddingRight => 7f;

    public IDictionary<string, Action> ContextMenuControls => null;

    public Timer()
    {
        BigTextLabel = new SimpleLabel()
        {
            HorizontalAlignment = StringAlignment.Far,
            VerticalAlignment = StringAlignment.Near,
            Width = 493,
            Text = "0",
        };

        SmallTextLabel = new SimpleLabel()
        {
            HorizontalAlignment = StringAlignment.Near,
            VerticalAlignment = StringAlignment.Near,
            Width = 257,
            Text = "0",
        };

        BigMeasureLabel = new SimpleLabel()
        {
            Text = "88:88:88",
            IsMonospaced = true
        };

        Formatter = new GeneralTimeFormatter
        {
            Accuracy = TimeAccuracy.Hundredths,
            NullFormat = NullFormat.ZeroWithAccuracy,
            DigitsFormat = DigitsFormat.SingleDigitSeconds
        };

        Settings = new TimerSettings();
        UpdateTimeFormat();
        Cache = new GraphicsCache();
    }

    public static void DrawBackground(DrawingContext g, Color timerColor, Color settingsColor1, Color settingsColor2,
        float width, float height, DeltasGradientType gradientType)
    {
        Color background1 = settingsColor1;
        Color background2 = settingsColor2;
        if (gradientType is DeltasGradientType.PlainWithDeltaColor
            or DeltasGradientType.HorizontalWithDeltaColor
            or DeltasGradientType.VerticalWithDeltaColor)
        {
            timerColor.ToHSV(out double h, out double s, out double v);
            Color newColor = ColorExtensions.FromHSV(h, s * 0.5, v * 0.25);

            if (gradientType == DeltasGradientType.PlainWithDeltaColor)
            {
                background1 = Color.FromArgb(timerColor.A * 7 / 12, newColor);
            }
            else
            {
                background1 = Color.FromArgb(timerColor.A / 6, newColor);
                background2 = Color.FromArgb(timerColor.A, newColor);
            }
        }

        bool plain = gradientType is DeltasGradientType.Plain or DeltasGradientType.PlainWithDeltaColor;
        g.FillGradient(
            background1,
            plain ? background1 : background2,
            gradientType is DeltasGradientType.Horizontal or DeltasGradientType.HorizontalWithDeltaColor,
            width, height);
    }

    private void DrawGeneral(DrawingContext g, LiveSplitState state, float width, float height)
    {
        DrawBackground(g, TimerColor, Settings.BackgroundColor, Settings.BackgroundColor2, width, height, Settings.BackgroundGradient);

        if (!Equals(state.LayoutSettings.TimerFont, TimerFont) || Settings.DecimalsSize != PreviousDecimalsSize)
        {
            TimerFont = state.LayoutSettings.TimerFont;
            TimerDecimalPlacesFont = new Font(TimerFont.FontFamily.Name, TimerFont.SizeInPixels / 50f * Settings.DecimalsSize, TimerFont.Style, GraphicsUnit.Pixel);
            PreviousDecimalsSize = Settings.DecimalsSize;
        }

        BigTextLabel.Font = BigMeasureLabel.Font = TimerFont;
        SmallTextLabel.Font = TimerDecimalPlacesFont;

        BigMeasureLabel.SetActualWidth();
        SmallTextLabel.SetActualWidth();

        float unscaledWidth = Math.Max(10, BigMeasureLabel.ActualWidth + SmallTextLabel.ActualWidth + 11);
        float unscaledHeight = 45f;

        float widthFactor = (width - 14) / (unscaledWidth - 14);
        float heightFactor = height / unscaledHeight;
        float adjustValue = !Settings.CenterTimer ? 7f : 0f;
        float scale = Math.Min(widthFactor, heightFactor);
        if (scale <= 0 || float.IsNaN(scale))
        {
            return;
        }

        Matrix transform = Matrix.CreateTranslation(-unscaledWidth + adjustValue, -0.5f * unscaledHeight)
            * Matrix.CreateScale(scale, scale)
            * Matrix.CreateTranslation(width - adjustValue, height / 2);
        if (Settings.CenterTimer)
        {
            transform = Matrix.CreateTranslation(-(width - (unscaledWidth * scale)) / 2f / scale, 0)
                * Matrix.CreateTranslation(-unscaledWidth + adjustValue, -0.5f * unscaledHeight)
                * Matrix.CreateScale(scale, scale)
                * Matrix.CreateTranslation(width - adjustValue, height / 2);
        }

        using (g.PushTransform(transform))
        {
            DrawUnscaled(g, state, unscaledWidth, unscaledHeight);
        }

        ActualWidth = scale * (SmallTextLabel.ActualWidth + BigTextLabel.ActualWidth);
    }

    public void DrawUnscaled(DrawingContext g, LiveSplitState state, float width, float height)
    {
        BigTextLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
        BigTextLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
        BigTextLabel.HasShadow = state.LayoutSettings.DropShadows;
        SmallTextLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
        SmallTextLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
        SmallTextLabel.HasShadow = state.LayoutSettings.DropShadows;

        Font smallFont = TimerDecimalPlacesFont;
        Font bigFont = TimerFont;

        FontPixelMetrics bigMetrics = DrawingHelpers.GetMetrics(bigFont);
        FontPixelMetrics smallMetrics = DrawingHelpers.GetMetrics(smallFont);
        float ascent = (float)bigMetrics.Ascent;
        float descent = (float)bigMetrics.Descent;
        float smallAscent = (float)smallMetrics.Ascent;

        // GDI+ places the top of the cell ascent at the label's Y; Avalonia places the top of
        // the line box there, which may include extra space above the ascent.
        float bigLineOffset = (float)bigMetrics.TopOffset;
        float smallLineOffset = (float)smallMetrics.TopOffset;

        float shift = (height - ascent - descent) / 2f;

        BigTextLabel.X = width - 499 - SmallTextLabel.ActualWidth;
        SmallTextLabel.X = width - SmallTextLabel.ActualWidth - 6;
        BigTextLabel.Y = shift - bigLineOffset;
        SmallTextLabel.Y = shift + ascent - smallAscent - smallLineOffset;
        BigTextLabel.Height = 150f;
        SmallTextLabel.Height = 150f;

        BigTextLabel.IsMonospaced = true;
        SmallTextLabel.IsMonospaced = true;

        if (Settings.ShowGradient && BigTextLabel.Brush is ISolidColorBrush solid)
        {
            Color originalColor = solid.Color.ToDrawing();
            originalColor.ToHSV(out double h, out double s, out double v);

            Color bottomColor = ColorExtensions.FromHSV(h, s, 0.8 * v);
            Color topColor = ColorExtensions.FromHSV(h, 0.5 * s, Math.Min(1, (1.5 * v) + 0.1));

            float bigTop = BigTextLabel.Y + bigLineOffset;
            float smallTop = SmallTextLabel.Y + smallLineOffset;
            BigTextLabel.Brush = DrawingHelpers.CreateLinearGradient(
                new Point(BigTextLabel.X, bigTop),
                new Point(BigTextLabel.X, bigTop + ascent + descent),
                topColor,
                bottomColor);

            SmallTextLabel.Brush = DrawingHelpers.CreateLinearGradient(
                new Point(SmallTextLabel.X, smallTop),
                new Point(SmallTextLabel.X, smallTop + ascent + descent + smallFont.SizeInPixels - bigFont.SizeInPixels),
                topColor,
                bottomColor);
        }

        BigTextLabel.Draw(g);
        SmallTextLabel.Draw(g);
    }

    protected void UpdateTimeFormat()
    {
        Formatter.DigitsFormat = Settings.DigitsFormat switch
        {
            "1" => DigitsFormat.SingleDigitSeconds,
            "00:01" => DigitsFormat.DoubleDigitMinutes,
            "0:00:01" => DigitsFormat.SingleDigitHours,
            _ => DigitsFormat.DoubleDigitHours
        };

        Formatter.Accuracy = Settings.Accuracy switch
        {
            ".234" => TimeAccuracy.Milliseconds,
            ".23" => TimeAccuracy.Hundredths,
            ".2" => TimeAccuracy.Tenths,
            _ => TimeAccuracy.Seconds
        };
    }

    public virtual TimeSpan? GetTime(LiveSplitState state, TimingMethod method)
    {
        return state.CurrentPhase == TimerPhase.NotRunning
            ? state.Run.Offset
            : state.CurrentTime[method];
    }

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion)
    {
        DrawGeneral(g, state, width, VerticalHeight);
    }

    public void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion)
    {
        DrawGeneral(g, state, HorizontalWidth, height);
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

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        Cache.Restart();

        TimingMethod timingMethod = state.CurrentTimingMethod;
        if (Settings.TimingMethod == "Real Time")
        {
            timingMethod = TimingMethod.RealTime;
        }
        else if (Settings.TimingMethod == "Game Time")
        {
            timingMethod = TimingMethod.GameTime;
        }

        UpdateTimeFormat();

        TimeSpan? timeValue = GetTime(state, timingMethod);

        if (timeValue == null && timingMethod == TimingMethod.GameTime)
        {
            timeValue = GetTime(state, TimingMethod.RealTime);
        }

        if (timeValue != null)
        {
            string timeString = Formatter.Format(timeValue);
            int dotIndex = timeString.IndexOf('.');
            if (Formatter.Accuracy != TimeAccuracy.Seconds && dotIndex >= 0)
            {
                BigTextLabel.Text = timeString[..dotIndex];
                SmallTextLabel.Text = timeString[dotIndex..];
            }
            else
            {
                BigTextLabel.Text = timeString;
                SmallTextLabel.Text = "";
            }
        }
        else
        {
            SmallTextLabel.Text = TimeFormatConstants.DASH;
            BigTextLabel.Text = "";
        }

        if (state.CurrentPhase == TimerPhase.NotRunning || state.CurrentTime[timingMethod] < TimeSpan.Zero)
        {
            TimerColor = state.LayoutSettings.NotRunningColor;
        }
        else if (state.CurrentPhase == TimerPhase.Paused)
        {
            TimerColor = state.LayoutSettings.PausedColor;
        }
        else if (state.CurrentPhase == TimerPhase.Ended)
        {
            TimerColor = state.Run[^1].Comparisons[state.CurrentComparison][timingMethod] == null || state.CurrentTime[timingMethod] < state.Run[^1].Comparisons[state.CurrentComparison][timingMethod]
                ? state.LayoutSettings.PersonalBestColor
                : state.LayoutSettings.BehindLosingTimeColor;
        }
        else if (state.CurrentPhase == TimerPhase.Running)
        {
            TimerColor = state.CurrentSplit.Comparisons[state.CurrentComparison][timingMethod] != null
                ? LiveSplitStateHelper.GetSplitColor(state, state.CurrentTime[timingMethod] - state.CurrentSplit.Comparisons[state.CurrentComparison][timingMethod],
                    state.CurrentSplitIndex, true, false, state.CurrentComparison, timingMethod)
                    ?? state.LayoutSettings.AheadGainingTimeColor
                : state.LayoutSettings.AheadGainingTimeColor;
        }

        Color color = Settings.OverrideSplitColors ? Settings.TimerColor : TimerColor;
        BigTextLabel.ForeColor = color;
        SmallTextLabel.ForeColor = color;

        Cache["TimerText"] = BigTextLabel.Text + SmallTextLabel.Text;
        Cache["TimerColor"] = color.ToArgb();

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

public class TimerFactory : IComponentFactory
{
    public string ComponentName => "Timer";
    public string Description => "Displays the current time of the attempt.";
    public ComponentCategory Category => ComponentCategory.Timer;

    public IComponent Create(LiveSplitState state)
    {
        return new Timer();
    }
}
