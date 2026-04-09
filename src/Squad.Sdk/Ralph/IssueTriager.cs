using System.Text.RegularExpressions;

namespace Squad.Sdk.Ralph;

public sealed record RosterMember(string Name, string Label, string Role);
public sealed record TriageResult(RosterMember Agent, string Reason);
public sealed record IssueRoutingRule(string Pattern, string AgentName);

public static class IssueTriager
{
    /// <summary>
    /// Parse the ## Members table from team.md content.
    /// Returns one RosterMember per active row.
    /// </summary>
    public static IReadOnlyList<RosterMember> ParseRoster(string teamMdContent)
    {
        var members = new List<RosterMember>();
        var inMembers = false;

        foreach (var line in teamMdContent.Split('\n'))
        {
            if (line.TrimStart().StartsWith("## Members")) { inMembers = true; continue; }
            if (inMembers && line.TrimStart().StartsWith("##")) break;
            if (!inMembers || !line.StartsWith('|') || line.Contains("---") || line.Contains("Name")) continue;

            var cells = line.Split('|').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            if (cells.Length < 2) continue;
            var status = cells.Length >= 4 ? cells[3] : "Active";
            if (!status.Contains("Active")) continue;

            var name = cells[0].ToLowerInvariant();
            var role = cells[1];
            members.Add(new RosterMember(name, $"squad:{name}", role));
        }

        return members;
    }

    /// <summary>
    /// Triage an issue against routing rules and roster.
    /// Returns the first matching assignment, or null if no match.
    /// </summary>
    public static TriageResult? Triage(
        string title,
        string? body,
        IEnumerable<string> labels,
        IReadOnlyList<IssueRoutingRule> rules,
        IReadOnlyList<RosterMember> roster)
    {
        if (roster.Count == 0) return null;

        var text = (title + " " + (body ?? "")).ToLowerInvariant();

        foreach (var rule in rules)
        {
            if (!Regex.IsMatch(text, rule.Pattern, RegexOptions.IgnoreCase)) continue;
            var agent = roster.FirstOrDefault(m =>
                m.Name.Equals(rule.AgentName, StringComparison.OrdinalIgnoreCase));
            if (agent != null)
                return new TriageResult(agent, $"matched pattern: {rule.Pattern}");
        }

        return null;
    }
}
