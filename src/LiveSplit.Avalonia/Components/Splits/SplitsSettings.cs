using LiveSplit.TimeFormatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Color = System.Drawing.Color;

namespace LiveSplit.UI.Components;

public enum ExtendedGradientType
{
    Plain, Vertical, Horizontal, Alternating
}

public enum ColumnType
{
    Delta, SplitTime, DeltaorSplitTime, SegmentDelta, SegmentTime, SegmentDeltaorSegmentTime, CustomVariable
}

public class ColumnData
{
    public string Name { get; set; }
    public ColumnType Type { get; set; }
    public string Comparison { get; set; }
    public string TimingMethod { get; set; }

    public ColumnData(string name, ColumnType type, string comparison, string method)
    {
        Name = name;
        Type = type;
        Comparison = comparison;
        TimingMethod = method;
    }

    public static ColumnData FromXml(XmlNode node)
    {
        var element = (XmlElement)node;
        return new ColumnData(element["Name"].InnerText,
            Enum.Parse<ColumnType>(element["Type"].InnerText),
            element["Comparison"].InnerText,
            element["TimingMethod"].InnerText);
    }

    public int CreateElement(XmlDocument document, XmlElement element)
    {
        return SettingsHelper.CreateSetting(document, element, "Version", "1.5") ^
            SettingsHelper.CreateSetting(document, element, "Name", Name) ^
            SettingsHelper.CreateSetting(document, element, "Type", Type) ^
            SettingsHelper.CreateSetting(document, element, "Comparison", Comparison) ^
            SettingsHelper.CreateSetting(document, element, "TimingMethod", TimingMethod);
    }
}

public class SplitsSettings : ComponentSettings
{
    public event EventHandler SplitLayoutChanged;

    [Setting("Total Splits", Minimum = 1, Maximum = 99)]
    public int VisualSplitCount { get; set; } = 8;
    [Setting("Upcoming Splits", Minimum = 0, Maximum = 97)]
    public int SplitPreviewCount { get; set; } = 1;
    public bool ShowThinSeparators { get; set; } = true;
    public bool AlwaysShowLastSplit { get; set; } = true;
    public bool ShowBlankSplits { get; set; } = true;
    public bool LockLastSplit { get; set; } = true;
    public bool SeparatorLastSplit { get; set; } = true;
    [Setting("Split Height", Minimum = 0, Maximum = 30)]
    public float SplitHeight { get; set; } = 3.6f;
    [Setting("Split Width", Minimum = 0, Maximum = 400)]
    public float SplitWidth { get; set; } = 20;
    public bool Display2Rows { get; set; }

    public bool DisplayIcons { get; set; } = true;
    public bool IconShadows { get; set; } = true;
    [Setting("Icon Size", Minimum = 0, Maximum = 256)]
    public float IconSize { get; set; } = 24f;

    public GradientType CurrentSplitGradient { get; set; } = GradientType.Vertical;
    public Color CurrentSplitTopColor { get; set; } = Color.FromArgb(51, 115, 244);
    public Color CurrentSplitBottomColor { get; set; } = Color.FromArgb(21, 53, 116);
    public ExtendedGradientType BackgroundGradient { get; set; } = ExtendedGradientType.Alternating;
    public Color BackgroundColor { get; set; } = Color.Transparent;
    public Color BackgroundColor2 { get; set; } = Color.FromArgb(1, 255, 255, 255);

    public bool AutomaticAbbreviations { get; set; }
    public bool OverrideTextColor { get; set; }
    public Color BeforeNamesColor { get; set; } = Color.FromArgb(255, 255, 255);
    public Color CurrentNamesColor { get; set; } = Color.FromArgb(255, 255, 255);
    public Color AfterNamesColor { get; set; } = Color.FromArgb(255, 255, 255);
    public bool OverrideTimesColor { get; set; }
    public Color BeforeTimesColor { get; set; } = Color.FromArgb(255, 255, 255);
    public Color CurrentTimesColor { get; set; } = Color.FromArgb(255, 255, 255);
    public Color AfterTimesColor { get; set; } = Color.FromArgb(255, 255, 255);

    public TimeAccuracy SplitTimesAccuracy { get; set; } = TimeAccuracy.Seconds;
    public TimeAccuracy DeltasAccuracy { get; set; } = TimeAccuracy.Tenths;
    public bool DropDecimals { get; set; } = true;
    public bool OverrideDeltasColor { get; set; }
    public Color DeltasColor { get; set; } = Color.FromArgb(255, 255, 255);

