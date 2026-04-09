namespace Squad.Sdk.CostTracker;

public record CostEntry
{
    public string Agent { get; init; } = string.Empty;
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public decimal EstimatedCost { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string? Model { get; init; }
    public string? SessionId { get; init; }
}

public record AgentCostSummary
{
    public string Agent { get; init; } = string.Empty;
    public int TotalInputTokens { get; init; }
    public int TotalOutputTokens { get; init; }
    public decimal TotalCost { get; init; }
    public int SessionCount { get; init; }
}
