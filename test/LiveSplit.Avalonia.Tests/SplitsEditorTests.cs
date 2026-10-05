using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
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
