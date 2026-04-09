namespace Squad.Sdk.Skills;

public record SkillDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
}
