using LiveSplit.Model.Comparisons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using static LiveSplit.Model.IndexedTimeHelper;
using static LiveSplit.UI.SettingsHelper;

namespace LiveSplit.Model.RunFactories;

/// <summary>
/// Managed parser for LiveSplit's own splits format (.lss), used instead of the native
/// livesplit-core parser so that no platform-specific binaries are needed. It reads every
/// .lss version the Windows version writes (1.0 through 1.7). Other timers' formats are not
/// supported yet.
/// </summary>
public class StandardFormatsRunFactory : IRunFactory
{
    public Stream Stream { get; set; }
    public string FilePath { get; set; }

    public StandardFormatsRunFactory(Stream stream = null, string filePath = "")
    {
        Stream = stream;
        FilePath = filePath;
    }

    public IRun Create(IComparisonGeneratorsFactory factory)
    {
        var document = new XmlDocument();
        try
        {
            document.Load(Stream);
        }
        catch (XmlException ex)
        {
            throw new NotSupportedException("Only LiveSplit splits files (.lss) are supported by this version of LiveSplit.", ex);
        }

        XmlElement parent = document["Run"]
            ?? throw new NotSupportedException("Only LiveSplit splits files (.lss) are supported by this version of LiveSplit.");

        var run = new Run(factory);
        Version version = ParseAttributeVersion(parent);

        // Files from before metadata existed get empty values, like the livesplit-core parser.
        run.Metadata.PlatformName = string.Empty;
        run.Metadata.RegionName = string.Empty;

        if (version >= new Version(1, 6))
        {
            ParseMetadata(parent["Metadata"], run);
        }

        run.GameIcon = GetImageFromElement(parent["GameIcon"]);
        run.GameName = ParseString(parent["GameName"]);
        run.CategoryName = ParseString(parent["CategoryName"]);
        run.Offset = ParseTimeSpan(parent["Offset"]);
        run.AttemptCount = ParseInt(parent["AttemptCount"]);

        if (version >= new Version(1, 7) && parent["LayoutPath"] is XmlElement layoutPath && layoutPath.InnerText.Length > 0)
        {
            run.LayoutPath = layoutPath.InnerText;
        }

        ParseAttemptHistory(version, parent, run);

        foreach (XmlElement segmentElement in parent["Segments"].GetElementsByTagName("Segment").OfType<XmlElement>())
        {
            run.Add(ParseSegment(version, segmentElement, run));
        }

        if (parent["SegmentGroups"] is XmlElement segmentGroups)
        {
            ApplyNativeSegmentGroups(segmentGroups, run);
        }

        var autoSplitterDocument = new XmlDocument();
        autoSplitterDocument.LoadXml(parent["AutoSplitterSettings"]?.OuterXml ?? "<AutoSplitterSettings />");
        run.AutoSplitterSettings = autoSplitterDocument.DocumentElement;
        run.AutoSplitterSettings.Attributes.Append(ToAttribute(autoSplitterDocument, "gameName", run.GameName));

        if (!string.IsNullOrEmpty(FilePath))
        {
            run.FilePath = FilePath;
        }

        if (run.Count < 1)
        {
            throw new Exception("Run factory created a run without at least one segment");
        }

        return run;
    }

    /// <summary>
    /// Splits files written by newer versions (1.8.1+) store subsplit groups natively. Like
    /// the livesplit-core based parser, convert them to the legacy naming scheme the
    /// components understand: "-Name" for subsplits and "{Group} Name" for a group's last split.
    /// </summary>
    private static void ApplyNativeSegmentGroups(XmlElement segmentGroups, IRun run)
    {
        foreach (XmlElement group in segmentGroups.ChildNodes.OfType<XmlElement>().Where(x => x.Name == "SegmentGroup"))
        {
            if (!int.TryParse(group.GetAttribute("start"), out int start)
                || !int.TryParse(group.GetAttribute("end"), out int end)
                || start < 0 || end > run.Count || end <= start)
            {
                continue;
            }

            for (int i = start; i < end - 1; i++)
            {
                run[i].Name = "-" + run[i].Name;
            }

            string groupName = group["Name"]?.InnerText;
            if (!string.IsNullOrEmpty(groupName))
            {
                run[end - 1].Name = $"{{{groupName}}} {run[end - 1].Name}";
            }
        }
    }

