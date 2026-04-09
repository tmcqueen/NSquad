using Squad.Sdk.Config;
using Squad.Sdk.Events;

namespace Squad.Sdk.Coordinator;

/// <summary>
/// Routes messages to agents based on config-driven routing rules.
/// RouteMessage() selects the agent name — actual session spawning
/// happens in the SquadClient layer.
/// </summary>
public sealed class SquadCoordinator
{
    private readonly SquadConfig _config;
    private readonly EventBus _eventBus;
    private readonly RoutingEngine _router;

    public SquadCoordinator(SquadConfig config, EventBus eventBus)
    {
        _config = config;
        _eventBus = eventBus;
        _router = new RoutingEngine(config.Routing?.Rules ?? []);
    }

    /// <summary>
    /// Determine which agent should handle the given message.
    /// Returns the matched agent, the configured default, or first agent as fallback.
    /// </summary>
    public string RouteMessage(string message)
    {
        var matched = _router.Match(message);
        if (matched is not null)
        {
            _ = _eventBus.PublishAsync(new CoordinatorRoutingEvent(null, matched, "single", message));
            return matched;
        }

        var fallback = _config.Routing?.DefaultAgent
                       ?? _config.Agents.FirstOrDefault()?.Name
                       ?? throw new InvalidOperationException("No agents configured.");

        _ = _eventBus.PublishAsync(new CoordinatorRoutingEvent(null, fallback, "fallback", message));
        return fallback;
    }
}
