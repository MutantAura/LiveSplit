using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LiveSplit.UI.Components;
using LiveSplit.View;
using System;
using System.IO;

namespace LiveSplit;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        BuiltInComponents.Register();
        Themes.ThemeManager.Initialize();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ParseArguments(desktop.Args ?? [], out string splitsPath, out string layoutPath);

            // Exit explicitly once the timer window has closed. With OnMainWindowClose, Avalonia
            // makes a single shutdown attempt when the main window closes and abandons it if any
            // other window is still open at that moment, e.g. a save prompt that is still closing
            // when the timer closes through its context menu. The process then keeps running with
            // no windows.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var timer = new TimerWindow(splitsPath, layoutPath);
            timer.Closed += (s, e) => desktop.Shutdown();
            desktop.MainWindow = timer;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Accepts the same arguments as the Windows version (-s splits.lss, -l layout.lsl), as well
    /// as plain .lss/.lsl paths, e.g. from a file association.
    /// </summary>
    private static void ParseArguments(string[] args, out string splitsPath, out string layoutPath)
    {
        splitsPath = null;
        layoutPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "-s" && i + 1 < args.Length)
            {
                splitsPath = args[++i];
            }
            else if (arg == "-l" && i + 1 < args.Length)
            {
                layoutPath = args[++i];
            }
            else if (arg.EndsWith(".lsl", StringComparison.OrdinalIgnoreCase) && File.Exists(arg))
            {
                layoutPath = arg;
            }
            else if (File.Exists(arg))
            {
                splitsPath = arg;
            }
        }
    }
}
