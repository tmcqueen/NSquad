namespace Squad.Sdk.Coordinator;

public enum SpawnStrategy { Direct, Single, Multi, Fallback }

public sealed record CoordinatorResult
{
    public required SpawnStrategy Strategy { get; init; }
    public string? MatchedAgent { get; init; }
    public IReadOnlyList<string> SpawnedAgents { get; init; } = [];
    public IReadOnlyList<string> Responses { get; init; } = [];
    public long DurationMs { get; init; }
    public string? Error { get; init; }
}
