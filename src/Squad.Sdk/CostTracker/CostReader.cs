using System.Text.Json;

namespace Squad.Sdk.CostTracker;

/// <summary>Reads and aggregates cost log entries from the .squad/costs/ directory.</summary>
public static class CostReader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Read all cost log entries from .squad/costs/*.json</summary>
    public static async Task<IReadOnlyList<CostEntry>> LoadEntriesAsync(
        string squadDir,
        CancellationToken ct = default)
    {
        var costsDir = Path.Combine(squadDir, "costs");
        if (!Directory.Exists(costsDir))
            return [];

        List<CostEntry> entries = new List<CostEntry>();
        foreach (var file in Directory.GetFiles(costsDir, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var entry = JsonSerializer.Deserialize<CostEntry>(json, _options);
                if (entry is not null) entries.Add(entry);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { /* skip malformed/unreadable files */ }
        }

        return entries.OrderBy(e => e.Timestamp).ToList();
    }

    /// <summary>Aggregate cost entries by agent name.</summary>
    public static IReadOnlyDictionary<string, AgentCostSummary> Summarize(
        IEnumerable<CostEntry> entries)
    {
        return entries
            .GroupBy(e => e.Agent)
            .ToDictionary(
                g => g.Key,
                g => new AgentCostSummary
                {
                    Agent = g.Key,
                    TotalInputTokens = g.Sum(e => e.InputTokens),
                    TotalOutputTokens = g.Sum(e => e.OutputTokens),
                    TotalCost = g.Sum(e => e.EstimatedCost),
                    SessionCount = g.Select(e => e.SessionId).Distinct().Count(),
                });
    }
}
