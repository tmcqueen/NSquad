using System.Text.Json;
using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

public sealed record AgentExportData(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Charter,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? History);

public sealed record ExportManifest
{
    public string Version { get; init; } = "1.0";
    [JsonPropertyName("exported_at")]
    public string ExportedAt { get; init; } = DateTimeOffset.UtcNow.ToString("O");
    [JsonPropertyName("squad_version")]
    public string SquadVersion { get; init; } = "0.1.0";
    public Dictionary<string, JsonElement> Casting { get; init; } = new();
    public Dictionary<string, AgentExportData> Agents { get; init; } = new();
    public List<string> Skills { get; init; } = new();
}
