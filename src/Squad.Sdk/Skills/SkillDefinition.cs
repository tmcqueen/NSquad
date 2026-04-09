namespace Squad.Sdk.Skills;

/// <summary>A skill discovered from a <c>.squad/skills/</c> subdirectory.</summary>
public record SkillDefinition
{
    /// <summary>Skill name derived from the directory name.</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>Full markdown content of the SKILL.md file.</summary>
    public string Content { get; init; } = string.Empty;
    /// <summary>Absolute path to the SKILL.md file.</summary>
    public string Path { get; init; } = string.Empty;
}
