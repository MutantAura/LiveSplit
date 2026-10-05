using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using LiveSplit.Options;
using LiveSplit.View;
using System;
using System.IO;
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
    Libadwaita
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
/// </summary>
public static class ThemeManager
{
    private const string FileName = "appearance.xml";

    private static Styles activeLayer;
    private static bool openedHandlerRegistered;

    public static AppTheme Theme { get; private set; } = AppTheme.Fluent;
    public static ThemeMode Mode { get; private set; } = ThemeMode.Dark;

    public static string DisplayName(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.WinUI => "WinUI 3",
            AppTheme.Libadwaita => "Libadwaita",
            _ => "Fluent (default)"
        };
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
            Window.WindowOpenedEvent.AddClassHandler(typeof(Window), (sender, e) =>
            {
                if (sender is Window window)
                {
                    Decorate(window);
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
            root.AppendChild(document.CreateElement("Theme")).InnerText = Theme.ToString();
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

        app.RequestedThemeVariant = mode switch
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
            _ => null
        };

        if (activeLayer != null)
        {
            app.Styles.Add(activeLayer);
        }

        if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (Window window in desktop.Windows)
            {
                Decorate(window);
            }
        }
    }

    /// <summary>
    /// Applies per-window effects that styles can't express: the Mica backdrop of WinUI 3 on
    /// Windows 11. The timer window is drawn by the layout and is left alone.
    /// </summary>
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
