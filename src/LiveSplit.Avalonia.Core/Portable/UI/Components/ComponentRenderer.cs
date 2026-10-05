using Avalonia;
using Avalonia.Media;
using LiveSplit.Model;
using LiveSplit.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using DrawingFont = LiveSplit.Drawing.Font;

namespace LiveSplit.UI.Components;

/// <summary>
/// Records whether any component requested a redraw during an update pass.
/// </summary>
public class Invalidator : IInvalidator
{
    public Matrix Transform { get; set; } = Matrix.Identity;
    public bool HasInvalidated { get; private set; }

    public void Invalidate(float x, float y, float width, float height)
    {
        HasInvalidated = true;
    }

    public void Restart()
    {
        HasInvalidated = false;
        Transform = Matrix.Identity;
    }
}

/// <summary>
/// Avalonia port of LiveSplit.Core's ComponentRenderer. Lays the layout's components out in a
/// row or column, merging adjacent paddings exactly like the Windows version.
/// </summary>
public class ComponentRenderer
{
    public IList<IComponent> VisibleComponents { get; set; } = [];

    public float OverallSize = 10f;

    public float MinimumWidth
        => VisibleComponents.Count == 0 ? 0 : VisibleComponents.Max(x => x.MinimumWidth);

    public float MinimumHeight
        => VisibleComponents.Count == 0 ? 0 : VisibleComponents.Max(x => x.MinimumHeight);

    private readonly Dictionary<IComponent, FontOverrides> overrideLookup = [];

    private float GetPaddingAbove(int index)
    {
        while (index > 0)
        {
            index--;
            IComponent component = VisibleComponents[index];
            if (component.VerticalHeight != 0)
            {
                return component.PaddingBottom;
            }
        }

        return 0f;
    }

    private float GetPaddingBelow(int index)
    {
        while (index < VisibleComponents.Count - 1)
        {
            index++;
            IComponent component = VisibleComponents[index];
            if (component.VerticalHeight != 0)
            {
                return component.PaddingTop;
            }
        }

        return 0f;
    }

    private float GetPaddingToLeft(int index)
    {
        while (index > 0)
        {
            index--;
            IComponent component = VisibleComponents[index];
            if (component.HorizontalWidth != 0)
            {
                return component.PaddingLeft;
            }
        }

        return 0f;
    }

    private float GetPaddingToRight(int index)
    {
        while (index < VisibleComponents.Count - 1)
        {
            index++;
            IComponent component = VisibleComponents[index];
            if (component.HorizontalWidth != 0)
            {
                return component.PaddingRight;
            }
        }

        return 0f;
    }

    protected float GetHeightVertical(int index)
    {
        IComponent component = VisibleComponents[index];
        float bottomPadding = Math.Min(GetPaddingBelow(index), component.PaddingBottom) / 2f;
        return component.VerticalHeight - (bottomPadding * 2f);
    }

    protected float GetWidthHorizontal(int index)
    {
        IComponent component = VisibleComponents[index];
        float rightPadding = Math.Min(GetPaddingToRight(index), component.PaddingRight) / 2f;
        return component.HorizontalWidth - (rightPadding * 2f);
    }

    public void CalculateOverallSize(LayoutMode mode)
    {
        float totalSize = 0f;
        for (int index = 0; index < VisibleComponents.Count; index++)
        {
            totalSize += mode == LayoutMode.Vertical ? GetHeightVertical(index) : GetWidthHorizontal(index);
        }

        OverallSize = Math.Max(totalSize, 1f);
    }