    public bool ShowColumnLabels { get; set; }
    public Color LabelsColor { get; set; } = Color.FromArgb(255, 255, 255);

    [Hidden]
    public IList<ColumnData> ColumnsList { get; set; } =
    [
        new ColumnData("+/-", ColumnType.Delta, "Current Comparison", "Current Timing Method"),
        new ColumnData("Time", ColumnType.SplitTime, "Current Comparison", "Current Timing Method")
    ];

    public override void OnSettingChanged(string propertyName)
    {
        base.OnSettingChanged(propertyName);
        SplitLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    public override void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);
        CurrentSplitTopColor = SettingsHelper.ParseColor(element["CurrentSplitTopColor"]);
        CurrentSplitBottomColor = SettingsHelper.ParseColor(element["CurrentSplitBottomColor"]);
        VisualSplitCount = SettingsHelper.ParseInt(element["VisualSplitCount"]);
        SplitPreviewCount = SettingsHelper.ParseInt(element["SplitPreviewCount"]);
        DisplayIcons = SettingsHelper.ParseBool(element["DisplayIcons"]);
        ShowThinSeparators = SettingsHelper.ParseBool(element["ShowThinSeparators"]);
        AlwaysShowLastSplit = SettingsHelper.ParseBool(element["AlwaysShowLastSplit"]);
        SplitWidth = SettingsHelper.ParseFloat(element["SplitWidth"]);
        AutomaticAbbreviations = SettingsHelper.ParseBool(element["AutomaticAbbreviations"], false);
        ShowColumnLabels = SettingsHelper.ParseBool(element["ShowColumnLabels"], false);
        LabelsColor = SettingsHelper.ParseColor(element["LabelsColor"], Color.FromArgb(255, 255, 255));
        OverrideTimesColor = SettingsHelper.ParseBool(element["OverrideTimesColor"], false);
        BeforeTimesColor = SettingsHelper.ParseColor(element["BeforeTimesColor"], Color.FromArgb(255, 255, 255));
        CurrentTimesColor = SettingsHelper.ParseColor(element["CurrentTimesColor"], Color.FromArgb(255, 255, 255));
        AfterTimesColor = SettingsHelper.ParseColor(element["AfterTimesColor"], Color.FromArgb(255, 255, 255));
        SplitHeight = SettingsHelper.ParseFloat(element["SplitHeight"], 6);
        CurrentSplitGradient = Enum.Parse<GradientType>(SettingsHelper.ParseString(element["CurrentSplitGradient"], GradientType.Vertical.ToString()));
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.Transparent);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.Transparent);
        BackgroundGradient = Enum.Parse<ExtendedGradientType>(SettingsHelper.ParseString(element["BackgroundGradient"], ExtendedGradientType.Plain.ToString()));
        SeparatorLastSplit = SettingsHelper.ParseBool(element["SeparatorLastSplit"], true);
        DropDecimals = SettingsHelper.ParseBool(element["DropDecimals"], true);
        DeltasAccuracy = SettingsHelper.ParseEnum(element["DeltasAccuracy"], TimeAccuracy.Tenths);
        OverrideDeltasColor = SettingsHelper.ParseBool(element["OverrideDeltasColor"], false);
        DeltasColor = SettingsHelper.ParseColor(element["DeltasColor"], Color.FromArgb(255, 255, 255));
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"], false);
        SplitTimesAccuracy = SettingsHelper.ParseEnum(element["SplitTimesAccuracy"], TimeAccuracy.Seconds);
        ShowBlankSplits = SettingsHelper.ParseBool(element["ShowBlankSplits"], true);
        LockLastSplit = SettingsHelper.ParseBool(element["LockLastSplit"], false);
        IconSize = SettingsHelper.ParseFloat(element["IconSize"], 24f);
        IconShadows = SettingsHelper.ParseBool(element["IconShadows"], true);

        ColumnsList.Clear();
        if (version >= new Version(1, 5))
        {
            foreach (XmlNode child in element["Columns"].ChildNodes)
            {
                ColumnsList.Add(ColumnData.FromXml(child));
            }
        }
        else
        {
            string comparison = SettingsHelper.ParseString(element["Comparison"]);
            if (SettingsHelper.ParseBool(element["ShowSplitTimes"]))
            {
                ColumnsList.Add(new ColumnData("+/-", ColumnType.Delta, comparison, "Current Timing Method"));
                ColumnsList.Add(new ColumnData("Time", ColumnType.SplitTime, comparison, "Current Timing Method"));
            }
            else
            {
                ColumnsList.Add(new ColumnData("+/-", ColumnType.DeltaorSplitTime, comparison, "Current Timing Method"));
            }
        }

        if (version >= new Version(1, 3))
        {
            BeforeNamesColor = SettingsHelper.ParseColor(element["BeforeNamesColor"]);
            CurrentNamesColor = SettingsHelper.ParseColor(element["CurrentNamesColor"]);
            AfterNamesColor = SettingsHelper.ParseColor(element["AfterNamesColor"]);
            OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        }
        else
        {
            BeforeNamesColor = CurrentNamesColor = AfterNamesColor = version >= new Version(1, 2)
                ? SettingsHelper.ParseColor(element["SplitNamesColor"])
                : Color.FromArgb(255, 255, 255);
            OverrideTextColor = !SettingsHelper.ParseBool(element["UseTextColor"], true);
        }

        SplitLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        int hashCode = SettingsHelper.CreateSetting(document, parent, "Version", "1.6") ^
            SettingsHelper.CreateSetting(document, parent, "CurrentSplitTopColor", CurrentSplitTopColor) ^
            SettingsHelper.CreateSetting(document, parent, "CurrentSplitBottomColor", CurrentSplitBottomColor) ^
            SettingsHelper.CreateSetting(document, parent, "VisualSplitCount", VisualSplitCount) ^
            SettingsHelper.CreateSetting(document, parent, "SplitPreviewCount", SplitPreviewCount) ^
            SettingsHelper.CreateSetting(document, parent, "DisplayIcons", DisplayIcons) ^
            SettingsHelper.CreateSetting(document, parent, "ShowThinSeparators", ShowThinSeparators) ^
            SettingsHelper.CreateSetting(document, parent, "AlwaysShowLastSplit", AlwaysShowLastSplit) ^
            SettingsHelper.CreateSetting(document, parent, "SplitWidth", SplitWidth) ^
            SettingsHelper.CreateSetting(document, parent, "SplitTimesAccuracy", SplitTimesAccuracy) ^
            SettingsHelper.CreateSetting(document, parent, "AutomaticAbbreviations", AutomaticAbbreviations) ^
            SettingsHelper.CreateSetting(document, parent, "BeforeNamesColor", BeforeNamesColor) ^
            SettingsHelper.CreateSetting(document, parent, "CurrentNamesColor", CurrentNamesColor) ^
            SettingsHelper.CreateSetting(document, parent, "AfterNamesColor", AfterNamesColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
            SettingsHelper.CreateSetting(document, parent, "BeforeTimesColor", BeforeTimesColor) ^
            SettingsHelper.CreateSetting(document, parent, "CurrentTimesColor", CurrentTimesColor) ^
            SettingsHelper.CreateSetting(document, parent, "AfterTimesColor", AfterTimesColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTimesColor", OverrideTimesColor) ^
            SettingsHelper.CreateSetting(document, parent, "ShowBlankSplits", ShowBlankSplits) ^
            SettingsHelper.CreateSetting(document, parent, "LockLastSplit", LockLastSplit) ^
            SettingsHelper.CreateSetting(document, parent, "IconSize", IconSize) ^
            SettingsHelper.CreateSetting(document, parent, "IconShadows", IconShadows) ^
            SettingsHelper.CreateSetting(document, parent, "SplitHeight", SplitHeight) ^
            SettingsHelper.CreateSetting(document, parent, "CurrentSplitGradient", CurrentSplitGradient) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
            SettingsHelper.CreateSetting(document, parent, "SeparatorLastSplit", SeparatorLastSplit) ^
            SettingsHelper.CreateSetting(document, parent, "DeltasAccuracy", DeltasAccuracy) ^
            SettingsHelper.CreateSetting(document, parent, "DropDecimals", DropDecimals) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideDeltasColor", OverrideDeltasColor) ^
            SettingsHelper.CreateSetting(document, parent, "DeltasColor", DeltasColor) ^
            SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
            SettingsHelper.CreateSetting(document, parent, "ShowColumnLabels", ShowColumnLabels) ^
            SettingsHelper.CreateSetting(document, parent, "LabelsColor", LabelsColor);

        XmlElement columnsElement = null;
        if (document != null)
        {
            columnsElement = document.CreateElement("Columns");
            parent.AppendChild(columnsElement);
        }

        int count = 1;
        foreach (ColumnData columnData in ColumnsList.ToList())
        {
            XmlElement settings = null;
            if (document != null)
            {
                settings = document.CreateElement("Settings");
                columnsElement.AppendChild(settings);
            }

            hashCode ^= columnData.CreateElement(document, settings) * count;
            count++;
        }

        return hashCode;
    }
}
