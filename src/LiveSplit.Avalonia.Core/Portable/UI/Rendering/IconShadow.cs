using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.IO;
using System.Runtime.InteropServices;
using DrawingColor = System.Drawing.Color;

namespace LiveSplit.UI;

/// <summary>
/// Port of LiveSplit.Core's IconShadow: a blurred silhouette of a 24x24 icon on a 30x30 canvas.
/// </summary>
public static class IconShadow
{
    public static readonly float[] Kernel = [0.398942f, 0.241971f, 0.053991f, 0.00443185f];

    public static Drawing.Image Generate(Drawing.Image image, DrawingColor shadowColor)
    {
        Bitmap source = image?.ToBitmap();
        if (source == null)
        {
            return null;
        }

        const float ShadowStrength = 0.8f;
        double alpha = 255f * (Math.Pow((shadowColor.A / 255f) - 1f, 3) + 1f);

        float[] scaledAlpha = GetScaledAlpha(source, 24);

        float Get1(int x, int y)
        {
            x -= 3;
            y -= 3;
            return x < 0 || y < 0 || x >= 24 || y >= 24 ? 0f : scaledAlpha[(y * 24) + x];
        }

        var temp = new float[30 * 30];
        for (int x = 0; x < 30; ++x)
        {
            for (int y = 0; y < 30; ++y)
            {
                float result = Kernel[0] * Get1(x, y);
                for (int i = 1; i < 4; ++i)
                {
                    result += Kernel[i] * (Get1(x - i, y) + Get1(x + i, y));
                }

                temp[(y * 30) + x] = (float)Math.Min(1.0, alpha * result / 255.0);
            }
        }

        float Get2(int x, int y)
        {
            return x < 0 || y < 0 || x >= 30 || y >= 30 ? 0f : temp[(y * 30) + x];
        }

        var pixels = new byte[30 * 30 * 4];
        for (int x = 0; x < 30; ++x)
        {
            for (int y = 0; y < 30; ++y)
            {
                float result = Kernel[0] * Get2(x, y);
                for (int i = 1; i < 4; ++i)
                {
                    result += Kernel[i] * (Get2(x, y - i) + Get2(x, y + i));
                }

                byte a = (byte)Math.Clamp((int)((ShadowStrength * alpha * result) + 0.5f), 0, 255);
                int offset = ((y * 30) + x) * 4;
                // Premultiplied BGRA.
                pixels[offset] = (byte)(shadowColor.B * a / 255);
                pixels[offset + 1] = (byte)(shadowColor.G * a / 255);
                pixels[offset + 2] = (byte)(shadowColor.R * a / 255);
                pixels[offset + 3] = a;
            }
        }

        using var bitmap = new WriteableBitmap(new PixelSize(30, 30), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (ILockedFramebuffer buffer = bitmap.Lock())
        {
            for (int row = 0; row < 30; row++)
            {
                Marshal.Copy(pixels, row * 30 * 4, buffer.Address + (row * buffer.RowBytes), 30 * 4);
            }
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return new Drawing.Image(stream.ToArray());
    }

    private static float[] GetScaledAlpha(Bitmap source, int size)
    {
        using var target = new RenderTargetBitmap(new PixelSize(size, size));
        using (Avalonia.Media.DrawingContext context = target.CreateDrawingContext())
        {
            context.DrawImage(source, new Rect(0, 0, size, size));
        }

        var buffer = new byte[size * size * 4];
        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            target.CopyPixels(new PixelRect(0, 0, size, size), handle.AddrOfPinnedObject(), buffer.Length, size * 4);
        }
        finally
        {
            handle.Free();
        }

        var result = new float[size * size];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = buffer[(i * 4) + 3] / 255f;
        }

        return result;
    }
}
