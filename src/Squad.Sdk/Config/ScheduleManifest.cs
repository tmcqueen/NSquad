using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

/// <summary>Defines when a schedule fires.</summary>
public sealed record ScheduleTrigger
{
    /// <summary>Trigger type: cron, interval, event, or startup.</summary>
    public string Type { get; init; } = "cron";
    /// <summary>Cron expression (when Type is cron).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cron { get; init; }
    /// <summary>Polling interval in seconds (when Type is interval).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? IntervalSeconds { get; init; }
    /// <summary>GitHub event name (when Type is event).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Event { get; init; }
}

/// <summary>Task to execute when the schedule fires.</summary>
public sealed record ScheduleTask(
    /// <summary>Task type identifier.</summary>
    string Type,
    /// <summary>Reference to the task definition (e.g. agent name or skill path).</summary>
    string Ref);

/// <summary>Retry policy for failed schedule runs.</summary>
public sealed record ScheduleRetry(
    /// <summary>Maximum number of retry attempts.</summary>
    int MaxRetries,
    /// <summary>Seconds to wait between retries.</summary>
    int BackoffSeconds);

/// <summary>A single schedule definition in .squad/schedules.json.</summary>
public sealed record ScheduleEntry
{
    /// <summary>Unique schedule identifier.</summary>
    public string Id { get; init; } = "";
    /// <summary>Human-readable display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Whether this schedule is active.</summary>
    public bool Enabled { get; init; } = true;
    /// <summary>When this schedule fires.</summary>
    public ScheduleTrigger Trigger { get; init; } = new();
    /// <summary>What to execute when triggered.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScheduleTask? Task { get; init; }
    /// <summary>Required capability providers.</summary>
    public List<string> Providers { get; init; } = new();
    /// <summary>Optional retry policy on failure.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScheduleRetry? Retry { get; init; }
}

/// <summary>Collection of schedule definitions loaded from .squad/schedules.json.</summary>
public sealed record ScheduleManifest(
    /// <summary>All defined schedules.</summary>
    List<ScheduleEntry> Schedules)
{
    /// <summary>Create an empty manifest.</summary>
    public ScheduleManifest() : this(new List<ScheduleEntry>()) { }
}

/// <summary>Persisted result of a single schedule execution.</summary>
public sealed record ScheduleRun(
    /// <summary>ISO 8601 timestamp of the last execution.</summary>
    string LastRun,
    /// <summary>Execution status: success, failure, or running.</summary>
    string Status,
    /// <summary>Error message if status is failure, otherwise null.</summary>
    string? Error = null);

/// <summary>Persisted execution state for all schedules.</summary>
public sealed record ScheduleState(
    /// <summary>Maps schedule id to its last run state.</summary>
    Dictionary<string, ScheduleRun> Runs)
{
    /// <summary>Create an empty state.</summary>
    public ScheduleState() : this(new Dictionary<string, ScheduleRun>()) { }
}
