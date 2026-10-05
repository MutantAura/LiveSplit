using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.Input;
using LiveSplit.Options;
using LiveSplit.Themes;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LiveSplit.View;

/// <summary>
/// Application settings: hotkey profiles and general options. Edits the settings in place;
/// returns the selected hotkey profile, or null if cancelled (the caller restores a copy).
/// </summary>
public sealed class SettingsWindow : Window
{
    private readonly ISettings settings;
    private readonly ComboBox profileSelector;
    private readonly StackPanel hotkeyPanel;
    private Action<Keys> pendingCapture;

    private string SelectedProfile => profileSelector.SelectedItem as string;

    public static Task<string> Show(Window owner, ISettings settings, string currentProfile, CompositeHook hook)
    {
        return new SettingsWindow(settings, currentProfile, hook).ShowDialog<string>(owner);
    }

    internal SettingsWindow(ISettings settings, string currentProfile, CompositeHook hook)
    {
        this.settings = settings;

        // Theme changes preview immediately; restore the previous theme unless OK is pressed.
        AppTheme originalTheme = ThemeManager.Theme;
        ThemeMode originalMode = ThemeManager.Mode;
        bool accepted = false;
        Closed += (s, e) =>
        {
            if (accepted)
            {
                ThemeManager.Save();
            }
            else if (ThemeManager.Theme != originalTheme || ThemeManager.Mode != originalMode)
            {
                ThemeManager.Apply(originalTheme, originalMode);
            }
        };

        Title = "Settings";
        Width = 560;
        Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        profileSelector = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        profileSelector.SelectionChanged += (s, e) => BuildHotkeyPanel();

        var addProfile = new Button { Content = "New" };
        addProfile.Click += (s, e) => AddProfile();
        var removeProfile = new Button { Content = "Remove" };
        removeProfile.Click += (s, e) => RemoveProfile();

        hotkeyPanel = new StackPanel { Spacing = 6 };

        var profileRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 6 };
        AddToGrid(profileRow, new TextBlock { Text = "Hotkey Profile", VerticalAlignment = VerticalAlignment.Center }, 0);
        AddToGrid(profileRow, profileSelector, 1);
        AddToGrid(profileRow, addProfile, 2);
        AddToGrid(profileRow, removeProfile, 3);

        var hotkeysTab = new StackPanel { Spacing = 8, Margin = new Thickness(8), Children = { profileRow, hotkeyPanel } };
        if (hook?.IsUnavailable == true)
        {
            hotkeysTab.Children.Insert(0, new TextBlock
            {
                Text = "Global hotkeys are unavailable on this system (for example under Wayland, or without the macOS Accessibility permission). Hotkeys only work while LiveSplit has focus.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Orange
            });
        }

