using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AvaloniaColor = Avalonia.Media.Color;
using DrawingColor = System.Drawing.Color;
using DrawingFont = LiveSplit.Drawing.Font;

namespace LiveSplit.UI;

/// <summary>
/// Horizontal or vertical text alignment, mirroring GDI+'s StringAlignment.
/// </summary>
public enum StringAlignment
{
    Near = 0,
    Center = 1,
    Far = 2
}

/// <summary>
/// Vertical font metrics in pixels for a given font size. <see cref="Baseline"/> is the distance
/// from the top of a drawn line of text to its baseline.
/// </summary>
public readonly record struct FontPixelMetrics(double Ascent, double Descent, double LineHeight, double Baseline)
{
    /// <summary>
    /// The space between the top of the line box and the top of the ascent.
    /// </summary>
    public double TopOffset => Baseline - Ascent;
}

/// <summary>
/// Helpers that translate LiveSplit's GDI+-style drawing model to Avalonia.
/// </summary>
public static class DrawingHelpers
{
    private static readonly Dictionary<(string Name, Drawing.FontStyle Style), Typeface> typefaces = [];
    private static readonly Dictionary<uint, SolidColorBrush> brushes = [];

    /// <summary>
    /// The family used when a layout references a font that is not installed.
    /// </summary>
    public static string FallbackFontFamily { get; set; } = "fonts:Inter#Inter";

    /// <summary>
    /// The scale from layout units to device independent pixels of the component being drawn.
    /// Mirrors <c>Graphics.Transform.Elements[0]</c>, which some components use to snap thin lines.
    /// </summary>
    public static double CurrentScale { get; set; } = 1.0;

    public static AvaloniaColor ToAvalonia(this DrawingColor color)
    {
        return AvaloniaColor.FromArgb(color.A, color.R, color.G, color.B);
    }

    public static DrawingColor ToDrawing(this AvaloniaColor color)
    {
        return DrawingColor.FromArgb(color.A, color.R, color.G, color.B);
    }

    /// <summary>
    /// Returns a cached, immutable-in-practice solid brush for the color.
    /// </summary>
    public static IBrush ToBrush(this DrawingColor color)
    {
        uint key = (uint)color.ToArgb();
        if (!brushes.TryGetValue(key, out SolidColorBrush brush))
        {
            brush = new SolidColorBrush(color.ToAvalonia());
            brushes[key] = brush;
        }

        return brush;
    }

