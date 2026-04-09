using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

/// <summary>A single registered upstream squad source.</summary>
public sealed record UpstreamSource
{
    /// <summary>Local alias for this upstream.</summary>
    public string Name { get; init; } = "";
    /// <summary>Source type: local, git, or export.</summary>
    public string Type { get; init; } = "local";
    /// <summary>Path, URL, or owner/repo identifying the upstream.</summary>
    public string Source { get; init; } = "";
    /// <summary>Git branch or tag (git type only).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ref { get; init; }
    /// <summary>ISO 8601 timestamp when this upstream was registered.</summary>
    [JsonPropertyName("added_at")]
    public string AddedAt { get; init; } = DateTimeOffset.UtcNow.ToString("O");
    /// <summary>ISO 8601 timestamp of the last sync, or null if never synced.</summary>
    [JsonPropertyName("last_synced")]
    public string? LastSynced { get; init; }
}

/// <summary>Registry of all configured upstream squad sources, persisted to .squad/upstream.json.</summary>
public sealed record UpstreamConfig(
    /// <summary>Ordered list of upstream sources.</summary>
    List<UpstreamSource> Upstreams)
{
    /// <summary>Create an empty config.</summary>
    public UpstreamConfig() : this(new List<UpstreamSource>()) { }
}
