using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.Input;
using LiveSplit.Model.RunFactories;
using LiveSplit.Model.RunSavers;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.Options.SettingsSavers;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.LayoutFactories;
using LiveSplit.UI.LayoutSavers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DialogResult = LiveSplit.UI.DialogResult;
using IComponent = LiveSplit.UI.Components.IComponent;
using MessageBox = LiveSplit.UI.MessageBox;
using MessageBoxButtons = LiveSplit.UI.MessageBoxButtons;

namespace LiveSplit.View;

/// <summary>
/// The main timer window: a borderless, optionally transparent window that renders the layout
/// and hosts the right-click menu. Ported from LiveSplit.View's TimerForm.
/// </summary>
public partial class TimerWindow : UI.Portable.Form
{
    public ISettings Settings { get; set; }
    public ILayout Layout { get; set; }
    public LiveSplitState CurrentState { get; set; }
    public ITimerModel Model { get; set; }

    protected ComponentRenderer ComponentRenderer { get; } = new();
    protected IComparisonGeneratorsFactory ComparisonGeneratorsFactory { get; } = new StandardComparisonGeneratorsFactory();
    protected StandardFormatsRunFactory RunFactory { get; } = new();
    protected IRunSaver RunSaver { get; } = new XMLRunSaver();
    protected ILayoutSaver LayoutSaver { get; } = new XMLLayoutSaver();
    protected ISettingsSaver SettingsSaver { get; } = new XMLSettingsSaver();

    protected CompositeHook Hook { get; set; }

    protected bool InTimerOnlyMode { get; set; }
    protected bool ResetMessageShown { get; set; }
    protected bool IsInDialogMode { get; set; }
    protected bool InvalidationRequired { get; set; }

    private readonly TimerCanvas canvas;
    private readonly DispatcherTimer refreshTimer;
    private readonly Invalidator invalidator = new();
    private readonly GraphicsCache globalCache = new();
    private float oldSize = -1;
    private int refreshesRemaining;
    private bool closeConfirmed;

