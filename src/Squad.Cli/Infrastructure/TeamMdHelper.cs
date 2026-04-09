namespace Squad.Cli.Infrastructure;

/// <summary>
/// Reads and writes the @copilot section in .squad/team.md.
/// </summary>
public static class TeamMdHelper
{
    private const string CopilotRowMarker = "@copilot";
    private const string AutoAssignComment = "<!-- copilot-auto-assign: true -->";

    public static bool HasCopilot(string content) =>
        content.Contains(CopilotRowMarker, StringComparison.OrdinalIgnoreCase) ||
        content.Contains("🤖 Coding Agent", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Insert the Copilot Coding Agent row into the Members table.
    /// If autoAssign is true, also inserts a comment marker.
    /// </summary>
    public static string InsertCopilotSection(string content, bool autoAssign)
    {
        // Add Copilot row before the closing of the Members table
        const string copilotRow = "| @copilot | 🤖 Coding Agent | — | ✅ Active |";
        var lines = content.Split('\n').ToList();

        // Find the last table row in the Members section
        int lastTableRow = -1;
        bool inMembers = false;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("## Members")) { inMembers = true; continue; }
            if (inMembers && lines[i].TrimStart().StartsWith("##")) { inMembers = false; break; }
            if (inMembers && lines[i].TrimStart().StartsWith("|") && !lines[i].Contains("---"))
                lastTableRow = i;
        }

        if (lastTableRow >= 0)
            lines.Insert(lastTableRow + 1, copilotRow);
        else
            lines.Add(copilotRow);

        if (autoAssign)
            lines.Add(AutoAssignComment);

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Remove the Copilot row and auto-assign comment from team.md content.
    /// </summary>
    public static string RemoveCopilotSection(string content)
    {
        var lines = content.Split('\n')
            .Where(l => !l.Contains(CopilotRowMarker, StringComparison.OrdinalIgnoreCase)
                     && !l.Contains("🤖 Coding Agent", StringComparison.OrdinalIgnoreCase)
                     && l.Trim() != AutoAssignComment)
            .ToList();
        return string.Join('\n', lines);
    }

    public static string ReadTeamMd(string squadDir)
    {
        string path = Path.Combine(squadDir, "team.md");
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    public static void WriteTeamMd(string squadDir, string content)
    {
        File.WriteAllText(Path.Combine(squadDir, "team.md"), content);
    }
}
