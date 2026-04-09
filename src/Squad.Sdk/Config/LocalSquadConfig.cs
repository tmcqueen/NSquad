using System.Text.Json;
using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

/// <summary>
/// Schema for .squad/config.json — machine-local settings (never committed).
/// </summary>
public sealed record LocalSquadConfig
{
    public int Version { get; init; } = 1;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TeamRoot { get; init; }
    public bool EconomyMode { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectKey { get; init; }

    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

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

    public void Save(string squadDir)
    {
        Directory.CreateDirectory(squadDir);
        var path = Path.Combine(squadDir, "config.json");
        File.WriteAllText(path, JsonSerializer.Serialize(this, _opts) + "\n");
    }
}
