using Avalonia.Headless.XUnit;
using LiveSplit.Drawing;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Model.RunSavers;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.LayoutFactories;
using LiveSplit.UI.LayoutSavers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Xunit;

namespace LiveSplit.Tests;

public class RunFileTests
{
    public static IEnumerable<object[]> LiveSplitRunFiles()
    {
        return Directory.EnumerateFiles(Fixtures.RunFiles, "*.lss")
            .Where(x => !Path.GetFileName(x).Contains("fuzz"))
            .Select(x => new object[] { Path.GetFileName(x) });
    }

    private static IRun Load(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return new StandardFormatsRunFactory(stream, path).Create(new StandardComparisonGeneratorsFactory());
    }

    private static IRun Reload(IRun run)
    {
        using var stream = new MemoryStream();
        new XMLRunSaver().Save(run, stream);
        stream.Position = 0;
        return new StandardFormatsRunFactory(stream, null).Create(new StandardComparisonGeneratorsFactory());
    }

    [Theory]
    [MemberData(nameof(LiveSplitRunFiles))]
    public void ParsesLiveSplitSplitsFiles(string fileName)
    {
        IRun run = Load(Path.Combine(Fixtures.RunFiles, fileName));

        Assert.NotEmpty(run);
        Assert.Contains(Run.PersonalBestComparisonName, run.CustomComparisons);
    }

    [Theory]
    [MemberData(nameof(LiveSplitRunFiles))]
    public void SavedSplitsReloadIdentically(string fileName)
    {
        IRun original = Load(Path.Combine(Fixtures.RunFiles, fileName));
        IRun reloaded = Reload(original);

        Assert.Equal(original.GameName, reloaded.GameName);
        Assert.Equal(original.CategoryName, reloaded.CategoryName);
        Assert.Equal(original.Offset, reloaded.Offset);
        Assert.Equal(original.AttemptCount, reloaded.AttemptCount);
        Assert.Equal(original.AttemptHistory.Count, reloaded.AttemptHistory.Count);
        Assert.Equal(original.CustomComparisons, reloaded.CustomComparisons);
        Assert.Equal(original.Metadata.PlatformName, reloaded.Metadata.PlatformName);
        Assert.Equal(original.Metadata.RegionName, reloaded.Metadata.RegionName);
        Assert.Equal(original.Metadata.VariableValueNames, reloaded.Metadata.VariableValueNames);
        Assert.Equal(original.GameIcon?.Data, reloaded.GameIcon?.Data);
        Assert.Equal(original.Count, reloaded.Count);

        for (int i = 0; i < original.Count; i++)
        {
            ISegment a = original[i];
            ISegment b = reloaded[i];
            Assert.Equal(a.Name, b.Name);
            Assert.Equal(a.Icon?.Data, b.Icon?.Data);
            Assert.Equal(a.BestSegmentTime, b.BestSegmentTime);
            foreach (string comparison in original.CustomComparisons)
            {
                Assert.Equal(a.Comparisons[comparison], b.Comparisons[comparison]);
            }

            Assert.Equal(a.SegmentHistory.Count, b.SegmentHistory.Count);
            foreach (KeyValuePair<int, Time> entry in a.SegmentHistory)
            {
                Assert.Equal(entry.Value, b.SegmentHistory[entry.Key]);
            }
        }
    }

    [Fact]
    public void ReadsCelesteRunDetails()
    {
        IRun run = Load(Path.Combine(Fixtures.RunFiles, "Celeste - Any% (1.2.1.5).lss"));

        Assert.Equal("Celeste", run.GameName);
        Assert.Equal("Any%", run.CategoryName);
        Assert.NotNull(run.GameIcon);
        Assert.True(run.AttemptHistory.Count > 0);
        Assert.Contains(run, x => x.PersonalBestSplitTime.RealTime != null);
    }

    [Fact]
    public void ConvertsNativeSegmentGroupsToLegacySubsplitNames()
    {
        IRun run = Load(Path.Combine(Fixtures.RunFiles, "celeste_native_segment_groups.lss"));

        // Group "Okay" spans segments 1..3 (end exclusive 4).
        Assert.StartsWith("-", run[1].Name);
        Assert.StartsWith("-", run[2].Name);
        Assert.StartsWith("{Okay} ", run[3].Name);
        Assert.False(run[0].Name.StartsWith('-'));
    }

    [Fact]
    public void FuzzedFilesDoNotCrash()
    {
        foreach (string path in Directory.EnumerateFiles(Fixtures.RunFiles, "*fuzz*.lss"))
        {
            try
            {
                Load(path);
            }
            catch (Exception ex) when (ex is not (StackOverflowException or OutOfMemoryException))
            {
                // Rejecting a malformed file is fine; crashing the process is not.
            }
        }
    }

    [Fact]
    public void PreservesAutoSplitterSettingsThroughSave()
    {
        IRun run = Load(Path.Combine(Fixtures.RunFiles, "Celeste - Any% (1.2.1.5).lss"));
        run.AutoSplitterSettings.InnerXml = "<Version>1.2</Version><CustomSettings><Setting id=\"x\" type=\"bool\">True</Setting></CustomSettings>";
        run.AutoSplitter = AutoSplitter.PreserveSettings(run.AutoSplitterSettings);

        var document = new XmlDocument();
        using (var stream = new MemoryStream())
        {
            new XMLRunSaver().Save(run, stream);
            stream.Position = 0;
            document.Load(stream);
        }

        Assert.Equal(run.AutoSplitterSettings.InnerXml, document["Run"]["AutoSplitterSettings"].InnerXml);
    }
}

public class LayoutFileTests
{
    public LayoutFileTests()
    {
        BuiltInComponents.Register();
    }

