using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LiveSplit.Model;
using System;
using System.Collections.Generic;
using System.Xml;
using DrawingColor = System.Drawing.Color;

namespace LiveSplit.UI.Components;

public class LineComponent : IComponent
{
    public float PaddingTop => 0f;
    public float PaddingLeft => 0f;
    public float PaddingBottom => 0f;
    public float PaddingRight => 0f;

    public float VerticalHeight { get; set; }
    public float HorizontalWidth { get; set; }
    public DrawingColor LineColor { get; set; }

    public LineComponent(int size, DrawingColor lineColor)
    {
        VerticalHeight = size;
        HorizontalWidth = size;
        LineColor = lineColor;
    }

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion)
    {
        g.FillRectangle(LineColor, 0, 0, width, VerticalHeight);
    }

    public void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion)
    {
        g.FillRectangle(LineColor, 0, 0, HorizontalWidth, height);
    }

    public string ComponentName => throw new NotSupportedException();
    public float MinimumWidth => 0f;
    public float MinimumHeight => 0f;
    public IDictionary<string, Action> ContextMenuControls => null;

    public Control GetSettingsControl(LayoutMode mode)
    {
        return null;
    }

    public void SetSettings(XmlNode settings) { }

    public XmlNode GetSettings(XmlDocument document)
    {
        throw new NotSupportedException();
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        invalidator?.Invalidate(0, 0, width, height);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

public class SeparatorComponent : IComponent, ISettingsHashCodeProvider
{
    public float PaddingTop => 0;
    public float PaddingLeft => 0;
    public float PaddingBottom => 0;
    public float PaddingRight => 0;

    public float DisplayedSize { get; set; }
    public bool UseSeparatorColor { get; set; }
    public bool LockToBottom { get; set; }

    protected LineComponent Line { get; set; }

    public GraphicsCache Cache { get; set; }

    public float VerticalHeight => 2f;
    public float MinimumWidth => 0;
    public float HorizontalWidth => 2f;
    public float MinimumHeight => 0;

    public SeparatorComponent()
    {
        Line = new LineComponent(2, DrawingColor.White);
        DisplayedSize = 2f;
        UseSeparatorColor = true;
        LockToBottom = false;
        Cache = new GraphicsCache();
    }

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion)
    {
        if (DisplayedSize <= 0)
        {
            return;
        }

        Line.LineColor = UseSeparatorColor ? state.LayoutSettings.SeparatorsColor : state.LayoutSettings.ThinSeparatorsColor;
        float scale = (float)DrawingHelpers.CurrentScale;
        float newHeight = Math.Max((int)((DisplayedSize * scale) + 0.5f), 1) / scale;
        Line.VerticalHeight = newHeight;

        float offset = LockToBottom ? 2f - newHeight : DisplayedSize > 1 ? (2f - newHeight) / 2f : 0;
        using (g.PushTransform(Matrix.CreateTranslation(0, offset)))
        {
            Line.DrawVertical(g, state, width, clipRegion);
        }
    }

    public void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion)
    {
        if (DisplayedSize <= 0)
        {
            return;
        }

        Line.LineColor = UseSeparatorColor ? state.LayoutSettings.SeparatorsColor : state.LayoutSettings.ThinSeparatorsColor;
        float scale = (float)DrawingHelpers.CurrentScale;
        float newWidth = Math.Max((int)((DisplayedSize * scale) + 0.5f), 1) / scale;
        Line.HorizontalWidth = newWidth;

        float offset = LockToBottom ? 2f - newWidth : DisplayedSize > 1 ? (2f - newWidth) / 2f : 0;
        using (g.PushTransform(Matrix.CreateTranslation(offset, 0)))
        {
            Line.DrawHorizontal(g, state, height, clipRegion);
        }
    }

    public string ComponentName
        => "----------------------------------------------------------------------------";

    public Control GetSettingsControl(LayoutMode mode)
    {
        return null;
    }

    public void SetSettings(XmlNode settings) { }

    public XmlNode GetSettings(XmlDocument document)
    {
        return document.CreateElement("SeparatorSettings");
    }

    public IDictionary<string, Action> ContextMenuControls => null;

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        Cache.Restart();
        Cache["DisplayedSize"] = DisplayedSize;
        Cache["UseSeparatorColor"] = UseSeparatorColor;
        Cache["LockToBottom"] = LockToBottom;

        if (invalidator != null && Cache.HasChanged)
        {
            invalidator.Invalidate(0, 0, width, height);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public int GetSettingsHashCode()
    {
        return 1;
    }
}

public class ThinSeparatorComponent : IComponent
{
    public float PaddingTop => 0f;
    public float PaddingLeft => 0f;
    public float PaddingBottom => 0f;
    public float PaddingRight => 0f;

    public bool LockToBottom { get; set; }

    public GraphicsCache Cache { get; set; }

    protected LineComponent Line { get; set; }

    public float VerticalHeight => 1f;
    public float MinimumWidth => 0f;
    public float HorizontalWidth => 1f;
    public float MinimumHeight => 0f;

    public ThinSeparatorComponent()
    {
        Line = new LineComponent(1, DrawingColor.White);
        Cache = new GraphicsCache();
    }

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion)
    {
        Line.LineColor = state.LayoutSettings.ThinSeparatorsColor;
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
        Line.LineColor = state.LayoutSettings.ThinSeparatorsColor;
        float scale = (float)DrawingHelpers.CurrentScale;
        float newWidth = Math.Max((int)((1f * scale) + 0.5f), 1) / scale;
        Line.HorizontalWidth = newWidth;

        using (g.PushTransform(Matrix.CreateTranslation(LockToBottom ? 1f - newWidth : 0, 0)))
        {
            Line.DrawHorizontal(g, state, height, clipRegion);
        }
    }

    public string ComponentName => "Thin Separator";

    public Control GetSettingsControl(LayoutMode mode)
    {
        return null;
    }

    public void SetSettings(XmlNode settings) { }

    public XmlNode GetSettings(XmlDocument document)
    {
        throw new NotSupportedException();
    }

    public IDictionary<string, Action> ContextMenuControls => null;

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        Cache.Restart();
        Cache["LockToBottom"] = LockToBottom;

        if (invalidator != null && Cache.HasChanged)
        {
            invalidator.Invalidate(0, 0, width, height);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Base class for components that do not draw anything (auto splitters, sounds, ...).
/// </summary>
public abstract class LogicComponent : IComponent
{
    public abstract string ComponentName { get; }

    public float HorizontalWidth => 0;
    public float MinimumHeight => 0;
    public float VerticalHeight => 0;
    public float MinimumWidth => 0;
    public float PaddingTop => 0;
    public float PaddingBottom => 0;
    public float PaddingLeft => 0;
    public float PaddingRight => 0;

    public IDictionary<string, Action> ContextMenuControls { get; protected set; }

    public void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion) { }

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion) { }

    public abstract Control GetSettingsControl(LayoutMode mode);
    public abstract XmlNode GetSettings(XmlDocument document);
    public abstract void SetSettings(XmlNode settings);
    public abstract void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode);
    public abstract void Dispose();
}
