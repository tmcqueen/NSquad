using Microsoft.Extensions.Logging;
using Orleans;
using Squad.Server.Models;
using Squad.Server.Services;

namespace Squad.Server.Grains;

/// <summary>
/// Silent logger and decision merger. Observes all agent output streams passively.
/// </summary>
public sealed class ScribeAgentGrain : AgentGrain
{
    public ScribeAgentGrain(
        [PersistentState("agent", "agentStore")]
        IPersistentState<AgentGrainState> state,
        ISquadClientFactory clientFactory,
        ILogger<ScribeAgentGrain> logger)
        : base(state, clientFactory, logger) { }

    protected override string GetCharterPath() => "templates/scribe/charter.md";
    protected override IReadOnlyList<AgentTool> GetTools() => [];
}
