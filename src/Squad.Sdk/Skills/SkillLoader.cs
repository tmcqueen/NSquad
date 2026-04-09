namespace Squad.Sdk.Skills;

/// <summary>Discovers skill definitions from a squad's <c>.squad/skills/</c> directory.</summary>
public static class SkillLoader
{
    /// <summary>
    /// Discover all skills in .squad/skills/ — each sub-directory containing
    /// a SKILL.md file is a skill. Returns skill name and content.
    /// </summary>
    public static async Task<IReadOnlyList<SkillDefinition>> DiscoverAsync(
        string squadDir,
        CancellationToken ct = default)
    {
        var skillsDir = System.IO.Path.Combine(squadDir, "skills");
        if (!Directory.Exists(skillsDir))
            return [];

        var skills = new List<SkillDefinition>();
        foreach (var dir in Directory.GetDirectories(skillsDir))
        {
            var skillMd = System.IO.Path.Combine(dir, "SKILL.md");
            if (!File.Exists(skillMd)) continue;

            var content = await File.ReadAllTextAsync(skillMd, ct);
            skills.Add(new SkillDefinition
            {
                Name = System.IO.Path.GetFileName(dir),
                Content = content,
                Path = skillMd,
            });
        }

        return skills;
    }
}
