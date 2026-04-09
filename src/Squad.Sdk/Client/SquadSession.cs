using System.Text;
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

    /// <summary>Unique identifier for this session.</summary>
    public string SessionId { get; }
    /// <summary>Agent name associated with this session, or null if unspecified.</summary>
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
        TaskCompletionSource done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        StringBuilder response = new System.Text.StringBuilder();

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

        using var reg = ct.Register(() => done.TrySetCanceled(ct));
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
        TaskCompletionSource done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = _inner.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    onDelta(delta.Data.DeltaContent);
                    if (_eventBus is not null)
                        _ = _eventBus.PublishAsync(new StreamDeltaEvent(SessionId, AgentName, delta.Data.DeltaContent, 0))
                            .ContinueWith(t => { }, TaskContinuationOptions.OnlyOnFaulted);
                    break;
                case GitHub.Copilot.SDK.SessionIdleEvent:
                    done.TrySetResult();
                    break;
                case GitHub.Copilot.SDK.SessionErrorEvent err:
                    done.TrySetException(new SquadSessionException(err.Data.Message));
                    break;
            }
        });

        using var reg = ct.Register(() => done.TrySetCanceled(ct));
        await _inner.SendAsync(new MessageOptions { Prompt = prompt });
        await done.Task;
    }

    /// <summary>Subscribe directly to the underlying CopilotSession events.</summary>
    public IDisposable OnEvent(Action<SessionEvent> handler) => _inner.On(new SessionEventHandler(handler));

    /// <summary>Abort the current in-progress request for this session.</summary>
    public async Task AbortAsync() => await _inner.AbortAsync();

    /// <summary>Publish a <see cref="SessionDestroyedEvent"/> and dispose the underlying session.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_eventBus is not null)
            await _eventBus.PublishAsync(new SessionDestroyedEvent(SessionId, AgentName));
        await _inner.DisposeAsync();
    }

    private void WireEvents()
    {
        if (_eventBus is null) return;
        _ = _eventBus.PublishAsync(new SessionCreatedEvent(SessionId, AgentName))
            .ContinueWith(t => { }, TaskContinuationOptions.OnlyOnFaulted);

        _inner.On(evt =>
        {
            switch (evt)
            {
                case GitHub.Copilot.SDK.SessionIdleEvent:
                    _ = _eventBus.PublishAsync(new Events.SessionIdleEvent(SessionId, AgentName))
                        .ContinueWith(t => { }, TaskContinuationOptions.OnlyOnFaulted);
                    break;
                case GitHub.Copilot.SDK.SessionErrorEvent err:
                    _ = _eventBus.PublishAsync(new Events.SessionErrorEvent(SessionId, AgentName, err.Data.Message))
                        .ContinueWith(t => { }, TaskContinuationOptions.OnlyOnFaulted);
                    break;
            }
        });
    }
}

/// <summary>Thrown when a <see cref="SquadSession"/> encounters a session error event from the server.</summary>
public sealed class SquadSessionException(string message) : Exception(message);
