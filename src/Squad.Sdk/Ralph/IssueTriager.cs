using System.Text.RegularExpressions;

namespace Squad.Sdk.Ralph;

/// <summary>An active agent entry parsed from the <c>## Members</c> table in team.md.</summary>
public sealed record RosterMember(
    /// <summary>Lowercased agent name.</summary>
    string Name,
    /// <summary>GitHub label used to assign this agent (e.g. <c>squad:alice</c>).</summary>
    string Label,
    /// <summary>Human-readable role from the Members table.</summary>
    string Role);

/// <summary>The outcome of triaging an issue against routing rules and the roster.</summary>
public sealed record TriageResult(
    /// <summary>The roster member assigned to handle the issue.</summary>
    RosterMember Agent,
    /// <summary>Human-readable explanation of why this agent was selected.</summary>
    string Reason);

/// <summary>A regex-based routing rule that maps issue text to an agent name.</summary>
public sealed record IssueRoutingRule(
    /// <summary>Regex pattern tested case-insensitively against the issue title and body.</summary>
    string Pattern,
    /// <summary>Name of the agent to assign when this pattern matches.</summary>
    string AgentName);

/// <summary>Triages GitHub issues to squad agents based on routing rules and roster membership.</summary>
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
            bool matched;
            try
            {
                matched = Regex.IsMatch(text, rule.Pattern,
                    RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);
            }
            catch (ArgumentException) { continue; }

            if (!matched) continue;
            var agent = roster.FirstOrDefault(m =>
                m.Name.Equals(rule.AgentName, StringComparison.OrdinalIgnoreCase));
            if (agent != null)
                return new TriageResult(agent, $"matched pattern: {rule.Pattern}");
        }

        return null;
    }
}
