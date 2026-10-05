using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace LiveSplit.Tests;

public class SettingsEditorTests
{
    public static IEnumerable<object[]> ComponentPaths()
    {
        BuiltInComponents.Register();
        return ComponentManager.ComponentFactories.Keys.Select(x => new object[] { x });
    }

    /// <summary>
    /// Regression test: selecting a component in the layout editor shows its settings panel,
    /// which must be attachable to a window (the Splits panel used to crash the app).
    /// </summary>
    [AvaloniaTheory]
    [MemberData(nameof(ComponentPaths))]
    public void SettingsPanelsCanBeShown(string path)
    {
        IRun run = new Run(new StandardComparisonGeneratorsFactory());
        run.AddSegment("Segment");
        var state = new LiveSplitState(run, null, new Layout { Settings = new StandardLayoutSettingsFactory().Create() }, null, new StandardSettingsFactory().Create());

        IComponent component = ComponentManager.LoadLayoutComponent(path, state).Component;
        var host = new ContentControl();
        var window = new Window { Content = host };
        window.Show();

        foreach (LayoutMode mode in new[] { LayoutMode.Vertical, LayoutMode.Horizontal })
        {
            host.Content = component.GetSettingsControl(mode);
            Dispatcher.UIThread.RunJobs();
        }

        window.Close();
    }
}
