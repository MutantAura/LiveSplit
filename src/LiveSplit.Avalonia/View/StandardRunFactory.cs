using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;

namespace LiveSplit.View;

/// <summary>
/// Creates the empty single-segment run used in timer-only mode.
/// </summary>
public class StandardRunFactory : IRunFactory
{
    public IRun Create(IComparisonGeneratorsFactory factory)
    {
        var run = new Run(factory)
        {
            GameName = "",
            CategoryName = ""
        };
        run.AddSegment("");
        return run;
    }
}
