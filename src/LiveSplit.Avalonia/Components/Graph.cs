using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Color = System.Drawing.Color;

namespace LiveSplit.UI.Components;

public class GraphSettings : ComponentSettings
{
    [Setting("Height", Minimum = 10, Maximum = 1000)]
    public float GraphHeight { get; set; } = 120;
    [Setting("Width", Minimum = 10, Maximum = 2000)]
    public float GraphWidth { get; set; } = 180;
    [Setting(Options = ["{Comparisons}"])]
    public string Comparison { get; set; } = "Current Comparison";
    [Setting("Live Graph")]
    public bool IsLiveGraph { get; set; } = true;
    public bool FlipGraph { get; set; }
    public bool ShowBestSegments { get; set; }

    public Color BehindGraphColor { get; set; } = Color.FromArgb(115, 40, 40);
    public Color AheadGraphColor { get; set; } = Color.FromArgb(40, 115, 52);
    public Color GridlinesColor { get; set; } = Color.FromArgb(0x50, 0x0, 0x0, 0x0);
    public Color PartialFillColorBehind { get; set; } = Color.FromArgb(25, 255, 255, 255);
    public Color CompleteFillColorBehind { get; set; } = Color.FromArgb(50, 255, 255, 255);
    public Color PartialFillColorAhead { get; set; } = Color.FromArgb(25, 255, 255, 255);
    public Color CompleteFillColorAhead { get; set; } = Color.FromArgb(50, 255, 255, 255);
    public Color GraphColor { get; set; } = Color.White;
    public Color GraphGoldColor { get; set; } = Color.FromArgb(216, 175, 31);
    public Color ShadowsColor { get; set; } = Color.FromArgb(0x38, 0x0, 0x0, 0x0);
    public Color GraphLinesColor { get; set; } = Color.White;

    public override void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);
        GraphHeight = SettingsHelper.ParseFloat(element["Height"]);
        GraphWidth = SettingsHelper.ParseFloat(element["Width"]);
        BehindGraphColor = SettingsHelper.ParseColor(element["BehindGraphColor"]);
        AheadGraphColor = SettingsHelper.ParseColor(element["AheadGraphColor"]);
        GridlinesColor = SettingsHelper.ParseColor(element["GridlinesColor"]);
        FlipGraph = SettingsHelper.ParseBool(element["FlipGraph"], false);
        Comparison = SettingsHelper.ParseString(element["Comparison"], "Current Comparison");
        ShowBestSegments = SettingsHelper.ParseBool(element["ShowBestSegments"], false);
        GraphGoldColor = SettingsHelper.ParseColor(element["GraphGoldColor"], Color.Gold);
        GraphColor = SettingsHelper.ParseColor(element["GraphColor"]);
        ShadowsColor = SettingsHelper.ParseColor(element["ShadowsColor"]);
        GraphLinesColor = SettingsHelper.ParseColor(element["GraphLinesColor"]);
        IsLiveGraph = SettingsHelper.ParseBool(element["LiveGraph"]);
        if (version >= new Version(1, 2))
        {
            PartialFillColorBehind = SettingsHelper.ParseColor(element["PartialFillColorBehind"]);
            CompleteFillColorBehind = SettingsHelper.ParseColor(element["CompleteFillColorBehind"]);
            PartialFillColorAhead = SettingsHelper.ParseColor(element["PartialFillColorAhead"]);
            CompleteFillColorAhead = SettingsHelper.ParseColor(element["CompleteFillColorAhead"]);
        }
        else
        {
            PartialFillColorAhead = PartialFillColorBehind = SettingsHelper.ParseColor(element["PartialFillColor"]);
            CompleteFillColorAhead = CompleteFillColorBehind = SettingsHelper.ParseColor(element["CompleteFillColor"]);
        }
    }

    protected override int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.5") ^
            SettingsHelper.CreateSetting(document, parent, "Height", GraphHeight) ^
            SettingsHelper.CreateSetting(document, parent, "Width", GraphWidth) ^
            SettingsHelper.CreateSetting(document, parent, "BehindGraphColor", BehindGraphColor) ^
            SettingsHelper.CreateSetting(document, parent, "AheadGraphColor", AheadGraphColor) ^
            SettingsHelper.CreateSetting(document, parent, "GridlinesColor", GridlinesColor) ^
            SettingsHelper.CreateSetting(document, parent, "PartialFillColorBehind", PartialFillColorBehind) ^
            SettingsHelper.CreateSetting(document, parent, "CompleteFillColorBehind", CompleteFillColorBehind) ^
            SettingsHelper.CreateSetting(document, parent, "PartialFillColorAhead", PartialFillColorAhead) ^
            SettingsHelper.CreateSetting(document, parent, "CompleteFillColorAhead", CompleteFillColorAhead) ^
            SettingsHelper.CreateSetting(document, parent, "GraphColor", GraphColor) ^
            SettingsHelper.CreateSetting(document, parent, "ShadowsColor", ShadowsColor) ^
            SettingsHelper.CreateSetting(document, parent, "GraphLinesColor", GraphLinesColor) ^
            SettingsHelper.CreateSetting(document, parent, "LiveGraph", IsLiveGraph) ^
            SettingsHelper.CreateSetting(document, parent, "FlipGraph", FlipGraph) ^
            SettingsHelper.CreateSetting(document, parent, "Comparison", Comparison) ^
            SettingsHelper.CreateSetting(document, parent, "ShowBestSegments", ShowBestSegments) ^
            SettingsHelper.CreateSetting(document, parent, "GraphGoldColor", GraphGoldColor);
    }
}

