using System.Text.Json;
using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

/// <summary>
/// Schema for .squad/config.json — machine-local settings (never committed).
/// </summary>
public sealed record LocalSquadConfig
{
    /// <summary>Config schema version.</summary>
    public int Version { get; init; } = 1;
    /// <summary>Path to the shared team root (remote mode only).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TeamRoot { get; init; }
    /// <summary>When true, prefer economy-tier models to reduce cost.</summary>
    public bool EconomyMode { get; init; }
    /// <summary>Optional project identifier for multi-project squads.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectKey { get; init; }

    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Load from .squad/config.json, returning defaults if the file is absent or unreadable.</summary>
    public static LocalSquadConfig Load(string squadDir)
    {
        var path = Path.Combine(squadDir, "config.json");
        if (!File.Exists(path)) return new LocalSquadConfig();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LocalSquadConfig>(json, _opts) ?? new LocalSquadConfig();
        }
        catch (JsonException)
        {
            return new LocalSquadConfig();
        }
        catch (IOException)
        {
            return new LocalSquadConfig();
        }
    }

    /// <summary>Persist this config to .squad/config.json, creating the directory if needed.</summary>
    public void Save(string squadDir)
    {
        Directory.CreateDirectory(squadDir);
        var path = Path.Combine(squadDir, "config.json");
        File.WriteAllText(path, JsonSerializer.Serialize(this, _opts) + "\n");
    }
}
