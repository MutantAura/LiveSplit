using LiveSplit.Options;
using LiveSplit.UI;
using System;
using System.Xml;

namespace LiveSplit.Racetime;

/// <summary>
/// Settings of the racetime.gg integration. Stored under the same plugin name and with the same
/// elements as the Windows LiveSplit.Racetime component, so settings files stay interchangeable.
/// </summary>
public sealed class RacetimeSettings : RaceProviderSettings
{
    public const string PluginName = "LiveSplit.Racetime.dll";

    public override string Name { get => PluginName; set { } }
    public override string DisplayName => "racetime.gg";
    public override string WebsiteLink => RacetimeConfig.WebRoot;
    public override string RulesLink => RacetimeConfig.WebRoot + "about/rules";

    public bool LoadChatHistory { get; set; } = true;

    // Not used by either front end, but kept so that the setting survives a round trip.
    public bool HideResults { get; set; }

    public override void FromXml(XmlElement element, Version version)
    {
        base.FromXml(element, version);
        LoadChatHistory = SettingsHelper.ParseBool(element["LoadChatHistory"], true);
        HideResults = SettingsHelper.ParseBool(element["HideResults"], false);
    }

    public override XmlElement ToXml(XmlDocument document)
    {
        XmlElement element = base.ToXml(document);
        SettingsHelper.CreateSetting(document, element, "LoadChatHistory", LoadChatHistory);
        SettingsHelper.CreateSetting(document, element, "HideResults", HideResults);
        return element;
    }

    public override object Clone()
    {
        return new RacetimeSettings
        {
            Enabled = Enabled,
            LoadChatHistory = LoadChatHistory,
            HideResults = HideResults
        };
    }
}

/// <summary>
/// racetime.gg endpoints and the OAuth client registered for LiveSplit (the same client and
/// redirect address as the Windows component).
/// </summary>
internal static class RacetimeConfig
{
    public const string WebRoot = "https://racetime.gg/";
    public const string SocketRoot = "wss://racetime.gg/";

    public const string RacesEndpoint = "races/data";
    public const string AuthorizeEndpoint = "o/authorize";
    public const string TokenEndpoint = "o/token";
    public const string UserInfoEndpoint = "o/userinfo";
    public const string DoneEndpoint = "o/done";
    public const string DeniedEndpoint = "o/done?error=access_denied";

    public const string ClientId = "dKgJFc2jZjgMtUT4Ik9kyJSCZkVqRK1poCcvpKox";
    public const string ClientSecret = "Rl6gGXy9zPw5xkSa0gSdE69lnlqbTwuZDAFNSDTgY5QKbQ9AqsqDYlotNGMBboDGkZFbJm9fSIpfI3sCGc3UiiZ2sEpk8KpxYf4kh0G7TjmYmgn0AXyNJNzkgIorHAcB";
    public const string Scopes = "read chat_message race_action";
    public const int RedirectPort = 4888;
    public const string RedirectUri = "http://127.0.0.1:4888/";

    public static string RaceSocketUrl(string raceId)
    {
        return $"{SocketRoot}ws/o/race/{raceId[(raceId.IndexOf('/') + 1)..]}";
    }

    public static string RaceUrl(string raceId)
    {
        return WebRoot + raceId;
    }
}