/// <summary>
/// The graph itself; the layout uses <see cref="GraphCompositeComponent"/>, which adds the
/// separator lines above and below it.
/// </summary>
public class GraphComponent : IComponent
{
    public float PaddingTop => 0f;
    public float PaddingLeft => 0f;
    public float PaddingBottom => 0f;
    public float PaddingRight => 0f;

    public List<TimeSpan?> Deltas { get; set; } = [];
    public TimeSpan? FinalSplit { get; set; } = TimeSpan.Zero;
    public TimeSpan MaxDelta { get; set; }
    public TimeSpan MinDelta { get; set; }
    public bool IsLiveDeltaActive { get; set; }

    public GraphicsCache Cache { get; set; } = new();

    public float VerticalHeight => Settings.GraphHeight;
    public float MinimumWidth => 20;
    public float HorizontalWidth => Settings.GraphWidth;
    public float MinimumHeight => 20;

    public IDictionary<string, Action> ContextMenuControls => null;

    public TimeSpan GraphEdgeValue { get; set; } = new(0, 0, 0, 0, 200);
    public float GraphEdgeMin { get; set; } = 5;

    public GraphSettings Settings { get; set; }

    public GraphComponent(GraphSettings settings)
    {
        Settings = settings;
    }

    private void DrawGeneral(DrawingContext g, LiveSplitState state, float width, float height)
    {
        Matrix transform = Settings.FlipGraph
            ? Matrix.CreateTranslation(0, -height) * Matrix.CreateScale(1, -1)
            : Matrix.Identity;

        using (g.PushTransform(transform))
        {
            DrawUnflipped(g, state, width, height);
        }
    }

