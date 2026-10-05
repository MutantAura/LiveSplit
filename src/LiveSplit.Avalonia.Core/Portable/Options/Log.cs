using System;
using System.Diagnostics;
using System.IO;

namespace LiveSplit.Options;

/// <summary>
/// Portable replacement for LiveSplit.Core's Log, which writes to the Windows event log.
/// Messages go to the trace listeners and to LiveSplit.log in the user's data directory.
/// </summary>
public static class Log
{
    public static string LogFilePath { get; }

    static Log()
    {
        try
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LiveSplit");
            Directory.CreateDirectory(directory);
            LogFilePath = Path.Combine(directory, "LiveSplit.log");

            var listener = new TextWriterTraceListener(LogFilePath, "LiveSplitFile")
            {
                Filter = new EventTypeFilter(SourceLevels.Warning)
            };
            Trace.Listeners.Add(listener);
            Trace.AutoFlush = true;
        }
        catch { }
    }

    public static void Error(Exception ex)
    {
        try
        {
            Trace.TraceError("{0}\n\n{1}", ex?.Message, ex?.StackTrace);
        }
        catch { }
    }

    public static void Error(string message)
    {
        try
        {
            Trace.TraceError(message);
        }
        catch { }
    }

    public static void Info(string message)
    {
        try
        {
            Trace.TraceInformation(message);
        }
        catch { }
    }

    public static void Warning(string message)
    {
        try
        {
            Trace.TraceWarning(message);
        }
        catch { }
    }
}
