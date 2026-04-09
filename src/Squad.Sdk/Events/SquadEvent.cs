namespace Squad.Sdk.Events;

/// <summary>Base type for all Squad events.</summary>
public abstract record SquadEvent(
    /// <summary>Session identifier associated with this event, or null for non-session events.</summary>
    string? SessionId,
    /// <summary>Agent name associated with this event, or null if not agent-specific.</summary>
    string? AgentName,
    /// <summary>UTC timestamp when the event was created.</summary>
    DateTimeOffset Timestamp);

// ── Lifecycle Events ────────────────────────────────────────────────

/// <summary>Published when a new session is successfully created.</summary>
public sealed record SessionCreatedEvent(string SessionId, string? AgentName)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

/// <summary>Published when a session transitions to the idle state.</summary>
public sealed record SessionIdleEvent(string SessionId, string? AgentName)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

/// <summary>Published when a session encounters an error.</summary>
public sealed record SessionErrorEvent(string SessionId, string? AgentName, string Message)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

/// <summary>Published when a session is disposed or destroyed.</summary>
public sealed record SessionDestroyedEvent(string SessionId, string? AgentName)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

// ── Streaming Events ────────────────────────────────────────────────

/// <summary>Published for each incremental content chunk during a streaming response.</summary>
public sealed record StreamDeltaEvent(
    /// <summary>Session that produced this delta.</summary>
    string SessionId,
    /// <summary>Agent producing this delta.</summary>
    string? AgentName,
    /// <summary>The incremental text content.</summary>
    string Content,
    /// <summary>Zero-based chunk index within the current response.</summary>
    int Index)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

/// <summary>Published for each incremental reasoning chunk when extended thinking is active.</summary>
public sealed record ReasoningDeltaEvent(
    /// <summary>Session that produced this reasoning delta.</summary>
    string SessionId,
    /// <summary>Agent producing this reasoning delta.</summary>
    string? AgentName,
    /// <summary>The incremental reasoning text.</summary>
    string Content,
    /// <summary>Zero-based chunk index within the current reasoning block.</summary>
    int Index)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

/// <summary>Published after a session turn completes, carrying token usage and cost data.</summary>
public sealed record UsageEvent(
    /// <summary>Session that incurred the usage.</summary>
    string SessionId,
    /// <summary>Agent that incurred the usage.</summary>
    string? AgentName,
    /// <summary>Model identifier used for this turn.</summary>
    string Model,
    /// <summary>Number of input (prompt) tokens consumed.</summary>
    int InputTokens,
    /// <summary>Number of output (completion) tokens generated.</summary>
    int OutputTokens,
    /// <summary>Estimated cost in USD for this turn.</summary>
    decimal EstimatedCost)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

// ── Coordinator Events ──────────────────────────────────────────────

/// <summary>Published by the coordinator each time a message is routed to an agent.</summary>
public sealed record CoordinatorRoutingEvent(
    /// <summary>Session identifier, or null for pre-session routing.</summary>
    string? SessionId,
    /// <summary>Agent selected by the routing decision.</summary>
    string? AgentName,
    /// <summary>Routing strategy applied: "single", "fallback", etc.</summary>
    string Strategy,
    /// <summary>The message text that was routed.</summary>
    string Message)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);