    public TimerWindow(string splitsPath = null, string layoutPath = null)
    {
        Title = "LiveSplit";
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://LiveSplit.Avalonia/Assets/Icon.ico")));
        WindowDecorations = WindowDecorations.None;
        CanResize = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        WindowStartupLocation = WindowStartupLocation.Manual;
        MinWidth = 25;
        MinHeight = 25;

        canvas = new TimerCanvas(this);
        Content = canvas;

        Init(splitsPath, layoutPath);

        refreshTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, Settings.RefreshRate)), DispatcherPriority.Render, (s, e) => TimerElapsed());
        refreshTimer.Start();

        Closing += TimerWindow_Closing;
    }

    private void Init(string splitsPath, string layoutPath)
    {
        CurrentState = new LiveSplitState(null, this, null, null, null);
        Model = new DoubleTapPrevention(new TimerModel());

        LoadSettings();

        CurrentState.CurrentHotkeyProfile = Settings.HotkeyProfiles.First().Key;

        IRun timerOnlyRun = new StandardRunFactory().Create(ComparisonGeneratorsFactory);
        IRun run = timerOnlyRun;
        try
        {
            if (!string.IsNullOrEmpty(splitsPath))
            {
                UpdateStateFromSplitsPath(splitsPath);
                run = LoadRunFromFile(splitsPath);
            }
            else if (Settings.RecentSplits.Count > 0)
            {
                RecentSplitsFile lastSplitFile = Settings.RecentSplits[^1];
                if (!string.IsNullOrEmpty(lastSplitFile.Path) && File.Exists(lastSplitFile.Path))
                {
                    UpdateStateFromSplitsPath(lastSplitFile.Path);
                    run = LoadRunFromFile(lastSplitFile.Path);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
            run = timerOnlyRun;
        }

        run.FixSplits();
        run.AutoSplitter = AutoSplitter.PreserveSettings(run.AutoSplitterSettings);
        CurrentState.Run = run;
        CurrentState.Settings = Settings;

        try
        {
            if (!string.IsNullOrEmpty(layoutPath))
            {
                Layout = LoadLayoutFromFile(layoutPath);
            }
            else if (Settings.RecentLayouts.Count > 0
                && !string.IsNullOrEmpty(Settings.RecentLayouts[^1])
                && File.Exists(Settings.RecentLayouts[^1]))
            {
                Layout = LoadLayoutFromFile(Settings.RecentLayouts[^1]);
            }
            else if (run == timerOnlyRun)
            {
                Layout = CreateTimerOnlyLayout();
            }
            else
            {
                Layout = CreateDefaultLayout();
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
            Layout = CreateDefaultLayout();
        }

        InTimerOnlyMode = run == timerOnlyRun;

        CurrentState.LayoutSettings = Layout.Settings;

        SwitchComparisonGenerators();
        SwitchComparison(Settings.LastComparison);
        Model.CurrentState = CurrentState;

        CurrentState.OnReset += CurrentState_OnReset;

        SetLayout(Layout);

        Hook = new CompositeHook(false);
        Hook.KeyOrButtonPressed += Hook_KeyOrButtonPressed;
        Settings.RegisterHotkeys(Hook, CurrentState.CurrentHotkeyProfile);

        InitRaceProviders();
    }

    #region Layouts

    private ILayout CreateDefaultLayout()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LiveSplit.DefaultLayout.lsl");
        ILayout layout = new XMLLayoutFactory(stream).Create(CurrentState);
        layout.X = layout.Y = 100;
        return layout;
    }

    private ILayout CreateTimerOnlyLayout()
    {
        var layout = new Layout
        {
            VerticalWidth = 252,
            VerticalHeight = 50,
            HorizontalWidth = 252,
            HorizontalHeight = 50,
            X = 100,
            Y = 100,
            Mode = LayoutMode.Vertical,
            Settings = new StandardLayoutSettingsFactory().Create()
        };
        layout.LayoutComponents.Add(ComponentManager.LoadLayoutComponent("LiveSplit.Timer.dll", CurrentState));
        return layout;
    }

    private ILayout LoadLayoutFromFile(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        ILayout layout = new XMLLayoutFactory(stream).Create(CurrentState);
        layout.FilePath = filePath;
        Settings.AddToRecentLayouts(filePath);
        return layout;
    }

    private void SetLayout(ILayout layout)
    {
        if (Layout != null && Layout != layout)
        {
            foreach (IComponent component in Layout.Components.Except(layout.Components))
            {
                component.Dispose();
            }
        }

        Layout = layout;
        CurrentState.Layout = layout;
        ComponentRenderer.VisibleComponents = [.. Layout.Components];
        CurrentState.LayoutSettings = layout.Settings;
        UpdateRefreshesRemaining();

        if (Layout.Mode == LayoutMode.Vertical)
        {
            if (Layout.VerticalWidth != UI.Layout.InvalidSize && Layout.VerticalHeight != UI.Layout.InvalidSize)
            {
                Width = Layout.VerticalWidth;
                Height = Layout.VerticalHeight;
            }
        }
        else if (Layout.HorizontalWidth != UI.Layout.InvalidSize && Layout.HorizontalHeight != UI.Layout.InvalidSize)
        {
            Width = Layout.HorizontalWidth;
            Height = Layout.HorizontalHeight;
        }

        Position = KeepOnScreen(new PixelPoint(Layout.X, Layout.Y));
        Topmost = Layout.Settings.AlwaysOnTop;
        oldSize = -1;
        InvalidationRequired = true;
    }

    /// <summary>
    /// Moves the window back onto a screen if the saved position would leave it invisible.
    /// </summary>
    private PixelPoint KeepOnScreen(PixelPoint position)
    {
        const int MinimumVisibleSize = 32;
        IReadOnlyList<Screen> screens = Screens?.All ?? [];
        if (screens.Count == 0)
        {
            return position;
        }

        var bounds = new PixelRect(position, PixelSize.FromSize(new Size(Width, Height), 1.0));
        bool visible = screens.Any(screen =>
        {
            PixelRect intersection = screen.Bounds.Intersect(bounds);
            return intersection.Width >= Math.Min(MinimumVisibleSize, bounds.Width)
                && intersection.Height >= Math.Min(MinimumVisibleSize, bounds.Height);
        });

        if (visible)
        {
            return position;
        }

        PixelRect workingArea = (Screens.ScreenFromBounds(bounds) ?? Screens.Primary ?? screens[0]).WorkingArea;
        return new PixelPoint(
            Math.Max(workingArea.X, Math.Min(bounds.X, workingArea.Right - bounds.Width)),
            Math.Max(workingArea.Y, Math.Min(bounds.Y, workingArea.Bottom - bounds.Height)));
    }

    public async Task<bool> OpenLayoutFromFile(string filePath)
    {
        if (!await WarnUserAboutLayoutSave(true))
        {
            return false;
        }

        try
        {
            SetLayout(LoadLayoutFromFile(filePath));
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e);
            await MessageBox.Show(this, "The selected file was not recognized as a layout file. (" + e.Message + ")", "Error");
            return false;
        }
    }

    public async Task LoadDefaultLayout()
    {
        if (await WarnUserAboutLayoutSave(true))
        {
            ILayout layout = CreateDefaultLayout();
            layout.X = Position.X;
            layout.Y = Position.Y;
            SetLayout(layout);
            Settings.AddToRecentLayouts("");
        }
    }

    private void StoreWindowGeometryInLayout()
    {
        if (Layout.Mode == LayoutMode.Vertical)
        {
            Layout.VerticalWidth = (int)Math.Round(Width);
            Layout.VerticalHeight = (int)Math.Round(Height);
        }
        else
        {
            Layout.HorizontalWidth = (int)Math.Round(Width);
            Layout.HorizontalHeight = (int)Math.Round(Height);
        }

        Layout.X = Position.X;
        Layout.Y = Position.Y;
    }

    public async Task<bool> SaveLayout()
    {
        StoreWindowGeometryInLayout();

        string savePath = Layout.FilePath;
        if (savePath == null)
        {
            return await SaveLayoutAs();
        }

        try
        {
            using var memoryStream = new MemoryStream();
            LayoutSaver.Save(Layout, memoryStream);
            await File.WriteAllBytesAsync(savePath, memoryStream.ToArray());
            Layout.HasChanged = false;
            Settings.AddToRecentLayouts(savePath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            await MessageBox.Show(this, "Layout could not be saved!", "Save Failed");
            return false;
        }
    }

    public async Task<bool> SaveLayoutAs()
    {
        IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Layout As",
            DefaultExtension = "lsl",
            SuggestedFileName = "Layout.lsl",
            FileTypeChoices = [FileTypes.Layouts, FilePickerFileTypes.All]
        });

        string path = file?.TryGetLocalPath();
        if (path == null)
        {
            return false;
        }

        Layout.FilePath = path;
        return await SaveLayout();
    }

    private async Task<bool> WarnUserAboutLayoutSave(bool canCancel)
    {
        if (!Layout.HasChanged)
        {
            return true;
        }

        DialogResult result = await MessageBox.Show(this,
            "Your layout has been updated but not yet saved.\nDo you want to save your layout now?",
            "Save Layout?",
            canCancel ? MessageBoxButtons.YesNoCancel : MessageBoxButtons.YesNo);

        return result switch
        {
            DialogResult.Yes => await SaveLayout(),
            DialogResult.Cancel => false,
            _ => true
        };
    }

    private async Task<bool> WarnAndRemoveTimerOnly(bool canCancel)
    {
        if (!InTimerOnlyMode)
        {
            return true;
        }

        if (!await WarnUserAboutLayoutSave(canCancel))
        {
            return false;
        }

        InTimerOnlyMode = false;
        ILayout layout;
        try
        {
            string lastLayoutPath = Settings.RecentLayouts.LastOrDefault(x => !string.IsNullOrEmpty(x) && File.Exists(x));
            layout = lastLayoutPath != null ? LoadLayoutFromFile(lastLayoutPath) : CreateDefaultLayout();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            layout = CreateDefaultLayout();
        }

        layout.X = Position.X;
        layout.Y = Position.Y;
        SetLayout(layout);
        return true;
    }

    #endregion

    #region Splits

    private IRun LoadRunFromFile(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        RunFactory.Stream = stream;
        RunFactory.FilePath = filePath;
        IRun run = RunFactory.Create(ComparisonGeneratorsFactory);
        Settings.AddToRecentSplits(filePath, run, CurrentState.CurrentTimingMethod, CurrentState.CurrentHotkeyProfile);
        return run;
    }

    private void UpdateStateFromSplitsPath(string filePath)
    {
        RecentSplitsFile recentSplitsFile = Settings.RecentSplits.LastOrDefault(splitsFile => splitsFile.Path == filePath);
        if (recentSplitsFile.Path != null)
        {
            CurrentState.CurrentTimingMethod = recentSplitsFile.LastTimingMethod;
            if (Settings.HotkeyProfiles.ContainsKey(recentSplitsFile.LastHotkeyProfile ?? ""))
            {
                CurrentState.CurrentHotkeyProfile = recentSplitsFile.LastHotkeyProfile;
                if (Hook != null)
                {
                    Settings.RegisterHotkeys(Hook, CurrentState.CurrentHotkeyProfile);
                }
            }
        }
    }

    private void AddCurrentSplitsToLRU()
    {
        if (CurrentState.Run != null && Settings.RecentSplits.Any(x => x.Path == CurrentState.Run.FilePath))
        {
            Settings.AddToRecentSplits(CurrentState.Run.FilePath, CurrentState.Run, CurrentState.CurrentTimingMethod, CurrentState.CurrentHotkeyProfile);
        }
    }

    public void SetRun(IRun run)
    {
        run.ComparisonGenerators = [.. CurrentState.Run.ComparisonGenerators];
        foreach (IComparisonGenerator generator in run.ComparisonGenerators)
        {
            generator.Run = run;
        }

        run.FixSplits();
        run.AutoSplitter ??= AutoSplitter.PreserveSettings(run.AutoSplitterSettings);
        CurrentState.Run = run;
        InvalidationRequired = true;
        RegenerateComparisons();
        SwitchComparison(CurrentState.CurrentComparison);
        UpdateRefreshesRemaining();

        if (!string.IsNullOrEmpty(run.LayoutPath))
        {
            if (run.LayoutPath == "?default")
            {
                _ = LoadDefaultLayout();
            }
            else if (CurrentState.Layout.FilePath != run.LayoutPath && File.Exists(run.LayoutPath))
            {
                _ = OpenLayoutFromFile(run.LayoutPath);
            }
        }
    }

    public async Task<bool> OpenRunFromFile(string filePath)
    {
        try
        {
            if (!await WarnUserAboutSplitsSave())
            {
                return false;
            }

            if (!await WarnAndRemoveTimerOnly(true))
            {
                return false;
            }

            AddCurrentSplitsToLRU();
            UpdateStateFromSplitsPath(filePath);
            IRun run = LoadRunFromFile(filePath);
            SetRun(run);
            CurrentState.CallRunManuallyModified();
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e);
            await MessageBox.Show(this, "The selected file was not recognized as a splits file.\n\n" + e.Message, "Error");
            return false;
        }
    }

    private async Task OpenSplits()
    {
        IStorageFolder startLocation = null;
        if (Settings.RecentSplits.Count > 0 && !string.IsNullOrEmpty(Settings.RecentSplits[^1].Path))
        {
            startLocation = await StorageProvider.TryGetFolderFromPathAsync(Path.GetDirectoryName(Settings.RecentSplits[^1].Path));
        }

        IsInDialogMode = true;
        try
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Splits",
                SuggestedStartLocation = startLocation,
                FileTypeFilter = [FileTypes.Splits, FilePickerFileTypes.All]
            });

            string path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path != null)
            {
                await OpenRunFromFile(path);
            }
        }
        finally
        {
            IsInDialogMode = false;
        }
    }

    private async Task OpenLayout()
    {
        IsInDialogMode = true;
        try
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Layout",
                FileTypeFilter = [FileTypes.Layouts, FilePickerFileTypes.All]
            });

            string path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path != null)
            {
                await OpenLayoutFromFile(path);
            }
        }
        finally
        {
            IsInDialogMode = false;
        }
    }

    public async Task<bool> SaveSplitsAs(bool promptPBMessage)
    {
        IsInDialogMode = true;
        try
        {
            IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Splits As",
                DefaultExtension = "lss",
                SuggestedFileName = CurrentState.Run.GetExtendedFileName(),
                FileTypeChoices = [FileTypes.Splits]
            });

            string path = file?.TryGetLocalPath();
            if (path == null)
            {
                return false;
            }

            if (!path.EndsWith(".lss", StringComparison.OrdinalIgnoreCase))
            {
                await MessageBox.Show(this, "Cannot save splits with a file type that is not .lss", "Save Failed");
                return false;
            }

            CurrentState.Run.FilePath = path;
            return await SaveSplits(promptPBMessage);
        }
        finally
        {
            IsInDialogMode = false;
        }
    }

    public async Task<bool> SaveSplits(bool promptPBMessage)
    {
        string savePath = CurrentState.Run.FilePath;
        if (savePath == null)
        {
            return await SaveSplitsAs(promptPBMessage);
        }

        CurrentState.Run.FixSplits();

        TimingMethod method = CurrentState.CurrentTimingMethod;
        DialogResult result = DialogResult.No;
        if (promptPBMessage && ((CurrentState.CurrentPhase == TimerPhase.Ended
            && CurrentState.Run[^1].PersonalBestSplitTime[method] != null
            && CurrentState.Run[^1].SplitTime[method] >= CurrentState.Run[^1].PersonalBestSplitTime[method])
            || CurrentState.CurrentPhase is TimerPhase.Running or TimerPhase.Paused))
        {
            result = await MessageBox.Show(this,
                "This run did not beat your current splits. Would you like to save this run as a Personal Best?",
                "Save as Personal Best?",
                MessageBoxButtons.YesNoCancel);

            if (result == DialogResult.Yes)
            {
                Model.ResetAndSetAttemptAsPB();
            }
            else if (result == DialogResult.Cancel)
            {
                return false;
            }
        }

        LiveSplitState stateCopy = CurrentState;
        if (result == DialogResult.No)
        {
            var modelCopy = new TimerModel();
            stateCopy = CurrentState.Clone() as LiveSplitState;
            modelCopy.CurrentState = stateCopy;
            modelCopy.Reset();
        }

        try
        {
            using var memoryStream = new MemoryStream();
            RunSaver.Save(stateCopy.Run, memoryStream);
            await File.WriteAllBytesAsync(savePath, memoryStream.ToArray());
            CurrentState.Run.HasChanged = false;
            Settings.AddToRecentSplits(savePath, stateCopy.Run, CurrentState.CurrentTimingMethod, CurrentState.CurrentHotkeyProfile);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            await MessageBox.Show(this, "Splits could not be saved!", "Save Failed");
            return false;
        }
    }

    private async Task<bool> WarnUserAboutSplitsSave()
    {
        if (InTimerOnlyMode)
        {
            Model.Reset();
            return true;
        }

        bool safeToContinue = true;
        if (CurrentState.Run.HasChanged)
        {
            DialogResult result = await MessageBox.Show(this,
                "Your splits have been updated but not yet saved.\nDo you want to save your splits now?",
                "Save Splits?",
                MessageBoxButtons.YesNoCancel);

            if (result == DialogResult.Yes)
            {
                safeToContinue = await SaveSplits(false);
            }
            else if (result == DialogResult.Cancel)
            {
                return false;
            }
        }

        if (safeToContinue)
        {
            Model.Reset();
        }

        return safeToContinue;
    }

    private async Task CloseSplits()
    {
        bool needToChangeLayout = Layout.Components.Count() != 1 || Layout.Components.First().ComponentName != "Timer";
        if (!await WarnUserAboutSplitsSave())
        {
            return;
        }

        if (needToChangeLayout && !await WarnUserAboutLayoutSave(true))
        {
            return;
        }

        AddCurrentSplitsToLRU();
        IRun run = new StandardRunFactory().Create(ComparisonGeneratorsFactory);
        Model.Reset();
        SetRun(run);
        Settings.AddToRecentSplits("", null, TimingMethod.RealTime, CurrentState.CurrentHotkeyProfile);
        InTimerOnlyMode = true;
        if (needToChangeLayout)
        {
            ILayout layout = CreateTimerOnlyLayout();
            layout.Settings = Layout.Settings;
            layout.X = Position.X;
            layout.Y = Position.Y;
            layout.Mode = Layout.Mode;
            SetLayout(layout);
            Settings.AddToRecentLayouts("");
        }
    }

    #endregion

    #region Timer control

    private void CurrentState_OnReset(object sender, TimerPhase e)
    {
        RegenerateComparisons();
        if (InTimerOnlyMode)
        {
            IRun timerOnlyRun = new StandardRunFactory().Create(ComparisonGeneratorsFactory);
            timerOnlyRun.Offset = CurrentState.Run.Offset;
            SetRun(timerOnlyRun);
        }
    }

    private void StartOrSplit()
    {
        switch (CurrentState.CurrentPhase)
        {
            case TimerPhase.Running:
                Model.Split();
                break;
            case TimerPhase.Paused:
                Model.Pause();
                break;
            case TimerPhase.NotRunning:
                Model.Start();
                break;
            case TimerPhase.Ended:
                Model.Reset();
                break;
        }
    }

    private async Task<DialogResult> WarnAboutResetting()
    {
        TimingMethod method = CurrentState.CurrentTimingMethod;
        bool warnUser = false;
        for (int index = 0; index < CurrentState.Run.Count; index++)
        {
            if (LiveSplitStateHelper.CheckBestSegment(CurrentState, index, method))
            {
                warnUser = true;
                break;
            }
        }

        if ((!warnUser && CurrentState.Run[^1].SplitTime[method] != null && CurrentState.Run[^1].PersonalBestSplitTime[method] == null)
            || CurrentState.Run[^1].SplitTime[method] < CurrentState.Run[^1].PersonalBestSplitTime[method])
        {
            warnUser = true;
        }

        return warnUser
            ? await MessageBox.Show(this, "You have beaten some of your best times.\nDo you want to update them?", "Update Times?", MessageBoxButtons.YesNoCancel)
            : DialogResult.Yes;
    }

    private async Task Reset()
    {
        if (ResetMessageShown)
        {
            return;
        }

        DialogResult result = DialogResult.Yes;
        if (Settings.WarnOnReset && !InTimerOnlyMode)
        {
            ResetMessageShown = true;
            try
            {
                result = await WarnAboutResetting();
            }
            finally
            {
                ResetMessageShown = false;
            }
        }

        if (result == DialogResult.Yes)
        {
            Model.Reset();
        }
        else if (result == DialogResult.No)
        {
            Model.Reset(false);
        }
    }

    private void Hook_KeyOrButtonPressed(object sender, KeyOrButton e)
    {
        Dispatcher.UIThread.Post(() => HandleHotkey(e, fromGlobalHook: true));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // When the global hook is running it already sees every key press, so the window only
        // needs to handle keys itself when global hooks are unavailable (e.g. on Wayland).
        if (Hook == null || Hook.IsUnavailable)
        {
            Keys key = KeyMapping.FromAvalonia(e.Key, e.KeyModifiers);
            if (key != Keys.None)
            {
                HandleHotkey(new KeyOrButton(key), fromGlobalHook: false);
            }
        }
    }

    private void HandleHotkey(KeyOrButton e, bool fromGlobalHook)
    {
        try
        {
            if (!Settings.HotkeyProfiles.TryGetValue(CurrentState.CurrentHotkeyProfile, out HotkeyProfile hotkeyProfile))
            {
                return;
            }

            bool active = !fromGlobalHook || IsActive || hotkeyProfile.GlobalHotkeysEnabled;
            if (active && !ResetMessageShown && !IsInDialogMode && !HasOpenDialogs)
            {
                if (hotkeyProfile.SplitKey == e)
                {
                    Delayed(hotkeyProfile.HotkeyDelay, StartOrSplit);
                }
                else if (hotkeyProfile.UndoKey == e)
                {
                    Model.UndoSplit();
                }
                else if (hotkeyProfile.SkipKey == e)
                {
                    Model.SkipSplit();
                }
                else if (hotkeyProfile.ResetKey == e)
                {
                    _ = Reset();
                }
                else if (hotkeyProfile.PauseKey == e)
                {
                    Delayed(hotkeyProfile.HotkeyDelay, Model.Pause);
                }
                else if (hotkeyProfile.SwitchComparisonPrevious == e)
                {
                    Model.SwitchComparisonPrevious();
                }
                else if (hotkeyProfile.SwitchComparisonNext == e)
                {
                    Model.SwitchComparisonNext();
                }
            }

            if (hotkeyProfile.ToggleGlobalHotkeys == e)
            {
                hotkeyProfile.GlobalHotkeysEnabled = !hotkeyProfile.GlobalHotkeysEnabled;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private bool HasOpenDialogs => OwnedWindows.Count > 0;

    private static void Delayed(float delaySeconds, Action action)
    {
        if (delaySeconds > 0)
        {
            DispatcherTimer.RunOnce(action, TimeSpan.FromSeconds(delaySeconds));
        }
        else
        {
            action();
        }
    }

    #endregion

    #region Comparisons

    private void RegenerateComparisons()
    {
        if (CurrentState?.Run != null)
        {
            foreach (IComparisonGenerator generator in CurrentState.Run.ComparisonGenerators)
            {
                generator.Generate(CurrentState.Settings);
            }
        }
    }

    private void SwitchComparisonGenerators()
    {
        IEnumerable<IComparisonGenerator> allGenerators = new StandardComparisonGeneratorsFactory().GetAllGenerators(CurrentState.Run);
        foreach (IComparisonGenerator generator in allGenerators)
        {
            IComparisonGenerator generatorInRun = CurrentState.Run.ComparisonGenerators.FirstOrDefault(x => x.Name == generator.Name);
            if (generatorInRun != null)
            {
                CurrentState.Run.ComparisonGenerators.Remove(generatorInRun);
            }

            if (Settings.ComparisonGeneratorStates.TryGetValue(generator.Name, out bool enabled) && enabled)
            {
                CurrentState.Run.ComparisonGenerators.Add(generator);
            }
        }

        SwitchComparison(CurrentState.CurrentComparison);
        RegenerateComparisons();
    }

    private void SwitchComparison(string name)
    {
        if (name == null || !CurrentState.Run.Comparisons.Contains(name))
        {
            name = Run.PersonalBestComparisonName;
        }

        CurrentState.CurrentComparison = name;
    }

    #endregion

    #region Settings

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsPath))
            {
                using FileStream stream = File.OpenRead(AppPaths.SettingsPath);
                Settings = new XMLSettingsFactory(stream).Create();
                return;
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        Settings = new StandardSettingsFactory().Create();
    }

    private bool SaveSettingsToDisk()
    {
        try
        {
            using var memoryStream = new MemoryStream();
            SettingsSaver.Save(Settings, memoryStream);
            File.WriteAllBytes(AppPaths.SettingsPath, memoryStream.ToArray());
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return false;
        }
    }

    #endregion

    #region Rendering and window sizing

    private void UpdateRefreshesRemaining()
    {
        refreshesRemaining = 5;
    }

    /// <summary>
    /// Runs one refresh cycle immediately (used by tests).
    /// </summary>
    internal void Tick()
    {
        TimerElapsed();
    }

    private void TimerElapsed()
    {
        try
        {
            KeepLayoutSize();
            FixSize();
            MaintainMinimumSize();

            Topmost = Layout.Settings.AlwaysOnTop;
            Opacity = Math.Clamp(Layout.Settings.Opacity, 0.05, 1.0);

            if (refreshesRemaining > 0 || InvalidationRequired)
            {
                InvalidationRequired = false;
                InvalidateForm();
                return;
            }

            globalCache.Restart();
            globalCache["LayoutHashCode"] = new XMLLayoutSaver().CreateLayoutNode(null, null, Layout);
            if (globalCache.HasChanged)
            {
                InvalidateForm();
                return;
            }

            invalidator.Restart();
            ComponentRenderer.Update(invalidator, CurrentState, (float)Width, (float)Height, Layout.Mode);
            if (invalidator.HasInvalidated)
            {
                canvas.InvalidateVisual();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            canvas.InvalidateVisual();
        }
    }

    protected void InvalidateForm()
    {
        foreach (IComponent component in Layout.Components)
        {
            try
            {
                component.Update(null, CurrentState, (float)Width, (float)Height, Layout.Mode);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        canvas.InvalidateVisual();
    }

    /// <summary>
    /// Keeps the scale of the layout constant when components change size by growing or
    /// shrinking the window along the layout direction.
    /// </summary>
    private void FixSize()
    {
        ComponentRenderer.VisibleComponents = [.. Layout.Components];
        ComponentRenderer.CalculateOverallSize(Layout.Mode);
        float currentSize = ComponentRenderer.OverallSize;

        if (refreshesRemaining <= 0)
        {
            if (oldSize > 0 && oldSize != currentSize)
            {
                if (Layout.Mode == LayoutMode.Vertical)
                {
                    Height = Math.Round(currentSize / oldSize * Height);
                }
                else
                {
                    Width = Math.Round(currentSize / oldSize * Width);
                }
            }

            oldSize = currentSize;
            int minSize = (int)((currentSize / 5) + 0.5f);
            if (Layout.Mode == LayoutMode.Vertical)
            {
                MinWidth = 25;
                MinHeight = Math.Max(minSize, 25);
            }
            else
            {
                MinWidth = Math.Max(minSize, 25);
                MinHeight = 25;
            }
        }
    }

    private void KeepLayoutSize()
    {
        if (refreshesRemaining <= 0)
        {
            return;
        }

        if (Layout.Mode == LayoutMode.Vertical)
        {
            if (Layout.VerticalWidth > 0 && Layout.VerticalHeight > 0)
            {
                Width = Layout.VerticalWidth;
                Height = Layout.VerticalHeight;
            }
        }
        else if (Layout.HorizontalWidth > 0 && Layout.HorizontalHeight > 0)
        {
            Width = Layout.HorizontalWidth;
            Height = Layout.HorizontalHeight;
        }

        ComponentRenderer.CalculateOverallSize(Layout.Mode);
        if (oldSize != ComponentRenderer.OverallSize)
        {
            UpdateRefreshesRemaining();
        }
        else
        {
            refreshesRemaining--;
        }

        oldSize = ComponentRenderer.OverallSize;
    }

    private void MaintainMinimumSize()
    {
        if (ComponentRenderer.OverallSize <= 0)
        {
            return;
        }

        if (Layout.Mode == LayoutMode.Vertical)
        {
            double minimumWidth = ComponentRenderer.MinimumWidth * (Height / ComponentRenderer.OverallSize);
            if (Width < minimumWidth)
            {
                Height = Math.Round(Height / (minimumWidth / Width));
            }
        }
        else
        {
            double minimumHeight = ComponentRenderer.MinimumHeight * (Width / ComponentRenderer.OverallSize);
            if (Height < minimumHeight)
            {
                Width = Math.Round(Width / (minimumHeight / Height));
            }
        }
    }

    internal void Paint(DrawingContext g, Size size)
    {
        float width = (float)size.Width;
        float height = (float)size.Height;
        if (width <= 0 || height <= 0 || Layout == null)
        {
            return;
        }

        DrawBackground(g, width, height);

        ComponentRenderer.VisibleComponents = [.. Layout.Components];
        ComponentRenderer.CalculateOverallSize(Layout.Mode);
        float scaleFactor = Layout.Mode == LayoutMode.Vertical
            ? height / ComponentRenderer.OverallSize
            : width / ComponentRenderer.OverallSize;

        float transformedWidth = width;
        float transformedHeight = height;
        if (Layout.Mode == LayoutMode.Vertical)
        {
            transformedWidth /= scaleFactor;
        }
        else
        {
            transformedHeight /= scaleFactor;
        }

        using (g.PushTransform(Matrix.CreateScale(scaleFactor, scaleFactor)))
        {
            ComponentRenderer.Render(g, CurrentState, transformedWidth, transformedHeight, Layout.Mode, scaleFactor * RenderScaling);
        }
    }

    private void DrawBackground(DrawingContext g, float width, float height)
    {
        LayoutSettings settings = Layout.Settings;
        if (settings.BackgroundType == BackgroundType.Image)
        {
            Avalonia.Media.Imaging.Bitmap bitmap = settings.BackgroundImage?.ToBitmap();
            if (bitmap != null)
            {
                // Crop the image to the window's aspect ratio, like the Windows version.
                double imageWidth = bitmap.Size.Width;
                double imageHeight = bitmap.Size.Height;
                double croppedWidth = imageWidth;
                double croppedHeight = imageHeight;
                if (imageWidth / imageHeight > width / height)
                {
                    croppedWidth = imageHeight * (width / height);
                }
                else
                {
                    croppedHeight = imageWidth * (height / width);
                }

                using (g.PushOpacity(Math.Clamp(settings.ImageOpacity, 0, 1)))
                {
                    g.DrawImage(bitmap,
                        new Rect((imageWidth - croppedWidth) / 2, (imageHeight - croppedHeight) / 2, croppedWidth, croppedHeight),
                        new Rect(0, 0, width, height));
                }
            }
        }
        else
        {
            g.FillGradient(
                settings.BackgroundColor,
                settings.BackgroundType == BackgroundType.SolidColor ? settings.BackgroundColor : settings.BackgroundColor2,
                settings.BackgroundType == BackgroundType.HorizontalGradient,
                width, height);
        }
    }

    #endregion

    #region Closing

    private async void TimerWindow_Closing(object sender, WindowClosingEventArgs e)
    {
        if (closeConfirmed)
        {
            return;
        }

        e.Cancel = true;

        if (!await CloseRaceRoom() || !await WarnUserAboutSplitsSave() || !await WarnUserAboutLayoutSave(true))
        {
            return;
        }

        Settings.LastComparison = CurrentState.CurrentComparison;
        AddCurrentSplitsToLRU();
        SaveSettingsToDisk();

        foreach (IComponent component in Layout.Components)
        {
            component.Dispose();
        }

        refreshTimer.Stop();
        Hook?.Dispose();

        closeConfirmed = true;
        Close();
    }

    #endregion
}

/// <summary>
/// The control that draws the layout. Drawing is delegated to the window.
/// </summary>
internal sealed class TimerCanvas(TimerWindow window) : Control
{
    public override void Render(DrawingContext context)
    {
        try
        {
            window.Paint(context, Bounds.Size);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }
}

internal static class FileTypes
{
    public static FilePickerFileType Splits { get; } = new("LiveSplit Splits") { Patterns = ["*.lss"] };
    public static FilePickerFileType Layouts { get; } = new("LiveSplit Layout") { Patterns = ["*.lsl"] };
}
