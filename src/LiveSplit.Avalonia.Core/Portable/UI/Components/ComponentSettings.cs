using System;
using System.Diagnostics.CodeAnalysis;
using System.Xml;

namespace LiveSplit.UI.Components;

/// <summary>
/// Excludes a settings property from the generated settings editor.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class HiddenAttribute : Attribute;

/// <summary>
/// Customizes how a settings property is shown in the generated settings editor.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingAttribute(string label = null) : Attribute
{
    public string Label { get; } = label;
    public double Minimum { get; init; } = double.NaN;
    public double Maximum { get; init; } = double.NaN;
    public double Increment { get; init; } = double.NaN;

    /// <summary>
    /// For string properties: the values offered in a drop-down instead of a free text box.
    /// "{Comparisons}" is replaced with the run's comparisons.
    /// </summary>
    public string[] Options { get; init; }
}

/// <summary>
/// Base class for component settings. Settings are plain properties so that the front end can
/// generate an editor for them; serialization keeps the exact XML of the Windows version so
/// layouts can be shared between both.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public abstract class ComponentSettings
{
    /// <summary>
    /// Raised after a property was changed through the settings editor.
    /// </summary>
    public event EventHandler<string> SettingChanged;

    /// <summary>
    /// Raised after any component's settings were changed through a settings editor.
    /// </summary>
    public static event EventHandler<string> AnySettingChanged;

    public abstract void SetSettings(XmlNode node);

    protected abstract int CreateSettingsNode(XmlDocument document, XmlElement parent);

    public XmlNode GetSettings(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Settings");
        CreateSettingsNode(document, parent);
        return parent;
    }

    public int GetSettingsHashCode()
    {
        return CreateSettingsNode(null, null);
    }

    public virtual void OnSettingChanged(string propertyName)
    {
        SettingChanged?.Invoke(this, propertyName);
        AnySettingChanged?.Invoke(this, propertyName);
    }
}
