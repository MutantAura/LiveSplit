using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.Themes;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.LayoutFactories;
using LiveSplit.View;
using System.IO;
using System.Linq;
using Xunit;

namespace LiveSplit.Tests;

public class ThemeTests
{
    private static LiveSplitState CreateState()
    {
        BuiltInComponents.Register();
        string runPath = Path.Combine(Fixtures.RunFiles, "Celeste - Any% (1.2.1.5).lss");
        IRun run;
        using (FileStream stream = File.OpenRead(runPath))
        {
            run = new StandardFormatsRunFactory(stream, runPath).Create(new StandardComparisonGeneratorsFactory());
        }

        var state = new LiveSplitState(run, null, null, null, new StandardSettingsFactory().Create());
        string layoutPath = Path.Combine(Fixtures.RepoRoot, "src", "LiveSplit.View", "Resources", "DefaultLayout.lsl");
        using (FileStream stream = File.OpenRead(layoutPath))
        {
            state.Layout = new XMLLayoutFactory(stream).Create(state);
        }

        state.LayoutSettings = state.Layout.Settings;
        state.CurrentComparison = Run.PersonalBestComparisonName;
        return state;
    }

    private static void Capture(Window window, string name)
    {
        window.Show();
        for (int i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        using WriteableBitmap frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string directory = Path.Combine(Fixtures.ScreenshotDirectory, "themes");
        Directory.CreateDirectory(directory);
        frame.Save(Path.Combine(directory, name + ".png"));
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(AppTheme.Fluent, ThemeMode.Dark)]
    [InlineData(AppTheme.Fluent, ThemeMode.Light)]
    [InlineData(AppTheme.WinUI, ThemeMode.Dark)]
    [InlineData(AppTheme.WinUI, ThemeMode.Light)]
    [InlineData(AppTheme.Libadwaita, ThemeMode.Dark)]
    [InlineData(AppTheme.Libadwaita, ThemeMode.Light)]
    [InlineData(AppTheme.MacOS, ThemeMode.Dark)]
    [InlineData(AppTheme.MacOS, ThemeMode.Light)]
    [InlineData(AppTheme.Classic, ThemeMode.Dark)]
    [InlineData(AppTheme.Classic, ThemeMode.Light)]
    public void ThemedWindowsRender(AppTheme theme, ThemeMode mode)
    {
        AppTheme previousTheme = ThemeManager.Theme;
        ThemeMode previousMode = ThemeManager.Mode;
        try
        {
            ThemeManager.Apply(theme, mode);
            string suffix = $"{theme}-{mode}".ToLowerInvariant();
            LiveSplitState state = CreateState();

            Capture(new SettingsWindow(new StandardSettingsFactory().Create(), "Default", null), "settings-" + suffix);
            Capture(new SplitsEditorWindow(state, new TimerModel { CurrentState = state }, new FakeSpeedrunCom().Api), "splits-editor-" + suffix);

            var layoutEditor = new LayoutEditorWindow(state, () => { });
            Capture(layoutEditor, "layout-editor-" + suffix);
        }
        finally
        {
            ThemeManager.Apply(previousTheme, previousMode);
        }
    }

    [Theory]
    [InlineData(true, false, false, AppTheme.Fluent)]
    [InlineData(true, true, false, AppTheme.WinUI)]
    [InlineData(false, false, true, AppTheme.MacOS)]
    [InlineData(false, false, false, AppTheme.Libadwaita)]
    public void DefaultsToThePlatformTheme(bool isWindows, bool isWindows11, bool isMacOS, AppTheme expected)
    {
        Assert.Equal(expected, ThemeManager.GetPlatformDefault(isWindows, isWindows11, isMacOS));
    }

    /// <summary>
    /// Regression test: after switching from Classic to Fluent, the settings window kept
    /// Classic's white tab page and small check boxes (styles of control template parts), which
    /// made Fluent's light text unreadable in dark mode. Open windows must look like newly
    /// opened ones after switching.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(AppTheme.Classic)]
    [InlineData(AppTheme.MacOS)]
    public void SwitchingThemesRemovesThePreviousThemesTemplateStyles(AppTheme from)
    {
        AppTheme previousTheme = ThemeManager.Theme;
        ThemeMode previousMode = ThemeManager.Mode;
        try
        {
            string Describe(Window window)
            {
                Avalonia.Controls.Presenters.ContentPresenter page = window.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>()
                    .First(x => x.Name == "PART_SelectedContentHost");
                Border checkBox = window.GetVisualDescendants().OfType<Border>().First(x => x.Name == "NormalRectangle");
                Border pipe = window.GetVisualDescendants().OfType<TabItem>().First(x => x.IsSelected)
                    .GetVisualDescendants().OfType<Border>().First(x => x.Name == "PART_SelectedPipe");
                Avalonia.Controls.Presenters.ItemsPresenter tabStrip = window.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ItemsPresenter>()
                    .First(x => x.Name == "PART_ItemsPresenter");
                return $"page={page.Background} {page.BorderThickness}, check box={checkBox.Width}x{checkBox.Height}, "
                    + $"tab underline={pipe.IsVisible}, tab strip={tabStrip.HorizontalAlignment}";
            }

            Window Open()
            {
                var window = new SettingsWindow(new StandardSettingsFactory().Create(), "Default", null);
                window.Show();
                Dispatcher.UIThread.RunJobs();
                return window;
            }

            ThemeManager.Apply(AppTheme.Fluent, ThemeMode.Dark);
            Window reference = Open();
            string expected = Describe(reference);
            reference.Close();

            ThemeManager.Apply(from, ThemeMode.Dark);
            Window window = Open();
            Assert.NotEqual(expected, Describe(window));

            ThemeManager.Apply(AppTheme.Fluent, ThemeMode.Dark);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(expected, Describe(window));
            window.Close();
        }
        finally
        {
            ThemeManager.Apply(previousTheme, previousMode);
        }
    }

    /// <summary>
    /// Regression test: Classic's tabs were offset from the page frame below them. As in Windows
    /// Forms, the frame starts where the tabs do, unselected tabs stand on the frame's top line
    /// and the selected tab covers that line, joining the page.
    /// </summary>
    [AvaloniaFact]
    public void ClassicTabsJoinThePageFrame()
    {
        AppTheme previousTheme = ThemeManager.Theme;
        ThemeMode previousMode = ThemeManager.Mode;
        try
        {
            ThemeManager.Apply(AppTheme.Classic, ThemeMode.Light);
            var window = new SettingsWindow(new StandardSettingsFactory().Create(), "Default", null);
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Avalonia.Rect Bounds(Avalonia.Visual v) => new(v.TranslatePoint(default, window)!.Value, v.Bounds.Size);
            TabControl tabs = window.GetVisualDescendants().OfType<TabControl>().First();
            Avalonia.Rect page = Bounds(tabs.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First(x => x.Name == "PART_SelectedContentHost"));
            TabItem[] items = [.. tabs.GetVisualDescendants().OfType<TabItem>()];
            Avalonia.Rect selected = Bounds(items[0]);
            Assert.True(items[0].IsSelected);

            Assert.Equal(Bounds(tabs).Left, page.Left);
            Assert.Equal(page.Left, selected.Left);
            Assert.Equal(page.Top + 1, selected.Bottom);
            foreach (TabItem item in items.Skip(1))
            {
                Avalonia.Rect bounds = Bounds(item);
                Assert.Equal(page.Top, bounds.Bottom);
                Assert.True(bounds.Top > selected.Top, "Unselected tabs are lower than the selected one.");
            }

            // The tabs are drawn over the page, so the selected tab hides the frame line below it.
            Avalonia.Controls.Presenters.ItemsPresenter strip = tabs.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ItemsPresenter>().First(x => x.Name == "PART_ItemsPresenter");
            Assert.True(strip.ZIndex > 0);

            window.Close();
        }
        finally
        {
            ThemeManager.Apply(previousTheme, previousMode);
        }
    }

    [AvaloniaFact]
    public void SwitchingThemesUpdatesResources()
    {
        AppTheme previousTheme = ThemeManager.Theme;
        ThemeMode previousMode = ThemeManager.Mode;
        try
        {
            var window = new Window { Content = new Button { Content = "Test" } };
            window.Show();

            ThemeManager.Apply(AppTheme.Libadwaita, ThemeMode.Dark);
            Assert.True(window.TryFindResource("OverlayCornerRadius", window.ActualThemeVariant, out object libadwaitaRadius));
            Assert.Equal(new Avalonia.CornerRadius(12), libadwaitaRadius);

            // The window itself must use the theme's background (GTK's dark window_bg), not Fluent's black.
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Avalonia.Media.Color.Parse("#222226"), ((Avalonia.Media.ISolidColorBrush)window.Background).Color);

            ThemeManager.Apply(AppTheme.WinUI, ThemeMode.Light);
            Assert.True(window.TryFindResource("OverlayCornerRadius", window.ActualThemeVariant, out object winUIRadius));
            Assert.Equal(new Avalonia.CornerRadius(8), winUIRadius);
            Assert.True(window.TryFindResource("SystemControlBackgroundAltHighBrush", window.ActualThemeVariant, out object background));
            Assert.Equal(Avalonia.Media.Color.Parse("#F3F3F3"), ((Avalonia.Media.ISolidColorBrush)background).Color);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Avalonia.Media.Color.Parse("#F3F3F3"), ((Avalonia.Media.ISolidColorBrush)window.Background).Color);

            // Classic is always light, whatever the appearance setting.
            ThemeManager.Apply(AppTheme.Classic, ThemeMode.Dark);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Avalonia.Styling.ThemeVariant.Light, window.ActualThemeVariant);
            Assert.Equal(Avalonia.Media.Color.Parse("#F0F0F0"), ((Avalonia.Media.ISolidColorBrush)window.Background).Color);
            Assert.True(window.TryFindResource("ControlCornerRadius", window.ActualThemeVariant, out object classicRadius));
            Assert.Equal(new Avalonia.CornerRadius(0), classicRadius);
            Assert.Equal(ThemeMode.Dark, ThemeManager.Mode);

            ThemeManager.Apply(AppTheme.MacOS, ThemeMode.Light);
            Assert.True(window.TryFindResource("SystemAccentColor", window.ActualThemeVariant, out object accent));
            Assert.Equal(Avalonia.Media.Color.Parse("#007AFF"), accent);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Avalonia.Media.Color.Parse("#ECECEC"), ((Avalonia.Media.ISolidColorBrush)window.Background).Color);

            window.Close();
        }
        finally
        {
            ThemeManager.Apply(previousTheme, previousMode);
        }
    }
}
