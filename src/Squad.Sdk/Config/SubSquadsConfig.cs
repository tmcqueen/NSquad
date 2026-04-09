using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

public sealed record SubSquadDefinition
{
    public string Name { get; init; } = "";
    public string LabelFilter { get; init; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Workflow { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
    public List<string> FolderScope { get; init; } = new();
}

public sealed record SubSquadsConfig
{
    public List<SubSquadDefinition> Workstreams { get; init; } = new();
    public string DefaultWorkflow { get; init; } = "feature";
}
