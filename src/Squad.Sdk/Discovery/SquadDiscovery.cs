using System.Text;
using System.Text.Json;
using Squad.Sdk.Config;

namespace Squad.Sdk.Discovery;

/// <summary>GitHub contact info for a discovered squad.</summary>
/// <param name="Repo">GitHub owner/repo slug for the squad's repository.</param>
/// <param name="Labels">Labels used to route work to this squad.</param>
public sealed record SquadContact(string Repo, List<string> Labels);

/// <summary>Manifest describing what a squad accepts and how to contact it.</summary>
/// <param name="Name">Human-readable squad name.</param>
/// <param name="Accepts">Work types or domains this squad accepts.</param>
/// <param name="Contact">GitHub contact information for this squad.</param>
public sealed record SquadManifest(string Name, List<string> Accepts, SquadContact Contact);

/// <summary>A squad discovered from a local upstream source.</summary>
/// <param name="SquadDir">Absolute path to the discovered squad's .squad directory.</param>
/// <param name="Manifest">Parsed manifest from that squad's squad.manifest.json.</param>
public sealed record DiscoveredSquad(string SquadDir, SquadManifest Manifest);

/// <summary>Discovers squads registered as local upstreams.</summary>
public static class SquadDiscovery
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Discover squads registered as local upstreams that have a squad.manifest.json.
    /// </summary>
    public static async Task<IReadOnlyList<DiscoveredSquad>> DiscoverAsync(
        string cwd, CancellationToken ct = default)
    {
        var upstreamPath = Path.Combine(cwd, ".squad", "upstream.json");
        if (!File.Exists(upstreamPath)) return Array.Empty<DiscoveredSquad>();

        UpstreamConfig config;
        try
        {
            var json = await File.ReadAllTextAsync(upstreamPath, ct);
            config = JsonSerializer.Deserialize<UpstreamConfig>(json, _opts) ?? new UpstreamConfig();
        }
        catch { return Array.Empty<DiscoveredSquad>(); }

        List<DiscoveredSquad> discovered = new List<DiscoveredSquad>();
        foreach (var upstream in config.Upstreams.Where(u => u.Type == "local"))
        {
            var manifestPath = Path.Combine(upstream.Source, ".squad", "squad.manifest.json");
            if (!File.Exists(manifestPath)) continue;
            try
            {
                var json = await File.ReadAllTextAsync(manifestPath, ct);
                var manifest = JsonSerializer.Deserialize<SquadManifest>(json, _opts);
                if (manifest != null)
                    discovered.Add(new DiscoveredSquad(Path.Combine(upstream.Source, ".squad"), manifest));
            }
            catch { /* skip unreadable */ }
        }

        return discovered;
    }

    /// <summary>Format a list of discovered squads as a human-readable text table.</summary>
    public static string FormatTable(IReadOnlyList<DiscoveredSquad> squads)
    {
        if (squads.Count == 0) return "No squads discovered.";
        StringBuilder sb = new System.Text.StringBuilder("\nDiscovered Squads:\n\n");
        foreach (var s in squads)
        {
            sb.AppendLine($"  {s.Manifest.Name}");
            sb.AppendLine($"    Accepts: {string.Join(", ", s.Manifest.Accepts)}");
            sb.AppendLine($"    Repo: {s.Manifest.Contact.Repo}");
        }
        return sb.ToString();
    }
}
