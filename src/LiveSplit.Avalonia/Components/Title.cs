using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LiveSplit.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Color = System.Drawing.Color;
using Font = LiveSplit.Drawing.Font;

namespace LiveSplit.UI.Components;

public enum AlignmentType
{
    Auto = 0,
    Left = 1,
    Center = 2
}

public class TitleSettings : ComponentSettings
{
    public bool ShowGameName { get; set; } = true;
    public bool ShowCategoryName { get; set; } = true;
    public bool ShowAttemptCount { get; set; } = true;
    public bool ShowFinishedRunsCount { get; set; }
    [Hidden] public bool ShowCount => ShowAttemptCount || ShowFinishedRunsCount;
    public AlignmentType TextAlignment { get; set; } = AlignmentType.Auto;
    public bool SingleLine { get; set; }
    public bool DisplayGameIcon { get; set; } = true;
    public bool ShowRegion { get; set; }
    public bool ShowPlatform { get; set; }
    public bool ShowVariables { get; set; } = true;
    public bool OverrideTitleColor { get; set; }
    public Color TitleColor { get; set; } = Color.FromArgb(255, 255, 255, 255);
    [Hidden] public bool OverrideTitleFont { get; set; }
    [Hidden] public Font TitleFont { get; set; }
    public GradientType BackgroundGradient { get; set; } = GradientType.Vertical;
    public Color BackgroundColor { get; set; } = Color.FromArgb(255, 42, 42, 42);
    public Color BackgroundColor2 { get; set; } = Color.FromArgb(255, 19, 19, 19);

    public override void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);
        DisplayGameIcon = SettingsHelper.ParseBool(element["DisplayGameIcon"], true);
        if (version >= new Version(1, 2))
        {
            TitleFont = SettingsHelper.GetFontFromElement(element["TitleFont"]);
            if (version >= new Version(1, 3))
            {
                OverrideTitleFont = SettingsHelper.ParseBool(element["OverrideTitleFont"]);
                TextAlignment = version >= new Version(1, 7, 3)
                    ? (AlignmentType)SettingsHelper.ParseInt(element["TextAlignment"], 0)
                    : DisplayGameIcon && SettingsHelper.ParseBool(element["CenterTitle"], false)
                        ? AlignmentType.Center
                        : AlignmentType.Auto;
            }
            else
            {
                OverrideTitleFont = !SettingsHelper.ParseBool(element["UseLayoutSettingsFont"]);
            }
        }

        ShowGameName = SettingsHelper.ParseBool(element["ShowGameName"], true);
        ShowCategoryName = SettingsHelper.ParseBool(element["ShowCategoryName"], true);
        ShowAttemptCount = SettingsHelper.ParseBool(element["ShowAttemptCount"]);
        TitleColor = SettingsHelper.ParseColor(element["TitleColor"], Color.FromArgb(255, 255, 255, 255));
        OverrideTitleColor = SettingsHelper.ParseBool(element["OverrideTitleColor"], false);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.FromArgb(42, 42, 42, 255));
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.FromArgb(19, 19, 19, 255));
        BackgroundGradient = Enum.Parse<GradientType>(SettingsHelper.ParseString(element["BackgroundGradient"], GradientType.Vertical.ToString()));
        ShowFinishedRunsCount = SettingsHelper.ParseBool(element["ShowFinishedRunsCount"], false);
        SingleLine = SettingsHelper.ParseBool(element["SingleLine"], false);
        ShowRegion = SettingsHelper.ParseBool(element["ShowRegion"], false);
        ShowPlatform = SettingsHelper.ParseBool(element["ShowPlatform"], false);
        ShowVariables = SettingsHelper.ParseBool(element["ShowVariables"], true);
    }

    protected override int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.7.3") ^
            SettingsHelper.CreateSetting(document, parent, "ShowGameName", ShowGameName) ^
            SettingsHelper.CreateSetting(document, parent, "ShowCategoryName", ShowCategoryName) ^
            SettingsHelper.CreateSetting(document, parent, "ShowAttemptCount", ShowAttemptCount) ^
            SettingsHelper.CreateSetting(document, parent, "ShowFinishedRunsCount", ShowFinishedRunsCount) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTitleColor", OverrideTitleColor) ^
            SettingsHelper.CreateSetting(document, parent, "SingleLine", SingleLine) ^
            SettingsHelper.CreateSetting(document, parent, "TitleColor", TitleColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
            SettingsHelper.CreateSetting(document, parent, "DisplayGameIcon", DisplayGameIcon) ^
            SettingsHelper.CreateSetting(document, parent, "ShowRegion", ShowRegion) ^
            SettingsHelper.CreateSetting(document, parent, "ShowPlatform", ShowPlatform) ^
            SettingsHelper.CreateSetting(document, parent, "ShowVariables", ShowVariables) ^
            SettingsHelper.CreateSetting(document, parent, "TextAlignment", (int)TextAlignment);
    }
}

