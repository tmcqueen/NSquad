using Microsoft.Extensions.Logging;
using Orleans;
using Squad.Server.Models;
using Squad.Server.Services;

namespace Squad.Server.Grains;

/// <summary>
/// Lead/orchestrator grain. Has agent management tools: WakeAgent, SuspendAgent,
/// SendTo, GetAgentStatus. These are used when the LLM invokes them during a conversation.
/// </summary>

[GrainType(Constants.SquadLeader), KeepAlive]
public sealed class SquadLeaderAgentGrain : AgentGrain, IAgentGrain, ITestSquadLeaderGrain
{
    private readonly IGrainFactory _grainFactory;
    private readonly ISquadConfigProvider _configProvider;
    private IReadOnlyList<AgentTool>? _tools;

    public SquadLeaderAgentGrain(
        [PersistentState(Constants.Agent, Constants.AgentStateStore)]
        IPersistentState<AgentGrainState> state,
        ISquadClientFactory clientFactory,
        IGrainFactory grainFactory,
        ISquadConfigProvider configProvider,
        ILogger<SquadLeaderAgentGrain> logger)
        : base(state, clientFactory, logger)
    {
        _grainFactory = grainFactory;
        _configProvider = configProvider;
    }

    protected override string GetCharterPath() => "templates/squadleader/charter.md";

    protected override IReadOnlyList<AgentTool> GetTools()
    {
        _tools ??=
        [
            new AgentTool(Constants.WakeAgent, "Activate an agent by name", WakeAgentAsync),
            new AgentTool(Constants.SuspendAgent, "Suspend an agent by name", SuspendAgentAsync),
            new AgentTool(Constants.SendTo, "Send a message to a specific agent (format: 'agentName|prompt')", SendToAgentAsync),
            new AgentTool(Constants.GetAgentStatus, "List all agents and their current status", GetAllStatusAsync),
        ];
        return _tools;
    }

    // ITestSquadLeaderGrain — test-only introspection
    public Task<IReadOnlyList<string>> GetToolNamesAsync()
        => Task.FromResult<IReadOnlyList<string>>(GetTools().Select(t => t.Name).ToList());

    // --- Tool handlers ---

    private async Task<string> WakeAgentAsync(string agentName)
    {
        var grain = AgentGrainResolver.Resolve(_grainFactory, agentName.Trim());
        await grain.WakeAsync();
        return $"Agent '{agentName.Trim()}' is now awake.";
    }

    private async Task<string> SuspendAgentAsync(string agentName)
    {
        var grain = AgentGrainResolver.Resolve(_grainFactory, agentName.Trim());
        await grain.SuspendAsync();
        return $"Agent '{agentName.Trim()}' suspended.";
    }

    private async Task<string> SendToAgentAsync(string args)
    {
        var parts = args.Split('|', 2);
        if (parts.Length < 2)
            return "Error: SendTo requires format 'agentName|prompt'";

        var grain = AgentGrainResolver.Resolve(_grainFactory, parts[0].Trim());
        await grain.SendAsync(parts[1].Trim());
        return $"Message sent to '{parts[0].Trim()}'.";
    }

    private async Task<string> GetAllStatusAsync(string _)
    {
        var names = _configProvider.GetAllAgentNames();
        var statuses = new Dictionary<string, string>();

        foreach (var name in names)
        {
            var grain = AgentGrainResolver.Resolve(_grainFactory, name);
            var status = await grain.GetStatusAsync();
            statuses[name] = status.ToString();
        }

        return System.Text.Json.JsonSerializer.Serialize(statuses);
    }
}