    private void DrawUnflipped(DrawingContext g, LiveSplitState state, float width, float height)
    {
        TimeSpan totalDelta = MinDelta - MaxDelta;

        CalculateMiddleAndGraphEdge(height, totalDelta, out float graphEdge, out float graphHeight, out float middle);

        g.FillRectangle(Settings.BehindGraphColor, 0, 0, width, middle);
        g.FillRectangle(Settings.AheadGraphColor, 0, middle, width, (graphHeight * 2) - middle);

        CalculateGridlines(state, width, totalDelta, graphEdge, graphHeight, out double gridValueX, out double gridValueY);
        DrawGridlines(g, width, graphHeight, middle, gridValueX, gridValueY, new Pen(Settings.GridlinesColor.ToBrush(), 2.0));

        try
        {
            DrawGraph(g, state, width, totalDelta, graphEdge, graphHeight, middle);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private void DrawGraph(DrawingContext g, LiveSplitState state, float width, TimeSpan totalDelta, float graphEdge, float graphHeight, float middle)
    {
        if (Deltas.Count == 0)
        {
            return;
        }

        var circleList = new List<Point>();
        float heightOne = graphHeight;
        if (totalDelta != TimeSpan.Zero)
        {
            heightOne = (float)(((-MaxDelta.TotalMilliseconds) / totalDelta.TotalMilliseconds
                * (graphHeight - graphEdge) * 2) + graphEdge);
        }

        float heightTwo = 0;
        float widthOne = 0;
        float widthTwo = 0;
        int y = 0;

        var pointArray = new List<Point> { new(0, middle) };
        circleList.Add(new Point(widthOne, heightOne));

        while (y < Deltas.Count)
        {
            while (Deltas[y] == null && y < Deltas.Count - 1)
            {
                y++;
            }

            if (Deltas[y] != null)
            {
                CalculateRightSideCoordinates(state, width, totalDelta, graphEdge, graphHeight, ref heightTwo, ref widthTwo, y);
                DrawFillBeneathGraph(g, totalDelta, middle, heightOne, heightTwo, widthOne, widthTwo, y, pointArray);
                circleList.Add(new Point(widthTwo, heightTwo));
                CalculateLeftSideCoordinates(state, width, totalDelta, graphEdge, graphHeight, ref heightOne, ref widthOne, y);
            }
            else
            {
                DrawFinalPolygon(g, middle, pointArray);
            }

            y++;
        }

        DrawCirclesAndLines(g, state, width, circleList);
    }

    private void DrawCirclesAndLines(DrawingContext g, LiveSplitState state, float width, List<Point> circleList)
    {
        int i = Deltas.Count - 1;
        circleList.Reverse();
        Point previousCircle = circleList[0];
        circleList.RemoveAt(0);

        foreach (Point circle in circleList)
        {
            while (Deltas[i] == null)
            {
                i--;
            }

            Color color = Settings.GraphColor;
            bool finalDelta = previousCircle.X == width && IsLiveDeltaActive;
            if (!finalDelta && Settings.ShowBestSegments && LiveSplitStateHelper.CheckBestSegment(state, i, state.CurrentTimingMethod))
            {
                color = Settings.GraphGoldColor;
            }

            DrawLineShadowed(g, color, previousCircle, circle, Settings.FlipGraph);

            if (!finalDelta)
            {
                DrawEllipseShadowed(g, color, previousCircle.X - 2.5, previousCircle.Y - 2.5, 5, 5, Settings.FlipGraph);
            }

            previousCircle = circle;
            i--;
        }
    }

    private void CalculateLeftSideCoordinates(LiveSplitState state, float width, TimeSpan totalDelta, float graphEdge, float graphHeight, ref float heightOne, ref float widthOne, int y)
    {
        heightOne = totalDelta == TimeSpan.Zero
            ? graphHeight
            : ((float)((Deltas[y].Value.TotalMilliseconds - MaxDelta.TotalMilliseconds) / totalDelta.TotalMilliseconds)
                * (graphHeight - graphEdge) * 2) + graphEdge;

        if (y != Deltas.Count - 1 && state.Run[y].SplitTime[state.CurrentTimingMethod] != null)
        {
            widthOne = (float)(state.Run[y].SplitTime[state.CurrentTimingMethod].Value.TotalMilliseconds / FinalSplit.Value.TotalMilliseconds * width);
        }
    }

    private void CalculateRightSideCoordinates(LiveSplitState state, float width, TimeSpan totalDelta, float graphEdge, float graphHeight, ref float heightTwo, ref float widthTwo, int y)
    {
        if (y == Deltas.Count - 1 && IsLiveDeltaActive)
        {
            widthTwo = width;
        }
        else if (state.Run[y].SplitTime[state.CurrentTimingMethod] != null)
        {
            widthTwo = (float)(state.Run[y].SplitTime[state.CurrentTimingMethod].Value.TotalMilliseconds / FinalSplit.Value.TotalMilliseconds * width);
        }

        heightTwo = totalDelta != TimeSpan.Zero
            ? (float)(((Deltas[y].Value.TotalMilliseconds - MaxDelta.TotalMilliseconds) / totalDelta.TotalMilliseconds
                * (graphHeight - graphEdge) * 2) + graphEdge)
            : graphHeight;
    }

    private void DrawFillBeneathGraph(DrawingContext g, TimeSpan totalDelta, float middle, float heightOne, float heightTwo, float widthOne, float widthTwo, int y, List<Point> pointArray)
    {
        if ((heightTwo - middle) / (heightOne - middle) > 0)
        {
            AddFillOneSide(g, middle, heightOne, heightTwo, widthOne, widthTwo, y, pointArray);
        }
        else
        {
            float ratio = (heightOne - middle) / (heightOne - heightTwo);
            if (float.IsNaN(ratio))
            {
                ratio = 0.0f;
            }

            AddFillFirstHalf(g, totalDelta, middle, heightOne, widthOne, widthTwo, y, pointArray, ratio);
            AddFillSecondHalf(g, totalDelta, middle, heightTwo, widthOne, widthTwo, y, pointArray, ratio);
        }

        if (y == Deltas.Count - 1)
        {
            DrawFinalPolygon(g, middle, pointArray);
        }
    }

    private void DrawFinalPolygon(DrawingContext g, float middle, List<Point> pointArray)
    {
        pointArray.Add(new Point(pointArray[^1].X, middle));
        if (pointArray.Count > 1)
        {
            FillPolygon(g, pointArray[^2].Y > middle ? Settings.CompleteFillColorAhead : Settings.CompleteFillColorBehind, pointArray);
        }
    }

    // Adds the second portion of the fill if the graph goes from ahead to behind or vice versa.
    private void AddFillSecondHalf(DrawingContext g, TimeSpan totalDelta, float middle, float heightTwo, float widthOne, float widthTwo, int y, List<Point> pointArray, float ratio)
    {
        if (y == Deltas.Count - 1 && IsLiveDeltaActive)
        {
            if (totalDelta != TimeSpan.Zero)
            {
                FillPolygon(g, heightTwo > middle ? Settings.PartialFillColorAhead : Settings.PartialFillColorBehind,
                [
                    new(widthOne + ((widthTwo - widthOne) * ratio), middle),
                    new(widthTwo, heightTwo),
                    new(widthTwo, middle)
                ]);
            }
        }
        else
        {
            pointArray.Clear();
            pointArray.Add(new Point(widthOne + ((widthTwo - widthOne) * ratio), middle));
            pointArray.Add(new Point(widthTwo, heightTwo));
        }
    }

    // Adds the first portion of the fill if the graph goes from ahead to behind or vice versa.
    private void AddFillFirstHalf(DrawingContext g, TimeSpan totalDelta, float middle, float heightOne, float widthOne, float widthTwo, int y, List<Point> pointArray, float ratio)
    {
        Color color = heightOne > middle ? Settings.PartialFillColorAhead : Settings.PartialFillColorBehind;
        if (y == Deltas.Count - 1 && IsLiveDeltaActive)
        {
            if (totalDelta != TimeSpan.Zero)
            {
                FillPolygon(g, color,
                [
                    new(widthOne, middle),
                    new(widthOne, heightOne),
                    new(widthOne + ((widthTwo - widthOne) * ratio), middle)
                ]);
            }
        }
        else
        {
            pointArray.Add(new Point(widthOne + ((widthTwo - widthOne) * ratio), middle));
            FillPolygon(g, heightOne > middle ? Settings.CompleteFillColorAhead : Settings.CompleteFillColorBehind, pointArray);
        }
    }

    // Adds the fill under the graph if the current portion is either completely ahead or completely behind.
    private void AddFillOneSide(DrawingContext g, float middle, float heightOne, float heightTwo, float widthOne, float widthTwo, int y, List<Point> pointArray)
    {
        if (y == Deltas.Count - 1 && IsLiveDeltaActive)
        {
            FillPolygon(g, heightTwo > middle ? Settings.PartialFillColorAhead : Settings.PartialFillColorBehind,
            [
                new(widthOne, middle),
                new(widthOne, heightOne),
                new(widthTwo, heightTwo),
                new(widthTwo, middle)
            ]);
        }
        else
        {
            pointArray.Add(new Point(widthTwo, heightTwo));
        }
    }

    private static void FillPolygon(DrawingContext g, Color color, IReadOnlyList<Point> points)
    {
        if (color.A == 0 || points.Count < 3)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(points[0], true);
            for (int i = 1; i < points.Count; i++)
            {
                context.LineTo(points[i]);
            }

            context.EndFigure(true);
        }

        g.DrawGeometry(color.ToBrush(), null, geometry);
    }

    private static void DrawGridlines(DrawingContext g, float width, float graphHeight, float middle, double gridValueX, double gridValueY, Pen pen)
    {
        if (gridValueX > 0)
        {
            for (double x = gridValueX; x < width; x += gridValueX)
            {
                g.DrawLine(pen, new Point(x, 0), new Point(x, graphHeight * 2));
            }
        }

        for (float y = middle - 1; y > 0; y -= (float)gridValueY)
        {
            g.DrawLine(pen, new Point(0, y), new Point(width, y));
            if (gridValueY < 0)
            {
                break;
            }
        }

        for (float y = middle; y < graphHeight * 2; y += (float)gridValueY)
        {
            g.DrawLine(pen, new Point(0, y), new Point(width, y));
            if (gridValueY < 0)
            {
                break;
            }
        }
    }

    private void CalculateMiddleAndGraphEdge(float height, TimeSpan totalDelta, out float graphEdge, out float graphHeight, out float middle)
    {
        graphEdge = 0;
        graphHeight = height / 2.0f;
        middle = graphHeight;
        if (totalDelta != TimeSpan.Zero)
        {
            graphEdge = (float)(GraphEdgeValue.TotalMilliseconds / (-totalDelta.TotalMilliseconds + (GraphEdgeValue.TotalMilliseconds * 2)) * ((graphHeight * 2) - (GraphEdgeMin * 2)));
            graphEdge += GraphEdgeMin;
            middle = (float)((-(MaxDelta.TotalMilliseconds / totalDelta.TotalMilliseconds) * (graphHeight - graphEdge) * 2) + graphEdge);
        }
    }

    private void CalculateGridlines(LiveSplitState state, float width, TimeSpan totalDelta, float graphEdge, float graphHeight, out double gridValueX, out double gridValueY)
    {
        if (state.CurrentPhase != TimerPhase.NotRunning && FinalSplit > TimeSpan.Zero)
        {
            gridValueX = 1000;
            while (FinalSplit.Value.TotalMilliseconds / gridValueX > width / 20)
            {
                gridValueX *= 6;
            }

            gridValueX = gridValueX / FinalSplit.Value.TotalMilliseconds * width;
        }
        else
        {
            gridValueX = -1;
        }

        if (state.CurrentPhase != TimerPhase.NotRunning && totalDelta < TimeSpan.Zero)
        {
            gridValueY = 1000;
            while ((-totalDelta.TotalMilliseconds) / gridValueY > (graphHeight - graphEdge) * 2 / 20)
            {
                gridValueY *= 6;
            }

            gridValueY = gridValueY / (-totalDelta.TotalMilliseconds) * (graphHeight - graphEdge) * 2;
        }
        else
        {
            gridValueY = -1;
        }
    }

    private void DrawLineShadowed(DrawingContext g, Color color, Point p1, Point p2, bool flipShadow)
    {
        var shadowPen = new Pen(Settings.ShadowsColor.ToBrush(), 1.75, lineCap: PenLineCap.Round);
        var pen = new Pen(color.ToBrush(), 1.75, lineCap: PenLineCap.Round);
        int direction = flipShadow ? -1 : 1;
        for (int offset = 1; offset <= 3; offset++)
        {
            var shift = new Vector(1, direction * offset);
            g.DrawLine(shadowPen, p1 + shift, p2 + shift);
        }

        g.DrawLine(pen, p1, p2);
    }

    private void DrawEllipseShadowed(DrawingContext g, Color color, double x, double y, double width, double height, bool flipShadow)
    {
        IBrush shadowBrush = Settings.ShadowsColor.ToBrush();
        int direction = flipShadow ? -1 : 1;
        for (int offset = 1; offset <= 3; offset++)
        {
            g.DrawEllipse(shadowBrush, null, new Rect(x + 1, y + (direction * offset), width, height));
        }

        g.DrawEllipse(color.ToBrush(), null, new Rect(x, y, width, height));
    }

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion)
    {
        DrawGeneral(g, state, width, VerticalHeight);
    }

