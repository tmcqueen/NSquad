namespace Squad.Sdk.Events;

/// <summary>Base type for all Squad events.</summary>
/// <param name="SessionId">Session identifier associated with this event, or null for non-session events.</param>
/// <param name="AgentName">Agent name associated with this event, or null if not agent-specific.</param>
/// <param name="Timestamp">UTC timestamp when the event was created.</param>
public abstract record SquadEvent(string? SessionId, string? AgentName, DateTimeOffset Timestamp);

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
/// <param name="SessionId">Session that produced this delta.</param>
/// <param name="AgentName">Agent producing this delta.</param>
/// <param name="Content">The incremental text content.</param>
/// <param name="Index">Zero-based chunk index within the current response.</param>
public sealed record StreamDeltaEvent(string SessionId, string? AgentName, string Content, int Index)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

/// <summary>Published for each incremental reasoning chunk when extended thinking is active.</summary>
/// <param name="SessionId">Session that produced this reasoning delta.</param>
/// <param name="AgentName">Agent producing this reasoning delta.</param>
/// <param name="Content">The incremental reasoning text.</param>
/// <param name="Index">Zero-based chunk index within the current reasoning block.</param>
public sealed record ReasoningDeltaEvent(string SessionId, string? AgentName, string Content, int Index)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

/// <summary>Published after a session turn completes, carrying token usage and cost data.</summary>
/// <param name="SessionId">Session that incurred the usage.</param>
/// <param name="AgentName">Agent that incurred the usage.</param>
/// <param name="Model">Model identifier used for this turn.</param>
/// <param name="InputTokens">Number of input (prompt) tokens consumed.</param>
/// <param name="OutputTokens">Number of output (completion) tokens generated.</param>
/// <param name="EstimatedCost">Estimated cost in USD for this turn.</param>
public sealed record UsageEvent(
    string SessionId,
    string? AgentName,
    string Model,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCost)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

// ── Coordinator Events ──────────────────────────────────────────────

/// <summary>Published by the coordinator each time a message is routed to an agent.</summary>
/// <param name="SessionId">Session identifier, or null for pre-session routing.</param>
/// <param name="AgentName">Agent selected by the routing decision.</param>
/// <param name="Strategy">Routing strategy applied: "single", "fallback", etc.</param>
/// <param name="Message">The message text that was routed.</param>
public sealed record CoordinatorRoutingEvent(
    string? SessionId,
    string? AgentName,
    string Strategy,
    string Message)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);
