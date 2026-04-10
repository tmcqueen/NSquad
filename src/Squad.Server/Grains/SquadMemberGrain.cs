using Microsoft.Extensions.Logging;
using Orleans;
using Squad.Server.Models;
using Squad.Server.Services;

namespace Squad.Server.Grains;

/// <summary>
/// Configurable generic agent grain. Reads charter path from ISquadConfigProvider
/// at activation time, keyed by grain primary key (agent name).
/// </summary>
public sealed class SquadMemberGrain : AgentGrain
{
    private readonly ISquadConfigProvider _configProvider;
    private SquadMemberConfiguration? _config;

    public SquadMemberGrain(
        [PersistentState("agent", "agentStore")]
        IPersistentState<AgentGrainState> state,
        ISquadClientFactory clientFactory,
        ISquadConfigProvider configProvider,
        ILogger<SquadMemberGrain> logger)
        : base(state, clientFactory, logger)
    {
        _configProvider = configProvider;
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        _config = _configProvider.GetMemberConfig(this.GetPrimaryKeyString());
        await base.OnActivateAsync(ct);
    }

    protected override string GetCharterPath() =>
        _config?.CharterPath ?? $"templates/{this.GetPrimaryKeyString()}/charter.md";

    protected override IReadOnlyList<AgentTool> GetTools() => [];
}
