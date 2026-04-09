using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

public sealed record ScheduleTrigger
{
    public string Type { get; init; } = "cron"; // "cron" | "interval" | "event" | "startup"
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cron { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? IntervalSeconds { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Event { get; init; }
}

public sealed record ScheduleTask(string Type, string Ref);

public sealed record ScheduleRetry(int MaxRetries, int BackoffSeconds);

public sealed record ScheduleEntry
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public ScheduleTrigger Trigger { get; init; } = new();
    public ScheduleTask Task { get; init; } = new("print", "echo hello");
    public List<string> Providers { get; init; } = new();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScheduleRetry? Retry { get; init; }
}

public sealed record ScheduleManifest(List<ScheduleEntry> Schedules)
{
    public ScheduleManifest() : this(new List<ScheduleEntry>()) { }
}

public sealed record ScheduleRun(string LastRun, string Status, string? Error = null);

public sealed record ScheduleState(Dictionary<string, ScheduleRun> Runs)
{
    public ScheduleState() : this(new Dictionary<string, ScheduleRun>()) { }
}
