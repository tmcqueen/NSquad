using GitHub.Copilot.SDK;
using Squad.Sdk.Events;

namespace Squad.Sdk.Client;

/// <summary>
/// Wraps CopilotClient. Single entry point for creating Squad sessions.
/// Use CreateAsync() for normal usage.
/// </summary>
public sealed class SquadClient : IAsyncDisposable
{
    private readonly CopilotClient _inner;
    private readonly EventBus? _eventBus;
    private bool _disposed;

    private SquadClient(CopilotClient inner, EventBus? eventBus)
    {
        _inner = inner;
        _eventBus = eventBus;
    }

    /// <summary>Create and start a SquadClient.</summary>
    public static async Task<SquadClient> CreateAsync(
        SquadClientOptions? options = null,
        EventBus? eventBus = null,
        CancellationToken ct = default)
    {
        var clientOptions = new CopilotClientOptions
        {
            CliPath = options?.CliPath,
            Cwd = options?.WorkingDirectory,
            AutoRestart = options?.AutoRestart ?? true,
            LogLevel = options?.LogLevel ?? "info",
            Environment = options?.Environment,
        };

        var client = new CopilotClient(clientOptions);
        await client.StartAsync(ct);
        return new SquadClient(client, eventBus);
    }

    /// <summary>Create a new Squad session for an agent.</summary>
    public async Task<SquadSession> CreateSessionAsync(
        SquadSessionOptions? options = null,
        CancellationToken ct = default)
    {
        var sessionConfig = new SessionConfig
        {
            OnPermissionRequest = PermissionHandler.ApproveAll,
            Model = options?.Model,
            SessionId = options?.SessionId,
            Streaming = options?.Streaming ?? true,
            AvailableTools = options?.AvailableTools?.Count > 0
                ? [.. options.AvailableTools]
                : null,
            ExcludedTools = options?.ExcludedTools?.Count > 0
                ? [.. options.ExcludedTools]
                : null,
        };

        if (options?.SystemMessageAppend is { Length: > 0 } append)
        {
            sessionConfig.SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = append,
            };
        }

        var inner = await _inner.CreateSessionAsync(sessionConfig, ct);
        return new SquadSession(inner, options?.AgentName, _eventBus);
    }

    /// <summary>Resume an existing session by ID.</summary>
    public async Task<SquadSession> ResumeSessionAsync(
        string sessionId,
        string? agentName = null,
        CancellationToken ct = default)
    {
        var inner = await _inner.ResumeSessionAsync(sessionId, new ResumeSessionConfig
        {
            OnPermissionRequest = PermissionHandler.ApproveAll,
        }, ct);
        return new SquadSession(inner, agentName, _eventBus);
    }

    public async Task<IReadOnlyList<SessionMetadata>> ListSessionsAsync(CancellationToken ct = default)
        => await _inner.ListSessionsAsync(null, ct);

    public async Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
        => await _inner.DeleteSessionAsync(sessionId, ct);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _inner.StopAsync();
    }
}
