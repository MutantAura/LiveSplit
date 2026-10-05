using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.UI.Components;

/// <summary>
/// The generated Splits settings form plus an editor for the list of columns.
/// </summary>
public static class SplitsSettingsEditor
{
    private static readonly string[] TimingMethods = ["Current Timing Method", "Real Time", "Game Time"];

    public static Control Create(SplitsSettings settings, LiveSplitState state)
    {
        var columnsPanel = new StackPanel { Spacing = 6, Margin = new Thickness(8, 0, 8, 8) };

        void Changed()
        {
            settings.OnSettingChanged(nameof(SplitsSettings.ColumnsList));
        }

        void Rebuild()
        {
            columnsPanel.Children.Clear();
            columnsPanel.Children.Add(new TextBlock { Text = "Columns", FontWeight = Avalonia.Media.FontWeight.Bold, Margin = new Thickness(0, 8, 0, 0) });

            for (int index = 0; index < settings.ColumnsList.Count; index++)
            {
                columnsPanel.Children.Add(CreateColumnEditor(settings, state, index, Rebuild, Changed));
            }

            var add = new Button { Content = "Add Column" };
            add.Click += (s, e) =>
            {
                settings.ColumnsList.Add(new ColumnData("", ColumnType.Delta, "Current Comparison", "Current Timing Method"));
                Changed();
                Rebuild();
            };
            columnsPanel.Children.Add(add);
        }

        Rebuild();

        var content = new StackPanel();
        content.Children.Add(SettingsEditor.CreateForm(settings, state, settings.OnSettingChanged));
        content.Children.Add(columnsPanel);

        return new ScrollViewer { Content = content };
    }

    private static Control CreateColumnEditor(SplitsSettings settings, LiveSplitState state, int index, Action rebuild, Action changed)
    {
        ColumnData column = settings.ColumnsList[index];

        var comparisons = new List<string> { "Current Comparison" };
        if (state?.Run != null)
        {
            comparisons.AddRange(state.Run.Comparisons.Where(x => x != NoneComparisonGenerator.ComparisonName));
        }

        if (!comparisons.Contains(column.Comparison))
        {
            comparisons.Add(column.Comparison);
        }

        var name = new TextBox { Text = column.Name, PlaceholderText = "Name", MinWidth = 90 };
        name.TextChanged += (s, e) =>
        {
            column.Name = name.Text ?? "";
            changed();
        };

        ColumnType[] types = Enum.GetValues<ColumnType>();
        var type = new ComboBox
        {
            ItemsSource = types.Select(ColumnTypeName).ToList(),
            SelectedIndex = Array.IndexOf(types, column.Type)
        };
        type.SelectionChanged += (s, e) =>
        {
            if (type.SelectedIndex >= 0)
            {
                column.Type = types[type.SelectedIndex];
                changed();
            }
        };

        var comparison = new ComboBox { ItemsSource = comparisons, SelectedItem = column.Comparison };
        comparison.SelectionChanged += (s, e) =>
        {
            if (comparison.SelectedItem is string selected)
            {
                column.Comparison = selected;
                changed();
            }
        };

        var timingMethod = new ComboBox { ItemsSource = TimingMethods, SelectedItem = column.TimingMethod };
        timingMethod.SelectionChanged += (s, e) =>
        {
            if (timingMethod.SelectedItem is string selected)
            {
                column.TimingMethod = selected;
                changed();
            }
        };

        var up = new Button { Content = "↑", IsEnabled = index > 0 };
        up.Click += (s, e) =>
        {
            settings.ColumnsList.RemoveAt(index);
            settings.ColumnsList.Insert(index - 1, column);
            changed();
            rebuild();
        };

        var down = new Button { Content = "↓", IsEnabled = index < settings.ColumnsList.Count - 1 };
        down.Click += (s, e) =>
        {
            settings.ColumnsList.RemoveAt(index);
            settings.ColumnsList.Insert(index + 1, column);
            changed();
            rebuild();
        };

        var remove = new Button { Content = "✕" };
        remove.Click += (s, e) =>
        {
            settings.ColumnsList.RemoveAt(index);
            changed();
            rebuild();
        };

        return new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemSpacing = 4,
            LineSpacing = 4,
            Children = { name, type, comparison, timingMethod, up, down, remove }
        };
    }

    private static string ColumnTypeName(ColumnType type)
    {
        return type switch
        {
            ColumnType.Delta => "Delta",
            ColumnType.SplitTime => "Split Time",
            ColumnType.DeltaorSplitTime => "Delta or Split Time",
            ColumnType.SegmentDelta => "Segment Delta",
            ColumnType.SegmentTime => "Segment Time",
            ColumnType.SegmentDeltaorSegmentTime => "Segment Delta or Segment Time",
            ColumnType.CustomVariable => "Custom Variable",
            _ => type.ToString()
        };
    }
}
