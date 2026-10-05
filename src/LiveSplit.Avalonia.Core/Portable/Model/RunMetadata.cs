using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.Model;

/// <summary>
/// Portable copy of LiveSplit.Core's RunMetadata. It keeps the metadata stored in splits files
/// (run id, platform, region, variables and custom variables) but does not resolve it against
/// speedrun.com, because the SpeedrunComSharp client is not available on this target yet.
/// </summary>
public class RunMetadata
{
    public IRun LiveSplitRun { get; private set; }

    private string runId;
    private string platformName;
    private string regionName;
    private bool usesEmulator;

    public event EventHandler PropertyChanged;

    public string RunID
    {
        get => runId;
        set
        {
            runId = value;

            if (value != null)
            {
                TriggerPropertyChanged(false);
            }
        }
    }

    public string PlatformName
    {
        get => platformName;
        set
        {
            if (platformName != value)
            {
                TriggerPropertyChanged(true);
            }

            platformName = value;
        }
    }

    public string RegionName
    {
        get => regionName;
        set
        {
            if (regionName != value)
            {
                TriggerPropertyChanged(true);
            }

            regionName = value;
        }
    }

    public IDictionary<string, string> VariableValueNames { get; set; }

    /// <summary>
    ///     A dictionary mapping custom variable names to <see cref="CustomVariable"/> objects.
    /// </summary>
    public Dictionary<string, CustomVariable> CustomVariables { get; private set; } = [];

    public CustomVariable GetOrAddCustomVariable(string name)
    {
        if (!CustomVariables.TryGetValue(name, out CustomVariable variable))
        {
            CustomVariables.Add(name, variable = new());
        }

        return variable;
    }

    public string CustomVariableValue(string name)
    {
        return GetOrAddCustomVariable(name).Value;
    }

    public void SetCustomVariable(string name, string value)
    {
        CustomVariable v = GetOrAddCustomVariable(name);
        v.Value = value;
        if (v.IsPermanent)
        {
            LiveSplitRun.HasChanged = true;
        }
    }

    public bool UsesEmulator
    {
        get => usesEmulator;
        set
        {
            if (usesEmulator != value)
            {
                TriggerPropertyChanged(true);
            }

            usesEmulator = value;
        }
    }

    public bool GameAvailable => false;
    public bool CategoryAvailable => false;

    // speedrun.com data is never available offline; these keep shared code that inspects it compiling.
    public SpeedrunComGame Game => null;
    public SpeedrunComCategory Category => null;
    public SpeedrunComRegion Region => null;

    public RunMetadata(IRun run)
    {
        LiveSplitRun = run;
        VariableValueNames = new Dictionary<string, string>();
        CustomVariables = [];
    }

    public void Refresh() { }

    public RunMetadata Clone(IRun run)
    {
        return new RunMetadata(run)
        {
            runId = runId,
            platformName = platformName,
            regionName = regionName,
            usesEmulator = usesEmulator,
            VariableValueNames = VariableValueNames.ToDictionary(x => x.Key, x => x.Value),
            CustomVariables = CustomVariables.ToDictionary(x => x.Key, x => x.Value.Clone()),
        };
    }

    private void TriggerPropertyChanged(bool clearRunID)
    {
        PropertyChanged?.Invoke(this, new MetadataChangedEventArgs(clearRunID));
    }
}

public sealed class SpeedrunComGame
{
    public IReadOnlyList<SpeedrunComVariable> FullGameVariables { get; } = [];
    public IReadOnlyList<SpeedrunComRegion> Regions { get; } = [];
    public IReadOnlyList<string> Platforms { get; } = [];
}

public sealed class SpeedrunComCategory
{
    public string ID { get; init; }
}

public sealed class SpeedrunComRegion
{
    public string Abbreviation { get; init; }
}

public sealed class SpeedrunComVariable
{
    public string Name { get; init; }
    public string CategoryID { get; init; }
}

/// <summary>
///     A custom variable that has a value and can be marked permanent.
/// </summary>
public sealed class CustomVariable
{
    public string Value { get; set; }

    public bool IsPermanent { get; private set; }

    public CustomVariable()
        : this(null, false) { }

    public CustomVariable(string value, bool isPermanent)
    {
        Value = value;
        IsPermanent = isPermanent;
    }

    public CustomVariable AsPermanent()
    {
        IsPermanent = true;
        return this;
    }

    public CustomVariable Clone()
    {
        return new CustomVariable(Value, IsPermanent);
    }
}
