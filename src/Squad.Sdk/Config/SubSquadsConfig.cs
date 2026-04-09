using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

/// <summary>Definition of a single SubSquad workstream.</summary>
public sealed record SubSquadDefinition
{
    /// <summary>Unique stream name.</summary>
    public string Name { get; init; } = "";
    /// <summary>GitHub label used to filter issues and PRs into this stream.</summary>
    public string LabelFilter { get; init; } = "";
    /// <summary>Workflow override; falls back to <see cref="SubSquadsConfig.DefaultWorkflow"/> if null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Workflow { get; init; }
    /// <summary>Optional human-readable description.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
    /// <summary>File path prefixes that scope this stream's work.</summary>
    public List<string> FolderScope { get; init; } = new();
}

/// <summary>Configuration for all SubSquad workstreams, loaded from .squad/streams.json.</summary>
public sealed record SubSquadsConfig
{
    /// <summary>Defined workstreams.</summary>
    public List<SubSquadDefinition> Workstreams { get; init; } = new();
    /// <summary>Default workflow applied when a stream doesn't specify one.</summary>
    public string DefaultWorkflow { get; init; } = "feature";
}
