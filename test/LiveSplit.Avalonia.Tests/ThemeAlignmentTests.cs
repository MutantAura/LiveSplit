using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LiveSplit.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace LiveSplit.Tests;

public class ThemeAlignmentTests
{
    private static byte[] Render(Window window, out int stride)
    {
        for (int i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        using WriteableBitmap frame = window.CaptureRenderedFrame();
        using var buffer = frame.Lock();
        stride = buffer.RowBytes;
        byte[] pixels = new byte[stride * buffer.Size.Height];
        Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);
        return pixels;
    }

    /// <summary>
    /// Regression test: in the Libadwaita theme, button labels sat about 3px above the center,
    /// because buttons are taller than their content and put it at the top. Every control's text
    /// must be centered like the others'.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(AppTheme.Fluent)]
    [InlineData(AppTheme.WinUI)]
    [InlineData(AppTheme.Libadwaita)]
    [InlineData(AppTheme.MacOS)]
    [InlineData(AppTheme.Classic)]
    public void TextIsVerticallyCenteredInControls(AppTheme theme)
    {
        AppTheme previousTheme = ThemeManager.Theme;
        ThemeMode previousMode = ThemeManager.Mode;
        ThemeManager.Apply(theme, ThemeMode.Light);
        try
        {
            // "Hgy" spans from ascender to descender, so its middle should be at the control's middle.
            var controls = new Dictionary<string, TemplatedControl>
            {
                ["TextBox"] = new TextBox { Text = "Text Hgy", Width = 160 },
                ["Button"] = new Button { Content = "Button Hgy" },
                ["ToggleButton"] = new ToggleButton { Content = "Toggle Hgy" },
                ["ComboBox"] = new ComboBox { ItemsSource = new[] { "Combo Hgy" }, SelectedIndex = 0, Width = 160 },
                ["CheckBox"] = new CheckBox { Content = "Check Hgy" },
                ["TabItem"] = new TabItem { Header = "Tab Hgy" }
            };

            var panel = new StackPanel { Spacing = 12, Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Left };
            foreach ((string name, TemplatedControl control) in controls)
            {
                panel.Children.Add(control is TabItem tab ? new TabControl { ItemsSource = new[] { tab } } : control);
            }

            var window = new Window { Content = panel, Width = 400, Height = 500 };
            window.Show();

            // Render with and without visible text; the difference is the text itself.
            byte[] withText = Render(window, out int stride);
            foreach (TemplatedControl control in controls.Values)
            {
                control.Foreground = Brushes.Transparent;
            }

            byte[] withoutText = Render(window, out _);

            double TextOffset(Control control)
            {
                Point origin = control.TranslatePoint(default, window)!.Value;
                int top = (int)Math.Round(origin.Y), height = (int)Math.Round(control.Bounds.Height);
                int left = (int)Math.Round(origin.X), width = (int)Math.Round(control.Bounds.Width);
                int inkTop = -1, inkBottom = -1;
                for (int y = top; y < top + height; y++)
                {
                    for (int x = left; x < left + width; x++)
                    {
                        int i = (y * stride) + (x * 4);
                        int difference = Math.Abs(withText[i] - withoutText[i]) + Math.Abs(withText[i + 1] - withoutText[i + 1]) + Math.Abs(withText[i + 2] - withoutText[i + 2]);
                        if (difference > 60)
                        {
                            inkTop = inkTop < 0 ? y : inkTop;
                            inkBottom = y;
                            break;
                        }
                    }
                }

                Assert.True(inkTop >= 0, $"No text found in {control.GetType().Name}.");
                return ((inkTop + inkBottom) / 2.0) - (top + (height / 2.0));
            }

            // Compare against the median, so a single control's rounding doesn't skew the reference.
            Dictionary<string, double> offsets = [];
            foreach ((string name, TemplatedControl control) in controls)
            {
                offsets[name] = TextOffset(control);
            }

            double[] sorted = [.. offsets.Values.Order()];
            double reference = (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
            foreach ((string name, double offset) in offsets)
            {
                Assert.True(Math.Abs(offset - reference) <= 1.5,
                    $"{theme}: the text of {name} is {offset - reference:0.#}px off compared to the other controls.");
            }

            window.Close();
        }
        finally
        {
            ThemeManager.Apply(previousTheme, previousMode);
        }
    }
}
