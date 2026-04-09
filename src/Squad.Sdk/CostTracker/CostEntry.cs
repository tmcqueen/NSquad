namespace Squad.Sdk.CostTracker;

/// <summary>A single recorded token-usage and cost entry for one agent session turn.</summary>
public record CostEntry
{
    /// <summary>Name of the agent that incurred the cost.</summary>
    public string Agent { get; init; } = string.Empty;
    /// <summary>Number of input (prompt) tokens consumed.</summary>
    public int InputTokens { get; init; }
    /// <summary>Number of output (completion) tokens generated.</summary>
    public int OutputTokens { get; init; }
    /// <summary>Estimated cost in USD for this entry.</summary>
    public decimal EstimatedCost { get; init; }
    /// <summary>UTC timestamp when the entry was recorded.</summary>
    public DateTimeOffset Timestamp { get; init; }
    /// <summary>Model identifier used for this turn, or null if unknown.</summary>
    public string? Model { get; init; }
    /// <summary>Session identifier, or null if not associated with a session.</summary>
    public string? SessionId { get; init; }
}

/// <summary>Aggregated cost totals for a single agent across all recorded sessions.</summary>
public record AgentCostSummary
{
    /// <summary>Name of the agent.</summary>
    public string Agent { get; init; } = string.Empty;
    /// <summary>Sum of all input tokens across all sessions.</summary>
    public int TotalInputTokens { get; init; }
    /// <summary>Sum of all output tokens across all sessions.</summary>
    public int TotalOutputTokens { get; init; }
    /// <summary>Total estimated cost in USD across all sessions.</summary>
    public decimal TotalCost { get; init; }
    /// <summary>Number of distinct sessions contributing to this summary.</summary>
    public int SessionCount { get; init; }
}
