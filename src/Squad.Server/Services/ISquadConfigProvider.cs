using Squad.Server.Models;

namespace Squad.Server.Services;
/*
* <summary>
* Provides SquadMemberConfiguration by agent name, read from the loaded squad.config.json.
* Used by SquadMemberGrain at activation time to load its charter and tool list.
* </summary>
*/
public interface ISquadConfigProvider
{
    /// <summary>Returns configuration for the named agent, or null if not found.</summary>
    SquadMemberConfiguration? GetMemberConfig(string agentName);

    /// <summary>Returns all agent names defined in squad.config.json.</summary>
    IReadOnlyList<string> GetAllAgentNames();
}
