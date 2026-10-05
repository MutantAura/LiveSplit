using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MessageBox = LiveSplit.UI.MessageBox;

namespace LiveSplit.View;

public partial class TimerWindow
{
    private const double ResizeBorder = 6;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        PointerPoint point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            WindowEdge? edge = Layout.Settings.AllowResizing ? GetEdge(point.Position) : null;
            if (edge.HasValue)
            {
                BeginResizeDrag(edge.Value, e);
            }
            else if (Layout.Settings.AllowMoving)
            {
                BeginMoveDrag(e);
            }
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            e.Handled = true;
            ShowContextMenu();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        WindowEdge? edge = Layout.Settings.AllowResizing ? GetEdge(e.GetPosition(this)) : null;
        Cursor = edge switch
        {
            WindowEdge.North or WindowEdge.South => new Cursor(StandardCursorType.SizeNorthSouth),
            WindowEdge.East or WindowEdge.West => new Cursor(StandardCursorType.SizeWestEast),
            WindowEdge.NorthWest => new Cursor(StandardCursorType.TopLeftCorner),
            WindowEdge.NorthEast => new Cursor(StandardCursorType.TopRightCorner),
            WindowEdge.SouthWest => new Cursor(StandardCursorType.BottomLeftCorner),
            WindowEdge.SouthEast => new Cursor(StandardCursorType.BottomRightCorner),
            _ => Cursor.Default
        };
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (e.Delta.Y > 0)
        {
            Model.ScrollUp();
        }
        else if (e.Delta.Y < 0)
        {
            Model.ScrollDown();
        }
    }

    protected override void OnResized(WindowResizedEventArgs e)
    {
        base.OnResized(e);
        InvalidationRequired = true;
    }

    private WindowEdge? GetEdge(Point position)
    {
        bool left = position.X < ResizeBorder;
        bool right = position.X > Bounds.Width - ResizeBorder;
        bool top = position.Y < ResizeBorder;
        bool bottom = position.Y > Bounds.Height - ResizeBorder;

        return (left, right, top, bottom) switch
        {
            (true, _, true, _) => WindowEdge.NorthWest,
            (_, true, true, _) => WindowEdge.NorthEast,
            (true, _, _, true) => WindowEdge.SouthWest,
            (_, true, _, true) => WindowEdge.SouthEast,
            (true, _, _, _) => WindowEdge.West,
            (_, true, _, _) => WindowEdge.East,
            (_, _, true, _) => WindowEdge.North,
            (_, _, _, true) => WindowEdge.South,
            _ => null
        };
    }

    private void ShowContextMenu()
    {
        var menu = new ContextMenu { ItemsSource = BuildMenu() };
        menu.Open(this);
    }

    private static MenuItem Item(string header, Action action, bool enabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled };
        item.Click += (s, e) => action();
        return item;
    }

    private static MenuItem Item(string header, Func<Task> action, bool enabled = true)
    {
        return Item(header, () => { _ = action(); }, enabled);
    }

    private static MenuItem Check(string header, bool isChecked, Action action)
    {
        var item = new MenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = isChecked
        };
        item.Click += (s, e) => action();
        return item;
    }

    private static MenuItem Submenu(string header, IEnumerable<object> items)
    {
        return new MenuItem { Header = header, ItemsSource = items.ToList() };
    }

    private List<object> BuildMenu()
    {
        return
        [
            Item("Edit Splits...", EditSplits),
            Submenu("Open Splits", BuildOpenSplitsMenu()),
            Item("Save Splits", () => SaveSplits(true)),
            Item("Save Splits As...", () => SaveSplitsAs(true)),
            Item("Close Splits", CloseSplits),
            new Separator(),
            Submenu("Control", BuildControlMenu()),
            Submenu("Compare Against", BuildComparisonsMenu()),
            .. BuildRaceMenus(),
            new Separator(),
            Item("Edit Layout...", EditLayout),
            Submenu("Open Layout", BuildOpenLayoutMenu()),
            Item("Save Layout", SaveLayout),
            Item("Save Layout As...", SaveLayoutAs),
            new Separator(),
            Item("Settings", EditSettings),
            new Separator(),
            Item("About", ShowAbout),
            Item("Exit", Close)
        ];
    }

    private IEnumerable<object> BuildOpenSplitsMenu()
    {
        yield return Item("From File...", OpenSplits);

        List<RecentSplitsFile> recent = [.. Settings.RecentSplits
            .Where(x => !string.IsNullOrEmpty(x.Path))
            .Reverse()
            .DistinctBy(x => x.Path)
            .Take(10)];

        if (recent.Count > 0)
        {
            yield return new Separator();
            foreach (RecentSplitsFile file in recent)
            {
                string title = string.IsNullOrEmpty(file.GameName)
                    ? Path.GetFileName(file.Path)
                    : $"{file.GameName} - {file.CategoryName}";
                string path = file.Path;
                yield return Item(title.Replace("_", "__"), () => OpenRunFromFile(path), File.Exists(path));
            }
        }
    }

    private IEnumerable<object> BuildOpenLayoutMenu()
    {
        yield return Item("From File...", OpenLayout);
        yield return Item("Default", LoadDefaultLayout);

        List<string> recent = [.. Settings.RecentLayouts.Where(x => !string.IsNullOrEmpty(x)).Reverse().Distinct().Take(10)];
        if (recent.Count > 0)
        {
            yield return new Separator();
            foreach (string path in recent)
            {
                string layoutPath = path;
                yield return Item(Path.GetFileNameWithoutExtension(path).Replace("_", "__"), () => OpenLayoutFromFile(layoutPath), File.Exists(path));
            }
        }
    }

    private IEnumerable<object> BuildControlMenu()
    {
        TimerPhase phase = CurrentState.CurrentPhase;
        string splitHeader = phase switch
        {
            TimerPhase.NotRunning => "Start",
            TimerPhase.Paused => "Resume",
            _ => "Split"
        };

        yield return Item(splitHeader, StartOrSplit, phase != TimerPhase.Ended);
        yield return Item("Reset", Reset, phase != TimerPhase.NotRunning);
        yield return Item("Undo Split", Model.UndoSplit, phase != TimerPhase.NotRunning && CurrentState.CurrentSplitIndex > 0);
        yield return Item("Skip Split", Model.SkipSplit, phase is TimerPhase.Running or TimerPhase.Paused && CurrentState.CurrentSplitIndex < CurrentState.Run.Count - 1);
        yield return Item("Pause", Model.Pause, phase == TimerPhase.Running);
        yield return Item("Undo All Pauses", Model.UndoAllPauses, phase != TimerPhase.NotRunning && CurrentState.PauseTime.HasValue);
        yield return new Separator();

        HotkeyProfile profile = Settings.HotkeyProfiles[CurrentState.CurrentHotkeyProfile];
        yield return Check(Hook.IsUnavailable ? "Global Hotkeys (unavailable)" : "Global Hotkeys", profile.GlobalHotkeysEnabled,
            () => profile.GlobalHotkeysEnabled = !profile.GlobalHotkeysEnabled);

        if (Settings.HotkeyProfiles.Count > 1)
        {
            yield return Submenu("Hotkey Profile", Settings.HotkeyProfiles.Keys.Select(name =>
                (object)Check(name, name == CurrentState.CurrentHotkeyProfile, () =>
                {
                    CurrentState.CurrentHotkeyProfile = name;
                    Settings.RegisterHotkeys(Hook, name);
                })));
        }

        IEnumerable<IDictionary<string, Action>> componentControls = Layout.Components
            .Select(x => x.ContextMenuControls)
            .Where(x => x is { Count: > 0 });

        foreach (IDictionary<string, Action> section in componentControls)
        {
            yield return new Separator();
            foreach (KeyValuePair<string, Action> control in section)
            {
                yield return Item(control.Key, control.Value);
            }
        }
    }

    private IEnumerable<object> BuildComparisonsMenu()
    {
        foreach (string comparison in CurrentState.Run.CustomComparisons)
        {
            string name = comparison;
            yield return Check(name.Replace("_", "__"), CurrentState.CurrentComparison == name, () => SwitchComparison(name));
        }

        if (CurrentState.Run.ComparisonGenerators.Count > 0)
        {
            yield return new Separator();
        }

        foreach (IComparisonGenerator generator in CurrentState.Run.ComparisonGenerators)
        {
            string name = generator.Name;
            yield return Check(name, CurrentState.CurrentComparison == name, () => SwitchComparison(name));
        }

        yield return new Separator();
        yield return Check("Real Time", CurrentState.CurrentTimingMethod == TimingMethod.RealTime,
            () => CurrentState.CurrentTimingMethod = TimingMethod.RealTime);
        yield return Check("Game Time", CurrentState.CurrentTimingMethod == TimingMethod.GameTime,
            () => CurrentState.CurrentTimingMethod = TimingMethod.GameTime);
    }

    private async Task EditSplits()
    {
        IsInDialogMode = true;
        try
        {
            var runCopy = (IRun)CurrentState.Run.Clone();
            bool accepted = await SplitsEditorWindow.Show(this, CurrentState, Model);
            if (accepted)
            {
                CurrentState.CallRunManuallyModified();
                RegenerateComparisons();
                SwitchComparison(CurrentState.CurrentComparison);

                // Editing splits leaves timer-only mode and brings up a full layout.
                await WarnAndRemoveTimerOnly(true);
            }
            else
            {
                SetRun(runCopy);
            }

            InvalidationRequired = true;
        }
        finally
        {
            IsInDialogMode = false;
        }
    }

    private async Task EditLayout()
    {
        IsInDialogMode = true;
        try
        {
            StoreWindowGeometryInLayout();
            var layoutCopy = (ILayout)Layout.Clone();
            var layoutXml = new System.Xml.XmlDocument();
            using (var stream = new MemoryStream())
            {
                LayoutSaver.Save(Layout, stream);
                stream.Position = 0;
                layoutXml.Load(stream);
            }

            bool accepted = await LayoutEditorWindow.Show(this, CurrentState, () =>
            {
                ComponentRenderer.VisibleComponents = [.. Layout.Components];
                InvalidationRequired = true;
            });

            if (accepted)
            {
                Layout.HasChanged = true;
                InTimerOnlyMode = false;
                SetLayout(Layout);
            }
            else
            {
                // Restore the layout as it was before editing, including component settings.
                using var stream = new MemoryStream();
                layoutXml.Save(stream);
                stream.Position = 0;
                ILayout restored = new UI.LayoutFactories.XMLLayoutFactory(stream).Create(CurrentState);
                restored.FilePath = layoutCopy.FilePath;
                restored.HasChanged = layoutCopy.HasChanged;
                SetLayout(restored);
            }
        }
        finally
        {
            IsInDialogMode = false;
        }
    }

    private async Task EditSettings()
    {
        IsInDialogMode = true;
        try
        {
            var oldSettings = (ISettings)Settings.Clone();
            Settings.UnregisterAllHotkeys(Hook);

            string profile = await SettingsWindow.Show(this, Settings, CurrentState.CurrentHotkeyProfile, Hook);
            if (profile == null)
            {
                CurrentState.Settings = Settings = oldSettings;
                RegenerateComparisons();
            }
            else
            {
                CurrentState.CurrentHotkeyProfile = profile;
                SwitchComparisonGenerators();
                SaveSettingsToDisk();
            }

            Settings.RegisterHotkeys(Hook, CurrentState.CurrentHotkeyProfile);
            refreshTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, Settings.RefreshRate));
        }
        finally
        {
            IsInDialogMode = false;
        }
    }

    private async Task ShowAbout()
    {
        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";
        await MessageBox.Show(this,
            $"LiveSplit {version} (cross-platform preview)\n\n" +
            "A sleek, highly customizable timer for speedrunners.\n" +
            "https://livesplit.org\n\n" +
            $"Running on {System.Runtime.InteropServices.RuntimeInformation.OSDescription} " +
            $"with .NET {Environment.Version}.\n" +
            $"Settings: {AppPaths.SettingsPath}",
            "About LiveSplit");
    }
}