[GlobalFontConsumer(GlobalFont.TextFont)]
public class Title : IComponent, ISettingsHashCodeProvider, IFontOverridesMigration
{
    public TitleSettings Settings { get; set; }

    public float VerticalHeight { get; set; }

    public GraphicsCache Cache { get; set; }

    protected int FinishedRunsInHistory { get; set; }

    public float MinimumWidth => GameNameLabel.X + AttemptCountLabel.ActualWidth + 5;

    public float HorizontalWidth
    {
        get
        {
            if (!Settings.ShowCount)
            {
                return Math.Max(GameNameLabel.ActualWidth, CategoryNameLabel.ActualWidth) + GameNameLabel.X + 5;
            }

            // If the category + attempt is longer than the name, just return category + attempts
            if (CategoryNameLabel.ActualWidth + AttemptCountLabel.ActualWidth > GameNameLabel.ActualWidth)
            {
                return CategoryNameLabel.ActualWidth + AttemptCountLabel.ActualWidth + CategoryNameLabel.X + 5;
            }

            // The game name is longer than the category+attempts, so center the category, then add the attempts and compare with the game name.
            float centeredCategoryWidth = (GameNameLabel.ActualWidth / 2) + (CategoryNameLabel.ActualWidth / 2) + AttemptCountLabel.ActualWidth;
            return centeredCategoryWidth > GameNameLabel.ActualWidth
                ? centeredCategoryWidth + CategoryNameLabel.X + 5
                : GameNameLabel.ActualWidth + GameNameLabel.X + 5;
        }
    }

    public IDictionary<string, Action> ContextMenuControls => null;

    public float PaddingTop => 0f;
    public float PaddingLeft => 7f;
    public float PaddingBottom => 0f;
    public float PaddingRight => 7f;

    protected SimpleLabel GameNameLabel = new();
    protected SimpleLabel CategoryNameLabel = new();
    protected SimpleLabel AttemptCountLabel = new();

    protected Font TitleFont { get; set; }

    public float MinimumHeight { get; set; }

    public Title()
    {
        VerticalHeight = 10;
        Settings = new TitleSettings();
        Cache = new GraphicsCache();
    }

    private void DrawGeneral(DrawingContext g, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        g.FillGradient(
            Settings.BackgroundColor,
            Settings.BackgroundGradient == GradientType.Plain ? Settings.BackgroundColor : Settings.BackgroundColor2,
            Settings.BackgroundGradient == GradientType.Horizontal,
            width, height);

        TitleFont = Settings.OverrideTitleFont && Settings.TitleFont != null ? Settings.TitleFont : state.LayoutSettings.TextFont;
        MinimumHeight = VerticalHeight = (float)DrawingHelpers.GetLineHeight(TitleFont) * 1.7f;

        bool showGameIcon = state.Run.GameIcon != null && Settings.DisplayGameIcon;
        if (showGameIcon)
        {
            DrawGameIcon(g, state, height);
        }

        DrawAttemptCount(g, state, width, height);

        CalculatePadding(height, mode, showGameIcon, out float startPadding, out float titleEndPadding, out float categoryEndPadding);

        DrawGameName(g, state, width, height, showGameIcon, startPadding, titleEndPadding);
        DrawCategoryName(g, state, width, height, showGameIcon, startPadding, categoryEndPadding);
    }

    private void CalculatePadding(float height, LayoutMode mode, bool showGameIcon, out float startPadding, out float titleEndPadding, out float categoryEndPadding)
    {
        startPadding = 5;
        titleEndPadding = 5;
        categoryEndPadding = 5;

        if (showGameIcon)
        {
            startPadding += height + 3;
        }

        if (mode == LayoutMode.Vertical && Settings.ShowCount)
        {
            if (string.IsNullOrEmpty(CategoryNameLabel.Text))
            {
                titleEndPadding += AttemptCountLabel.ActualWidth;
            }
            else
            {
                categoryEndPadding += AttemptCountLabel.ActualWidth;
            }
        }
    }

    private void ApplyStyle(SimpleLabel label, LiveSplitState state)
    {
        label.Font = TitleFont;
        label.ForeColor = Settings.OverrideTitleColor ? Settings.TitleColor : state.LayoutSettings.TextColor;
        label.HasShadow = state.LayoutSettings.DropShadows;
        label.ShadowColor = state.LayoutSettings.ShadowsColor;
        label.OutlineColor = state.LayoutSettings.TextOutlineColor;
    }

    private bool IsCentered(bool showGameIcon)
    {
        return Settings.TextAlignment == AlignmentType.Center || (Settings.TextAlignment == AlignmentType.Auto && !showGameIcon);
    }

