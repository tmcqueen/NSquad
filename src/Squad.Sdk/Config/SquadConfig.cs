namespace Squad.Sdk.Config;

/// <summary>Top-level squad configuration loaded from squad.config.json.</summary>
public record SquadConfig
{
    public string? Version { get; init; }
    public TeamConfig Team { get; init; } = new();
    public List<AgentConfig> Agents { get; init; } = [];
    public RoutingConfig? Routing { get; init; }
    public ModelConfig? Models { get; init; }
    public BudgetConfig? Budget { get; init; }
    public CastingConfig? Casting { get; init; }
    public TelemetryConfig? Telemetry { get; init; }
}

public record TeamConfig
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public record AgentConfig
{
    public string Name { get; init; } = string.Empty;
    public string? Role { get; init; }
    public string? Model { get; init; }
    public string? Charter { get; init; }
    public List<string> Skills { get; init; } = [];
    public Dictionary<string, object?> Metadata { get; init; } = [];
}

public record RoutingConfig
{
    public List<RoutingRule> Rules { get; init; } = [];
    public string? DefaultAgent { get; init; }
    public string? FallbackAgent { get; init; }
}

public record RoutingRule
{
    public string Pattern { get; init; } = string.Empty;
    public string Agent { get; init; } = string.Empty;
    public List<string> WorkTypes { get; init; } = [];
    public int Priority { get; init; }
}

public record ModelConfig
{
    public string DefaultModel { get; init; } = "claude-sonnet-4.5";
    public string DefaultTier { get; init; } = "standard";
}

public record BudgetConfig
{
    public decimal? MaxCostPerSession { get; init; }
    public decimal? MaxCostPerDay { get; init; }
    public int? MaxTokensPerSession { get; init; }
}

public record CastingConfig
{
    public Dictionary<string, string> Assignments { get; init; } = [];
}

public record TelemetryConfig
{
    public bool Enabled { get; init; }
    public string? OtlpEndpoint { get; init; }
    public string? ServiceName { get; init; }
}
