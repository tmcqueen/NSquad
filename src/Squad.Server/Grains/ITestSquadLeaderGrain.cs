using Orleans;

namespace Squad.Server.Grains;

/* 
 * <summary>Test-only interface that exposes SquadLeader's tool list for introspection.</summary>
 */
public interface ITestSquadLeaderGrain : IAgentGrain
{
    Task<IReadOnlyList<string>> GetToolNamesAsync();
}
