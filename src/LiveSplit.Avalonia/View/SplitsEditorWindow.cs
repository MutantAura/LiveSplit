using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using LiveSplit.Model;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using MessageBox = LiveSplit.UI.MessageBox;

namespace LiveSplit.View;

/// <summary>
/// Edits the current run in place: game, category, offset, attempts, icons, segments and
/// comparisons. The caller restores a copy of the run if the dialog is cancelled.
/// </summary>
public sealed class SplitsEditorWindow : Window
{
    private readonly LiveSplitState state;
    private readonly ITimerModel model;
    private readonly IRun run;
    private readonly ObservableCollection<SegmentRow> rows = [];
    private readonly DataGrid grid;
    private readonly StackPanel comparisonButtons;
    private TimingMethod method = TimingMethod.RealTime;

    public static Task<bool> Show(Window owner, LiveSplitState state, ITimerModel model)
    {
        var window = new SplitsEditorWindow(state, model);
        return window.ShowDialog<bool>(owner);
    }

    internal SplitsEditorWindow(LiveSplitState state, ITimerModel model)
    {
        this.state = state;
        this.model = model;
        run = state.Run;
        Title = "Splits Editor";
        Width = 820;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        bool timerRunning = state.CurrentPhase != TimerPhase.NotRunning;

        var gameName = new TextBox { Text = run.GameName, PlaceholderText = "Game Name" };
        gameName.TextChanged += (s, e) => run.GameName = gameName.Text ?? "";
        var categoryName = new TextBox { Text = run.CategoryName, PlaceholderText = "Category" };
        categoryName.TextChanged += (s, e) => run.CategoryName = categoryName.Text ?? "";

        var offset = new TextBox { Text = new ShortTimeFormatter().Format(run.Offset), Width = 120 };
        offset.LostFocus += (s, e) =>
        {
            try
            {
                run.Offset = TimeSpanParser.Parse(offset.Text);
            }
            catch
            {
                offset.Text = new ShortTimeFormatter().Format(run.Offset);
            }
        };

        var attempts = new NumericUpDown { Value = run.AttemptCount, Minimum = 0, Maximum = int.MaxValue, FormatString = "0", Width = 140 };
        attempts.ValueChanged += (s, e) => run.AttemptCount = (int)(attempts.Value ?? 0);

        var gameIcon = new Button { Content = "Set Game Icon..." };
        gameIcon.Click += async (s, e) => run.GameIcon = await PickImage() ?? run.GameIcon;
        var removeGameIcon = new Button { Content = "Remove Game Icon" };
        removeGameIcon.Click += (s, e) => run.GameIcon = null;

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 8,
            RowSpacing = 6
        };
        AddCell(header, new TextBlock { Text = "Game", VerticalAlignment = VerticalAlignment.Center }, 0, 0);
        AddCell(header, gameName, 0, 1);
        AddCell(header, new TextBlock { Text = "Category", VerticalAlignment = VerticalAlignment.Center }, 0, 2);
        AddCell(header, categoryName, 0, 3);
        AddCell(header, new TextBlock { Text = "Start Timer At", VerticalAlignment = VerticalAlignment.Center }, 1, 0);
        AddCell(header, offset, 1, 1);
        AddCell(header, new TextBlock { Text = "Attempts", VerticalAlignment = VerticalAlignment.Center }, 1, 2);
        AddCell(header, attempts, 1, 3);
        var iconPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { gameIcon, removeGameIcon } };
        AddCell(header, iconPanel, 2, 1);
        Grid.SetColumnSpan(iconPanel, 3);

        var methodSelector = new ComboBox { ItemsSource = new[] { "Real Time", "Game Time" }, SelectedIndex = 0 };
        methodSelector.SelectionChanged += (s, e) =>
        {
            method = methodSelector.SelectedIndex == 1 ? TimingMethod.GameTime : TimingMethod.RealTime;
            Refresh();
        };

        grid = new DataGrid
        {
            ItemsSource = rows,
            CanUserSortColumns = false,
            CanUserReorderColumns = false,
            AutoGenerateColumns = false,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            IsReadOnly = timerRunning
        };
        grid.CellEditEnding += Grid_CellEditEnding;
        grid.CellEditEnded += (s, e) => Refresh();
        BuildColumns();

        Button Action(string text, Action action, bool enabled = true)
        {
            var button = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = enabled };
            button.Click += (s, e) => action();
            return button;
        }

        var segmentButtons = new StackPanel
        {
            Spacing = 4,
            Width = 160,
            Children =
            {
                new TextBlock { Text = "Timing Method" },
                methodSelector,
                Action("Insert Above", () => InsertSegment(0), !timerRunning),
                Action("Insert Below", () => InsertSegment(1), !timerRunning),
                Action("Remove Segment", RemoveSegment, !timerRunning),
                Action("Move Up", () => MoveSegment(-1), !timerRunning),
                Action("Move Down", () => MoveSegment(1), !timerRunning),
                Action("Set Icon...", async () => await SetSegmentIcon()),
                Action("Remove Icon", () =>
                {
                    if (SelectedIndex >= 0)
                    {
                        run[SelectedIndex].Icon = null;
                        Refresh();
                    }
                }),
                Action("Clear History", ClearHistory, !timerRunning),
                Action("Clear Times", ClearTimes, !timerRunning),
            }
        };

        comparisonButtons = new StackPanel { Spacing = 4 };
        segmentButtons.Children.Add(new TextBlock { Text = "Comparisons", Margin = new Thickness(0, 8, 0, 0) });
        segmentButtons.Children.Add(Action("Add Comparison...", async () => await AddComparison(), !timerRunning));
        segmentButtons.Children.Add(comparisonButtons);

        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80 };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
        ok.Click += (s, e) =>
        {
            run.FixSplits();
            Close(true);
        };
        cancel.Click += (s, e) => Close(false);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { ok, cancel }
        };

        if (timerRunning)
        {
            footer.Children.Insert(0, new TextBlock
            {
                Text = "Segments can't be changed while the timer is running.",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.Orange
            });
        }

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        DockPanel.SetDock(segmentButtons, Dock.Right);
        header.Margin = new Thickness(0, 0, 0, 10);
        footer.Margin = new Thickness(0, 10, 0, 0);
        segmentButtons.Margin = new Thickness(10, 0, 0, 0);
        root.Children.Add(header);
        root.Children.Add(footer);
        root.Children.Add(segmentButtons);
        root.Children.Add(grid);
        Content = root;

        Refresh();
    }

    private static void AddCell(Grid grid, Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    private int SelectedIndex => grid.SelectedItem is SegmentRow row ? row.Index : -1;

    private void BuildColumns()
    {
        grid.Columns.Clear();
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Icon",
            IsReadOnly = true,
            CellTemplate = new FuncDataTemplate<SegmentRow>((_, _) =>
            {
                var image = new Image { Width = 20, Height = 20 };
                image.DataContextChanged += (s, e) => image.Source = (image.DataContext as SegmentRow)?.Icon;
                return image;
            })
        });
        grid.Columns.Add(TextColumn("Segment Name", SegmentRow.NameKey, new DataGridLength(1, DataGridLengthUnitType.Star)));
        grid.Columns.Add(TextColumn("Split Time", SegmentRow.SplitTimeKey));
        grid.Columns.Add(TextColumn("Segment Time", SegmentRow.SegmentTimeKey));
        grid.Columns.Add(TextColumn("Best Segment", SegmentRow.BestSegmentKey));

        foreach (string comparison in run.CustomComparisons.Where(x => x != Run.PersonalBestComparisonName))
        {
            grid.Columns.Add(TextColumn(comparison, comparison));
        }

        comparisonButtons?.Children.Clear();
        foreach (string comparison in run.CustomComparisons.Where(x => x != Run.PersonalBestComparisonName))
        {
            string name = comparison;
            var rename = new Button { Content = $"Rename \"{name}\"...", HorizontalAlignment = HorizontalAlignment.Stretch };
            rename.Click += async (s, e) => await RenameComparison(name);
            var remove = new Button { Content = $"Remove \"{name}\"", HorizontalAlignment = HorizontalAlignment.Stretch };
            remove.Click += (s, e) => RemoveComparison(name);
            comparisonButtons?.Children.Add(rename);
            comparisonButtons?.Children.Add(remove);
        }
    }

    /// <summary>
    /// A text column that reads and writes the row directly instead of through reflection
    /// bindings, so the editor works with Native AOT. The column's Tag holds the value key.
    /// </summary>
    private static DataGridTemplateColumn TextColumn(string header, string key, DataGridLength? width = null)
    {
        return new DataGridTemplateColumn
        {
            Header = header,
            Tag = key,
            Width = width ?? DataGridLength.Auto,
            CellTemplate = new FuncDataTemplate<SegmentRow>((_, _) =>
            {
                var text = new TextBlock { Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Center };
                text.DataContextChanged += (s, e) => text.Text = (text.DataContext as SegmentRow)?.GetValue(key);
                return text;
            }),
            CellEditingTemplate = new FuncDataTemplate<SegmentRow>((_, _) =>
            {
                var box = new TextBox { VerticalAlignment = VerticalAlignment.Stretch };
                box.DataContextChanged += (s, e) => box.Text = (box.DataContext as SegmentRow)?.GetValue(key);
                box.AttachedToVisualTree += (s, e) =>
                {
                    box.Focus();
                    box.SelectAll();
                };
                return box;
            })
        };
    }

    private void Grid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit
            && e.Column.Tag is string key
            && e.EditingElement is TextBox box
            && e.Row.DataContext is SegmentRow row)
        {
            row.SetValue(key, box.Text);
        }
    }

    private void Refresh()
    {
        int selected = SelectedIndex;
        rows.Clear();
        for (int i = 0; i < run.Count; i++)
        {
            rows.Add(new SegmentRow(this, i));
        }

        if (selected >= 0 && selected < rows.Count)
        {
            grid.SelectedItem = rows[selected];
        }
    }

    private void InsertSegment(int offset)
    {
        int index = SelectedIndex < 0 ? (offset == 0 ? 0 : run.Count) : SelectedIndex + offset;
        var segment = new Segment("");
        run.Insert(index, segment);
        foreach (string comparison in run.CustomComparisons)
        {
            segment.Comparisons[comparison] = default;
        }

        FixAfterStructureChange();
        Refresh();
        grid.SelectedItem = rows[index];
    }

    private void RemoveSegment()
    {
        int index = SelectedIndex;
        if (index < 0 || run.Count <= 1)
        {
            return;
        }

        // The next segment now also covers the removed one, so merge the removed segment's
        // best segment and history times into it.
        if (index < run.Count - 1)
        {
            ISegment next = run[index + 1];
            ISegment removed = run[index];
            next.BestSegmentTime = AddTimes(removed.BestSegmentTime, next.BestSegmentTime);

            foreach (int attempt in next.SegmentHistory.Keys.ToList())
            {
                next.SegmentHistory[attempt] = removed.SegmentHistory.TryGetValue(attempt, out Time removedTime)
                    ? AddTimes(removedTime, next.SegmentHistory[attempt])
                    : default;
            }
        }

        run.RemoveAt(index);
        FixAfterStructureChange();
        Refresh();
        if (rows.Count > 0)
        {
            grid.SelectedItem = rows[Math.Min(index, rows.Count - 1)];
        }
    }

    private void MoveSegment(int direction)
    {
        int index = SelectedIndex;
        int target = index + direction;
        if (index < 0 || target < 0 || target >= run.Count)
        {
            return;
        }

        // Times belong to positions in the run, so only the names and icons move.
        (run[index].Name, run[target].Name) = (run[target].Name, run[index].Name);
        (run[index].Icon, run[target].Icon) = (run[target].Icon, run[index].Icon);
        run.HasChanged = true;
        Refresh();
        grid.SelectedItem = rows[target];
    }

    private void FixAfterStructureChange()
    {
        run.HasChanged = true;
        run.FixSplits();
    }

    private static Time AddTimes(Time a, Time b)
    {
        return new Time
        {
            RealTime = a.RealTime + b.RealTime,
            GameTime = a.GameTime + b.GameTime
        };
    }

    private void ClearHistory()
    {
        run.ClearHistory();
        Refresh();
    }

    private void ClearTimes()
    {
        run.ClearTimes();
        Refresh();
    }

    private async Task SetSegmentIcon()
    {
        int index = SelectedIndex;
        if (index < 0)
        {
            return;
        }

        Drawing.Image image = await PickImage();
        if (image != null)
        {
            run[index].Icon = image;
            Refresh();
        }
    }

    private async Task<Drawing.Image> PickImage()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose Icon",
            FileTypeFilter = [FilePickerFileTypes.ImageAll]
        });

        if (files.Count == 0)
        {
            return null;
        }

        await using System.IO.Stream stream = await files[0].OpenReadAsync();
        Drawing.Image image = Drawing.Image.FromStream(stream);
        return image.ToBitmap() != null ? image : null;
    }

    private async Task<string> AskForName(string title, string initial)
    {
        var textBox = new TextBox { Text = initial, MinWidth = 260 };
        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80 };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
        ok.Click += (s, e) => dialog.Close(textBox.Text);
        cancel.Click += (s, e) => dialog.Close(null);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 10,
            Children =
            {
                textBox,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { ok, cancel } }
            }
        };
        dialog.Opened += (s, e) => textBox.Focus();
        return await dialog.ShowDialog<string>(this);
    }

    private bool IsValidComparisonName(string name)
    {
        return !string.IsNullOrWhiteSpace(name)
            && !name.StartsWith("[Race]")
            && !run.Comparisons.Contains(name);
    }

    private async Task AddComparison()
    {
        string name = await AskForName("Add Comparison", "");
        if (name == null)
        {
            return;
        }

        name = name.Trim();
        if (!IsValidComparisonName(name))
        {
            await MessageBox.Show(this, "A comparison with this name already exists or the name is reserved.", "Add Comparison");
            return;
        }

        run.CustomComparisons.Add(name);
        foreach (ISegment segment in run)
        {
            segment.Comparisons[name] = default;
        }

        run.HasChanged = true;
        BuildColumns();
        Refresh();
    }

    private async Task RenameComparison(string oldName)
    {
        string newName = await AskForName("Rename Comparison", oldName);
        if (newName == null || newName.Trim() == oldName)
        {
            return;
        }

        newName = newName.Trim();
        if (!IsValidComparisonName(newName))
        {
            await MessageBox.Show(this, "A comparison with this name already exists or the name is reserved.", "Rename Comparison");
            return;
        }

        int index = run.CustomComparisons.IndexOf(oldName);
        run.CustomComparisons[index] = newName;
        foreach (ISegment segment in run)
        {
            segment.Comparisons[newName] = segment.Comparisons[oldName];
            segment.Comparisons.Remove(oldName);
        }

        if (state.CurrentComparison == oldName)
        {
            state.CurrentComparison = newName;
        }

        state.CallComparisonRenamed(new RenameEventArgs { OldName = oldName, NewName = newName });
        run.HasChanged = true;
        BuildColumns();
        Refresh();
    }

    private void RemoveComparison(string name)
    {
        run.CustomComparisons.Remove(name);
        foreach (ISegment segment in run)
        {
            segment.Comparisons.Remove(name);
        }

        if (state.CurrentComparison == name)
        {
            state.CurrentComparison = Run.PersonalBestComparisonName;
        }

        run.HasChanged = true;
        BuildColumns();
        Refresh();
    }

    /// <summary>
    /// One row of the segments grid. Times are shown and edited as text in the selected
    /// timing method.
    /// </summary>
    public sealed class SegmentRow(SplitsEditorWindow editor, int index)
    {
        private static readonly ShortTimeFormatter Formatter = new();

        public int Index { get; } = index;

        private IRun Run => editor.run;
        private ISegment Segment => Run[Index];
        private TimingMethod Method => editor.method;

        public Avalonia.Media.Imaging.Bitmap Icon => Segment.Icon?.ToBitmap();

        public string Name
        {
            get => Segment.Name;
            set
            {
                Segment.Name = value ?? "";
                Run.HasChanged = true;
            }
        }

        public string SplitTime
        {
            get => FormatTime(Segment.PersonalBestSplitTime[Method]);
            set => SetComparisonTime(Model.Run.PersonalBestComparisonName, value);
        }

        public string SegmentTime
        {
            get
            {
                TimeSpan? split = Segment.PersonalBestSplitTime[Method];
                TimeSpan? previous = PreviousSplitTime(Model.Run.PersonalBestComparisonName);
                return FormatTime(split - previous);
            }
            set
            {
                if (!TryParse(value, out TimeSpan? segmentTime))
                {
                    return;
                }

                // Changing a segment time shifts all following split times, like the Windows editor.
                TimeSpan? oldSplit = Segment.PersonalBestSplitTime[Method];
                TimeSpan? newSplit = segmentTime == null ? null : PreviousSplitTime(Model.Run.PersonalBestComparisonName) + segmentTime;
                SetTime(Index, Model.Run.PersonalBestComparisonName, newSplit);
                if (oldSplit != null && newSplit != null)
                {
                    TimeSpan difference = newSplit.Value - oldSplit.Value;
                    for (int i = Index + 1; i < Run.Count; i++)
                    {
                        TimeSpan? later = Run[i].PersonalBestSplitTime[Method];
                        if (later != null)
                        {
                            SetTime(i, Model.Run.PersonalBestComparisonName, later + difference);
                        }
                    }
                }

                Run.HasChanged = true;
            }
        }

        public string BestSegment
        {
            get => FormatTime(Segment.BestSegmentTime[Method]);
            set
            {
                if (TryParse(value, out TimeSpan? time))
                {
                    Time best = Segment.BestSegmentTime;
                    best[Method] = time;
                    Segment.BestSegmentTime = best;
                    Run.HasChanged = true;
                }
            }
        }

        // Keys of the built-in columns; any other key is a custom comparison name. The control
        // character keeps them from colliding with comparison names users can type.
        public const string NameKey = "\u0001Name";
        public const string SplitTimeKey = "\u0001SplitTime";
        public const string SegmentTimeKey = "\u0001SegmentTime";
        public const string BestSegmentKey = "\u0001BestSegment";

        public string GetValue(string key)
        {
            return key switch
            {
                NameKey => Name,
                SplitTimeKey => SplitTime,
                SegmentTimeKey => SegmentTime,
                BestSegmentKey => BestSegment,
                _ => FormatTime(Segment.Comparisons[key][Method])
            };
        }

        public void SetValue(string key, string value)
        {
            switch (key)
            {
                case NameKey:
                    Name = value;
                    break;
                case SplitTimeKey:
                    SplitTime = value;
                    break;
                case SegmentTimeKey:
                    SegmentTime = value;
                    break;
                case BestSegmentKey:
                    BestSegment = value;
                    break;
                default:
                    SetComparisonTime(key, value);
                    break;
            }
        }

        private TimeSpan? PreviousSplitTime(string comparison)
        {
            for (int i = Index - 1; i >= 0; i--)
            {
                TimeSpan? time = Run[i].Comparisons[comparison][Method];
                if (time != null)
                {
                    return time;
                }
            }

            return TimeSpan.Zero;
        }

        private void SetComparisonTime(string comparison, string value)
        {
            if (TryParse(value, out TimeSpan? time))
            {
                SetTime(Index, comparison, time);
                Run.HasChanged = true;
            }
        }

        private void SetTime(int index, string comparison, TimeSpan? value)
        {
            Time time = Run[index].Comparisons[comparison];
            time[Method] = value;
            Run[index].Comparisons[comparison] = time;
        }

        private static string FormatTime(TimeSpan? time)
        {
            return time == null ? "" : Formatter.Format(time);
        }

        private static bool TryParse(string text, out TimeSpan? time)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                time = null;
                return true;
            }

            try
            {
                time = TimeSpanParser.Parse(text.Trim());
                return true;
            }
            catch
            {
                time = null;
                return false;
            }
        }
    }
}
