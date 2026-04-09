namespace Squad.Sdk.Client;

/// <summary>Configuration options for <see cref="SquadClient"/>.</summary>
public sealed class SquadClientOptions
{
    /// <summary>Path to the copilot CLI executable. Defaults to "copilot" on PATH.</summary>
    public string? CliPath { get; init; }
    /// <summary>Working directory for the CLI process.</summary>
    public string? WorkingDirectory { get; init; }
    /// <summary>Auto-restart CLI process on crash.</summary>
    public bool AutoRestart { get; init; } = true;
    /// <summary>Log level passed to the CLI.</summary>
    public string LogLevel { get; init; } = "info";
    /// <summary>Extra environment variables for the CLI process.</summary>
    public Dictionary<string, string> Environment { get; init; } = [];
}

/// <summary>Options for a single Squad session created by <see cref="SquadClient.CreateSessionAsync"/>.</summary>
public sealed class SquadSessionOptions
{
    /// <summary>Agent name to associate with this session for routing and event metadata.</summary>
    public string? AgentName { get; init; }
    /// <summary>Model identifier override for this session.</summary>
    public string? Model { get; init; }
    /// <summary>Explicit session ID to use, or null to let the server assign one.</summary>
    public string? SessionId { get; init; }
    /// <summary>Whether to enable streaming for this session. Defaults to <c>true</c>.</summary>
    public bool Streaming { get; init; } = true;
    /// <summary>Additional content appended to the system message for this session.</summary>
    public string? SystemMessageAppend { get; init; }
    /// <summary>Explicit allow-list of tool names available to this session.</summary>
    public List<string> AvailableTools { get; init; } = [];
    /// <summary>Tool names excluded from this session even if normally available.</summary>
    public List<string> ExcludedTools { get; init; } = [];
}
