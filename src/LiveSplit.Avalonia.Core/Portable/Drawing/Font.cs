using System;

namespace LiveSplit.Drawing;

[Flags]
public enum FontStyle
{
    Regular = 0,
    Bold = 1,
    Italic = 2,
    Underline = 4,
    Strikeout = 8
}

public enum GraphicsUnit
{
    World = 0,
    Display = 1,
    Pixel = 2,
    Point = 3,
    Inch = 4,
    Document = 5,
    Millimeter = 6
}

/// <summary>
/// Platform-neutral replacement for <c>System.Drawing.Font</c>. It only describes a font;
/// the Avalonia front end resolves it to a typeface when drawing.
/// </summary>
public sealed class Font : ICloneable, IDisposable, IEquatable<Font>
{
    public string Name { get; }
    public float Size { get; }
    public FontStyle Style { get; }
    public GraphicsUnit Unit { get; }

    public FontFamilyName FontFamily => new(Name);

    public bool Bold => Style.HasFlag(FontStyle.Bold);
    public bool Italic => Style.HasFlag(FontStyle.Italic);

    public Font(string name, float size, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
    {
        Name = name;
        Size = size;
        Style = style;
        Unit = unit;
    }

    public Font(Font prototype, FontStyle style)
        : this(prototype.Name, prototype.Size, style, prototype.Unit) { }

    public Font(string name, float size, GraphicsUnit unit)
        : this(name, size, FontStyle.Regular, unit) { }

    /// <summary>
    /// The em size in device independent pixels (1/96 inch), matching how GDI+ resolves sizes.
    /// </summary>
    public float SizeInPixels => Unit switch
    {
        GraphicsUnit.Pixel or GraphicsUnit.World or GraphicsUnit.Display => Size,
        GraphicsUnit.Point => Size * 96f / 72f,
        GraphicsUnit.Inch => Size * 96f,
        GraphicsUnit.Document => Size * 96f / 300f,
        GraphicsUnit.Millimeter => Size * 96f / 25.4f,
        _ => Size
    };

    public object Clone()
    {
        return new Font(Name, Size, Style, Unit);
    }

    public void Dispose() { }

    public bool Equals(Font other)
    {
        return other is not null
            && other.Name == Name
            && other.Size == Size
            && other.Style == Style
            && other.Unit == Unit;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as Font);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Size, Style, Unit);
    }

    public override string ToString()
    {
        return $"[Font: Name={Name}, Size={Size}, Units={(int)Unit}, Style={Style}]";
    }
}

public readonly record struct FontFamilyName(string Name)
{
    public override string ToString()
    {
        return Name;
    }
}
