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
    public static IAgentGrain Resolve(IGrainFactory factory, string agentName) =>
        agentName.ToLowerInvariant() switch
        {
            "ralph" => factory.GetGrain<IAgentGrain>(
                agentName, grainClassNamePrefix: nameof(RalphAgentGrain)),
            "scribe" => factory.GetGrain<IAgentGrain>(
                agentName, grainClassNamePrefix: nameof(ScribeAgentGrain)),
            "squadleader" => factory.GetGrain<IAgentGrain>(
                agentName, grainClassNamePrefix: nameof(SquadLeaderAgentGrain)),
            _ => factory.GetGrain<IAgentGrain>(
                agentName, grainClassNamePrefix: nameof(SquadMemberGrain)),
        };
}
