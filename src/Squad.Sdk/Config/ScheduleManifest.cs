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
/// <param name="Type">Task type identifier.</param>
/// <param name="Ref">Reference to the task definition (e.g. agent name or skill path).</param>
public sealed record ScheduleTask(string Type, string Ref);

/// <summary>Retry policy for failed schedule runs.</summary>
/// <param name="MaxRetries">Maximum number of retry attempts.</param>
/// <param name="BackoffSeconds">Seconds to wait between retries.</param>
public sealed record ScheduleRetry(int MaxRetries, int BackoffSeconds);

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
/// <param name="Schedules">All defined schedules.</param>
public sealed record ScheduleManifest(List<ScheduleEntry> Schedules)
{
    /// <summary>Create an empty manifest.</summary>
    public ScheduleManifest() : this(new List<ScheduleEntry>()) { }
}

/// <summary>Persisted result of a single schedule execution.</summary>
/// <param name="LastRun">ISO 8601 timestamp of the last execution.</param>
/// <param name="Status">Execution status: success, failure, or running.</param>
/// <param name="Error">Error message if status is failure, otherwise null.</param>
public sealed record ScheduleRun(string LastRun, string Status, string? Error = null);

/// <summary>Persisted execution state for all schedules.</summary>
/// <param name="Runs">Maps schedule id to its last run state.</param>
public sealed record ScheduleState(Dictionary<string, ScheduleRun> Runs)
{
    /// <summary>Create an empty state.</summary>
    public ScheduleState() : this(new Dictionary<string, ScheduleRun>()) { }
}
