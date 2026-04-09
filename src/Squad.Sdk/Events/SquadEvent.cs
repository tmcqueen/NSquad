namespace Squad.Sdk.Events;

/// <summary>Base type for all Squad events.</summary>
public abstract record SquadEvent(
    string? SessionId,
    string? AgentName,
    DateTimeOffset Timestamp);

// ── Lifecycle Events ────────────────────────────────────────────────

public sealed record SessionCreatedEvent(string SessionId, string? AgentName)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

public sealed record SessionIdleEvent(string SessionId, string? AgentName)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

public sealed record SessionErrorEvent(string SessionId, string? AgentName, string Message)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

public sealed record SessionDestroyedEvent(string SessionId, string? AgentName)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

// ── Streaming Events ────────────────────────────────────────────────

public sealed record StreamDeltaEvent(
    string SessionId,
    string? AgentName,
    string Content,
    int Index)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

public sealed record ReasoningDeltaEvent(
    string SessionId,
    string? AgentName,
    string Content,
    int Index)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

public sealed record UsageEvent(
    string SessionId,
    string? AgentName,
    string Model,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCost)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

// ── Coordinator Events ──────────────────────────────────────────────

public sealed record CoordinatorRoutingEvent(
    string? SessionId,
    string? AgentName,
    string Strategy,
    string Message)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);
