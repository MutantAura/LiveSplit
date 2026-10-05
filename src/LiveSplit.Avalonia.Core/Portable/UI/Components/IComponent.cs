using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LiveSplit.Model;
using System;
using System.Collections.Generic;
using System.Xml;

namespace LiveSplit.UI.Components;

/// <summary>
/// Cross-platform counterpart of LiveSplit.Core's IComponent. The contract is the same, except
/// that components draw through Avalonia's <see cref="DrawingContext"/> instead of GDI+ and
/// expose their settings as an Avalonia <see cref="Control"/>.
/// </summary>
public interface IComponent : IDisposable
{
    /// <summary>
    /// Returns the name of the component.
    /// </summary>
    string ComponentName { get; }

    /// <summary>
    /// Returns the width of the component if it is rendered horizontally.
    /// </summary>
    float HorizontalWidth { get; }
    /// <summary>
    /// Returns the minimum height where the component still looks visually pleasing.
    /// </summary>
    float MinimumHeight { get; }

    /// <summary>
    /// Returns the height of the component if it is rendered vertically.
    /// </summary>
    float VerticalHeight { get; }
    /// <summary>
    /// Returns the minimum width where the component still looks visually pleasing.
    /// </summary>
    float MinimumWidth { get; }

    /// <summary>
    /// Returns the intrinsic padding of the component.
    /// <remarks>Padding is combined if two components with padding are next to each other.</remarks>
    /// </summary>
    float PaddingTop { get; }
    float PaddingBottom { get; }
    float PaddingLeft { get; }
    float PaddingRight { get; }

    /// <summary>
    /// Returns a Dictionary with all the controls available in the context menu for controlling the component.
    /// </summary>
    IDictionary<string, Action> ContextMenuControls { get; }

    /// <summary>
    /// Draws the contents of the component horizontally. The origin is the component's top left corner.
    /// </summary>
    void DrawHorizontal(DrawingContext g, LiveSplitState state, float height, Rect clipRegion);

    /// <summary>
    /// Draws the contents of the component vertically. The origin is the component's top left corner.
    /// </summary>
    void DrawVertical(DrawingContext g, LiveSplitState state, float width, Rect clipRegion);

    /// <summary>
    /// Returns a control the user can use to configure the component, or null if there is none.
    /// </summary>
    Control GetSettingsControl(LayoutMode mode);

    /// <summary>
    /// Returns the XML serialization of the component's settings.
    /// </summary>
    XmlNode GetSettings(XmlDocument document);

    /// <summary>
    /// Sets the settings of the component based on the serialized version of the settings.
    /// </summary>
    void SetSettings(XmlNode settings);

    /// <summary>
    /// Updates the component and invalidates the region that needs to be redrawn.
    /// </summary>
    void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode);
}

public interface IInvalidator
{
    Matrix Transform { get; set; }
    void Invalidate(float x, float y, float width, float height);
    void Restart();
}