    public static IEnumerable<object[]> LayoutFiles()
    {
        return Directory.EnumerateFiles(Fixtures.LayoutFiles, "*.lsl")
            .Append(Path.Combine(Fixtures.RepoRoot, "src", "LiveSplit.View", "Resources", "DefaultLayout.lsl"))
            .Select(x => new object[] { x });
    }

    private static LiveSplitState CreateState()
    {
        IRun run = new Run(new StandardComparisonGeneratorsFactory());
        run.AddSegment("Segment");
        return new LiveSplitState(run, null, null, null, new StandardSettingsFactory().Create());
    }

    private static ILayout Load(string path, LiveSplitState state)
    {
        using FileStream stream = File.OpenRead(path);
        ILayout layout = new XMLLayoutFactory(stream).Create(state);
        state.Layout = layout;
        state.LayoutSettings = layout.Settings;
        return layout;
    }

    private static string Save(ILayout layout)
    {
        using var stream = new MemoryStream();
        new XMLLayoutSaver().Save(layout, stream);
        stream.Position = 0;
        return new StreamReader(stream).ReadToEnd();
    }

    [Theory]
    [MemberData(nameof(LayoutFiles))]
    public void LoadsAllComponentsOfLayoutFiles(string path)
    {
        LiveSplitState state = CreateState();
        ILayout layout = Load(path, state);

        var document = new XmlDocument();
        document.Load(path);
        int componentCount = document["Layout"]["Components"].GetElementsByTagName("Component").Count;

        Assert.Equal(componentCount, layout.LayoutComponents.Count);
        Assert.NotNull(layout.Settings.TimerFont);
        Assert.NotNull(layout.Settings.TimesFont);
        Assert.NotNull(layout.Settings.TextFont);
    }

    [Theory]
    [MemberData(nameof(LayoutFiles))]
    public void SavedLayoutsAreStable(string path)
    {
        LiveSplitState state = CreateState();
        string first = Save(Load(path, state));

        string tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, first);
            string second = Save(Load(tempPath, CreateState()));
            Assert.Equal(first, second);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void KeepsSettingsOfUnavailableComponents()
    {
        string path = Path.Combine(Fixtures.LayoutFiles, "All.lsl");
        LiveSplitState state = CreateState();
        ILayout layout = Load(path, state);

        Assert.Contains(layout.Components, x => x is UnavailableComponent { Path: "PBChance.dll" });

        var original = new XmlDocument();
        original.Load(path);
        var saved = new XmlDocument();
        saved.LoadXml(Save(layout));

        static XmlElement FindSettings(XmlDocument document, string componentPath)
        {
            return document["Layout"]["Components"].GetElementsByTagName("Component").OfType<XmlElement>()
                .First(x => x["Path"].InnerText == componentPath)["Settings"];
        }

        foreach (string componentPath in new[] { "PBChance.dll", "LiveSplit.ScriptableAutoSplit.dll" })
        {
            Assert.Equal(FindSettings(original, componentPath).InnerXml, FindSettings(saved, componentPath).InnerXml);
        }
    }

    [Fact]
    public void ReadsLayoutFontsFromBinaryFormatterData()
    {
        ILayout layout = Load(Path.Combine(Fixtures.RepoRoot, "src", "LiveSplit.View", "Resources", "DefaultLayout.lsl"), CreateState());

        Assert.Equal("Century Gothic", layout.Settings.TimerFont.Name);
        Assert.True(layout.Settings.TimerFont.Bold);
        Assert.Equal("Segoe UI", layout.Settings.TextFont.Name);
        Assert.True(layout.Settings.TextFont.Size > 0);
    }
}

public class LegacyBinarySerializerTests
{
    [Theory]
    [InlineData("Segoe UI", 16f, FontStyle.Regular, GraphicsUnit.Pixel)]
    [InlineData("Century Gothic", 43.75f, FontStyle.Bold, GraphicsUnit.Pixel)]
    [InlineData("Comic Sans MS", 12.5f, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point)]
    public void FontsRoundTrip(string name, float size, FontStyle style, GraphicsUnit unit)
    {
        var font = new Font(name, size, style, unit);

        Font read = LegacyBinarySerializer.ReadFont(LegacyBinarySerializer.WriteFont(font));

        Assert.Equal(font, read);
    }

    [Fact]
    public void ImagesRoundTrip()
    {
        byte[] data = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4, 5];

        Image read = LegacyBinarySerializer.ReadImage(LegacyBinarySerializer.WriteImage(new Image(data)));

        Assert.Equal(data, read.Data);
    }

    [Fact]
    public void WrittenFontsMatchBinaryFormatterOutput()
    {
        // The default layout's text font was written by .NET Framework's BinaryFormatter.
        var document = new XmlDocument();
        document.Load(Path.Combine(Fixtures.RepoRoot, "src", "LiveSplit.View", "Resources", "DefaultLayout.lsl"));
        byte[] original = Convert.FromBase64String(document["Layout"]["Settings"]["TextFont"].InnerText.Trim());

        Font font = LegacyBinarySerializer.ReadFont(original);
        byte[] written = LegacyBinarySerializer.WriteFont(font);

        Assert.Equal(original, written);
    }

    [AvaloniaFact]
    public void DecodesIconsFromSplitsFiles()
    {
        string path = Path.Combine(Fixtures.RunFiles, "Celeste - Any% (1.2.1.5).lss");
        using FileStream stream = File.OpenRead(path);
        IRun run = new StandardFormatsRunFactory(stream, path).Create(new StandardComparisonGeneratorsFactory());

        Assert.NotNull(run.GameIcon.ToBitmap());
        Assert.True(run.GameIcon.Width > 0);
        Assert.All(run.Where(x => x.Icon != null), x => Assert.NotNull(x.Icon.ToBitmap()));
    }
}
