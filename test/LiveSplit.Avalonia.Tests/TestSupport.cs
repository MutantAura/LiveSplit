using Avalonia;
using Avalonia.Headless;
using LiveSplit.Tests;
using System;
using System.IO;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace LiveSplit.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}

public static class Fixtures
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string RunFiles => Path.Combine(RepoRoot, "lib", "livesplit-core", "tests", "run_files");
    public static string LayoutFiles => Path.Combine(RepoRoot, "lib", "livesplit-core", "tests", "layout_files");

    /// <summary>
    /// Rendered screenshots are written here for manual inspection.
    /// </summary>
    public static string ScreenshotDirectory
    {
        get
        {
            string directory = Path.Combine(RepoRoot, "artifacts", "screenshots", "avalonia");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }

    private static string FindRepoRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (directory != null && !File.Exists(Path.Combine(directory, "LiveSplit.slnx")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return directory ?? throw new InvalidOperationException("Could not find the repository root.");
    }
}
