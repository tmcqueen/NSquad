using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

public sealed record UpstreamSource
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "local"; // "local" | "git" | "export"
    public string Source { get; init; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ref { get; init; }
    [JsonPropertyName("added_at")]
    public string AddedAt { get; init; } = DateTimeOffset.UtcNow.ToString("O");
    [JsonPropertyName("last_synced")]
    public string? LastSynced { get; init; }
}

public sealed record UpstreamConfig(List<UpstreamSource> Upstreams)
{
    public UpstreamConfig() : this(new List<UpstreamSource>()) { }
}
