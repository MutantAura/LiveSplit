using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LiveSplit.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace LiveSplit.UI.Components;

/// <summary>
/// A component made of other components, laid out like the main layout (used by Splits).
/// </summary>
public class ComponentRendererComponent : ComponentRenderer, IComponent
{
    public float VerticalHeight
    {
        get
        {
            CalculateOverallSize(LayoutMode.Vertical);
            return OverallSize;
        }
    }

    public float HorizontalWidth
    {
        get
        {
            CalculateOverallSize(LayoutMode.Horizontal);
            return OverallSize;
        }
    }

    public float PaddingTop => VisibleComponents.Count > 0 ? VisibleComponents[0].PaddingTop : 0;
    public float PaddingLeft => VisibleComponents.Count > 0 ? VisibleComponents[0].PaddingLeft : 0;
    public float PaddingBottom => VisibleComponents.Count > 0 ? VisibleComponents[^1].PaddingBottom : 0;
    public float PaddingRight => VisibleComponents.Count > 0 ? VisibleComponents[^1].PaddingRight : 0;

    public void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion)
    {
        Render(g, state, width, 0, LayoutMode.Vertical, DrawingHelpers.CurrentScale);
    }

    public void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion)
    {
        Render(g, state, 0, height, LayoutMode.Horizontal, DrawingHelpers.CurrentScale);
    }

    public string ComponentName => throw new NotSupportedException();

    public Control GetSettingsControl(LayoutMode mode)
    {
        return null;
    }

    public void SetSettings(XmlNode settings) { }

    public XmlNode GetSettings(XmlDocument document)
    {
        throw new NotSupportedException();
    }

    public IDictionary<string, Action> ContextMenuControls
        => VisibleComponents
            .Select(x => x.ContextMenuControls)
            .Where(x => x != null)
            .SelectMany(x => x)
            .ToDictionary(x => x.Key, x => x.Value);

    public void Dispose()
    {
        foreach (IComponent component in VisibleComponents)
        {
            component.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
