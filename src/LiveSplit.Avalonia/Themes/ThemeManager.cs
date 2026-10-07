using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using LiveSplit.Options;
using LiveSplit.View;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace LiveSplit.Themes;

/// <summary>
/// The look of the application's own windows (settings, editors, dialogs and menus). The timer
/// itself is always drawn by the layout.
/// </summary>
public enum AppTheme
{
    Fluent,
    WinUI,
    Libadwaita,
    MacOS,
    Classic
}

public enum ThemeMode
{
    System,
    Light,
    Dark
}

/// <summary>
/// Applies and persists the selected theme. Themes are style layers on top of the Fluent theme
/// that override its resources, so every control keeps working the same way. The selection is
/// stored in appearance.xml next to settings.cfg, which stays compatible with the Windows version.
/// Unless the user picks another theme, the one matching the operating system is used.
/// </summary>
public static class ThemeManager
{
    private const string FileName = "appearance.xml";

    private static readonly List<Window> openWindows = [];
    private static Styles activeLayer;
    private static bool openedHandlerRegistered;

    public static AppTheme PlatformDefault { get; } = GetPlatformDefault(
        OperatingSystem.IsWindows(),
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000),
        OperatingSystem.IsMacOS());

    public static AppTheme Theme { get; private set; } = PlatformDefault;
    public static ThemeMode Mode { get; private set; } = ThemeMode.Dark;

    /// <summary>
    /// The theme that looks native: Fluent on Windows 10, WinUI 3 on Windows 11, the macOS
    /// theme on macOS and Libadwaita on Linux and other systems.
    /// </summary>
    internal static AppTheme GetPlatformDefault(bool isWindows, bool isWindows11, bool isMacOS)
    {
        if (isWindows)
        {
            return isWindows11 ? AppTheme.WinUI : AppTheme.Fluent;
        }

        return isMacOS ? AppTheme.MacOS : AppTheme.Libadwaita;
    }

    /// <summary>
    /// Themes that only have a light appearance, like the Windows Forms look of Classic.
    /// </summary>
    public static bool IsLightOnly(AppTheme theme)
    {
        return theme == AppTheme.Classic;
    }

    public static string DisplayName(AppTheme theme)
    {
        string name = theme switch
        {
            AppTheme.WinUI => "WinUI 3",
            AppTheme.Libadwaita => "Libadwaita",
            AppTheme.MacOS => "macOS",
            AppTheme.Classic => "Classic (Windows Forms)",
            _ => "Fluent"
        };

        return theme == PlatformDefault ? name + " (default)" : name;
    }

    public static string DisplayName(ThemeMode mode)
    {
        return mode switch
        {
            ThemeMode.Light => "Light",
            ThemeMode.Dark => "Dark",
            _ => "Follow system"
        };
    }

    private static string FilePath => Path.Combine(AppPaths.DataDirectory, FileName);

    /// <summary>
    /// Loads the saved selection and applies it. Called once at startup.
    /// </summary>
    public static void Initialize()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var document = new XmlDocument();
                document.Load(FilePath);
                XmlElement root = document["Appearance"];
                if (Enum.TryParse(root?["Theme"]?.InnerText, out AppTheme theme))
                {
                    Theme = theme;
                }

                if (Enum.TryParse(root?["Mode"]?.InnerText, out ThemeMode mode))
                {
                    Mode = mode;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        if (!openedHandlerRegistered)
        {
            openedHandlerRegistered = true;

            // Track open windows here rather than through the application lifetime, which only
            // exists for classic desktop apps (and not, for example, in headless tests).
            Window.WindowOpenedEvent.AddClassHandler(typeof(Window), (sender, e) =>
            {
                if (sender is Window window && !openWindows.Contains(window))
                {
                    openWindows.Add(window);
                    TagWindow(window);
                    Decorate(window);
                }
            });
            Window.WindowClosedEvent.AddClassHandler(typeof(Window), (sender, e) =>
            {
                if (sender is Window window)
                {
                    openWindows.Remove(window);
                }
            });
        }

        Apply(Theme, Mode);
    }

    public static void Save()
    {
        try
        {
            var document = new XmlDocument();
            XmlElement root = document.CreateElement("Appearance");
            document.AppendChild(root);

            // The platform's theme isn't stored, so it keeps following the platform (e.g. after
            // upgrading from Windows 10 to 11) until the user picks a different one.
            if (Theme != PlatformDefault)
            {
                root.AppendChild(document.CreateElement("Theme")).InnerText = Theme.ToString();
            }

            root.AppendChild(document.CreateElement("Mode")).InnerText = Mode.ToString();
            document.Save(FilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    /// <summary>
    /// Switches the theme immediately, including for windows that are already open.
    /// </summary>
    public static void Apply(AppTheme theme, ThemeMode mode)
    {
        Application app = Application.Current;
        if (app == null)
        {
            return;
        }

        Theme = theme;
        Mode = mode;

        // The mode is kept for the other themes even while a light-only theme is used.
        app.RequestedThemeVariant = IsLightOnly(theme) ? ThemeVariant.Light : mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        if (activeLayer != null)
        {
            app.Styles.Remove(activeLayer);
            activeLayer = null;
        }

        activeLayer = theme switch
        {
            AppTheme.WinUI => new WinUITheme(),
            AppTheme.Libadwaita => new LibadwaitaTheme(),
            AppTheme.MacOS => new MacOSTheme(),
            AppTheme.Classic => new ClassicTheme(),
            _ => null
        };

        if (activeLayer != null)
        {
            app.Styles.Add(activeLayer);
        }

        foreach (Window window in openWindows.ToList())
        {
            TagWindow(window);
            Decorate(window);
        }
    }

    /// <summary>
    /// Applies per-window effects that styles can't express: the Mica backdrop of WinUI 3 on
    /// Windows 11. The timer window is drawn by the layout and is left alone.
    /// </summary>
    /// <summary>
    /// Marks the window with the current theme (e.g. "theme-classic"). Styles that reach into
    /// control templates only apply within windows marked with their theme: Avalonia doesn't
    /// remove such styles from template parts when a theme's style layer is removed, but it does
    /// when they stop matching, so switching the mark switches them off.
    /// </summary>
    private static void TagWindow(Window window)
    {
        string themeClass = "theme-" + Theme.ToString().ToLowerInvariant();
        foreach (string old in window.Classes.Where(x => x.StartsWith("theme-", StringComparison.Ordinal) && x != themeClass).ToList())
        {
            window.Classes.Remove(old);
        }

        if (!window.Classes.Contains(themeClass))
        {
            window.Classes.Add(themeClass);
        }
    }

    private static void Decorate(Window window)
    {
        if (window is TimerWindow)
        {
            return;
        }

        bool mica = Theme == AppTheme.WinUI && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);
        if (mica)
        {
            window.TransparencyLevelHint = [WindowTransparencyLevel.Mica];
            window.Background = Brushes.Transparent;

            // Mica can be unavailable (e.g. transparency effects turned off); keep the solid
            // theme background then instead of a black window.
            if (window.IsVisible && window.ActualTransparencyLevel != WindowTransparencyLevel.Mica)
            {
                window.ClearValue(Window.BackgroundProperty);
            }
        }
        else
        {
            window.ClearValue(TopLevel.TransparencyLevelHintProperty);
            window.ClearValue(Window.BackgroundProperty);
        }
    }
}
