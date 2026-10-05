using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.View;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace LiveSplit.Tests;

public class RenderTests
{
    private static string CelestePath => Path.Combine(Fixtures.RunFiles, "Celeste - Any% (1.2.1.5).lss");

    /// <summary>
    /// Moves the timer forward without waiting, by shifting the attempt's start time back.
    /// </summary>
    private static void Advance(LiveSplitState state, TimeSpan time)
    {
        state.AdjustedStartTime -= time;
        state.StartTimeWithOffset -= time;
        state.StartTime -= time;
    }

    private static TimerWindow OpenWindow(string layoutPath)
    {
        var window = new TimerWindow(CelestePath, layoutPath);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void SimulateRun(TimerWindow window, int splits)
    {
        LiveSplitState state = window.CurrentState;

        // Bypass double tap prevention, which ignores splits less than 300ms apart.
        ITimerModel model = window.Model is DoubleTapPrevention prevention ? prevention.InternalModel : window.Model;
        model.Start();
        for (int i = 0; i < splits && i < state.Run.Count; i++)
        {
            TimeSpan? pbSegment = state.Run[i].PersonalBestSplitTime.RealTime - (i > 0 ? state.Run[i - 1].PersonalBestSplitTime.RealTime : TimeSpan.Zero);
            // Alternate between slightly faster and slower than the PB to exercise the delta colors.
            Advance(state, (pbSegment ?? TimeSpan.FromMinutes(2)) + TimeSpan.FromSeconds(i % 2 == 0 ? -1.5 : 2.3));
            model.Split();
        }

        Advance(state, TimeSpan.FromSeconds(42.37));
    }

    private static string Capture(TimerWindow window, string name)
    {
        for (int i = 0; i < 8; i++)
        {
            window.Tick();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        using WriteableBitmap frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string path = Path.Combine(Fixtures.ScreenshotDirectory, name + ".png");
        frame.Save(path);

        AssertHasVisibleContent(frame);
        return path;
    }

    private static void AssertHasVisibleContent(WriteableBitmap frame)
    {
        using var buffer = frame.Lock();
        int height = buffer.Size.Height;
        int width = buffer.Size.Width;
        var row = new byte[buffer.RowBytes];
        int opaquePixels = 0;
        for (int y = 0; y < height; y++)
        {
            Marshal.Copy(buffer.Address + (y * buffer.RowBytes), row, 0, row.Length);
            for (int x = 0; x < width; x++)
            {
                if (row[(x * 4) + 3] > 0)
                {
                    opaquePixels++;
                }
            }
        }

        Assert.True(opaquePixels > width * height / 10, $"Only {opaquePixels} of {width * height} pixels were drawn.");
    }

    [AvaloniaFact]
    public void RendersDefaultLayoutBeforeStart()
    {
        TimerWindow window = OpenWindow(Path.Combine(Fixtures.RepoRoot, "src", "LiveSplit.View", "Resources", "DefaultLayout.lsl"));
        Capture(window, "default-not-running");
    }

    [AvaloniaFact]
    public void RendersDefaultLayoutMidRun()
    {
        TimerWindow window = OpenWindow(Path.Combine(Fixtures.RepoRoot, "src", "LiveSplit.View", "Resources", "DefaultLayout.lsl"));
        SimulateRun(window, 5);
        Capture(window, "default-mid-run");
        Assert.Equal(TimerPhase.Running, window.CurrentState.CurrentPhase);
        Assert.Equal(5, window.CurrentState.CurrentSplitIndex);
    }

    [AvaloniaTheory]
    [InlineData("All.lsl")]
    [InlineData("dark.lsl")]
    [InlineData("WSplit.lsl")]
    [InlineData("WithBackgroundImage.lsl")]
    [InlineData("WithTimerDeltaBackground.lsl")]
    [InlineData("custom_variable_splits.lsl")]
    public void RendersFixtureLayoutsMidRun(string layout)
    {
        TimerWindow window = OpenWindow(Path.Combine(Fixtures.LayoutFiles, layout));
        SimulateRun(window, 6);
        Capture(window, Path.GetFileNameWithoutExtension(layout) + "-mid-run");
    }

    private static byte[] Pixels(TimerWindow window)
    {
        for (int i = 0; i < 8; i++)
        {
            window.Tick();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        using WriteableBitmap frame = window.CaptureRenderedFrame();
        using var buffer = frame.Lock();
        byte[] pixels = new byte[buffer.RowBytes * buffer.Size.Height];
        Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);
        return pixels;
    }

    /// <summary>
    /// Regression test: changing a font in the layout settings must change what is drawn.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("TextFont")]
    [InlineData("TimesFont")]
    [InlineData("TimerFont")]
    public void ChangingLayoutFontsChangesRendering(string font)
    {
        TimerWindow window = OpenWindow(Path.Combine(Fixtures.RepoRoot, "src", "LiveSplit.View", "Resources", "DefaultLayout.lsl"));
        SimulateRun(window, 3);
        byte[] before = Pixels(window);

        Options.LayoutSettings settings = window.Layout.Settings;
        var newFont = new Drawing.Font("Courier New", font == "TimerFont" ? 70 : 22, Drawing.FontStyle.Italic, Drawing.GraphicsUnit.Pixel);
        switch (font)
        {
            case "TextFont":
                settings.TextFont = newFont;
                break;
            case "TimesFont":
                settings.TimesFont = newFont;
                break;
            default:
                settings.TimerFont = newFont;
                break;
        }

        byte[] after = Pixels(window);
        Capture(window, "font-changed-" + font);
        Assert.NotEqual(before, after);
    }

    private static byte[] TopRows(TimerWindow window, int rows)
    {
        byte[] pixels = Pixels(window);
        return pixels[..(pixels.Length / (int)window.Bounds.Height * rows)];
    }

    /// <summary>
    /// Regression test: a component's font override (e.g. a Title with its own text font, as
    /// set by the Windows version) takes precedence over the layout font and can be turned off
    /// from the layout editor's component settings.
    /// </summary>
    [AvaloniaFact]
    public void ComponentFontOverridesCanBeEdited()
    {
        TimerWindow window = OpenWindow(Path.Combine(Fixtures.RepoRoot, "src", "LiveSplit.View", "Resources", "DefaultLayout.lsl"));
        var title = (UI.Components.LayoutComponent)window.Layout.LayoutComponents.First(x => x.Path == "LiveSplit.Title.dll");
        title.FontOverrides.OverrideTextFont = true;
        title.FontOverrides.TextFont = new Drawing.Font("Times New Roman", 18, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Pixel);

        // The title (top rows, above the first split) ignores the layout's text font while it is overridden.
        byte[] before = TopRows(window, 28);
        window.Layout.Settings.TextFont = new Drawing.Font("Courier New", 24, Drawing.FontStyle.Italic, Drawing.GraphicsUnit.Pixel);
        Assert.Equal(before, TopRows(window, 28));

        // The Title only uses the text font, so only that override is offered.
        // (The logical tree can list tab content more than once, hence Distinct.)
        var editor = new LayoutEditorWindow(window.CurrentState, () => { });
        editor.Show();
        Dispatcher.UIThread.RunJobs();
        List<CheckBox> overrides = [.. editor.GetLogicalDescendants().OfType<CheckBox>().Distinct().Where(x => (x.Content as string)?.StartsWith("Override ") == true && (x.Content as string).EndsWith(" Font"))];
        CheckBox textOverride = Assert.Single(overrides);
        Assert.Equal("Override Text Font", textOverride.Content);
        Assert.True(textOverride.IsChecked);

        for (int i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        using (WriteableBitmap frame = editor.CaptureRenderedFrame())
        {
            frame.Save(Path.Combine(Fixtures.ScreenshotDirectory, "layout-editor-font-overrides.png"));
        }

        textOverride.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        editor.Close();

        Assert.False(title.FontOverrides.OverrideTextFont);
        Assert.NotEqual(before, TopRows(window, 28));
    }

    /// <summary>
    /// Regression test: every font offered by the font dialog must be drawn with that font, not
    /// the fallback, including fonts listed under a shorter name than their stored family name
    /// (e.g. "Arial Rounded MT" for "Arial Rounded MT Bold").
    /// </summary>
    [AvaloniaFact]
    public void FontsOfferedByTheFontDialogAreUsed()
    {
        FontManager fonts = FontManager.Current;
        var unresolved = new List<string>();
        foreach (string name in fonts.SystemFonts.Select(x => x.Name).Distinct())
        {
            // Skip names the font manager itself can't load (it substitutes its default font).
            if (!fonts.TryGetGlyphTypeface(new Typeface(name), out GlyphTypeface glyph)
                || (glyph.FamilyName != name && glyph.FamilyName == fonts.DefaultFontFamily.Name))
            {
                continue;
            }

            Typeface typeface = DrawingHelpers.GetTypeface(new Drawing.Font(name, 16, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Pixel));
            if (typeface.FontFamily.Name != name)
            {
                unresolved.Add($"{name} -> {typeface.FontFamily.Name}");
            }
        }

        Assert.Empty(unresolved);
    }

    [AvaloniaFact]
    public void RendersFinishedRun()
    {
        TimerWindow window = OpenWindow(Path.Combine(Fixtures.RepoRoot, "src", "LiveSplit.View", "Resources", "DefaultLayout.lsl"));
        SimulateRun(window, window.CurrentState.Run.Count);
        Capture(window, "default-finished");
        Assert.Equal(TimerPhase.Ended, window.CurrentState.CurrentPhase);
    }
}
