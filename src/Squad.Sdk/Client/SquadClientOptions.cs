namespace Squad.Sdk.Client;

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

public sealed class SquadSessionOptions
{
    public string? AgentName { get; init; }
    public string? Model { get; init; }
    public string? SessionId { get; init; }
    public bool Streaming { get; init; } = true;
    public string? SystemMessageAppend { get; init; }
    public List<string> AvailableTools { get; init; } = [];
    public List<string> ExcludedTools { get; init; } = [];
}
