using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LiveSplit.Model;
using LiveSplit.TimeFormatters;
using System;
using System.Collections.Generic;
using System.Xml;

namespace LiveSplit.UI.Components;

/// <summary>
/// Avalonia port of LiveSplit.Core's InfoTextComponent: a "name ... value" row, the building
/// block of most information components.
/// </summary>
public class InfoTextComponent : IComponent
{
    public string InformationName { get => NameLabel.Text; set => NameLabel.Text = value; }
    public string InformationValue { get => ValueLabel.Text; set => ValueLabel.Text = value; }

    public GraphicsCache Cache { get; set; }

    public ICollection<string> AlternateNameText { get => NameLabel.AlternateText; set => NameLabel.AlternateText = value; }

    public SimpleLabel NameLabel { get; protected set; }
    public SimpleLabel ValueLabel { get; protected set; }

    public string LongestString { get; set; }
    protected SimpleLabel NameMeasureLabel { get; set; }

    public float PaddingTop { get; set; }
    public float PaddingLeft => 7f;
    public float PaddingBottom { get; set; }
    public float PaddingRight => 7f;

    public bool DisplayTwoRows { get; set; }

    public float VerticalHeight { get; set; }

    public float MinimumWidth => 20;

    public float HorizontalWidth
        => Math.Max(NameMeasureLabel.ActualWidth, ValueLabel.ActualWidth) + 10;

    public float MinimumHeight { get; set; }

    public InfoTextComponent(string informationName, string informationValue)
    {
        Cache = new GraphicsCache();
        NameLabel = new SimpleLabel()
        {
            HorizontalAlignment = StringAlignment.Near,
            Text = informationName
        };
        ValueLabel = new SimpleLabel()
        {
            HorizontalAlignment = StringAlignment.Far,
            Text = informationValue
        };
        NameMeasureLabel = new SimpleLabel();
        MinimumHeight = 25;
        VerticalHeight = 31;
        LongestString = "";
    }