        var tabs = new TabControl
        {
            ItemsSource = new[]
            {
                new TabItem { Header = "Hotkeys", Content = new ScrollViewer { Content = hotkeysTab } },
                new TabItem { Header = "General", Content = new ScrollViewer { Content = BuildGeneralPanel() } },
                new TabItem { Header = "Comparisons", Content = new ScrollViewer { Content = BuildComparisonsPanel() } }
            }
        };

        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80 };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
        ok.Click += (s, e) =>
        {
            accepted = true;
            Close(SelectedProfile ?? settings.HotkeyProfiles.Keys.First());
        };
        cancel.Click += (s, e) => Close(null);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 0),
            Children = { ok, cancel }
        };

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(tabs);
        Content = root;

        RefreshProfiles(currentProfile);
    }

    private static void AddToGrid(Grid grid, Control control, int column, int row = 0)
    {
        Grid.SetColumn(control, column);
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (pendingCapture != null)
        {
            Keys key = KeyMapping.FromAvalonia(e.Key, e.KeyModifiers);
            Keys keyCode = key & Keys.KeyCode;
            bool isModifierOnly = keyCode is Keys.LShiftKey or Keys.RShiftKey or Keys.LControlKey or Keys.RControlKey or Keys.LMenu or Keys.RMenu;
            if (key != Keys.None && !isModifierOnly)
            {
                Action<Keys> capture = pendingCapture;
                pendingCapture = null;
                capture(key);
            }

            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void RefreshProfiles(string selected)
    {
        profileSelector.ItemsSource = settings.HotkeyProfiles.Keys.ToList();
        profileSelector.SelectedItem = settings.HotkeyProfiles.ContainsKey(selected ?? "") ? selected : settings.HotkeyProfiles.Keys.First();
    }

    private void AddProfile()
    {
        string name = "Profile";
        for (int i = 2; settings.HotkeyProfiles.ContainsKey(name); i++)
        {
            name = $"Profile {i}";
        }

        HotkeyProfile template = settings.HotkeyProfiles[SelectedProfile ?? settings.HotkeyProfiles.Keys.First()];
        settings.HotkeyProfiles[name] = (HotkeyProfile)template.Clone();
        RefreshProfiles(name);
    }

    private void RemoveProfile()
    {
        if (settings.HotkeyProfiles.Count > 1 && SelectedProfile != null)
        {
            settings.HotkeyProfiles.Remove(SelectedProfile);
            RefreshProfiles(null);
        }
    }

    private void BuildHotkeyPanel()
    {
        hotkeyPanel.Children.Clear();
        if (SelectedProfile == null || !settings.HotkeyProfiles.TryGetValue(SelectedProfile, out HotkeyProfile profile))
        {
            return;
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8, RowSpacing = 6 };
        int row = 0;

        void AddHotkey(string label, Func<KeyOrButton> get, Action<KeyOrButton> set)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var button = new Button { Content = get()?.ToString() ?? "None", HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += (s, e) =>
            {
                button.Content = "Press a key...";
                pendingCapture = key =>
                {
                    set(new KeyOrButton(key));
                    button.Content = key.ToString();
                };
            };
            var clear = new Button { Content = "✕" };
            ToolTip.SetTip(clear, "Clear");
            clear.Click += (s, e) =>
            {
                pendingCapture = null;
                set(null);
                button.Content = "None";
            };

            AddToGrid(grid, new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }, 0, row);
            AddToGrid(grid, button, 1, row);
            AddToGrid(grid, clear, 2, row);
            row++;
        }

        AddHotkey("Start / Split", () => profile.SplitKey, x => profile.SplitKey = x);
        AddHotkey("Reset", () => profile.ResetKey, x => profile.ResetKey = x);
        AddHotkey("Undo Split", () => profile.UndoKey, x => profile.UndoKey = x);
        AddHotkey("Skip Split", () => profile.SkipKey, x => profile.SkipKey = x);
        AddHotkey("Pause", () => profile.PauseKey, x => profile.PauseKey = x);
        AddHotkey("Switch Comparison (Previous)", () => profile.SwitchComparisonPrevious, x => profile.SwitchComparisonPrevious = x);
        AddHotkey("Switch Comparison (Next)", () => profile.SwitchComparisonNext, x => profile.SwitchComparisonNext = x);
        AddHotkey("Toggle Global Hotkeys", () => profile.ToggleGlobalHotkeys, x => profile.ToggleGlobalHotkeys = x);

        var globalHotkeys = new CheckBox { Content = "Global Hotkeys", IsChecked = profile.GlobalHotkeysEnabled };
        globalHotkeys.IsCheckedChanged += (s, e) => profile.GlobalHotkeysEnabled = globalHotkeys.IsChecked == true;

        var doubleTap = new CheckBox { Content = "Double Tap Prevention", IsChecked = profile.DoubleTapPrevention };
        doubleTap.IsCheckedChanged += (s, e) => profile.DoubleTapPrevention = doubleTap.IsChecked == true;

        var delay = new NumericUpDown { Value = (decimal)profile.HotkeyDelay, Minimum = 0, Maximum = 10, Increment = 0.1m, FormatString = "0.0#", Width = 140 };
        delay.ValueChanged += (s, e) => profile.HotkeyDelay = (float)(delay.Value ?? 0);

        hotkeyPanel.Children.Add(grid);
        hotkeyPanel.Children.Add(globalHotkeys);
        hotkeyPanel.Children.Add(doubleTap);
        hotkeyPanel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { new TextBlock { Text = "Hotkey Delay (seconds)", VerticalAlignment = VerticalAlignment.Center }, delay }
        });
    }

    private Control BuildGeneralPanel()
    {
        var warnOnReset = new CheckBox { Content = "Warn On Reset If Better Times", IsChecked = settings.WarnOnReset };
        warnOnReset.IsCheckedChanged += (s, e) => settings.WarnOnReset = warnOnReset.IsChecked == true;

        var simpleSumOfBest = new CheckBox { Content = "Simple Sum of Best Calculation", IsChecked = settings.SimpleSumOfBest };
        simpleSumOfBest.IsCheckedChanged += (s, e) => settings.SimpleSumOfBest = simpleSumOfBest.IsChecked == true;

        var refreshRate = new NumericUpDown { Value = settings.RefreshRate, Minimum = 20, Maximum = 300, FormatString = "0", Width = 140 };
        refreshRate.ValueChanged += (s, e) => settings.RefreshRate = (int)(refreshRate.Value ?? 40);

        AppTheme[] themes = Enum.GetValues<AppTheme>();
        var theme = new ComboBox
        {
            ItemsSource = themes.Select(ThemeManager.DisplayName).ToList(),
            SelectedIndex = Array.IndexOf(themes, ThemeManager.Theme),
            MinWidth = 180
        };

        ThemeMode[] modes = Enum.GetValues<ThemeMode>();
        var mode = new ComboBox
        {
            ItemsSource = modes.Select(ThemeManager.DisplayName).ToList(),
            SelectedIndex = Array.IndexOf(modes, ThemeManager.Mode),
            MinWidth = 180
        };

        void ApplyTheme()
        {
            if (theme.SelectedIndex >= 0 && mode.SelectedIndex >= 0)
            {
                ThemeManager.Apply(themes[theme.SelectedIndex], modes[mode.SelectedIndex]);
            }
        }

        theme.SelectionChanged += (s, e) => ApplyTheme();
        mode.SelectionChanged += (s, e) => ApplyTheme();

        var appearance = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 12,
            RowSpacing = 6,
            Margin = new Thickness(0, 0, 0, 12)
        };
        AddToGrid(appearance, new TextBlock { Text = "Theme", VerticalAlignment = VerticalAlignment.Center }, 0, 0);
        AddToGrid(appearance, theme, 1, 0);
        AddToGrid(appearance, new TextBlock { Text = "Appearance", VerticalAlignment = VerticalAlignment.Center }, 0, 1);
        AddToGrid(appearance, mode, 1, 1);

        return new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(8),
            Children =
            {
                appearance,
                warnOnReset,
                simpleSumOfBest,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { new TextBlock { Text = "Refresh Rate (Hz)", VerticalAlignment = VerticalAlignment.Center }, refreshRate }
                },
                new TextBlock
                {
                    Text = $"Settings are stored in {AppPaths.SettingsPath}",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 12, 0, 0)
                }
            }
        };
    }

    private Control BuildComparisonsPanel()
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(8) };
        panel.Children.Add(new TextBlock { Text = "Generated comparisons shown in \"Compare Against\":" });

        var generators = new StandardComparisonGeneratorsFactory().GetAllGenerators(new Model.Run(new StandardComparisonGeneratorsFactory()));
        foreach (IComparisonGenerator generator in generators)
        {
            string name = generator.Name;
            var checkBox = new CheckBox
            {
                Content = name,
                IsChecked = settings.ComparisonGeneratorStates.TryGetValue(name, out bool enabled) && enabled
            };
            checkBox.IsCheckedChanged += (s, e) => settings.ComparisonGeneratorStates[name] = checkBox.IsChecked == true;
            panel.Children.Add(checkBox);
        }

        var hcpHistory = new NumericUpDown { Value = settings.HcpHistorySize, Minimum = 1, Maximum = 1000, FormatString = "0", Width = 140 };
        hcpHistory.ValueChanged += (s, e) => settings.HcpHistorySize = (int)(hcpHistory.Value ?? 20);
        var hcpBestRuns = new NumericUpDown { Value = settings.HcpNBestRuns, Minimum = 1, Maximum = 1000, FormatString = "0", Width = 140 };
        hcpBestRuns.ValueChanged += (s, e) => settings.HcpNBestRuns = (int)(hcpBestRuns.Value ?? 8);

        panel.Children.Add(new TextBlock { Text = "HCP Comparison", Margin = new Thickness(0, 12, 0, 0), FontWeight = FontWeight.Bold });
        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { new TextBlock { Text = "History Size", VerticalAlignment = VerticalAlignment.Center }, hcpHistory }
        });
        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { new TextBlock { Text = "Best Runs", VerticalAlignment = VerticalAlignment.Center }, hcpBestRuns }
        });

        return panel;
    }
}
