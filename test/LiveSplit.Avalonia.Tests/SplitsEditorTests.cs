using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.View;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace LiveSplit.Tests;

public class SplitsEditorTests
{
    private static (SplitsEditorWindow Window, IRun Run) Open(string game, string category)
    {
        IRun run = new Run(new StandardComparisonGeneratorsFactory()) { GameName = game, CategoryName = category };
        run.AddSegment("Summit");
        var state = new LiveSplitState(run, null, null, null, new StandardSettingsFactory().Create());
        var window = new SplitsEditorWindow(state, new TimerModel { CurrentState = state }, new FakeSpeedrunCom().Api);
        window.Show();
        Wait(() => window.PendingLookup.IsCompleted);
        Dispatcher.UIThread.RunJobs();
        return (window, run);
    }

    private static void Wait(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.Elapsed < TimeSpan.FromSeconds(10))
        {
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(condition());
    }

    // (The logical tree can list tab content more than once, hence Distinct.)
    private static ComboBox CategoryBox(Window window)
    {
        return window.GetLogicalDescendants().OfType<ComboBox>().Distinct().Single(x => x.IsEditable);
    }

    private static List<ComboBox> SubcategoryBoxes(Window window)
    {
        // Subcategory drop-downs are tagged with their variable's name.
        return [.. window.GetLogicalDescendants().OfType<ComboBox>().Distinct().Where(x => x.IsVisible && x.Tag is string)];
    }

    [AvaloniaFact]
    public void OffersSpeedrunComCategoriesAndSubcategories()
    {
        (SplitsEditorWindow window, IRun run) = Open("Celeste", "Any%");

        // Only the full game categories are offered.
        ComboBox category = CategoryBox(window);
        Assert.Equal(new[] { "Any%" }, (IEnumerable<string>)category.ItemsSource);
        Assert.Equal("Any%", category.Text);

        // Only the subcategory of this category; not the "Version" variable, other categories'
        // subcategories or level variables.
        ComboBox subcategory = Assert.Single(SubcategoryBoxes(window));
        Assert.Equal(new[] { "Seeded", "Set Seed" }, (IEnumerable<string>)subcategory.ItemsSource);

        // A subcategory is always selected: the run had none, so the leaderboard's default is used.
        Assert.Equal("Set Seed", subcategory.SelectedItem);
        Assert.Equal("Set Seed", run.Metadata.VariableValueNames["Seeded"]);
        Assert.Equal("Any% (Set Seed)", run.GetExtendedCategoryName());

        subcategory.SelectedItem = "Seeded";
        Assert.Equal("Seeded", run.Metadata.VariableValueNames["Seeded"]);

        // A category that isn't on speedrun.com can still be typed in, and has no subcategories.
        category.Text = "Any% No Dash";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Any% No Dash", run.CategoryName);
        Assert.Empty(SubcategoryBoxes(window));

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("Seeded", "Seeded")]
    [InlineData("No Longer On The Leaderboard", "Set Seed")]
    public void KeepsAValidSubcategoryOfTheRunAndReplacesAnInvalidOne(string stored, string expected)
    {
        IRun run = new Run(new StandardComparisonGeneratorsFactory()) { GameName = "Celeste", CategoryName = "Any%" };
        run.Metadata.VariableValueNames["Seeded"] = stored;
        run.AddSegment("Summit");
        var state = new LiveSplitState(run, null, null, null, new StandardSettingsFactory().Create());
        var window = new SplitsEditorWindow(state, new TimerModel { CurrentState = state }, new FakeSpeedrunCom().Api);
        window.Show();
        Wait(() => window.PendingLookup.IsCompleted);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(expected, Assert.Single(SubcategoryBoxes(window)).SelectedItem);
        Assert.Equal(expected, run.Metadata.VariableValueNames["Seeded"]);
        window.Close();
    }

    [AvaloniaFact]
    public void DropsSubcategoriesOfOtherCategories()
    {
        IRun run = new Run(new StandardComparisonGeneratorsFactory()) { GameName = "Celeste", CategoryName = "Any%" };
        run.Metadata.VariableValueNames["Other Category"] = "Seeded";
        run.Metadata.VariableValueNames["Version"] = "1.4";
        run.AddSegment("Summit");
        var state = new LiveSplitState(run, null, null, null, new StandardSettingsFactory().Create());
        var window = new SplitsEditorWindow(state, new TimerModel { CurrentState = state }, new FakeSpeedrunCom().Api);
        window.Show();
        Wait(() => window.PendingLookup.IsCompleted);
        Dispatcher.UIThread.RunJobs();

        // The subcategory of another category is removed; other variables are left alone.
        Assert.False(run.Metadata.VariableValueNames.ContainsKey("Other Category"));
        Assert.Equal("1.4", run.Metadata.VariableValueNames["Version"]);
        window.Close();
    }