    /// <summary>
    /// Draws all components. <paramref name="width"/> and <paramref name="height"/> are in layout
    /// units, i.e. already divided by <paramref name="scale"/>, which the caller has applied to
    /// the drawing context.
    /// </summary>
    public void Render(DrawingContext g, LiveSplitState state, float width, float height, LayoutMode mode, double scale)
    {
        var crashedComponents = new List<IComponent>();
        Dictionary<IComponent, FontOverrides> overrides = BuildOverrideLookup(state);
        var clipRegion = new Rect(0, 0, width, height);
        float offset = 0f;

        DrawingHelpers.CurrentScale = scale;

        for (int index = 0; index < VisibleComponents.Count; index++)
        {
            IComponent component = VisibleComponents[index];
            ApplyFontOverrides(overrides, component, state.LayoutSettings, out DrawingFont origTimer, out DrawingFont origTimes, out DrawingFont origText);
            try
            {
                if (mode == LayoutMode.Vertical)
                {
                    float topPadding = Math.Min(GetPaddingAbove(index), component.PaddingTop) / 2f;
                    float bottomPadding = Math.Min(GetPaddingBelow(index), component.PaddingBottom) / 2f;

                    using (g.PushTransform(Matrix.CreateTranslation(0, offset)))
                    using (g.PushClip(new Rect(0, topPadding, width, Math.Max(0, component.VerticalHeight - topPadding - bottomPadding))))
                    {
                        component.DrawVertical(g, state, width, clipRegion);
                    }

                    offset += component.VerticalHeight - (bottomPadding * 2f);
                }
                else
                {
                    float leftPadding = Math.Min(GetPaddingToLeft(index), component.PaddingLeft) / 2f;
                    float rightPadding = Math.Min(GetPaddingToRight(index), component.PaddingRight) / 2f;

                    using (g.PushTransform(Matrix.CreateTranslation(offset, 0)))
                    using (g.PushClip(new Rect(leftPadding, 0, Math.Max(0, component.HorizontalWidth - leftPadding - rightPadding), height)))
                    {
                        component.DrawHorizontal(g, state, height, clipRegion);
                    }

                    offset += component.HorizontalWidth - (rightPadding * 2f);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                crashedComponents.Add(component);
            }
            finally
            {
                FontOverrides.Restore(state.LayoutSettings, origTimer, origTimes, origText);
            }
        }

        if (crashedComponents.Count > 0)
        {
            VisibleComponents = [.. VisibleComponents.Except(crashedComponents)];
            state.Layout.LayoutComponents = [.. state.Layout.LayoutComponents.Where(y => !crashedComponents.Contains(y.Component))];
        }
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        float scaleFactor = mode == LayoutMode.Vertical
            ? height / OverallSize
            : width / OverallSize;
        Dictionary<IComponent, FontOverrides> overrides = BuildOverrideLookup(state);

        for (int index = 0; index < VisibleComponents.Count; index++)
        {
            IComponent component = VisibleComponents[index];
            ApplyFontOverrides(overrides, component, state.LayoutSettings, out DrawingFont origTimer, out DrawingFont origTimes, out DrawingFont origText);
            try
            {
                if (mode == LayoutMode.Vertical)
                {
                    float topPadding = Math.Min(GetPaddingAbove(index), component.PaddingTop) / 2f;
                    float bottomPadding = Math.Min(GetPaddingBelow(index), component.PaddingBottom) / 2f;
                    float totalHeight = scaleFactor * (component.VerticalHeight - topPadding - bottomPadding);
                    component.Update(invalidator, state, width, totalHeight, LayoutMode.Vertical);
                }
                else
                {
                    float leftPadding = Math.Min(GetPaddingToLeft(index), component.PaddingLeft) / 2f;
                    float rightPadding = Math.Min(GetPaddingToRight(index), component.PaddingRight) / 2f;
                    float totalWidth = scaleFactor * (component.HorizontalWidth - leftPadding - rightPadding);
                    component.Update(invalidator, state, totalWidth, height, LayoutMode.Horizontal);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                invalidator?.Invalidate(0, 0, width, height);
            }
            finally
            {
                FontOverrides.Restore(state.LayoutSettings, origTimer, origTimes, origText);
            }
        }
    }

    private Dictionary<IComponent, FontOverrides> BuildOverrideLookup(LiveSplitState state)
    {
        overrideLookup.Clear();
        foreach (ILayoutComponent layoutComponent in state.Layout.LayoutComponents)
        {
            if (layoutComponent is LayoutComponent componentWithOverrides
                && componentWithOverrides.FontOverrides.HasOverrides)
            {
                overrideLookup[componentWithOverrides.Component] = componentWithOverrides.FontOverrides;
            }
        }

        return overrideLookup;
    }

    private static void ApplyFontOverrides(Dictionary<IComponent, FontOverrides> lookup, IComponent component, LayoutSettings settings, out DrawingFont origTimer, out DrawingFont origTimes, out DrawingFont origText)
    {
        if (lookup.TryGetValue(component, out FontOverrides overrides))
        {
            overrides.ApplyTo(settings, out origTimer, out origTimes, out origText);
        }
        else
        {
            origTimer = settings.TimerFont;
            origTimes = settings.TimesFont;
            origText = settings.TextFont;
        }
    }
}