    public void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion)
    {
        DrawGeneral(g, state, HorizontalWidth, height);
    }

    public string ComponentName => "Graph";

    public Control GetSettingsControl(LayoutMode mode)
    {
        return null;
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        throw new NotSupportedException();
    }

    public void SetSettings(XmlNode settings)
    {
        throw new NotSupportedException();
    }

    protected void Calculate(LiveSplitState state)
    {
        string comparison = Settings.Comparison == "Current Comparison" ? state.CurrentComparison : Settings.Comparison;
        if (!state.Run.Comparisons.Contains(comparison))
        {
            comparison = state.CurrentComparison;
        }

        CalculateFinalSplit(state);
        CalculateDeltas(state, comparison);
        CheckLiveSegmentDelta(state, comparison);
    }

    private void CalculateFinalSplit(LiveSplitState state)
    {
        FinalSplit = TimeSpan.Zero;
        if (Settings.IsLiveGraph)
        {
            if (state.CurrentPhase != TimerPhase.NotRunning)
            {
                FinalSplit = state.CurrentTime[state.CurrentTimingMethod] ?? state.CurrentTime.RealTime;
            }
        }
        else
        {
            foreach (ISegment segment in state.Run)
            {
                if (segment.SplitTime[state.CurrentTimingMethod] != null)
                {
                    FinalSplit = segment.SplitTime[state.CurrentTimingMethod];
                }
            }
        }
    }

    private void CalculateDeltas(LiveSplitState state, string comparison)
    {
        Deltas = [];
        MaxDelta = TimeSpan.Zero;
        MinDelta = TimeSpan.Zero;
        for (int x = 0; x < state.Run.Count; x++)
        {
            TimeSpan? time = state.Run[x].SplitTime[state.CurrentTimingMethod] - state.Run[x].Comparisons[comparison][state.CurrentTimingMethod];
            if (time > MaxDelta)
            {
                MaxDelta = time.Value;
            }

            if (time < MinDelta)
            {
                MinDelta = time.Value;
            }

            Deltas.Add(time);
        }
    }

    private void CheckLiveSegmentDelta(LiveSplitState state, string comparison)
    {
        IsLiveDeltaActive = false;
        if (!Settings.IsLiveGraph || state.CurrentPhase is not (TimerPhase.Running or TimerPhase.Paused))
        {
            return;
        }

        TimeSpan? bestSeg = LiveSplitStateHelper.CheckLiveDelta(state, true, comparison, state.CurrentTimingMethod);
        TimeSpan? curSplit = state.Run[state.CurrentSplitIndex].Comparisons[comparison][state.CurrentTimingMethod];
        TimeSpan? curTime = state.CurrentTime[state.CurrentTimingMethod];
        if (bestSeg == null && curSplit != null && curTime - curSplit > MinDelta)
        {
            bestSeg = curTime - curSplit;
        }

        if (bestSeg != null)
        {
            if (bestSeg > MaxDelta)
            {
                MaxDelta = bestSeg.Value;
            }

            if (bestSeg < MinDelta)
            {
                MinDelta = bestSeg.Value;
            }

            Deltas.Add(bestSeg);
            IsLiveDeltaActive = true;
        }
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        Calculate(state);

        Cache.Restart();
        Cache["FinalSplit"] = FinalSplit.ToString();
        Cache["IsLiveDeltaActive"] = IsLiveDeltaActive;
        Cache["DeltasCount"] = Deltas.Count;
        for (int ind = 0; ind < Deltas.Count; ind++)
        {
            Cache["Deltas" + ind] = Deltas[ind] == null ? "null" : Deltas[ind].ToString();
        }

        if (invalidator != null && Cache.HasChanged)
        {
            invalidator.Invalidate(0, 0, width, height);
        }
    }

    public void Dispose() { }
}

