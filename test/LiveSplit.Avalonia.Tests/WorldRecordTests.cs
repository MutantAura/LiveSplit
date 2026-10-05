using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Xunit;

namespace LiveSplit.Tests;

public class WorldRecordTests
{
    private static (LiveSplitState State, WorldRecordComponent Component, FakeSpeedrunCom Server) Create(string game = "Celeste", string category = "Any%")
    {
        IRun run = new Run(new StandardComparisonGeneratorsFactory()) { GameName = game, CategoryName = category };
        run.AddSegment("Summit");
        run.Metadata.VariableValueNames["Seeded"] = "Set Seed";
        run.Metadata.VariableValueNames["Version"] = "1.4";

        var state = new LiveSplitState(run, null, new Layout { Settings = new StandardLayoutSettingsFactory().Create() }, null, new StandardSettingsFactory().Create());
        state.LayoutSettings = state.Layout.Settings;
        var server = new FakeSpeedrunCom();
        var component = new WorldRecordComponent(state, new SpeedrunComApi(new HttpClient(server)));
        return (state, component, server);
    }

    private static async Task Load(LiveSplitState state, WorldRecordComponent component, LayoutMode mode = LayoutMode.Vertical)
    {
        component.Update(new Invalidator(), state, 300, 30, mode);
        await component.PendingRefresh;
        component.Update(new Invalidator(), state, 300, 30, mode);
    }

    private static InfoTextComponent Text(WorldRecordComponent component)
    {
        return (InfoTextComponent)typeof(InfoComponentBase<WorldRecordSettings>)
            .GetProperty("InternalComponent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(component);
    }

    [Fact]
    public async Task ShowsTiedWorldRecordsFilteredBySubcategory()
    {
        (LiveSplitState state, WorldRecordComponent component, FakeSpeedrunCom server) = Create();
        await Load(state, component);

        Assert.Equal(2, component.Records.Count);
        Assert.Equal("World Record is 24:48.843 by secureaccount, SomeGuest", Text(component).InformationName);
        Assert.Contains("WR: 24:48.843 (2-way tie)", Text(component).AlternateNameText);

        // Only the per-game category's subcategory is filtered (variables are off by default),
        // and nothing else is sent.
        string leaderboard = Assert.Single(server.Requests, x => x.Contains("leaderboards/"));
        Assert.Equal(SpeedrunComApi.BaseUrl + "leaderboards/g1/category/c1?top=1&embed=players&var-v2=s2", leaderboard);
    }

    [Fact]
    public async Task ShowsRecordNextToTheNameWhenNotCentered()
    {
        (LiveSplitState state, WorldRecordComponent component, FakeSpeedrunCom server) = Create();
        server.Leaderboard = FakeSpeedrunCom.SingleRecord;
        component.Settings.CenteredText = false;
        await Load(state, component);

        Assert.Equal("24:48 by secureaccount", Text(component).InformationValue);
    }

    [Fact]
    public async Task AppliesFiltersAndTimingMethodFromSettings()
    {
        (LiveSplitState state, WorldRecordComponent component, FakeSpeedrunCom server) = Create();
        state.Run.Metadata.PlatformName = "Nintendo Switch";
        state.Run.Metadata.RegionName = "EUR / PAL";
        state.Run.Metadata.UsesEmulator = true;
        component.Settings.FilterVariables = true;
        component.Settings.FilterPlatform = true;
        component.Settings.FilterRegion = true;
        component.Settings.TimingMethod = "Real Time";
        await Load(state, component);

        string leaderboard = server.Requests.Single(x => x.Contains("leaderboards/"));
        Assert.Contains("platform=sw", leaderboard);
        Assert.Contains("region=eu", leaderboard);
        Assert.Contains("emulators=true", leaderboard);
        Assert.Contains("timing=realtime", leaderboard);
        Assert.Contains("var-v1=x1", leaderboard);
        Assert.Contains("var-v2=s2", leaderboard);
        Assert.DoesNotContain("var-v3", leaderboard);
        Assert.DoesNotContain("var-v4", leaderboard);

        // The first run has no real time on the leaderboard; like the Windows version, the
        // formatter shows a missing time as 0.
        Assert.StartsWith("World Record is 0 by", Text(component).InformationName);
    }

    [Fact]
    public async Task ShowsOwnPersonalBestWhenItBeatsTheRecord()
    {
        (LiveSplitState state, WorldRecordComponent component, FakeSpeedrunCom server) = Create();
        server.Leaderboard = FakeSpeedrunCom.SingleRecord;
        state.Run[0].PersonalBestSplitTime = new Time(TimingMethod.GameTime, TimeSpan.FromSeconds(1400));
        await Load(state, component);

        Assert.Equal("World Record is 23:20 by me", Text(component).InformationName);
    }

    [Fact]
    public async Task UnknownGameShowsUnknownRecord()
    {
        (LiveSplitState state, WorldRecordComponent component, FakeSpeedrunCom server) = Create(game: "Not A Real Game");
        await Load(state, component);

        Assert.Null(component.Records);
        Assert.Equal("Unknown World Record", Text(component).InformationName);
        Assert.DoesNotContain(server.Requests, x => x.Contains("leaderboards/"));
    }

    [Fact]
    public void ReadsAndWritesWindowsSettings()
    {
        var document = new XmlDocument();
        document.LoadXml("""
            <Settings>
              <Version>1.6</Version>
              <TextColor>FFFFFFFF</TextColor><OverrideTextColor>False</OverrideTextColor>
              <TimeColor>FFFF0000</TimeColor><OverrideTimeColor>True</OverrideTimeColor>
              <BackgroundColor>00000000</BackgroundColor><BackgroundColor2>00000000</BackgroundColor2>
              <BackgroundGradient>Plain</BackgroundGradient>
              <Display2Rows>False</Display2Rows><CenteredText>False</CenteredText>
              <FilterRegion>True</FilterRegion><FilterPlatform>False</FilterPlatform>
              <FilterVariables>True</FilterVariables><FilterSubcategories>False</FilterSubcategories>
              <TimingMethod>Game Time</TimingMethod><PrecisionType>Milliseconds</PrecisionType>
            </Settings>
            """);

        var settings = new WorldRecordSettings();
        settings.SetSettings(document.DocumentElement);
        Assert.False(settings.CenteredText);
        Assert.True(settings.OverrideTimeColor);
        Assert.True(settings.FilterRegion);
        Assert.False(settings.FilterSubcategories);
        Assert.Equal(SpeedrunComTiming.GameTime, settings.TimingOverride);
        Assert.Equal(WorldRecordPrecisionType.Milliseconds, settings.WRPrecision);

        XmlNode saved = settings.GetSettings(new XmlDocument());
        string[] expected = ["Version", "TextColor", "OverrideTextColor", "TimeColor", "OverrideTimeColor", "BackgroundColor", "BackgroundColor2",
            "BackgroundGradient", "Display2Rows", "CenteredText", "FilterRegion", "FilterPlatform", "FilterVariables", "FilterSubcategories",
            "TimingMethod", "PrecisionType"];
        Assert.Equal(expected, saved.ChildNodes.Cast<XmlNode>().Select(x => x.Name));
        Assert.Equal("Milliseconds", saved["PrecisionType"]!.InnerText);
    }

    [Fact]
    public void LayoutsResolveTheWindowsComponent()
    {
        BuiltInComponents.Register();
        (LiveSplitState state, _, _) = Create();
        ILayoutComponent component = ComponentManager.LoadLayoutComponent("LiveSplit.WorldRecord.dll", state);
        Assert.IsType<WorldRecordComponent>(component.Component);
    }
}