    /// <summary>
    /// Regression test: in a short window, the segment and comparison buttons used to overflow
    /// and draw over the OK and Cancel buttons.
    /// </summary>
    [AvaloniaFact]
    public void OkAndCancelStayVisibleInAShortWindow()
    {
        IRun run = new Run(new StandardComparisonGeneratorsFactory()) { GameName = "Celeste", CategoryName = "Any%" };
        for (int i = 0; i < 20; i++)
        {
            run.AddSegment($"Segment {i + 1}");
        }

        for (int i = 0; i < 6; i++)
        {
            run.CustomComparisons.Add($"Comparison {i + 1}");
        }

        var state = new LiveSplitState(run, null, null, null, new StandardSettingsFactory().Create());
        var window = new SplitsEditorWindow(state, new TimerModel { CurrentState = state }, new FakeSpeedrunCom().Api)
        {
            Height = 300
        };
        window.Show();
        Wait(() => window.PendingLookup.IsCompleted);
        for (int i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        Rect Bounds(Visual control)
        {
            Point topLeft = control.TranslatePoint(default, window)!.Value;
            return new Rect(topLeft, control.Bounds.Size);
        }

        List<Button> buttons = [.. window.GetLogicalDescendants().OfType<Button>().Distinct()];
        Button okButton = buttons.Single(x => x.Content as string == "OK");
        Button cancelButton = buttons.Single(x => x.Content as string == "Cancel");
        Rect ok = Bounds(okButton);
        Rect cancel = Bounds(cancelButton);
        Assert.True(ok.Bottom <= window.Bounds.Height && cancel.Bottom <= window.Bounds.Height);

        // Nothing is drawn on top of OK and Cancel: clicking them reaches them.
        foreach ((Button button, Rect bounds) in new[] { (okButton, ok), (cancelButton, cancel) })
        {
            IInputElement hit = window.InputHitTest(bounds.Center);
            Button hitButton = (hit as Visual)?.FindAncestorOfType<Button>(includeSelf: true);
            Assert.True(hitButton == button, $"{button.Content} is covered by {(hitButton?.Content ?? hit)?.ToString()}.");
        }

        // The button column ends above the OK and Cancel row, and scrolls instead.
        ScrollViewer column = window.GetLogicalDescendants().OfType<ScrollViewer>().Distinct()
            .Single(x => x.Content is StackPanel panel && panel.Children.OfType<TextBlock>().Any(t => t.Text == "Timing Method"));
        Assert.True(Bounds(column).Bottom <= ok.Top, $"Button column ends at {Bounds(column).Bottom}, OK starts at {ok.Top}.");
        Assert.True(column.Extent.Height > column.Viewport.Height);

        // The window can't be made shorter than its minimum height.
        Assert.Equal(window.MinHeight, window.Bounds.Height, 0.5);

        using (WriteableBitmap frame = window.CaptureRenderedFrame())
        {
            frame.Save(System.IO.Path.Combine(Fixtures.ScreenshotDirectory, "splits-editor-short.png"));
        }

        window.Close();
    }

    [AvaloniaFact]
    public void DoubleClickingAnIconPicksANewOne()
    {
        IRun run = new Run(new StandardComparisonGeneratorsFactory()) { GameName = "Celeste", CategoryName = "Any%" };
        run.AddSegment("Prologue");
        run.AddSegment("Forsaken City");
        run.AddSegment("Summit");

        var state = new LiveSplitState(run, null, null, null, new StandardSettingsFactory().Create());
        var window = new SplitsEditorWindow(state, new TimerModel { CurrentState = state }, new FakeSpeedrunCom().Api);
        var picked = new Drawing.Image([1, 2, 3]);
        int pickerCalls = 0;
        window.ImagePicker = () =>
        {
            pickerCalls++;
            return Task.FromResult(picked);
        };
        window.Show();
        Wait(() => window.PendingLookup.IsCompleted);
        for (int i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        // The icon cell of the third segment.
        Border cell = window.GetVisualDescendants().OfType<Border>()
            .Single(x => x.Child is Image && x.DataContext is SplitsEditorWindow.SegmentRow { Index: 2 });
        Point center = cell.TranslatePoint(new Point(cell.Bounds.Width / 2, cell.Bounds.Height / 2), window)!.Value;

        // A single click only selects.
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, pickerCalls);

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, pickerCalls);
        Assert.Same(picked, run[2].Icon);
        Assert.Null(run[1].Icon);
        window.Close();
    }

    [AvaloniaFact]
    public void UnknownGamesKeepTheDefaultCategories()
    {
        (SplitsEditorWindow window, IRun run) = Open("Not A Real Game", "Glitchless");

        ComboBox category = CategoryBox(window);
        Assert.Equal(new[] { "Any%", "Low%", "100%" }, (IEnumerable<string>)category.ItemsSource);
        Assert.Equal("Glitchless", category.Text);
        Assert.Equal("Glitchless", run.CategoryName);
        Assert.Empty(SubcategoryBoxes(window));
        Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), x => x.Text == "Not on speedrun.com; type the category name");

        window.Close();
    }
}
