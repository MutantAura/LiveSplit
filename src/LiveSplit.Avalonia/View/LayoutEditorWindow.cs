using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LiveSplit.View;

/// <summary>
/// Edits the current layout in place. Changes are shown live; the caller restores the
/// previous layout if the dialog is cancelled.
/// </summary>
public sealed class LayoutEditorWindow : Window
{
    private readonly LiveSplitState state;
    private readonly Action changed;
    private readonly ListBox componentList;
    private readonly ContentControl settingsHost;
    private readonly TabControl tabs;

    private ILayout Layout => state.Layout;

    public static Task<bool> Show(Window owner, LiveSplitState state, Action changed)
    {
        return new LayoutEditorWindow(state, changed).ShowDialog<bool>(owner);
    }

    internal LayoutEditorWindow(LiveSplitState state, Action changed)
    {
        this.state = state;
        this.changed = changed;
        Title = "Layout Editor";
        Width = 860;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        componentList = new ListBox { Width = 240 };
        componentList.SelectionChanged += (s, e) => ShowSelectedSettings();

        var add = new Button { Content = "Add ▾", HorizontalAlignment = HorizontalAlignment.Stretch };
        add.Flyout = new MenuFlyout { ItemsSource = BuildAddMenu() };
        var remove = CreateButton("Remove", RemoveComponent);
        var up = CreateButton("Move Up", () => MoveComponent(-1));
        var down = CreateButton("Move Down", () => MoveComponent(1));

        var orientation = new ComboBox
        {
            ItemsSource = new[] { "Vertical", "Horizontal" },
            SelectedIndex = Layout.Mode == LayoutMode.Vertical ? 0 : 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        orientation.SelectionChanged += (s, e) => SwitchOrientation(orientation.SelectedIndex == 0 ? LayoutMode.Vertical : LayoutMode.Horizontal);

        var listPanel = new DockPanel { Margin = new Thickness(0, 0, 10, 0) };
        var listButtons = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 6, 0, 0),
            Children =
            {
                new UniformGrid { Columns = 2, Children = { add, remove } },
                new UniformGrid { Columns = 2, Children = { up, down } },
                new TextBlock { Text = "Orientation", Margin = new Thickness(0, 6, 0, 0) },
                orientation
            }
        };
        DockPanel.SetDock(listButtons, Dock.Bottom);
        listPanel.Children.Add(listButtons);
        listPanel.Children.Add(componentList);

