using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.TimeFormatters;
using LiveSplit.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace LiveSplit.UI.Components;

public enum WorldRecordPrecisionType
{
    FromLeaderboard,
    Seconds,
    Milliseconds
}

/// <summary>
/// Settings of the World Record component, stored with the same elements as the Windows
/// LiveSplit.WorldRecord component.
/// </summary>
public class WorldRecordSettings : InfoTimeSettings
{
    public const string DefaultTimingMethod = "Default for Leaderboard";

    public bool CenteredText { get; set; } = true;

    [Setting("Filter by Subcategories")]
    public bool FilterSubcategories { get; set; } = true;
    [Setting("Filter by Variables")]
    public bool FilterVariables { get; set; }
    [Setting("Filter by Platform")]
    public bool FilterPlatform { get; set; }
    [Setting("Filter by Region")]
    public bool FilterRegion { get; set; }

    [Setting(Options = [DefaultTimingMethod, "Real Time", "Real Time Without Loads", "Game Time"])]
    public string TimingMethod { get; set; } = DefaultTimingMethod;

    [Setting("Precision")]
    public WorldRecordPrecisionType WRPrecision { get; set; } = WorldRecordPrecisionType.FromLeaderboard;

    public override void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        TextColor = SettingsHelper.ParseColor(element["TextColor"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        TimeColor = SettingsHelper.ParseColor(element["TimeColor"]);
        OverrideTimeColor = SettingsHelper.ParseBool(element["OverrideTimeColor"]);
        ParseBackground(element);
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"]);
        CenteredText = SettingsHelper.ParseBool(element["CenteredText"]);
        FilterRegion = SettingsHelper.ParseBool(element["FilterRegion"]);
        FilterPlatform = SettingsHelper.ParseBool(element["FilterPlatform"]);
        FilterVariables = SettingsHelper.ParseBool(element["FilterVariables"]);
        FilterSubcategories = SettingsHelper.ParseBool(element["FilterSubcategories"], true);
        TimingMethod = SettingsHelper.ParseString(element["TimingMethod"], DefaultTimingMethod);
        WRPrecision = SettingsHelper.ParseEnum(element["PrecisionType"], WorldRecordPrecisionType.FromLeaderboard);
    }

    protected override int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.6") ^
            SettingsHelper.CreateSetting(document, parent, "TextColor", TextColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
            SettingsHelper.CreateSetting(document, parent, "TimeColor", TimeColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTimeColor", OverrideTimeColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
            SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
            SettingsHelper.CreateSetting(document, parent, "CenteredText", CenteredText) ^
            SettingsHelper.CreateSetting(document, parent, "FilterRegion", FilterRegion) ^
            SettingsHelper.CreateSetting(document, parent, "FilterPlatform", FilterPlatform) ^
            SettingsHelper.CreateSetting(document, parent, "FilterVariables", FilterVariables) ^
            SettingsHelper.CreateSetting(document, parent, "FilterSubcategories", FilterSubcategories) ^
            SettingsHelper.CreateSetting(document, parent, "TimingMethod", TimingMethod) ^
            SettingsHelper.CreateSetting(document, parent, "PrecisionType", WRPrecision);
    }

    public SpeedrunComTiming? TimingOverride => TimingMethod switch
    {
        "Real Time" => SpeedrunComTiming.RealTime,
        "Real Time Without Loads" => SpeedrunComTiming.RealTimeWithoutLoads,
        "Game Time" => SpeedrunComTiming.GameTime,
        _ => null
    };
}

/// <summary>
/// Shows the world record of the run's speedrun.com leaderboard. Ported from the Windows
/// LiveSplit.WorldRecord component; speedrun.com is queried through <see cref="SpeedrunComApi"/>
/// because the run metadata of this front end isn't resolved against speedrun.com.
/// </summary>
/// <remarks>
/// Unlike the Windows version, the runner's own record isn't shown as "me", since this front end
/// has no speedrun.com login.
/// </remarks>
[GlobalFontConsumer(GlobalFont.TextFont)]
public class WorldRecordComponent : InfoComponentBase<WorldRecordSettings>
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private readonly SpeedrunComApi api;
    private readonly GraphicsCache cache = new();
    private readonly GeneralTimeFormatter wrFormatter = new AutomaticPrecisionTimeFormatter { Accuracy = TimeAccuracy.Milliseconds };
    private readonly GeneralTimeFormatter pbFormatter = new RegularTimeFormatter();
    private TimeStamp lastUpdate;
    private int requestVersion;
    private bool isLoading;
    private LayoutMode mode = LayoutMode.Vertical;

    public WorldRecordComponent(LiveSplitState state)
        : this(state, SpeedrunComApi.Shared) { }

    public WorldRecordComponent(LiveSplitState state, SpeedrunComApi api)
        : base(state, new WorldRecordSettings(), new InfoTextComponent("World Record", TimeFormatConstants.DASH))
    {
        this.api = api;
    }

    public override string ComponentName => "World Record";

    /// <summary>
    /// The first place runs of the leaderboard (several when tied), or null.
    /// </summary>
    public IReadOnlyList<SrcRecord> Records { get; private set; }

    public SrcGame Game { get; private set; }
    public SrcCategory Category { get; private set; }

    /// <summary>
    /// Completes when the current request to speedrun.com has finished. For tests.
    /// </summary>
    internal Task PendingRefresh { get; private set; } = Task.CompletedTask;

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        this.mode = mode;
        RunMetadata metadata = state.Run.Metadata;

        cache.Restart();
        cache["Game"] = state.Run.GameName;
        cache["Category"] = state.Run.CategoryName;
        cache["PlatformID"] = Settings.FilterPlatform ? metadata.PlatformName : null;
        cache["RegionID"] = Settings.FilterRegion ? metadata.RegionName : null;
        cache["UsesEmulator"] = Settings.FilterPlatform ? (bool?)metadata.UsesEmulator : null;
        cache["Variables"] = Settings.FilterVariables || Settings.FilterSubcategories ? string.Join(",", metadata.VariableValueNames.Values) : null;
        cache["FilterVariables"] = Settings.FilterVariables;
        cache["FilterSubcategories"] = Settings.FilterSubcategories;
        cache["TimingMethod"] = Settings.TimingMethod;
        cache["PrecisionType"] = Settings.WRPrecision;

        if (cache.HasChanged)
        {
            isLoading = true;
            Records = null;
            ShowWorldRecord();
            PendingRefresh = Refresh(state);
        }
        else if (lastUpdate != null && TimeStamp.Now - lastUpdate >= RefreshInterval)
        {
            PendingRefresh = Refresh(state);
        }
        else
        {
            cache["CenteredText"] = IsCentered;
            cache["RealPBTime"] = GetPBTime(TimingMethod.RealTime);
            cache["GamePBTime"] = GetPBTime(TimingMethod.GameTime);
            if (cache.HasChanged)
            {
                ShowWorldRecord();
            }
        }

        InternalComponent.Update(invalidator, state, width, height, mode);
    }

