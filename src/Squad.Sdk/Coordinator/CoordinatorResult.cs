namespace Squad.Sdk.Coordinator;

/// <summary>How the coordinator dispatched the message.</summary>
public enum SpawnStrategy
{
    /// <summary>Routed directly to a single matched agent.</summary>
    Direct,
    /// <summary>Sent to a single fallback agent.</summary>
    Single,
    /// <summary>Broadcast to multiple agents.</summary>
    Multi,
    /// <summary>Sent to the configured default agent when no rule matched.</summary>
    Fallback,
}

/// <summary>Result returned by a coordinator routing decision.</summary>
public sealed record CoordinatorResult
{
    /// <summary>The dispatch strategy that was applied.</summary>
    public required SpawnStrategy Strategy { get; init; }
    /// <summary>The agent name that was matched, or null if not applicable.</summary>
    public string? MatchedAgent { get; init; }
    /// <summary>Names of agents that were spawned as part of this result.</summary>
    public IReadOnlyList<string> SpawnedAgents { get; init; } = [];
    /// <summary>Collected responses from spawned agents.</summary>
    public IReadOnlyList<string> Responses { get; init; } = [];
    /// <summary>Total wall-clock duration of the routing operation in milliseconds.</summary>
    public long DurationMs { get; init; }
    /// <summary>Error message if routing failed, otherwise null.</summary>
    public string? Error { get; init; }
}
