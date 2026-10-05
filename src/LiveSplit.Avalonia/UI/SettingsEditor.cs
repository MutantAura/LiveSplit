using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DrawingColor = System.Drawing.Color;
using DrawingFont = LiveSplit.Drawing.Font;
using DrawingImage = LiveSplit.Drawing.Image;

namespace LiveSplit.UI.Components;

/// <summary>
/// Generates a settings form for a settings object by reflecting over its public read/write
/// properties. This replaces the hand-designed WinForms settings controls of each component.
/// </summary>
public static partial class SettingsEditor
{
    // The property types are known to the trimmer: ComponentSettings is annotated so that the
    // public properties of every subclass are kept, and other settings types are passed as T.

    public static Control Create(ComponentSettings settings, LiveSplitState state = null)
    {
        return new ScrollViewer { Content = CreateComponentForm(settings, state, settings.OnSettingChanged) };
    }

    private static Grid CreateComponentForm(ComponentSettings settings, LiveSplitState state, Action<string> onChanged)
    {
        return CreateForm(settings, settings.GetType(), state, onChanged);
    }

    public static Control Create<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T settings, LiveSplitState state, Action<string> onChanged)
    {
        return new ScrollViewer { Content = CreateForm(settings, state, onChanged) };
    }

    /// <summary>
    /// Builds the settings form without a scroll viewer, for embedding in a larger editor.
    /// </summary>
    public static Grid CreateForm<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T settings, LiveSplitState state, Action<string> onChanged)
    {
        return settings is ComponentSettings componentSettings
            ? CreateComponentForm(componentSettings, state, onChanged)
            : CreateForm(settings, typeof(T), state, onChanged);
    }

    private static Grid CreateForm(
        object settings,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type settingsType,
        LiveSplitState state,
        Action<string> onChanged)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(8),
            RowSpacing = 6,
            ColumnSpacing = 12
        };

        int row = 0;
        foreach (PropertyInfo property in settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite || property.GetSetMethod() == null
                || property.GetCustomAttribute<HiddenAttribute>() != null
                || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            SettingAttribute attribute = property.GetCustomAttribute<SettingAttribute>();
            Control editor = CreateEditor(settings, property, attribute, state, () => onChanged?.Invoke(property.Name));
            if (editor == null)
            {
                continue;
            }

            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var label = new TextBlock
            {
                Text = attribute?.Label ?? Humanize(property.Name),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(label, row);
            grid.Children.Add(label);

            editor.HorizontalAlignment = editor is CheckBox ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
            Grid.SetRow(editor, row);
            Grid.SetColumn(editor, 1);
            grid.Children.Add(editor);

            row++;
        }

        return grid;
    }

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex WordBoundary();

    public static string Humanize(string name)
    {
        return WordBoundary().Replace(name, " ").Replace(" 2", "2");
    }

    private static Control CreateEditor(object settings, PropertyInfo property, SettingAttribute attribute, LiveSplitState state, Action changed)
    {
        Type type = property.PropertyType;
        object value = property.GetValue(settings);

        void Set(object newValue)
        {
            property.SetValue(settings, newValue);
            changed();
        }

        if (type == typeof(bool))
        {
            var checkBox = new CheckBox { IsChecked = (bool)value };
            checkBox.IsCheckedChanged += (s, e) => Set(checkBox.IsChecked == true);
            return checkBox;
        }

        if (type.IsEnum)
        {
            // GetValuesAsUnderlyingType + ToObject avoids creating enum arrays at runtime (Native AOT).
            object[] values = [.. Enum.GetValuesAsUnderlyingType(type).Cast<object>().Select(x => Enum.ToObject(type, x))];
            var comboBox = new ComboBox
            {
                ItemsSource = values.Select(x => Humanize(x.ToString())).ToList(),
                SelectedIndex = Array.IndexOf(values, value)
            };
            comboBox.SelectionChanged += (s, e) =>
            {
                if (comboBox.SelectedIndex >= 0)
                {
                    Set(values[comboBox.SelectedIndex]);
                }
            };
            return comboBox;
        }

        if (type == typeof(int) || type == typeof(float) || type == typeof(double))
        {
            var numeric = new NumericUpDown
            {
                Value = Convert.ToDecimal(value, CultureInfo.InvariantCulture),
                Minimum = double.IsNaN(attribute?.Minimum ?? double.NaN) ? (type == typeof(int) ? int.MinValue : -1_000_000) : (decimal)attribute.Minimum,
                Maximum = double.IsNaN(attribute?.Maximum ?? double.NaN) ? (type == typeof(int) ? int.MaxValue : 1_000_000) : (decimal)attribute.Maximum,
                Increment = double.IsNaN(attribute?.Increment ?? double.NaN) ? (type == typeof(int) ? 1 : 0.5m) : (decimal)attribute.Increment,
                FormatString = type == typeof(int) ? "0" : "0.##",
                MinWidth = 120
            };
            numeric.ValueChanged += (s, e) =>
            {
                if (numeric.Value is decimal number)
                {
                    Set(Convert.ChangeType(number, type, CultureInfo.InvariantCulture));
                }
            };
            return numeric;
        }

        if (type == typeof(string))
        {
            if (attribute?.Options is { Length: > 0 } options)
            {
                var items = new List<string>();
                foreach (string option in options)
                {
                    if (option == "{Comparisons}")
                    {
                        items.Add("Current Comparison");
                        if (state?.Run != null)
                        {
                            items.AddRange(state.Run.Comparisons.Where(x => x != NoneComparisonGenerator.ComparisonName));
                        }
                    }
                    else
                    {
                        items.Add(option);
                    }
                }

                if (value is string current && !items.Contains(current))
                {
                    items.Add(current);
                }

                var comboBox = new ComboBox
                {
                    ItemsSource = items,
                    SelectedItem = value
                };
                comboBox.SelectionChanged += (s, e) =>
                {
                    if (comboBox.SelectedItem is string selected)
                    {
                        Set(selected);
                    }
                };
                return comboBox;
            }

            var textBox = new TextBox { Text = (string)value ?? "" };
            textBox.TextChanged += (s, e) => Set(textBox.Text ?? "");
            return textBox;
        }

        if (type == typeof(DrawingColor))
        {
            return CreateColorEditor((DrawingColor)value, color => Set(color));
        }

        if (type == typeof(DrawingFont))
        {
            return CreateFontEditor((DrawingFont)value, font => Set(font));
        }

        if (type == typeof(DrawingImage))
        {
            return CreateImageEditor((DrawingImage)value, image => Set(image));
        }

        return null;
    }

    public static Control CreateColorEditor(DrawingColor value, Action<DrawingColor> set)
    {
        var picker = new ColorPicker
        {
            Color = value.ToAvalonia(),
            IsAlphaEnabled = true,
            IsAlphaVisible = true,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        picker.ColorChanged += (s, e) => set(e.NewColor.ToDrawing());
        return picker;
    }

    public static Control CreateFontEditor(DrawingFont value, Action<DrawingFont> set)
    {
        var button = new Button
        {
            Content = value != null ? Describe(value) : "(default)",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        button.Click += async (s, e) =>
        {
            if (TopLevel.GetTopLevel(button) is Window owner)
            {
                DrawingFont font = await FontDialog.Show(owner, value);
                if (font != null)
                {
                    value = font;
                    button.Content = Describe(font);
                    set(font);
                }
            }
        };
        return button;
    }

    public static Control CreateImageEditor(DrawingImage value, Action<DrawingImage> set)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var choose = new Button { Content = value != null ? "Change Image..." : "Choose Image..." };
        var clear = new Button { Content = "Remove", IsEnabled = value != null };
        choose.Click += async (s, e) =>
        {
            TopLevel topLevel = TopLevel.GetTopLevel(choose);
            if (topLevel == null)
            {
                return;
            }

            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose Image",
                FileTypeFilter = [FilePickerFileTypes.ImageAll]
            });

            if (files.Count > 0)
            {
                await using System.IO.Stream stream = await files[0].OpenReadAsync();
                DrawingImage image = DrawingImage.FromStream(stream);
                if (image.ToBitmap() != null)
                {
                    choose.Content = "Change Image...";
                    clear.IsEnabled = true;
                    set(image);
                }
            }
        };
        clear.Click += (s, e) =>
        {
            choose.Content = "Choose Image...";
            clear.IsEnabled = false;
            set(null);
        };
        panel.Children.Add(choose);
        panel.Children.Add(clear);
        return panel;
    }

    public static string Describe(DrawingFont font)
    {
        string style = font.Style == Drawing.FontStyle.Regular ? "" : " " + font.Style.ToString().Replace(",", "");
        return $"{font.Name}{style} {font.Size:0.#}{(font.Unit == Drawing.GraphicsUnit.Point ? "pt" : "px")}";
    }
}
