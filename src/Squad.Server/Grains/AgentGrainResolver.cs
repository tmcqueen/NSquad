using Orleans;

namespace Squad.Server.Grains;

/* 
 * <summary>
 * Resolves the correct IAgentGrain reference for a given agent name.
 * Core agents (ralph, scribe, squadleader) use dedicated grain types.
 * All others use SquadMemberGrain.
 * </summary>
*/
public static class AgentGrainResolver
{
    public static IAgentGrain Resolve(IGrainFactory factory, string agentName)
    {
        string prefix = string.Equals(agentName, Constants.Ralph, StringComparison.OrdinalIgnoreCase) ? nameof(RalphAgentGrain) :
                        string.Equals(agentName, Constants.Scribe, StringComparison.OrdinalIgnoreCase) ? nameof(ScribeAgentGrain) :
                        string.Equals(agentName, Constants.SquadLeader, StringComparison.OrdinalIgnoreCase) ? nameof(SquadLeaderAgentGrain) :
                        nameof(SquadMemberGrain);
        return factory.GetGrain<IAgentGrain>(agentName, grainClassNamePrefix: prefix);
    }
}