    private bool IsCentered => Settings.CenteredText && !Settings.Display2Rows && mode == LayoutMode.Vertical;

    private async Task Refresh(LiveSplitState state)
    {
        lastUpdate = TimeStamp.Now;
        int version = ++requestVersion;
        string gameName = state.Run.GameName;
        string categoryName = state.Run.CategoryName;
        RunMetadata metadata = state.Run.Metadata;

        SrcGame game = null;
        SrcCategory category = null;
        IReadOnlyList<SrcRecord> records = null;
        try
        {
            game = await api.FindGameAsync(gameName);
            if (game != null && !string.IsNullOrWhiteSpace(categoryName))
            {
                IReadOnlyList<SrcCategory> categories = await api.GetCategoriesAsync(game.Id);
                category = categories.FirstOrDefault(x => x.IsPerGame && x.Name == categoryName)
                    ?? categories.FirstOrDefault(x => x.IsPerGame && string.Equals(x.Name, categoryName, StringComparison.OrdinalIgnoreCase));

                if (category != null)
                {
                    IReadOnlyList<SrcVariable> variables = await api.GetVariablesAsync(game.Id);
                    records = await api.GetWorldRecordsAsync(game.Id, category.Id, CreateFilter(game, category, variables, metadata, Settings));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Could not load the world record from speedrun.com: {ex.Message}");
        }

        // A newer request has started (e.g. the run was changed); its result wins.
        if (version != requestVersion)
        {
            return;
        }

        Game = game;
        Category = category;
        Records = records?.Count > 0 ? records : null;
        isLoading = false;
        ShowWorldRecord();
    }

    /// <summary>
    /// Builds the leaderboard filter from the run's metadata, the way the Windows version does:
    /// subcategories and other variables by their value names, platform and region by name.
    /// </summary>
    internal static SrcLeaderboardFilter CreateFilter(SrcGame game, SrcCategory category, IReadOnlyList<SrcVariable> variables, RunMetadata metadata, WorldRecordSettings settings)
    {
        var values = new Dictionary<string, string>();
        foreach (SrcVariable variable in variables.Where(x => x.IsFullGame && (x.CategoryId == null || x.CategoryId == category.Id)))
        {
            bool include = variable.IsSubcategory ? settings.FilterSubcategories : settings.FilterVariables;
            if (include
                && metadata.VariableValueNames.TryGetValue(variable.Name, out string label)
                && label != null
                && variable.ValueIdsByLabel.TryGetValue(label, out string valueId))
            {
                values[variable.Id] = valueId;
            }
        }

        return new SrcLeaderboardFilter
        {
            PlatformId = settings.FilterPlatform && metadata.PlatformName != null ? game.PlatformIds.GetValueOrDefault(metadata.PlatformName) : null,
            RegionId = settings.FilterRegion && metadata.RegionName != null ? game.RegionIds.GetValueOrDefault(metadata.RegionName) : null,
            Emulators = settings.FilterPlatform ? metadata.UsesEmulator : null,
            Timing = settings.TimingOverride,
            VariableValues = values
        };
    }

    private void ShowWorldRecord()
    {
        bool centered = IsCentered;
        SrcRecord record = Records?.FirstOrDefault();
        if (record != null)
        {
            SpeedrunComTiming timing = Settings.TimingOverride ?? Game.DefaultTiming;
            TimeSpan? wrTime = Settings.TimingOverride switch
            {
                SpeedrunComTiming.RealTime => record.RealTime,
                SpeedrunComTiming.RealTimeWithoutLoads => record.RealTimeWithoutLoads,
                SpeedrunComTiming.GameTime => record.GameTime,
                _ => record.Primary
            };

            bool millisecondsPrecision = IsMillisecondsPrecision(wrTime);
            bool fromLeaderboard = Settings.WRPrecision == WorldRecordPrecisionType.FromLeaderboard;
            pbFormatter.AutomaticPrecision = fromLeaderboard;
            wrFormatter.AutomaticPrecision = fromLeaderboard;
            wrFormatter.Accuracy = fromLeaderboard || millisecondsPrecision ? TimeAccuracy.Milliseconds : TimeAccuracy.Seconds;

            // Align the PB with the precision available on the leaderboard.
            pbFormatter.Accuracy = millisecondsPrecision ? TimeAccuracy.Milliseconds : TimeAccuracy.Seconds;

            string formatted = wrFormatter.Format(wrTime);
            string runners = string.Join(", ", Records.Select(x => string.Join(" & ", x.Players)));
            int ties = Records.Count;

            TimeSpan? pbTime = GetPBTime(timing == SpeedrunComTiming.RealTime ? TimingMethod.RealTime : TimingMethod.GameTime);
            if (IsPBTimeLower(pbTime, wrTime))
            {
                formatted = pbFormatter.Format(pbTime);
                runners = Category?.Players > 1 ? "us" : "me";
                ties = 1;
            }

            if (centered)
            {
                var texts = new List<string>
                {
                    $"World Record is {formatted} by {runners}",
                    $"World Record: {formatted} by {runners}",
                    $"WR: {formatted} by {runners}",
                    $"WR is {formatted} by {runners}"
                };

                if (ties > 1)
                {
                    texts.Add($"World Record is {formatted} ({ties}-way tie)");
                    texts.Add($"World Record: {formatted} ({ties}-way tie)");
                    texts.Add($"WR: {formatted} ({ties}-way tie)");
                    texts.Add($"WR is {formatted} ({ties}-way tie)");
                }

                InternalComponent.InformationName = texts[0];
                InternalComponent.AlternateNameText = texts;
            }
            else
            {
                InternalComponent.InformationValue = ties > 1 ? $"{formatted} ({ties}-way tie)" : $"{formatted} by {runners}";
            }
        }
        else if (isLoading)
        {
            if (centered)
            {
                InternalComponent.InformationName = "Loading World Record...";
                InternalComponent.AlternateNameText = ["Loading WR..."];
            }
            else
            {
                InternalComponent.InformationValue = "Loading...";
            }
        }
        else if (centered)
        {
            InternalComponent.InformationName = "Unknown World Record";
            InternalComponent.AlternateNameText = ["Unknown WR"];
        }
        else
        {
            InternalComponent.InformationValue = TimeFormatConstants.DASH;
        }
    }

    private bool IsMillisecondsPrecision(TimeSpan? recordTime)
    {
        return Settings.WRPrecision == WorldRecordPrecisionType.FromLeaderboard
            ? recordTime is not TimeSpan time || time.Milliseconds != 0
            : Settings.WRPrecision == WorldRecordPrecisionType.Milliseconds;
    }

    private bool IsPBTimeLower(TimeSpan? pbTime, TimeSpan? wrTime)
    {
        if (pbTime is not TimeSpan pb || wrTime is not TimeSpan wr)
        {
            return false;
        }

        return IsMillisecondsPrecision(wrTime)
            ? (int)pb.TotalMilliseconds <= (int)wr.TotalMilliseconds
            : (int)pb.TotalSeconds <= (int)wr.TotalSeconds;
    }

    private TimeSpan? GetPBTime(TimingMethod method)
    {
        if (CurrentState.Run.Count == 0)
        {
            return null;
        }

        ISegment lastSplit = CurrentState.Run[^1];
        TimeSpan? pbTime = lastSplit.PersonalBestSplitTime[method];
        TimeSpan? splitTime = lastSplit.SplitTime[method];
        return CurrentState.CurrentPhase == TimerPhase.Ended && splitTime < pbTime ? splitTime : pbTime;
    }

    protected override void PrepareDraw(LiveSplitState state)
    {
        base.PrepareDraw(state);

        if (IsCentered)
        {
            InternalComponent.NameLabel.HorizontalAlignment = StringAlignment.Center;
            InternalComponent.ValueLabel.HorizontalAlignment = StringAlignment.Center;
            InternalComponent.NameLabel.VerticalAlignment = StringAlignment.Center;
            InternalComponent.ValueLabel.VerticalAlignment = StringAlignment.Center;
            InternalComponent.InformationValue = "";
        }
        else
        {
            bool stacked = mode == LayoutMode.Horizontal || Settings.Display2Rows;
            InternalComponent.InformationName = "World Record";
            InternalComponent.AlternateNameText = ["WR"];
            InternalComponent.NameLabel.HorizontalAlignment = StringAlignment.Near;
            InternalComponent.ValueLabel.HorizontalAlignment = StringAlignment.Far;
            InternalComponent.NameLabel.VerticalAlignment = stacked ? StringAlignment.Near : StringAlignment.Center;
            InternalComponent.ValueLabel.VerticalAlignment = stacked ? StringAlignment.Far : StringAlignment.Center;
        }
    }
}
