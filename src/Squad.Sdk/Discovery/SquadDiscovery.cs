using System.Text.Json;
using Squad.Sdk.Config;

namespace Squad.Sdk.Discovery;

public sealed record SquadContact(string Repo, List<string> Labels);
public sealed record SquadManifest(string Name, List<string> Accepts, SquadContact Contact);
public sealed record DiscoveredSquad(string SquadDir, SquadManifest Manifest);

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

        var discovered = new List<DiscoveredSquad>();
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

    public static string FormatTable(IReadOnlyList<DiscoveredSquad> squads)
    {
        if (squads.Count == 0) return "No squads discovered.";
        var sb = new System.Text.StringBuilder("\nDiscovered Squads:\n\n");
        foreach (var s in squads)
        {
            sb.AppendLine($"  {s.Manifest.Name}");
            sb.AppendLine($"    Accepts: {string.Join(", ", s.Manifest.Accepts)}");
            sb.AppendLine($"    Repo: {s.Manifest.Contact.Repo}");
        }
        return sb.ToString();
    }
}
