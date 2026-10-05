using System;
using System.Xml;

namespace LiveSplit.Options;

/// <summary>
/// Portable copy of LiveSplit.Core's RaceProviderSettings without the WinForms settings control.
/// </summary>
public abstract class RaceProviderSettings : ICloneable
{
    public bool Enabled = true;
    public abstract string Name { get; set; }
    public abstract string DisplayName { get; }
    public abstract string WebsiteLink { get; }
    public abstract string RulesLink { get; }

    public abstract object Clone();

    public virtual void FromXml(XmlElement element, Version version)
    {
        XmlAttribute enabled = element.Attributes["enabled"];
        if (enabled == null || !bool.TryParse(enabled.Value, out Enabled))
        {
            Enabled = true;
        }
    }

    public virtual XmlElement ToXml(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Plugin");

        XmlAttribute providerName = document.CreateAttribute("name");
        providerName.InnerText = Name;
        parent.Attributes.Append(providerName);

        XmlAttribute enabled = document.CreateAttribute("enabled");
        enabled.InnerText = Enabled.ToString();
        parent.Attributes.Append(enabled);

        return parent;
    }
}

/// <summary>
/// Keeps the settings of a race provider that is not available in this build, so that they
/// survive a load/save round trip.
/// </summary>
public class UnloadedRaceProviderSettings : RaceProviderSettings
{
    public override string Name { get; set; }
    public override string DisplayName => Name;
    public override string WebsiteLink => null;
    public override string RulesLink => null;

    private string Content { get; set; }

    public override object Clone()
    {
        return new UnloadedRaceProviderSettings
        {
            Name = Name,
            Enabled = Enabled,
            Content = Content
        };
    }

    public override void FromXml(XmlElement element, Version version)
    {
        base.FromXml(element, version);
        Name = element.GetAttribute("name");
        Content = element.InnerXml;
    }

    public override XmlElement ToXml(XmlDocument document)
    {
        XmlElement element = base.ToXml(document);
        element.InnerXml = Content ?? "";
        return element;
    }
}