public class GraphSeparatorComponent(GraphSettings settings) : IComponent
{
    protected LineComponent Line { get; set; } = new(1, Color.White);
    protected GraphSettings Settings { get; set; } = settings;

    public bool LockToBottom { get; set; }

    public float PaddingTop => 0f;
    public float PaddingBottom => 0f;
    public float PaddingLeft => 0f;
    public float PaddingRight => 0f;

    public GraphicsCache Cache { get; set; } = new();

    public float VerticalHeight => 1f;
    public float MinimumWidth => 0f;
    public float HorizontalWidth => 1f;
    public float MinimumHeight => 0f;

    public IDictionary<string, Action> ContextMenuControls => null;

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion)
    {
        Line.LineColor = Settings.GraphLinesColor;
        float scale = (float)DrawingHelpers.CurrentScale;
        float newHeight = Math.Max((int)((1f * scale) + 0.5f), 1) / scale;
        Line.VerticalHeight = newHeight;
        using (g.PushTransform(Matrix.CreateTranslation(0, LockToBottom ? 1f - newHeight : 0)))
        {
            Line.DrawVertical(g, state, width, clipRegion);
        }
    }

    public void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion)
    {
        Line.LineColor = Settings.GraphLinesColor;
        float scale = (float)DrawingHelpers.CurrentScale;
        float newWidth = Math.Max((int)((1f * scale) + 0.5f), 1) / scale;
        Line.HorizontalWidth = newWidth;
        using (g.PushTransform(Matrix.CreateTranslation(LockToBottom ? 1f - newWidth : 0, 0)))
        {
            Line.DrawHorizontal(g, state, height, clipRegion);
        }
    }

    public string ComponentName => "Graph Separator";

    public Control GetSettingsControl(LayoutMode mode)
    {
        return null;
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        throw new NotSupportedException();
    }

    public void SetSettings(XmlNode settings)
    {
        throw new NotSupportedException();
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        Cache.Restart();
        Cache["LockToBottom"] = LockToBottom;
        if (invalidator != null && Cache.HasChanged)
        {
            invalidator.Invalidate(0, 0, width, height);
        }
    }

    public void Dispose() { }
}

