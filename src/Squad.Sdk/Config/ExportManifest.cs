using System.Text.Json;
using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

/// <summary>Serialized agent data included in a squad export.</summary>
public sealed record AgentExportData(
    /// <summary>Charter markdown content, or null if absent.</summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Charter,
    /// <summary>History markdown content, or null if absent.</summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? History);

/// <summary>Manifest written to squad-export.json by the export command.</summary>
public sealed record ExportManifest
{
    /// <summary>Export format version.</summary>
    public string Version { get; init; } = "1.0";
    /// <summary>ISO 8601 timestamp when the export was created.</summary>
    [JsonPropertyName("exported_at")]
    public string ExportedAt { get; init; } = DateTimeOffset.UtcNow.ToString("O");
    /// <summary>Squad CLI version that produced this export.</summary>
    [JsonPropertyName("squad_version")]
    public string SquadVersion { get; init; } = "0.1.0";
    /// <summary>Casting configuration snapshot.</summary>
    public Dictionary<string, JsonElement> Casting { get; init; } = new();
    /// <summary>Agent data keyed by agent name.</summary>
    public Dictionary<string, AgentExportData> Agents { get; init; } = new();
    /// <summary>SKILL.md content for each exported skill.</summary>
    public List<string> Skills { get; init; } = new();
}