    /// <summary>
    /// Metric-compatible replacements for Windows fonts commonly used in layouts, tried in
    /// order when the original font is not installed.
    /// </summary>
    private static readonly Dictionary<string, string[]> fontAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Century Gothic"] = ["URW Gothic", "URW Gothic L", "TeX Gyre Adventor"],
        ["Calibri"] = ["Carlito"],
        ["Cambria"] = ["Caladea"],
        ["Arial"] = ["Liberation Sans", "Arimo", "Nimbus Sans", "Helvetica"],
        ["Times New Roman"] = ["Liberation Serif", "Tinos", "Nimbus Roman", "Times"],
        ["Courier New"] = ["Liberation Mono", "Cousine", "Nimbus Mono PS", "Courier"],
        ["Consolas"] = ["Inconsolata", "DejaVu Sans Mono", "Menlo"],
        ["Segoe UI"] = ["Segoe UI Variable Text"],
    };

    private static string ResolveFamilyName(string name)
    {
        if (IsInstalled(name))
        {
            return name;
        }

        if (name != null && fontAliases.TryGetValue(name, out string[] aliases))
        {
            foreach (string alias in aliases)
            {
                if (IsInstalled(alias))
                {
                    return alias;
                }
            }
        }

        return FallbackFontFamily;
    }

    public static Typeface GetTypeface(DrawingFont font)
    {
        if (!typefaces.TryGetValue((font.Name, font.Style), out Typeface typeface))
        {
            var family = new FontFamily(ResolveFamilyName(font.Name));
            typeface = new Typeface(
                family,
                font.Italic ? Avalonia.Media.FontStyle.Italic : Avalonia.Media.FontStyle.Normal,
                font.Bold ? FontWeight.Bold : FontWeight.Normal);
            typefaces[(font.Name, font.Style)] = typeface;
        }

        return typeface;
    }

    private static HashSet<string> systemFamilies;

    private static bool IsInstalled(string familyName)
    {
        if (string.IsNullOrEmpty(familyName))
        {
            return false;
        }

        try
        {
            if (!FontManager.Current.TryGetGlyphTypeface(new Typeface(familyName), out GlyphTypeface glyphTypeface))
            {
                return false;
            }

            if (string.Equals(glyphTypeface.FamilyName, familyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // The font manager silently substitutes its default family for unknown names, so a
            // different family name usually means the font is missing. But many installed fonts
            // are listed under a shorter family name than the one stored in the font (e.g.
            // "Arial Rounded MT" is "Arial Rounded MT Bold", "Segoe UI Variable Text" is
            // "Segoe UI Variable"); accept those, as long as they didn't resolve to the default.
            systemFamilies ??= new HashSet<string>(FontManager.Current.SystemFonts.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            return systemFamilies.Contains(familyName)
                && !string.Equals(glyphTypeface.FamilyName, FontManager.Current.DefaultFontFamily.Name, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static FormattedText CreateText(string text, DrawingFont font, IBrush brush)
    {
        return new FormattedText(
            text ?? string.Empty,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            GetTypeface(font),
            Math.Max(font.SizeInPixels, 0.01),
            brush);
    }

    /// <summary>
    /// The horizontal padding GDI+'s DrawString and MeasureString add on each side of a string.
    /// </summary>
    public static double GdiPadding(DrawingFont font)
    {
        return font.SizeInPixels / 6.0;
    }

    /// <summary>
    /// Equivalent of GDI+'s Graphics.MeasureString: the advance width including GDI+'s padding,
    /// and the font's line height.
    /// </summary>
    public static Size MeasureString(string text, DrawingFont font)
    {
        FormattedText formatted = CreateText(text, font, null);
        return new Size(formatted.WidthIncludingTrailingWhitespace + (2 * GdiPadding(font)), GetLineHeight(font));
    }

    /// <summary>
    /// The advance width of the text without any padding, like TextRenderer.MeasureText with NoPadding.
    /// </summary>
    public static double MeasureAdvance(string text, DrawingFont font)
    {
        return CreateText(text, font, null).WidthIncludingTrailingWhitespace;
    }

    public static double GetLineHeight(DrawingFont font)
    {
        return CreateText("A", font, null).Height;
    }

    public static FontPixelMetrics GetMetrics(DrawingFont font)
    {
        double size = font.SizeInPixels;
        FormattedText sample = CreateText("0", font, null);
        if (FontManager.Current.TryGetGlyphTypeface(GetTypeface(font), out GlyphTypeface glyphTypeface))
        {
            FontMetrics metrics = glyphTypeface.Metrics;
            double scale = size / metrics.DesignEmHeight;
            double ascent = Math.Abs(metrics.Ascent) * scale;
            double descent = Math.Abs(metrics.Descent) * scale;
            return new FontPixelMetrics(ascent, descent, sample.Height, sample.Baseline);
        }

        return new FontPixelMetrics(sample.Baseline, sample.Height - sample.Baseline, sample.Height, sample.Baseline);
    }

    public static LinearGradientBrush CreateLinearGradient(Point start, Point end, DrawingColor startColor, DrawingColor endColor)
    {
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(start, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(end, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(startColor.ToAvalonia(), 0),
                new GradientStop(endColor.ToAvalonia(), 1)
            }
        };
    }

    public static void FillRectangle(this DrawingContext g, IBrush brush, double x, double y, double width, double height)
    {
        if (width > 0 && height > 0)
        {
            g.FillRectangle(brush, new Rect(x, y, width, height));
        }
    }

    public static void FillRectangle(this DrawingContext g, DrawingColor color, double x, double y, double width, double height)
    {
        if (color.A > 0)
        {
            g.FillRectangle(color.ToBrush(), x, y, width, height);
        }
    }

    /// <summary>
    /// Fills a rectangle with a plain color or a vertical/horizontal two-color gradient.
    /// </summary>
    public static void FillGradient(this DrawingContext g, DrawingColor color1, DrawingColor color2, bool horizontal, double width, double height)
    {
        if (color1.A == 0 && color2.A == 0)
        {
            return;
        }

        IBrush brush = color1 == color2
            ? color1.ToBrush()
            : CreateLinearGradient(new Point(0, 0), horizontal ? new Point(width, 0) : new Point(0, height), color1, color2);
        g.FillRectangle(brush, 0, 0, width, height);
    }

    public static void DrawImage(this DrawingContext g, Drawing.Image image, double x, double y, double width, double height)
    {
        Avalonia.Media.Imaging.Bitmap bitmap = image?.ToBitmap();
        if (bitmap != null && width > 0 && height > 0)
        {
            g.DrawImage(bitmap, new Rect(x, y, width, height));
        }
    }
}
