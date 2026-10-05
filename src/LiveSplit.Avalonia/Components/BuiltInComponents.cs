
namespace LiveSplit.UI.Components;

/// <summary>
/// Registers the components that ship with this front end. Each one is registered under the
/// file name of the Windows component it replaces, so existing layouts resolve to it.
/// </summary>
public static class BuiltInComponents
{
    public static void Register()
    {
        ComponentManager.Register(ComponentManager.SeparatorPath,
            new SimpleComponentFactory("Separator", "Shows a line to separate components.", ComponentCategory.Other, _ => new SeparatorComponent()));

        ComponentManager.Register("LiveSplit.Title.dll",
            new SimpleComponentFactory("Title", "Shows the current run title, run category, and game icon.", ComponentCategory.Information, _ => new Title()));
        ComponentManager.Register("LiveSplit.Splits.dll", new SplitsComponentFactory());
        ComponentManager.Register("LiveSplit.Timer.dll", new TimerFactory());
        ComponentManager.Register("LiveSplit.DetailedTimer.dll",
            new SimpleComponentFactory("Detailed Timer", "Displays the run timer, segment timer, and segment times for up to two comparisons.", ComponentCategory.Timer, state => new DetailedTimer(state)));

        ComponentManager.Register("LiveSplit.PreviousSegment.dll",
            new SimpleComponentFactory("Previous Segment", "Shows how much time you saved or lost on the previous segment in relation to a comparison.", ComponentCategory.Information, state => new PreviousSegment(state)));
        ComponentManager.Register("LiveSplit.SumOfBest.dll",
            new SimpleComponentFactory("Sum of Best", "Displays the current sum of best segments.", ComponentCategory.Information, state => new SumOfBestComponent(state)));
        ComponentManager.Register("LiveSplit.PossibleTimeSave.dll",
            new SimpleComponentFactory("Possible Time Save", "Shows how much time you can save on the current segment compared to your best segment.", ComponentCategory.Information, state => new PossibleTimeSave(state)));
        ComponentManager.Register("LiveSplit.RunPrediction.dll",
            new SimpleComponentFactory("Run Prediction", "Displays what the final time for the run would be if the runner would match a chosen comparison for the rest of the run.", ComponentCategory.Information, state => new RunPrediction(state)));
        ComponentManager.Register("LiveSplit.Delta.dll",
            new SimpleComponentFactory("Delta", "Displays the current delta to a chosen comparison.", ComponentCategory.Information, state => new DeltaComponent(state)));
        ComponentManager.Register("LiveSplit.CurrentComparison.dll",
            new SimpleComponentFactory("Current Comparison", "Displays the name of the comparison that the timer is currently comparing against.", ComponentCategory.Information, state => new CurrentComparison(state)));
        ComponentManager.Register("LiveSplit.ComparisonTime.dll",
            new SimpleComponentFactory("Comparison Time", "Displays the final or segment time of a comparison.", ComponentCategory.Information, state => new ComparisonTime(state)));
        ComponentManager.Register("LiveSplit.TotalPlaytime.dll",
            new SimpleComponentFactory("Total Playtime", "Displays the total amount of time that the current category has been played for.", ComponentCategory.Information, state => new TotalPlaytimeComponent(state)));
        ComponentManager.Register("LiveSplit.Text.dll",
            new SimpleComponentFactory("Text", "Displays the text that you specify.", ComponentCategory.Information, state => new TextComponent(state)));

        ComponentManager.Register("LiveSplit.Graph.dll",
            new SimpleComponentFactory("Graph", "Shows a graph of the current run in relation to a comparison.", ComponentCategory.Media, state => new GraphCompositeComponent(state)));

        ComponentManager.Register("LiveSplit.BlankSpace.dll",
            new SimpleComponentFactory("Blank Space", "Adds blank space to the layout.", ComponentCategory.Other, _ => new BlankSpace()));
    }
}
