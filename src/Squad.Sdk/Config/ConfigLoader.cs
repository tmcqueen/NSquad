using System.Text.Json;
using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

/// <summary>Loads squad.config.json from disk.</summary>
public static class ConfigLoader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Load squad.config.json from the given directory.
    /// Returns null if no config file exists.
    /// Throws <see cref="ConfigLoadException"/> on invalid JSON.
    /// </summary>
    public static async Task<SquadConfig?> LoadAsync(string directory, CancellationToken ct = default)
    {
        var path = Path.Combine(directory, "squad.config.json");
        if (!File.Exists(path))
            return null;

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, ct);
        }
        catch (IOException ex)
        {
            throw new ConfigLoadException($"Could not read {path}", ex);
        }

        try
        {
            var config = JsonSerializer.Deserialize<SquadConfig>(json, _options);
            return config;
        }
        catch (JsonException ex)
        {
            throw new ConfigLoadException($"Invalid JSON in {path}: {ex.Message}", ex);
        }
    }

    /// <summary>Synchronous version for contexts where async is inconvenient.</summary>
    public static SquadConfig? Load(string directory)
    {
        var path = Path.Combine(directory, "squad.config.json");
        if (!File.Exists(path))
            return null;

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new ConfigLoadException($"Could not read {path}", ex);
        }

        try
        {
            return JsonSerializer.Deserialize<SquadConfig>(json, _options);
        }
        catch (JsonException ex)
        {
            throw new ConfigLoadException($"Invalid JSON in {path}: {ex.Message}", ex);
        }
    }
}

/// <summary>Thrown when squad.config.json cannot be read or parsed.</summary>
public sealed class ConfigLoadException : Exception
{
    /// <summary>Create a <see cref="ConfigLoadException"/> with the given message and optional inner exception.</summary>
    public ConfigLoadException(string message, Exception? inner = null)
        : base(message, inner) { }
}
