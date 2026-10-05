using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LiveSplit.Model;
using LiveSplit.View;
using System;
using System.IO;
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

    [AvaloniaFact]
    public void RendersFinishedRun()
    {
        TimerWindow window = OpenWindow(Path.Combine(Fixtures.RepoRoot, "src", "LiveSplit.View", "Resources", "DefaultLayout.lsl"));
        SimulateRun(window, window.CurrentState.Run.Count);
        Capture(window, "default-finished");
        Assert.Equal(TimerPhase.Ended, window.CurrentState.CurrentPhase);
    }
}