public class GraphCompositeComponent : IComponent, ISettingsHashCodeProvider
{
    protected GraphSettings Settings { get; set; }
    public ComponentRendererComponent InternalComponent { get; protected set; }
    private readonly LiveSplitState state;

    public float PaddingTop => InternalComponent.PaddingTop;
    public float PaddingLeft => InternalComponent.PaddingLeft;
    public float PaddingBottom => InternalComponent.PaddingBottom;
    public float PaddingRight => InternalComponent.PaddingRight;

    public IDictionary<string, Action> ContextMenuControls => null;

    public GraphCompositeComponent(LiveSplitState state)
    {
        this.state = state;
        Settings = new GraphSettings();
        InternalComponent = new ComponentRendererComponent
        {
            VisibleComponents =
            [
                new GraphSeparatorComponent(Settings) { LockToBottom = true },
                new GraphComponent(Settings),
                new GraphSeparatorComponent(Settings) { LockToBottom = false }
            ]
        };
        state.ComparisonRenamed += (sender, e) =>
        {
            var args = (RenameEventArgs)e;
            if (Settings.Comparison == args.OldName)
            {
                Settings.Comparison = args.NewName;
                ((LiveSplitState)sender).Layout.HasChanged = true;
            }
        };
    }

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

    public string ComponentName
        => "Graph" + (Settings.Comparison == "Current Comparison"
            ? ""
            : " (" + CompositeComparisons.GetShortComparisonName(Settings.Comparison) + ")");

    public float HorizontalWidth => InternalComponent.HorizontalWidth;
    public float MinimumHeight => InternalComponent.MinimumHeight;
    public float VerticalHeight => InternalComponent.VerticalHeight;
    public float MinimumWidth => InternalComponent.MinimumWidth;

    public void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion)
    {
        InternalComponent.DrawHorizontal(g, state, height, clipRegion);
    }

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion)
    {
        InternalComponent.DrawVertical(g, state, width, clipRegion);
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        if (invalidator != null)
        {
            InternalComponent.Update(invalidator, state, width, height, mode);
        }
    }

    public void Dispose() { }

    public int GetSettingsHashCode()
    {
        return Settings.GetSettingsHashCode();
    }
}
