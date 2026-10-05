using LiveSplit.Model;
using LiveSplit.Options;
using System.Collections.Generic;
using System.Xml;

namespace LiveSplit.UI.Components;

public interface IComponentFactory
{
    string ComponentName { get; }
    string Description { get; }
    ComponentCategory Category { get; }
    IComponent Create(LiveSplitState state);
}

/// <summary>
/// Portable component registry. Instead of loading WinForms component DLLs from disk, factories
/// are registered by the front end under the file name of the component they replace
/// (e.g. "LiveSplit.Splits.dll"), so existing layout files resolve to the same components.
/// The separator is registered under <see cref="SeparatorPath"/>.
/// </summary>
public static class ComponentManager
{
    public const string SeparatorPath = "";

    public static string BasePath { get; set; }
    public static IDictionary<string, IComponentFactory> ComponentFactories { get; } = new Dictionary<string, IComponentFactory>();
    public static IDictionary<string, IRaceProviderFactory> RaceProviderFactories { get; } = new Dictionary<string, IRaceProviderFactory>();

    public static void Register(string path, IComponentFactory factory)
    {
        ComponentFactories[path] = factory;
    }

    public static ILayoutComponent LoadLayoutComponent(string path, LiveSplitState state)
    {
        path ??= SeparatorPath;

        if (!ComponentFactories.TryGetValue(path, out IComponentFactory factory))
        {
            Log.Warning($"Component \"{path}\" is not available in this version of LiveSplit; its settings are kept but it is not shown.");
            return new LayoutComponent(path, new UnavailableComponent(path));
        }

        return new LayoutComponent(path, factory.Create(state));
    }
}

/// <summary>
/// Stands in for a component that has not been ported to this front end. It draws nothing but
/// keeps the component's settings so that saving the layout does not lose them.
/// </summary>
public sealed class UnavailableComponent(string path) : LogicComponent
{
    private XmlNode settings;

    public string Path { get; } = path;

    public override string ComponentName
        => System.IO.Path.GetFileNameWithoutExtension(Path).Replace("LiveSplit.", "") + " (unavailable)";

    public override Avalonia.Controls.Control GetSettingsControl(LayoutMode mode)
    {
        return new Avalonia.Controls.TextBlock
        {
            Text = "This component is not available in the cross-platform version of LiveSplit yet. Its settings are preserved when the layout is saved.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(8)
        };
    }

    public override XmlNode GetSettings(XmlDocument document)
    {
        return settings != null
            ? document.ImportNode(settings, true)
            : document.CreateElement("Settings");
    }

    public override void SetSettings(XmlNode node)
    {
        if (node != null)
        {
            var copy = new XmlDocument();
            settings = copy.ImportNode(node, true);
        }
    }

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }

    public override void Dispose() { }
}
