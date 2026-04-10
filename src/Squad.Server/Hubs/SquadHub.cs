using Microsoft.AspNetCore.SignalR;
using Orleans;
using Orleans.Streams;
using Squad.Server.Grains;
using Squad.Server.Models;
using Squad.Server.Services;

namespace Squad.Server.Hubs;

/// <summary>
/// SignalR hub — the only client-facing interface to the Orleans silo.
/// Bridges WebSocket connections to grain method calls and Orleans stream subscriptions.
/// </summary>
public sealed class SquadHub : Hub
{
    private readonly IGrainFactory _grainFactory;
    private readonly IClusterClient _clusterClient;
    private readonly ISquadConfigProvider _configProvider;

    // Per-connection stream subscriptions — cleaned up on disconnect
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<StreamSubscriptionHandle<AgentStreamEvent>>>
        _subscriptions = new();

    public SquadHub(
        IGrainFactory grainFactory,
        IClusterClient clusterClient,
        ISquadConfigProvider configProvider)
    {
        _grainFactory = grainFactory;
        _clusterClient = clusterClient;
        _configProvider = configProvider;
    }

    /// <summary>
    /// Send a message to an agent. Subscribes to the agent's output stream
    /// and forwards deltas/completion/errors to the caller via SignalR callbacks.
    /// </summary>
    public async Task SendMessage(string agentName, string prompt)
    {
        var grain = AgentGrainResolver.Resolve(_grainFactory, agentName);

        var streamProvider = _clusterClient.GetStreamProvider("AgentStreams");
        var streamId = StreamId.Create("AgentOutput", agentName);
        var stream = streamProvider.GetStream<AgentStreamEvent>(streamId);

        var connectionId = Context.ConnectionId;
        var handle = await stream.SubscribeAsync(async (evt, _) =>
        {
            switch (evt.Type)
            {
                case AgentStreamEventType.Delta:
                    await Clients.Caller.SendAsync("OnDelta", agentName, evt.Text);
                    break;
                case AgentStreamEventType.Completed:
                    await Clients.Caller.SendAsync("OnComplete", agentName);
                    break;
                case AgentStreamEventType.StatusChanged:
                    await Clients.All.SendAsync("OnAgentStatusChanged", agentName, evt.Status?.ToString());
                    break;
                case AgentStreamEventType.Error:
                    await Clients.Caller.SendAsync("OnError", agentName, evt.Text);
                    break;
            }
        });

        _subscriptions.AddOrUpdate(
            connectionId,
            _ => [handle],
            (_, list) => { list.Add(handle); return list; });

        await grain.SendAsync(prompt);
    }

    /// <summary>Wake an agent (create its session).</summary>
    public async Task WakeAgent(string agentName)
    {
        var grain = AgentGrainResolver.Resolve(_grainFactory, agentName);
        await grain.WakeAsync();
    }

    /// <summary>Suspend an agent (close session, preserve state).</summary>
    public async Task SuspendAgent(string agentName)
    {
        var grain = AgentGrainResolver.Resolve(_grainFactory, agentName);
        await grain.SuspendAsync();
    }

    /// <summary>Get current status of all configured agents.</summary>
    public async Task<Dictionary<string, string>> GetAgentStatus()
    {
        var names = _configProvider.GetAllAgentNames();
        var statuses = new Dictionary<string, string>();

        foreach (var name in names)
        {
            var grain = AgentGrainResolver.Resolve(_grainFactory, name);
            var status = await grain.GetStatusAsync();
            statuses[name] = status.ToString();
        }

        return statuses;
    }

    /// <summary>Get message history for an agent (catch-up for newly connected clients).</summary>
    public async Task<IReadOnlyList<ChatMessage>> GetHistory(string agentName)
    {
        var grain = AgentGrainResolver.Resolve(_grainFactory, agentName);
        return await grain.GetHistoryAsync();
    }

    /// <summary>Clean up stream subscriptions when a client disconnects.</summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_subscriptions.TryRemove(Context.ConnectionId, out var handles))
        {
            foreach (var handle in handles)
            {
                try { await handle.UnsubscribeAsync(); }
                catch { /* best-effort cleanup */ }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
