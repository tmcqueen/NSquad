using System.Text.RegularExpressions;

namespace Squad.Sdk.PersonalSquad;

/// <summary>
/// Separates portable knowledge from project-specific sections in history.md content.
/// </summary>
public static class HistorySplitter
{
    private static readonly Regex[] ProjectPatterns =
    {
        new(@"^#{1,3}\s*key file paths", RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^#{1,3}\s*sprint",         RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^#{1,3}\s*pr\s*#",         RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^#{1,3}\s*file system",    RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^#{1,3}\s*session",        RegexOptions.IgnoreCase | RegexOptions.Multiline),
    };

    private static readonly Regex[] PortablePatterns =
    {
        new(@"^#{1,3}\s*learnings",           RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^#{1,3}\s*portable knowledge",  RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^#{1,3}\s*team updates",        RegexOptions.IgnoreCase | RegexOptions.Multiline),
    };

    public static string Split(string history, string sourceProject)
    {
        if (string.IsNullOrEmpty(history)) return history;

        var lines = history.Split('\n');
        var portable = new List<string>();
        var projectLearnings = new List<string>();
        var inProjectSection = false;

        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"^#{1,3}\s"))
            {
                if (ProjectPatterns.Any(p => p.IsMatch(line)))
                    inProjectSection = true;
                else if (PortablePatterns.Any(p => p.IsMatch(line)))
                    inProjectSection = false;
            }

            if (inProjectSection)
                projectLearnings.Add(line);
            else
                portable.Add(line);
        }

        var result = string.Join('\n', portable);
        if (projectLearnings.Count > 0)
            result += $"\n\n## Project Learnings (from import — {sourceProject})\n\n"
                    + string.Join('\n', projectLearnings);

        return result;
    }
}