    private void DrawCategoryName(DrawingContext g, LiveSplitState state, float width, float height, bool showGameIcon, float startPadding, float categoryEndPadding)
    {
        ApplyStyle(CategoryNameLabel, state);
        if (IsCentered(showGameIcon))
        {
            CategoryNameLabel.CalculateAlternateText(width - startPadding - categoryEndPadding);
            (CategoryNameLabel.X, CategoryNameLabel.Width) = CalculateCenteredPositionAndWidth(width, CategoryNameLabel.ActualWidth, startPadding, categoryEndPadding);
        }
        else
        {
            CategoryNameLabel.X = startPadding;
            CategoryNameLabel.Width = width - startPadding - categoryEndPadding;
        }

        CategoryNameLabel.Y = 0;
        CategoryNameLabel.HorizontalAlignment = StringAlignment.Near;
        CategoryNameLabel.VerticalAlignment = string.IsNullOrEmpty(GameNameLabel.Text) ? StringAlignment.Center : StringAlignment.Far;
        CategoryNameLabel.Height = height;
        CategoryNameLabel.Draw(g);
    }

    private void DrawAttemptCount(DrawingContext g, LiveSplitState state, float width, float height)
    {
        if (!Settings.ShowCount)
        {
            AttemptCountLabel.ActualWidth = 0;
            return;
        }

        ApplyStyle(AttemptCountLabel, state);
        AttemptCountLabel.HorizontalAlignment = StringAlignment.Far;
        AttemptCountLabel.VerticalAlignment = StringAlignment.Far;
        AttemptCountLabel.X = 0;
        AttemptCountLabel.Y = height - 40;
        AttemptCountLabel.Width = width - 5;
        AttemptCountLabel.Height = 40;
        AttemptCountLabel.SetActualWidth();
        AttemptCountLabel.Draw(g);
    }

    private void DrawGameName(DrawingContext g, LiveSplitState state, float width, float height, bool showGameIcon, float startPadding, float titleEndPadding)
    {
        ApplyStyle(GameNameLabel, state);
        if (IsCentered(showGameIcon))
        {
            GameNameLabel.CalculateAlternateText(width - startPadding - titleEndPadding);
            (GameNameLabel.X, GameNameLabel.Width) = CalculateCenteredPositionAndWidth(width, GameNameLabel.ActualWidth, startPadding, titleEndPadding);
        }
        else
        {
            GameNameLabel.X = startPadding;
            GameNameLabel.Width = width - startPadding - titleEndPadding;
        }

        GameNameLabel.HorizontalAlignment = StringAlignment.Near;
        GameNameLabel.VerticalAlignment = string.IsNullOrEmpty(CategoryNameLabel.Text) ? StringAlignment.Center : StringAlignment.Near;
        GameNameLabel.Y = 0;
        GameNameLabel.Height = height;
        GameNameLabel.Draw(g);
    }

    private static void DrawGameIcon(DrawingContext g, LiveSplitState state, float height)
    {
        Drawing.Image icon = state.Run.GameIcon;
        if (icon.Width <= 0 || icon.Height <= 0)
        {
            return;
        }

        float drawWidth = height - 4;
        float drawHeight = height - 4;
        if (icon.Width > icon.Height)
        {
            drawHeight *= icon.Height / (float)icon.Width;
        }
        else
        {
            drawWidth *= icon.Width / (float)icon.Height;
        }

        g.DrawImage(icon,
            7 + ((height - 4 - drawWidth) / 2),
            2 + ((height - 4 - drawHeight) / 2),
            drawWidth,
            drawHeight);
    }

    /// <summary>
    /// Returns the position and width of a string so that it is centered in the total width
    /// without overlapping the start or end padding.
    /// </summary>
    private static (float Position, float Width) CalculateCenteredPositionAndWidth(float totalWidth, float stringWidth, float startPadding, float endPadding)
    {
        float position;
        if (startPadding + stringWidth + endPadding > totalWidth)
        {
            position = startPadding;
        }
        else
        {
            position = Math.Max((totalWidth - stringWidth) / 2, startPadding);
            if (position + stringWidth > totalWidth - endPadding)
            {
                position = totalWidth - endPadding - stringWidth;
            }
        }

        return (position, totalWidth - endPadding - position);
    }

