using System;
using System.IO;

namespace LiveSplit.View;

/// <summary>
/// Locations of LiveSplit's own files. The Windows version keeps settings next to the
/// executable; here they go to the per-user configuration directory
/// (%APPDATA%\LiveSplit, ~/.config/LiveSplit or ~/Library/Application Support/LiveSplit)
/// unless a settings.cfg already exists next to the executable (portable installs).
/// </summary>
public static class AppPaths
{
    public const string SettingsFileName = "settings.cfg";

    public static string DataDirectory { get; } = ResolveDataDirectory();

    public static string SettingsPath => Path.Combine(DataDirectory, SettingsFileName);

    private static string ResolveDataDirectory()
    {
        string baseDirectory = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(baseDirectory, SettingsFileName)))
        {
            return baseDirectory;
        }

        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LiveSplit");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
