using Orleans;

namespace Squad.Server.Models;

[GenerateSerializer, Alias(nameof(SquadMemberConfiguration))]
public sealed class SquadMemberConfiguration
{
    [Id(0)] public string CharterPath { get; set; } = "";
    [Id(1)] public string Role { get; set; } = "";
    [Id(2)] public string Description { get; set; } = "";
    [Id(3)] public List<string> ToolNames { get; set; } = [];
    [Id(4)] public Dictionary<string, string> Parameters { get; set; } = [];
}