    public virtual void PrepareDraw(LiveSplitState state, LayoutMode mode)
    {
        NameMeasureLabel.Font = state.LayoutSettings.TextFont;
        ValueLabel.Font = state.LayoutSettings.TextFont;
        NameLabel.Font = state.LayoutSettings.TextFont;
        if (mode == LayoutMode.Vertical)
        {
            NameLabel.VerticalAlignment = StringAlignment.Center;
            ValueLabel.VerticalAlignment = StringAlignment.Center;
        }
        else
        {
            NameLabel.VerticalAlignment = StringAlignment.Near;
            ValueLabel.VerticalAlignment = StringAlignment.Far;
        }
    }

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion)
    {
        PrepareDraw(state, LayoutMode.Vertical);

        if (DisplayTwoRows)
        {
            VerticalHeight = 0.9f * (float)(DrawingHelpers.GetLineHeight(ValueLabel.Font) + DrawingHelpers.GetLineHeight(NameLabel.Font));
            PaddingTop = PaddingBottom = 0;
            DrawTwoRows(g, state, width, VerticalHeight);
        }
        else
        {
            VerticalHeight = 31;
            NameLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
            NameLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
            NameLabel.HasShadow = state.LayoutSettings.DropShadows;
            ValueLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
            ValueLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
            ValueLabel.HasShadow = state.LayoutSettings.DropShadows;

            float textHeight = 0.75f * (float)Math.Max(DrawingHelpers.GetLineHeight(ValueLabel.Font), DrawingHelpers.GetLineHeight(NameLabel.Font));
            PaddingTop = Math.Max(0, (VerticalHeight - textHeight) / 2f);
            PaddingBottom = PaddingTop;

            NameMeasureLabel.Text = LongestString;
            NameMeasureLabel.SetActualWidth();
            ValueLabel.SetActualWidth();

            NameLabel.Width = width - ValueLabel.ActualWidth - 10;
            NameLabel.Height = VerticalHeight;
            NameLabel.X = 5;
            NameLabel.Y = 0;

            ValueLabel.Width = ValueLabel.IsMonospaced ? width - 12 : width - 10;
            ValueLabel.Height = VerticalHeight;
            ValueLabel.Y = 0;
            ValueLabel.X = 5;

            NameLabel.Draw(g);
            ValueLabel.Draw(g);
        }
    }

    public void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion)
    {
        PrepareDraw(state, LayoutMode.Horizontal);
        DrawTwoRows(g, state, HorizontalWidth, height);
    }

    protected void DrawTwoRows(DrawingContext g, LiveSplitState state, float width, float height)
    {
        NameLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
        NameLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
        NameLabel.HasShadow = state.LayoutSettings.DropShadows;
        ValueLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
        ValueLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
        ValueLabel.HasShadow = state.LayoutSettings.DropShadows;

        if (InformationName != null && LongestString != null && InformationName.Length > LongestString.Length)
        {
            LongestString = InformationName;
        }

        NameMeasureLabel.Text = LongestString;
        NameMeasureLabel.Font = state.LayoutSettings.TextFont;
        NameMeasureLabel.SetActualWidth();
        ValueLabel.SetActualWidth();

        MinimumHeight = 0.85f * (float)(DrawingHelpers.GetLineHeight(ValueLabel.Font) + DrawingHelpers.GetLineHeight(NameLabel.Font));
        NameLabel.Width = width - 10;
        NameLabel.Height = height;
        NameLabel.X = 5;
        NameLabel.Y = 0;

        ValueLabel.Width = ValueLabel.IsMonospaced ? width - 12 : width - 10;
        ValueLabel.Height = height;
        ValueLabel.Y = 0;
        ValueLabel.X = 5;

        NameLabel.Draw(g);
        ValueLabel.Draw(g);
    }

    public virtual string ComponentName => throw new NotSupportedException();

    public virtual Control GetSettingsControl(LayoutMode mode)
    {
        return null;
    }

    public virtual void SetSettings(XmlNode settings) { }

    public virtual XmlNode GetSettings(XmlDocument document)
    {
        throw new NotSupportedException();
    }

    public virtual IDictionary<string, Action> ContextMenuControls => null;

    public virtual void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        Cache.Restart();
        Cache["NameText"] = InformationName;
        Cache["ValueText"] = InformationValue;
        Cache["NameColor"] = NameLabel.ForeColor.ToArgb();
        Cache["ValueColor"] = ValueLabel.ForeColor.ToArgb();
        Cache["DisplayTwoRows"] = DisplayTwoRows;

        if (invalidator != null && Cache.HasChanged)
        {
            invalidator.Invalidate(0, 0, width, height);
        }
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

public class InfoTimeComponent : InfoTextComponent
{
    public TimeSpan? TimeValue
    {
        get;
        set
        {
            field = value;
            InformationValue = Formatter.Format(field);
        }
    }

    public ITimeFormatter Formatter
    {
        get;
        set
        {
            if (value != null && value != field)
            {
                InformationValue = value.Format(TimeValue);
            }

            field = value;
        }
    }

    public override void PrepareDraw(LiveSplitState state, LayoutMode mode)
    {
        ValueLabel.IsMonospaced = true;
        ValueLabel.Font = state.LayoutSettings.TimesFont;
        NameMeasureLabel.Font = state.LayoutSettings.TextFont;
        NameLabel.Font = state.LayoutSettings.TextFont;
        if (mode == LayoutMode.Vertical)
        {
            NameLabel.VerticalAlignment = StringAlignment.Center;
            ValueLabel.VerticalAlignment = StringAlignment.Center;
        }
        else
        {
            NameLabel.VerticalAlignment = StringAlignment.Near;
            ValueLabel.VerticalAlignment = StringAlignment.Far;
        }
    }

    public InfoTimeComponent(string informationName, TimeSpan? timeValue, ITimeFormatter formatter)
        : base(informationName, "")
    {
        Formatter = formatter;
        TimeValue = timeValue;
    }
}
