using Squad.Sdk.Config;
using Squad.Server.Models;

namespace Squad.Server.Services;

public sealed class SquadConfigProvider : ISquadConfigProvider
{
    private readonly Dictionary<string, SquadMemberConfiguration> _configs;
    private readonly List<string> _names;

    public SquadConfigProvider(SquadConfig config)
    {
        _configs = config.Agents.ToDictionary(
            a => a.Name,
            a => new SquadMemberConfiguration
            {
                CharterPath = a.Charter ?? $"templates/{a.Name}/charter.md",
                Role = a.Role ?? "",
                Description = a.Charter ?? "",
                ToolNames = [.. a.Skills],
            },
            StringComparer.OrdinalIgnoreCase);

        _names = config.Agents.Select(a => a.Name).ToList();
    }

    public SquadMemberConfiguration? GetMemberConfig(string agentName)
        => _configs.GetValueOrDefault(agentName);

    public IReadOnlyList<string> GetAllAgentNames() => _names;
}
