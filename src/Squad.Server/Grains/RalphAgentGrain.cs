using Microsoft.Extensions.Logging;
using Orleans;
using Squad.Server.Models;
using Squad.Server.Services;

namespace Squad.Server.Grains;

/// <summary>
/// Persistent collaborator. Pre-set charter for code analysis and knowledge recall.
/// Tools list is empty for 0.4.0 — populated in 0.4.1.
/// </summary>
public sealed class RalphAgentGrain : AgentGrain
{
    public RalphAgentGrain(
        [PersistentState("agent", "agentStore")]
        IPersistentState<AgentGrainState> state,
        ISquadClientFactory clientFactory,
        ILogger<RalphAgentGrain> logger)
        : base(state, clientFactory, logger) { }

    protected override string GetCharterPath() => "templates/ralph/charter.md";
    protected override IReadOnlyList<AgentTool> GetTools() => [];
}
