using LiveSplit.Options;

namespace LiveSplit.UI.Components;

/// <summary>
/// Implemented by components that can cheaply hash their settings, which the layout saver
/// uses every refresh to detect layout changes. The Windows version finds a
/// GetSettingsHashCode method through reflection and `dynamic`, neither of which works with
/// Native AOT.
/// </summary>
public interface ISettingsHashCodeProvider
{
    int GetSettingsHashCode();
}

/// <summary>
/// Implemented by components that used to store their own font overrides, so old layouts can
/// be migrated to the per-component font overrides (found through reflection on Windows).
/// </summary>
public interface IFontOverridesMigration
{
    void MigrateFontOverrides(FontOverrides overrides);
}
