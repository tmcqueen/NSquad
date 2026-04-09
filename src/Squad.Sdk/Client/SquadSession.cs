using GitHub.Copilot.SDK;
using Squad.Sdk.Events;

namespace Squad.Sdk.Client;

/// <summary>
/// Wraps a CopilotSession, bridging SDK events to the Squad EventBus
/// and providing a typed streaming interface.
/// </summary>
public sealed class SquadSession : IAsyncDisposable
{
    private readonly CopilotSession _inner;
    private readonly EventBus? _eventBus;

    public string SessionId { get; }
    public string? AgentName { get; }

    internal SquadSession(CopilotSession inner, string? agentName, EventBus? eventBus = null)
    {
        _inner = inner;
        _eventBus = eventBus;
        SessionId = inner.SessionId;
        AgentName = agentName;

        WireEvents();
    }

    /// <summary>Send a message and return the full response when the session goes idle.</summary>
    public async Task<string> SendAsync(string prompt, CancellationToken ct = default)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new System.Text.StringBuilder();

        using var sub = _inner.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg:
                    response.Append(msg.Data.Content);
                    break;
                case GitHub.Copilot.SDK.SessionIdleEvent:
                    done.TrySetResult();
                    break;
                case GitHub.Copilot.SDK.SessionErrorEvent err:
                    done.TrySetException(new SquadSessionException(err.Data.Message));
                    break;
            }
        });

        ct.Register(() => done.TrySetCanceled(ct));
        await _inner.SendAsync(new MessageOptions { Prompt = prompt });
        await done.Task;

        return response.ToString();
    }

    /// <summary>
    /// Send a message and stream delta chunks via the provided callback.
    /// Awaits completion (SessionIdle) before returning.
    /// </summary>
    public async Task SendStreamingAsync(
        string prompt,
        Action<string> onDelta,
        CancellationToken ct = default)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = _inner.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    onDelta(delta.Data.DeltaContent);
                    if (_eventBus is not null)
                        _ = _eventBus.PublishAsync(new StreamDeltaEvent(SessionId, AgentName, delta.Data.DeltaContent, 0));
                    break;
                case GitHub.Copilot.SDK.SessionIdleEvent:
                    done.TrySetResult();
                    break;
                case GitHub.Copilot.SDK.SessionErrorEvent err:
                    done.TrySetException(new SquadSessionException(err.Data.Message));
                    break;
            }
        });

        ct.Register(() => done.TrySetCanceled(ct));
        await _inner.SendAsync(new MessageOptions { Prompt = prompt });
        await done.Task;
    }

    /// <summary>Subscribe directly to the underlying CopilotSession events.</summary>
    public IDisposable OnEvent(Action<SessionEvent> handler) => _inner.On(new SessionEventHandler(handler));

    public async Task AbortAsync() => await _inner.AbortAsync();

    public async ValueTask DisposeAsync()
    {
        if (_eventBus is not null)
            await _eventBus.PublishAsync(new SessionDestroyedEvent(SessionId, AgentName));
        await _inner.DisposeAsync();
    }

    private void WireEvents()
    {
        if (_eventBus is null) return;
        _ = _eventBus.PublishAsync(new SessionCreatedEvent(SessionId, AgentName));

        _inner.On(evt =>
        {
            switch (evt)
            {
                case GitHub.Copilot.SDK.SessionIdleEvent:
                    _ = _eventBus.PublishAsync(new Events.SessionIdleEvent(SessionId, AgentName));
                    break;
                case GitHub.Copilot.SDK.SessionErrorEvent err:
                    _ = _eventBus.PublishAsync(new Events.SessionErrorEvent(SessionId, AgentName, err.Data.Message));
                    break;
            }
        });
    }
}

public sealed class SquadSessionException(string message) : Exception(message);
