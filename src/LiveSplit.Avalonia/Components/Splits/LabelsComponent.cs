using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace LiveSplit.UI.Components;

[GlobalFontConsumer(GlobalFont.TextFont)]
public class LabelsComponent : IComponent
{
    public SplitsSettings Settings { get; set; }

    public GraphicsCache Cache { get; set; }

    public IEnumerable<ColumnData> ColumnsList { get; set; }
    public IList<SimpleLabel> LabelsList { get; set; }
    protected List<(int exLength, float exWidth, float width)> ColumnWidths { get; }

    public float PaddingTop => 0f;
    public float PaddingLeft => 0f;
    public float PaddingBottom => 0f;
    public float PaddingRight => 0f;

    public float VerticalHeight => 25 + Settings.SplitHeight;

    public float MinimumWidth { get; set; }

    public float HorizontalWidth => 10f; /* not available in horizontal mode */

    public float MinimumHeight { get; set; }

    public IDictionary<string, Action> ContextMenuControls => null;

    public LabelsComponent(SplitsSettings settings, IEnumerable<ColumnData> columns, List<(int exLength, float exWidth, float width)> columnWidths)
    {
        Settings = settings;
        MinimumHeight = 31;
        Cache = new GraphicsCache();
        LabelsList = [];
        ColumnsList = columns;
        ColumnWidths = columnWidths;
    }

    private void DrawGeneral(DrawingContext g, LiveSplitState state, float width, float height)
    {
        if (Settings.BackgroundGradient == ExtendedGradientType.Alternating)
        {
            g.FillRectangle(Settings.BackgroundColor, 0, 0, width, height);
        }

        foreach (SimpleLabel label in LabelsList)
        {
            label.ShadowColor = state.LayoutSettings.ShadowsColor;
            label.OutlineColor = state.LayoutSettings.TextOutlineColor;
            label.Y = 0;
            label.Height = height;
        }

        MinimumWidth = 10f;

        if (ColumnsList.Count() == LabelsList.Count)
        {
            while (ColumnWidths.Count < LabelsList.Count)
            {
                ColumnWidths.Add((0, 0f, 0f));
            }

            float curX = width - 7;
            for (int i = LabelsList.Count - 1; i >= 0; i--)
            {
                SimpleLabel label = LabelsList[i];
                float labelWidth = ColumnWidths[i].width;

                curX -= labelWidth + 5;
                label.Width = labelWidth;
                label.X = curX + 5;

                label.Font = state.LayoutSettings.TextFont;
                label.HasShadow = state.LayoutSettings.DropShadows;
                label.Draw(g);
            }
        }
    }

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion)
    {
        DrawGeneral(g, state, width, VerticalHeight);
    }

    public void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion)
    {
        DrawGeneral(g, state, HorizontalWidth, height);
    }

    public string ComponentName => "Labels";

    public Control GetSettingsControl(LayoutMode mode)
    {
        throw new NotSupportedException();
    }

    public void SetSettings(XmlNode settings)
    {
        throw new NotSupportedException();
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        throw new NotSupportedException();
    }

    protected void UpdateAll(LiveSplitState state)
    {
        RecreateLabels();

        int index = 0;
        foreach (ColumnData column in ColumnsList)
        {
            SimpleLabel label = LabelsList[index++];
            label.Text = string.IsNullOrEmpty(column.Name)
                ? CompositeComparisons.GetShortComparisonName(column.Comparison == "Current Comparison" ? state.CurrentComparison : column.Comparison)
                : column.Name;
            label.ForeColor = Settings.LabelsColor;
        }
    }

    protected void RecreateLabels()
    {
        if (ColumnsList != null && LabelsList.Count != ColumnsList.Count())
        {
            LabelsList.Clear();
            foreach (ColumnData _ in ColumnsList)
            {
                LabelsList.Add(new SimpleLabel()
                {
                    HorizontalAlignment = StringAlignment.Far,
                    VerticalAlignment = StringAlignment.Center
                });
            }
        }
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        UpdateAll(state);

        Cache.Restart();
        Cache["ColumnsCount"] = ColumnsList.Count();
        for (int index = 0; index < LabelsList.Count; index++)
        {
            SimpleLabel label = LabelsList[index];
            Cache["Columns" + index + "Text"] = label.Text;
            if (index < ColumnWidths.Count)
            {
                Cache["Columns" + index + "Width"] = ColumnWidths[index].width;
            }
        }

        if (invalidator != null && Cache.HasChanged)
        {
            invalidator.Invalidate(0, 0, width, height);
        }
    }

    public void Dispose() { }
}
