using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
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