        settingsHost = new ContentControl();
        tabs = new TabControl
        {
            ItemsSource = new[]
            {
                new TabItem { Header = "Component", Content = settingsHost },
                new TabItem { Header = "Layout", Content = CreateLayoutSettingsEditor() }
            }
        };

        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80 };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
        ok.Click += (s, e) => Close(true);
        cancel.Click += (s, e) => Close(false);
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 0),
            Children = { ok, cancel }
        };

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(footer, Dock.Bottom);
        DockPanel.SetDock(listPanel, Dock.Left);
        root.Children.Add(footer);
        root.Children.Add(listPanel);
        root.Children.Add(tabs);
        Content = root;

        RefreshList(0);

        ComponentSettings.AnySettingChanged += OnComponentSettingChanged;
        Closed += (s, e) => ComponentSettings.AnySettingChanged -= OnComponentSettingChanged;
    }

    private void OnComponentSettingChanged(object sender, string propertyName)
    {
        changed();
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += (s, e) => action();
        return button;
    }

    private List<object> BuildAddMenu()
    {
        var menu = new List<object>();
        foreach (IGrouping<ComponentCategory, KeyValuePair<string, IComponentFactory>> category in ComponentManager.ComponentFactories
            .Where(x => x.Key != ComponentManager.SeparatorPath)
            .GroupBy(x => x.Value.Category)
            .OrderBy(x => x.Key))
        {
            var categoryItem = new MenuItem { Header = category.Key.ToString() };
            var items = new List<object>();
            foreach (KeyValuePair<string, IComponentFactory> factory in category.OrderBy(x => x.Value.ComponentName))
            {
                var item = new MenuItem { Header = factory.Value.ComponentName };
                ToolTip.SetTip(item, factory.Value.Description);
                string path = factory.Key;
                item.Click += (s, e) => AddComponent(path);
                items.Add(item);
            }

            categoryItem.ItemsSource = items;
            menu.Add(categoryItem);
        }

        var separator = new MenuItem { Header = "Separator" };
        separator.Click += (s, e) => AddComponent(ComponentManager.SeparatorPath);
        menu.Add(new Separator());
        menu.Add(separator);
        return menu;
    }

    private void RefreshList(int selectedIndex)
    {
        componentList.ItemsSource = Layout.LayoutComponents.Select(x => x.Component.ComponentName).ToList();
        if (Layout.LayoutComponents.Count > 0)
        {
            componentList.SelectedIndex = Math.Clamp(selectedIndex, 0, Layout.LayoutComponents.Count - 1);
        }

        changed();
    }

    private void ShowSelectedSettings()
    {
        int index = componentList.SelectedIndex;
        if (index < 0 || index >= Layout.LayoutComponents.Count)
        {
            settingsHost.Content = null;
            return;
        }

        IComponent component = Layout.LayoutComponents[index].Component;
        Control control = null;
        try
        {
            control = component.GetSettingsControl(Layout.Mode);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        try
        {
            settingsHost.Content = control ?? new TextBlock
            {
                Text = "This component has no settings.",
                Margin = new Thickness(8),
                Foreground = Brushes.Gray
            };
        }
        catch (Exception ex)
        {
            // A broken settings panel must not take down the whole application.
            Log.Error(ex);
            settingsHost.Content = new TextBlock
            {
                Text = "The settings of this component could not be shown.",
                Margin = new Thickness(8),
                Foreground = Brushes.Gray
            };
        }

        tabs.SelectedIndex = 0;
    }

    private void AddComponent(string path)
    {
        ILayoutComponent component = ComponentManager.LoadLayoutComponent(path, state);
        int index = componentList.SelectedIndex >= 0 ? componentList.SelectedIndex + 1 : Layout.LayoutComponents.Count;
        Layout.LayoutComponents.Insert(index, component);
        RefreshList(index);
    }

    private void RemoveComponent()
    {
        int index = componentList.SelectedIndex;
        if (index < 0)
        {
            return;
        }

        Layout.LayoutComponents.RemoveAt(index);
        RefreshList(index);
    }

    private void MoveComponent(int direction)
    {
        int index = componentList.SelectedIndex;
        int target = index + direction;
        if (index < 0 || target < 0 || target >= Layout.LayoutComponents.Count)
        {
            return;
        }

        ILayoutComponent component = Layout.LayoutComponents[index];
        Layout.LayoutComponents.RemoveAt(index);
        Layout.LayoutComponents.Insert(target, component);
        RefreshList(target);
    }

    private void SwitchOrientation(LayoutMode mode)
    {
        if (Layout.Mode == mode)
        {
            return;
        }

        // Swap the stored window sizes, like the Windows layout editor does.
        if (mode == LayoutMode.Horizontal)
        {
            if (Layout.HorizontalWidth <= 0 || Layout.HorizontalHeight <= 0)
            {
                Layout.HorizontalWidth = 600;
                Layout.HorizontalHeight = 45;
            }
        }
        else if (Layout.VerticalWidth <= 0 || Layout.VerticalHeight <= 0)
        {
            Layout.VerticalWidth = 300;
            Layout.VerticalHeight = 500;
        }

        Layout.Mode = mode;
        ShowSelectedSettings();
        changed();
    }

    private Control CreateLayoutSettingsEditor()
    {
        return SettingsEditor.Create(Layout.Settings, state, _ => changed());
    }
}
