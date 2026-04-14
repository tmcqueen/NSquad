using Microsoft.Extensions.Logging;
using Orleans;
using Squad.Server.Models;
using Squad.Server.Services;

namespace Squad.Server.Grains;

/// <summary>
/// Silent logger and decision merger. Observes all agent output streams passively.
/// </summary>

[GrainType(Constants.Scribe), KeepAlive]
public sealed class ScribeAgentGrain : AgentGrain, IAgentGrain
{
    public ScribeAgentGrain(
        [PersistentState(Constants.Agent, Constants.AgentStateStore)]
        IPersistentState<AgentGrainState> state,
        ISquadClientFactory clientFactory,
        ILogger<ScribeAgentGrain> logger)
        : base(state, clientFactory, logger) { }

    protected override string GetCharterPath() => "templates/scribe/charter.md";
    protected override IReadOnlyList<AgentTool> GetTools() => [];
}
