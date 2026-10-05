using LiveSplit.Options;
using System;
using System.Diagnostics;

namespace LiveSplit.UI;

/// <summary>
/// Opens web pages in the default browser. Shell execution maps to the system handler on every
/// platform (ShellExecute on Windows, "open" on macOS and xdg-open on Linux).
/// </summary>
public static class UrlLauncher
{
    public static bool Open(string url)
    {
        try
        {
            using Process process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return false;
        }
    }
}
