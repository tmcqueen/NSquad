namespace Squad.Sdk.Config;

/// <summary>Top-level squad configuration loaded from squad.config.json.</summary>
public record SquadConfig
{
    /// <summary>Schema version from squad.config.json.</summary>
    public string? Version { get; init; }
    /// <summary>Team identity configuration.</summary>
    public TeamConfig Team { get; init; } = new();
    /// <summary>Configured agents.</summary>
    public List<AgentConfig> Agents { get; init; } = [];
    /// <summary>Issue routing rules.</summary>
    public RoutingConfig? Routing { get; init; }
    /// <summary>Default model preferences.</summary>
    public ModelConfig? Models { get; init; }
    /// <summary>Cost budget guardrails.</summary>
    public BudgetConfig? Budget { get; init; }
    /// <summary>Agent-to-role casting overrides.</summary>
    public CastingConfig? Casting { get; init; }
    /// <summary>OpenTelemetry export settings.</summary>
    public TelemetryConfig? Telemetry { get; init; }
}

/// <summary>Squad team identity.</summary>
public record TeamConfig
{
    /// <summary>Display name for the squad.</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>Optional one-line description.</summary>
    public string? Description { get; init; }
}

/// <summary>Configuration for a single squad agent.</summary>
public record AgentConfig
{
    /// <summary>Unique agent identifier within the squad.</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>Built-in role id (e.g. backend, lead).</summary>
    public string? Role { get; init; }
    /// <summary>Preferred Claude model override.</summary>
    public string? Model { get; init; }
    /// <summary>Charter content written to .squad/agents/{name}/charter.md.</summary>
    public string? Charter { get; init; }
    /// <summary>Skill names available to this agent.</summary>
    public List<string> Skills { get; init; } = [];
    /// <summary>Arbitrary key-value metadata.</summary>
    public Dictionary<string, object?> Metadata { get; init; } = [];
}

/// <summary>Issue routing configuration.</summary>
public record RoutingConfig
{
    /// <summary>Ordered list of pattern-to-agent routing rules.</summary>
    public List<RoutingRule> Rules { get; init; } = [];
    /// <summary>Fallback agent when no rule matches.</summary>
    public string? DefaultAgent { get; init; }
    /// <summary>Secondary fallback if the default agent is unavailable.</summary>
    public string? FallbackAgent { get; init; }
}

/// <summary>A single routing rule mapping a regex pattern to an agent.</summary>
public record RoutingRule
{
    /// <summary>Regex pattern tested against issue title and body.</summary>
    public string Pattern { get; init; } = string.Empty;
    /// <summary>Agent name to route matching issues to.</summary>
    public string Agent { get; init; } = string.Empty;
    /// <summary>Optional work-type filter.</summary>
    public List<string> WorkTypes { get; init; } = [];
    /// <summary>Higher values are evaluated first.</summary>
    public int Priority { get; init; }
}

/// <summary>Default model selection preferences.</summary>
public record ModelConfig
{
    /// <summary>Model id used when an agent does not specify one.</summary>
    public string DefaultModel { get; init; } = "claude-sonnet-4.5";
    /// <summary>Model tier: standard or economy.</summary>
    public string DefaultTier { get; init; } = "standard";
}

/// <summary>Cost and token guardrails applied per session and per day.</summary>
public record BudgetConfig
{
    /// <summary>Maximum USD cost allowed per session.</summary>
    public decimal? MaxCostPerSession { get; init; }
    /// <summary>Maximum USD cost allowed per calendar day.</summary>
    public decimal? MaxCostPerDay { get; init; }
    /// <summary>Maximum total tokens (input + output) per session.</summary>
    public int? MaxTokensPerSession { get; init; }
}

/// <summary>Explicit agent-to-role casting assignments.</summary>
public record CastingConfig
{
    /// <summary>Maps session key to agent name.</summary>
    public Dictionary<string, string> Assignments { get; init; } = [];
}

/// <summary>OpenTelemetry export settings.</summary>
public record TelemetryConfig
{
    /// <summary>Whether telemetry export is active.</summary>
    public bool Enabled { get; init; }
    /// <summary>OTLP gRPC endpoint URL.</summary>
    public string? OtlpEndpoint { get; init; }
    /// <summary>Service name reported in traces.</summary>
    public string? ServiceName { get; init; }
}