    private static void ParseMetadata(XmlElement metadata, IRun run)
    {
        if (metadata == null)
        {
            return;
        }

        run.Metadata.RunID = metadata["Run"]?.GetAttribute("id");
        if (metadata["Platform"] is XmlElement platform)
        {
            run.Metadata.PlatformName = platform.InnerText;
            run.Metadata.UsesEmulator = bool.TryParse(platform.GetAttribute("usesEmulator"), out bool usesEmulator) && usesEmulator;
        }

        run.Metadata.RegionName = metadata["Region"]?.InnerText ?? string.Empty;

        if (metadata["Variables"] is XmlElement variables)
        {
            foreach (XmlElement variable in variables.ChildNodes.OfType<XmlElement>())
            {
                run.Metadata.VariableValueNames[variable.GetAttribute("name")] = variable.InnerText;
            }
        }

        if (metadata["CustomVariables"] is XmlElement customVariables)
        {
            foreach (XmlElement variable in customVariables.ChildNodes.OfType<XmlElement>())
            {
                run.Metadata.GetOrAddCustomVariable(variable.GetAttribute("name")).AsPermanent().Value = variable.InnerText;
            }
        }
    }

    private static void ParseAttemptHistory(Version version, XmlElement parent, IRun run)
    {
        if (version >= new Version(1, 5, 0))
        {
            XmlElement attemptHistory = parent["AttemptHistory"];
            if (attemptHistory == null)
            {
                return;
            }

            foreach (XmlElement attemptNode in attemptHistory.GetElementsByTagName("Attempt").OfType<XmlElement>())
            {
                run.AttemptHistory.Add(Attempt.ParseXml(attemptNode));
            }
        }
        else
        {
            XmlElement runHistory = parent["RunHistory"];
            if (runHistory == null)
            {
                return;
            }

            foreach (XmlElement runHistoryNode in runHistory.GetElementsByTagName("Time").OfType<XmlElement>())
            {
                IIndexedTime indexedTime = version >= new Version(1, 4, 1) ? ParseXml(runHistoryNode) : ParseXmlOld(runHistoryNode);
                run.AttemptHistory.Add(new Attempt(indexedTime.Index, indexedTime.Time, null, null, null));
            }
        }
    }

    private static Segment ParseSegment(Version version, XmlElement segmentElement, IRun run)
    {
        var split = new Segment(ParseString(segmentElement["Name"]))
        {
            Icon = GetImageFromElement(segmentElement["Icon"])
        };

        if (version >= new Version(1, 3))
        {
            // Minimal files may omit everything but the name, which the Windows version accepts.
            IEnumerable<XmlElement> splitTimes = segmentElement["SplitTimes"]?.GetElementsByTagName("SplitTime").OfType<XmlElement>() ?? [];
            foreach (XmlElement comparisonElement in splitTimes)
            {
                string comparisonName = comparisonElement.GetAttribute("name");
                if (comparisonElement.InnerText.Length > 0)
                {
                    split.Comparisons[comparisonName] = ParseTime(version, comparisonElement);
                }

                if (!run.CustomComparisons.Contains(comparisonName))
                {
                    run.CustomComparisons.Add(comparisonName);
                }
            }
        }
        else
        {
            XmlElement pbSplit = segmentElement["PersonalBestSplitTime"];
            if (pbSplit != null && pbSplit.InnerText.Length > 0)
            {
                split.Comparisons[Run.PersonalBestComparisonName] = ParseTime(version, pbSplit);
            }
        }

        XmlElement goldSplit = segmentElement["BestSegmentTime"];
        if (goldSplit != null && goldSplit.InnerText.Length > 0)
        {
            split.BestSegmentTime = ParseTime(version, goldSplit);
        }

        XmlElement history = segmentElement["SegmentHistory"];
        if (history != null)
        {
            foreach (XmlElement node in history.GetElementsByTagName("Time").OfType<XmlElement>())
            {
                IIndexedTime indexedTime = version >= new Version(1, 4, 1) ? ParseXml(node) : ParseXmlOld(node);
                split.SegmentHistory.TryAdd(indexedTime.Index, indexedTime.Time);
            }
        }

        return split;
    }

    private static Time ParseTime(Version version, XmlElement element)
    {
        return version >= new Version(1, 4, 1) ? Time.FromXml(element) : Time.ParseText(element.InnerText);
    }
}
