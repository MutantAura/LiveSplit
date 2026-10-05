using System;
using System.IO;

namespace LiveSplit.Drawing;

/// <summary>
/// Platform-neutral replacement for <c>System.Drawing.Image</c>. It stores the encoded image
/// bytes (PNG, JPEG, BMP, GIF, ...) exactly as they appear in splits and layout files and
/// decodes them lazily through Avalonia when they are drawn.
/// </summary>
public sealed class Image : IDisposable
{
    private global::Avalonia.Media.Imaging.Bitmap bitmap;
    private bool decodeFailed;

    public byte[] Data { get; }

    public Image(byte[] data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public static Image FromStream(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return new Image(ms.ToArray());
    }

    /// <summary>
    /// Returns the decoded bitmap, or null if the data cannot be decoded.
    /// </summary>
    public global::Avalonia.Media.Imaging.Bitmap ToBitmap()
    {
        if (bitmap == null && !decodeFailed)
        {
            try
            {
                using var ms = new MemoryStream(Data, false);
                bitmap = new global::Avalonia.Media.Imaging.Bitmap(ms);
            }
            catch
            {
                decodeFailed = true;
            }
        }

        return bitmap;
    }

    public int Width => ToBitmap()?.PixelSize.Width ?? 0;
    public int Height => ToBitmap()?.PixelSize.Height ?? 0;

    public void Dispose()
    {
        bitmap?.Dispose();
        bitmap = null;
    }
}
