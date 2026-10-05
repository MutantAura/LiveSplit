using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Linq;
using System.Threading.Tasks;
using DrawingFont = LiveSplit.Drawing.Font;
using DrawingFontStyle = LiveSplit.Drawing.FontStyle;
using DrawingGraphicsUnit = LiveSplit.Drawing.GraphicsUnit;

namespace LiveSplit.UI;

public enum DialogResult
{
    None,
    OK,
    Cancel,
    Yes,
    No
}

public enum MessageBoxButtons
{
    OK,
    YesNo,
    YesNoCancel,
    OKCancel
}

/// <summary>
/// Cross-platform replacement for WinForms' MessageBox.
/// </summary>
public static class MessageBox
{
    public static Task<DialogResult> Show(Window owner, string text, string title, MessageBoxButtons buttons = MessageBoxButtons.OK)
    {
        var window = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 320,
            MaxWidth = 560,
            CanResize = false,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            ShowInTaskbar = owner == null
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        DialogResult[] results = buttons switch
        {
            MessageBoxButtons.YesNo => [DialogResult.Yes, DialogResult.No],
            MessageBoxButtons.YesNoCancel => [DialogResult.Yes, DialogResult.No, DialogResult.Cancel],
            MessageBoxButtons.OKCancel => [DialogResult.OK, DialogResult.Cancel],
            _ => [DialogResult.OK]
        };

        foreach (DialogResult result in results)
        {
            var button = new Button
            {
                Content = result.ToString(),
                MinWidth = 80,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                IsDefault = result is DialogResult.Yes or DialogResult.OK,
                IsCancel = result == results[^1] && results.Length > 1
            };
            button.Click += (s, e) => window.Close(result);
            buttonPanel.Children.Add(button);
        }

        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
                buttonPanel
            }
        };

        DialogResult cancelResult = results.Contains(DialogResult.Cancel) ? DialogResult.Cancel
            : results.Contains(DialogResult.No) ? DialogResult.No
            : DialogResult.OK;

        return owner != null
            ? ShowWithDefault(window, owner, cancelResult)
            : ShowStandalone(window, cancelResult);
    }

    private static async Task<DialogResult> ShowWithDefault(Window window, Window owner, DialogResult fallback)
    {
        DialogResult? result = await window.ShowDialog<DialogResult?>(owner);
        return result ?? fallback;
    }

    private static Task<DialogResult> ShowStandalone(Window window, DialogResult fallback)
    {
        var completion = new TaskCompletionSource<DialogResult>();
        DialogResult? result = null;
        window.Closing += (s, e) => result ??= fallback;
        window.Closed += (s, e) => completion.TrySetResult(result ?? fallback);
        window.Show();
        return completion.Task;
    }
}

/// <summary>
/// A simple font picker: family, size, bold and italic.
/// </summary>
public static class FontDialog
{
    public static async Task<DrawingFont> Show(Window owner, DrawingFont initial)
    {
        initial ??= new DrawingFont("Segoe UI", 16, DrawingFontStyle.Regular, DrawingGraphicsUnit.Pixel);

        var families = FontManager.Current.SystemFonts.Select(x => x.Name).Distinct().OrderBy(x => x).ToList();
        if (!families.Contains(initial.Name))
        {
            families.Insert(0, initial.Name);
        }

        var familyBox = new AutoCompleteBox
        {
            ItemsSource = families,
            Text = initial.Name,
            FilterMode = AutoCompleteFilterMode.ContainsOrdinal,
            MinWidth = 240
        };
        var familyList = new ListBox { ItemsSource = families, SelectedItem = initial.Name, Height = 220 };
        familyList.SelectionChanged += (s, e) =>
        {
            if (familyList.SelectedItem is string name)
            {
                familyBox.Text = name;
            }
        };

        var sizeBox = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 400,
            Increment = 1,
            Value = (decimal)initial.Size,
            FormatString = "0.##"
        };
        var unitBox = new ComboBox
        {
            ItemsSource = new[] { "px", "pt" },
            SelectedIndex = initial.Unit == DrawingGraphicsUnit.Point ? 1 : 0
        };
        var boldBox = new CheckBox { Content = "Bold", IsChecked = initial.Bold };
        var italicBox = new CheckBox { Content = "Italic", IsChecked = initial.Italic };
        var preview = new TextBlock { Text = "AaBbYyZz 0123456789", FontSize = 24, Margin = new Thickness(0, 8) };

        void UpdatePreview()
        {
            preview.FontFamily = new FontFamily(familyBox.Text ?? initial.Name);
            preview.FontWeight = boldBox.IsChecked == true ? FontWeight.Bold : FontWeight.Normal;
            preview.FontStyle = italicBox.IsChecked == true ? FontStyle.Italic : FontStyle.Normal;
        }

        familyBox.TextChanged += (s, e) => UpdatePreview();
        boldBox.IsCheckedChanged += (s, e) => UpdatePreview();
        italicBox.IsCheckedChanged += (s, e) => UpdatePreview();
        UpdatePreview();

        var window = new Window
        {
            Title = "Font",
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80 };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
        ok.Click += (s, e) =>
        {
            DrawingFontStyle style = (boldBox.IsChecked == true ? DrawingFontStyle.Bold : 0)
                | (italicBox.IsChecked == true ? DrawingFontStyle.Italic : 0);
            window.Close(new DrawingFont(
                string.IsNullOrWhiteSpace(familyBox.Text) ? initial.Name : familyBox.Text.Trim(),
                (float)(sizeBox.Value ?? (decimal)initial.Size),
                style,
                unitBox.SelectedIndex == 1 ? DrawingGraphicsUnit.Point : DrawingGraphicsUnit.Pixel));
        };
        cancel.Click += (s, e) => window.Close(null);

        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 8,
            Children =
            {
                familyBox,
                familyList,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { new TextBlock { Text = "Size", VerticalAlignment = VerticalAlignment.Center }, sizeBox, unitBox, boldBox, italicBox }
                },
                preview,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { ok, cancel }
                }
            }
        };

        return await window.ShowDialog<DrawingFont>(owner);
    }
}

/// <summary>
/// Wraps a settings control in a window with OK and Cancel buttons.
/// </summary>
public static class SettingsDialog
{
    public static Task<bool> Show(Window owner, string title, Control content, double width = 460, double height = 560)
    {
        var window = new Window
        {
            Title = title,
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80 };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
        ok.Click += (s, e) => window.Close(true);
        cancel.Click += (s, e) => window.Close(false);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(8)
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var dock = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        dock.Children.Add(buttons);
        dock.Children.Add(content);
        window.Content = dock;

        return window.ShowDialog<bool>(owner);
    }
}