    public void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion)
    {
        DrawGeneral(g, state, HorizontalWidth, height, LayoutMode.Horizontal);
    }

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion)
    {
        DrawGeneral(g, state, width, VerticalHeight, LayoutMode.Vertical);
    }

    public string ComponentName => "Title";

    public Control GetSettingsControl(LayoutMode mode)
    {
        return SettingsEditor.Create(Settings);
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        return Settings.GetSettings(document);
    }

    public void SetSettings(XmlNode settings)
    {
        Settings.SetSettings(settings);
        Cache = new GraphicsCache();
    }

    public void MigrateFontOverrides(Options.FontOverrides overrides)
    {
        if (Settings.OverrideTitleFont && Settings.TitleFont != null)
        {
            overrides.OverrideTextFont = true;
            overrides.TextFont = (Font)Settings.TitleFont.Clone();
            Settings.OverrideTitleFont = false;
        }
    }

    private static IEnumerable<string> GetCategoryNameAbbreviations(string categoryName)
    {
        int indexStart = categoryName.IndexOf('(');
        int indexEnd = categoryName.IndexOf(')', indexStart + 1);
        string afterParentheses = "";
        if (indexStart >= 0 && indexEnd >= 0)
        {
            string inside = categoryName.Substring(indexStart + 1, indexEnd - indexStart - 1);
            afterParentheses = categoryName[(indexEnd + 1)..].Trim();
            categoryName = categoryName[..indexStart].Trim();
            string[] splits = inside.Split(',');
            for (int i = splits.Length - 1; i > 0; --i)
            {
                yield return $"{categoryName} ({string.Join(",", splits.Take(i))}) {afterParentheses}".Trim();
            }
        }

        yield return $"{categoryName} {afterParentheses}".Trim();
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        string extendedCategoryName = state.Run.GetExtendedCategoryName(Settings.ShowRegion, Settings.ShowPlatform, Settings.ShowVariables);
        Cache.Restart();
        Cache["SingleLine"] = Settings.SingleLine;
        Cache["GameName"] = state.Run.GameName;
        Cache["CategoryName"] = extendedCategoryName;
        Cache["LayoutMode"] = mode;
        Cache["ShowGameName"] = Settings.ShowGameName;
        Cache["ShowCategoryName"] = Settings.ShowCategoryName;
        if (Cache.HasChanged)
        {
            string gameName = state.Run.GameName ?? "";
            if (Settings.SingleLine && Settings.ShowGameName && Settings.ShowCategoryName)
            {
                string text = $"{gameName} - {extendedCategoryName}";
                IEnumerable<string> gameAbbreviations = gameName.GetAbbreviations();
                string shortestGameName = gameAbbreviations.LastOrDefault() ?? gameName;
                IEnumerable<string> combinedAbbreviations1 = gameAbbreviations.Select(x => $"{x} - {extendedCategoryName}");
                IEnumerable<string> combinedAbbreviations2 = GetCategoryNameAbbreviations(extendedCategoryName).Select(x => $"{shortestGameName} - {x}");
                GameNameLabel.Text = text;
                GameNameLabel.AlternateText = mode == LayoutMode.Vertical ? [.. combinedAbbreviations1, .. combinedAbbreviations2] : [];
                CategoryNameLabel.Text = "";
            }
            else
            {
                GameNameLabel.Text = Settings.ShowGameName ? gameName : "";
                GameNameLabel.AlternateText = Settings.ShowGameName && mode == LayoutMode.Vertical ? [.. gameName.GetAbbreviations()] : [];
                CategoryNameLabel.Text = Settings.ShowCategoryName ? extendedCategoryName : "";
                CategoryNameLabel.AlternateText = Settings.ShowCategoryName && mode == LayoutMode.Vertical ? [.. GetCategoryNameAbbreviations(extendedCategoryName)] : [];
            }
        }

        Cache.Restart();
        Cache["AttemptHistoryCount"] = state.Run.AttemptHistory.Count;
        Cache["Run"] = state.Run;
        if (Cache.HasChanged)
        {
            FinishedRunsInHistory = state.Run.AttemptHistory.Count(x => x.Time.RealTime != null);
        }

        int totalFinishedRunsCount = FinishedRunsInHistory + (state.CurrentPhase == TimerPhase.Ended ? 1 : 0);
        if (Settings.ShowAttemptCount && Settings.ShowFinishedRunsCount)
        {
            AttemptCountLabel.Text = $"{totalFinishedRunsCount}/{state.Run.AttemptCount}";
        }
        else if (Settings.ShowAttemptCount)
        {
            AttemptCountLabel.Text = state.Run.AttemptCount.ToString();
        }
        else if (Settings.ShowFinishedRunsCount)
        {
            AttemptCountLabel.Text = totalFinishedRunsCount.ToString();
        }

        Cache.Restart();
        Cache["GameIcon"] = state.Run.GameIcon;
        Cache["GameNameLabel"] = GameNameLabel.Text;
        Cache["CategoryNameLabel"] = CategoryNameLabel.Text;
        Cache["AttemptCountLabel"] = AttemptCountLabel.Text;
        Cache["TextAlignment"] = Settings.TextAlignment;

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
