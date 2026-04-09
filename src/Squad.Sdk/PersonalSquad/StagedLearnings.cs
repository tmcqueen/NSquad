namespace Squad.Sdk.PersonalSquad;

public sealed record StagedLearning(string Filename, string Filepath, string Content);
public sealed record MergeResult(int Decisions);

public static class StagedLearnings
{
    /// <summary>
    /// Load staged learning files from .squad/extract/*.md
    /// </summary>
    public static IReadOnlyList<StagedLearning> Load(string cwd)
    {
        var extractDir = Path.Combine(cwd, ".squad", "extract");
        if (!Directory.Exists(extractDir)) return Array.Empty<StagedLearning>();

        return Directory.GetFiles(extractDir, "*.md")
            .OrderBy(f => f)
            .Select(f => new StagedLearning(Path.GetFileName(f), f, File.ReadAllText(f)))
            .ToList();
    }

    /// <summary>
    /// Merge learnings into personal squad's decisions.md
    /// </summary>
    public static async Task<MergeResult> MergeAsync(
        IEnumerable<StagedLearning> learnings, string personalSquadDir, CancellationToken ct = default)
    {
        var decisionsPath = Path.Combine(personalSquadDir, "decisions.md");
        var existing = File.Exists(decisionsPath) ? await File.ReadAllTextAsync(decisionsPath, ct) : "# Decisions\n";

        var newEntries = new System.Text.StringBuilder();
        var count = 0;
        foreach (var l in learnings)
        {
            newEntries.AppendLine($"\n---\n");
            newEntries.AppendLine($"*Extracted: {DateTimeOffset.UtcNow:O}*\n");
            newEntries.AppendLine(l.Content);
            count++;
        }

        if (count > 0)
        {
            var updated = existing.TrimEnd() + "\n" + newEntries;
            await File.WriteAllTextAsync(decisionsPath, updated, ct);
        }

        return new MergeResult(count);
    }
}
