using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using DrawingColor = System.Drawing.Color;
using DrawingFont = LiveSplit.Drawing.Font;

namespace LiveSplit.UI;

/// <summary>
/// Avalonia port of LiveSplit.Core's SimpleLabel: a single line of text inside a rectangle with
/// alignment, ellipsis trimming, alternate (shorter) texts, drop shadow, outline and
/// monospaced digits.
/// </summary>
public class SimpleLabel
{
    public string Text { get; set; }
    public ICollection<string> AlternateText { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public DrawingFont Font { get; set; }
    public IBrush Brush { get; set; }
    public StringAlignment HorizontalAlignment { get; set; }
    public StringAlignment VerticalAlignment { get; set; }
    public DrawingColor ShadowColor { get; set; }
    public DrawingColor OutlineColor { get; set; }

    public bool HasShadow { get; set; }
    public bool IsMonospaced { get; set; }

    public float ActualWidth { get; set; }

    public DrawingColor ForeColor
    {
        get => Brush is ISolidColorBrush solid ? solid.Color.ToDrawing() : DrawingColor.Black;
        set => Brush = value.ToBrush();
    }

    public SimpleLabel(
        string text = "",
        float x = 0.0f, float y = 0.0f,
        DrawingFont font = null, IBrush brush = null,
        float width = float.MaxValue, float height = float.MaxValue,
        StringAlignment horizontalAlignment = StringAlignment.Near,
        StringAlignment verticalAlignment = StringAlignment.Near,
        IEnumerable<string> alternateText = null)
    {
        Text = text;
        X = x;
        Y = y;
        Font = font ?? new DrawingFont("Arial", 1.0f);
        Brush = brush ?? Brushes.Black;
        Width = width;
        Height = height;
        HorizontalAlignment = horizontalAlignment;
        VerticalAlignment = verticalAlignment;
        IsMonospaced = false;
        HasShadow = true;
        ShadowColor = DrawingColor.FromArgb(128, 0, 0, 0);
        OutlineColor = DrawingColor.FromArgb(0, 0, 0, 0);
        AlternateText = [.. alternateText ?? []];
    }

    public void Draw(DrawingContext g)
    {
        if (!IsMonospaced)
        {
            string actualText = CalculateAlternateText(Width);
            DrawText(g, actualText, X, Y, Width, Height, HorizontalAlignment, true);
        }
        else
        {
            double measurement = MeasureCharacterActualWidth("0");
            SetActualWidth();
            string cutOffText = CutOff();

            double offset = HorizontalAlignment == StringAlignment.Far
                ? Width - MeasureActualWidth(cutOffText)
                : 0;

            foreach (char curChar in cutOffText)
            {
                double curOffset = char.IsDigit(curChar)
                    ? measurement
                    : MeasureCharacterActualWidth(curChar.ToString());

                DrawText(g, curChar.ToString(), X + offset - (curOffset / 2f), Y, curOffset * 2f, Height, StringAlignment.Center, false);

                offset += curOffset;
            }
        }
    }

    private void DrawText(DrawingContext g, string text, double x, double y, double width, double height, StringAlignment alignment, bool trim)
    {
        if (string.IsNullOrEmpty(text) || Brush == null)
        {
            return;
        }

        double padding = DrawingHelpers.GdiPadding(Font);
        FormattedText formatted = DrawingHelpers.CreateText(text, Font, Brush);
        double available = width - (2 * padding);
        if (trim && available > 0 && formatted.WidthIncludingTrailingWhitespace > available)
        {
            formatted.MaxTextWidth = available;
            formatted.MaxLineCount = 1;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
        }

        double textWidth = formatted.WidthIncludingTrailingWhitespace;
        double textX = alignment switch
        {
            StringAlignment.Center => x + ((width - textWidth) / 2),
            StringAlignment.Far => x + width - padding - textWidth,
            _ => x + padding
        };

        double textY = VerticalAlignment switch
        {
            StringAlignment.Center => y + ((height - formatted.Height) / 2),
            StringAlignment.Far => y + height - formatted.Height,
            _ => y
        };

        if (OutlineColor.A > 0)
        {
            Geometry geometry = formatted.BuildGeometry(new Point(textX, textY));
            if (geometry == null)
            {
                return;
            }

            if (HasShadow && ShadowColor.A > 0)
            {
                IBrush shadowBrush = ShadowColor.ToBrush();
                using (g.PushTransform(Matrix.CreateTranslation(1, 1)))
                {
                    g.DrawGeometry(shadowBrush, null, geometry);
                }

                using (g.PushTransform(Matrix.CreateTranslation(2, 2)))
                {
                    g.DrawGeometry(shadowBrush, null, geometry);
                }
            }

            var outline = new Pen(OutlineColor.ToBrush(), GetOutlineSize(Font.SizeInPixels), lineJoin: PenLineJoin.Round);
            g.DrawGeometry(null, outline, geometry);
            g.DrawGeometry(Brush, null, geometry);
        }
        else
        {
            if (HasShadow && ShadowColor.A > 0)
            {
                FormattedText shadow = DrawingHelpers.CreateText(text, Font, ShadowColor.ToBrush());
                shadow.MaxTextWidth = formatted.MaxTextWidth;
                shadow.MaxLineCount = formatted.MaxLineCount;
                shadow.Trimming = formatted.Trimming;
                g.DrawText(shadow, new Point(textX + 1, textY + 1));
                g.DrawText(shadow, new Point(textX + 2, textY + 2));
            }

            g.DrawText(formatted, new Point(textX, textY));
        }
    }

    private static double GetOutlineSize(double fontSize)
    {
        return 2.1 + (fontSize * 0.055);
    }

    public void SetActualWidth()
    {
        ActualWidth = !IsMonospaced
            ? (float)DrawingHelpers.MeasureString(Text, Font).Width
            : (float)MeasureActualWidth(Text);
    }

    public string CalculateAlternateText(float width)
    {
        string actualText = Text;
        ActualWidth = (float)DrawingHelpers.MeasureString(Text, Font).Width;
        foreach (string curText in AlternateText.OrderByDescending(x => x.Length))
        {
            if (width < ActualWidth)
            {
                actualText = curText;
                ActualWidth = (float)DrawingHelpers.MeasureString(actualText, Font).Width;
            }
            else
            {
                break;
            }
        }

        return actualText;
    }

    private double MeasureActualWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        double measurement = MeasureCharacterActualWidth("0");
        double offset = 0;

        foreach (char curChar in text)
        {
            offset += char.IsDigit(curChar)
                ? measurement
                : MeasureCharacterActualWidth(curChar.ToString());
        }

        return offset;
    }

    private double MeasureCharacterActualWidth(string text)
    {
        return Math.Round(DrawingHelpers.MeasureAdvance(text, Font));
    }

    private string CutOff()
    {
        if (ActualWidth < Width)
        {
            return Text ?? string.Empty;
        }

        string cutOffText = Text;
        while (ActualWidth >= Width && !string.IsNullOrEmpty(cutOffText))
        {
            cutOffText = cutOffText[..^1];
            ActualWidth = (float)MeasureActualWidth(cutOffText + "...");
        }

        if (ActualWidth >= Width)
        {
            return "";
        }

        return cutOffText + "...";
    }
}
