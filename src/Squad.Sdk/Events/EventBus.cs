namespace Squad.Sdk.Events;

/// <summary>
/// In-process pub/sub event bus for Squad session lifecycle events.
/// Handlers run in subscription order. Faulting handlers are isolated
/// via try/catch — they cannot disrupt other subscribers.
/// </summary>
public sealed class EventBus : IDisposable
{
    private readonly Lock _lock = new();
    private readonly List<Func<SquadEvent, ValueTask>> _handlers = [];
    private bool _disposed;

    /// <summary>Subscribe to all events. Returns an IDisposable to unsubscribe.</summary>
    public IDisposable Subscribe(Func<SquadEvent, ValueTask> handler)
    {
        lock (_lock) _handlers.Add(handler);
        return new Subscription(() => { lock (_lock) _handlers.Remove(handler); });
    }

    /// <summary>Subscribe with a synchronous handler.</summary>
    public IDisposable Subscribe(Action<SquadEvent> handler)
        => Subscribe(evt => { handler(evt); return ValueTask.CompletedTask; });

    /// <summary>Subscribe to a specific event type only.</summary>
    public IDisposable Subscribe<T>(Action<T> handler) where T : SquadEvent
        => Subscribe(evt => { if (evt is T typed) handler(typed); });

    /// <summary>Publish an event to all current subscribers.</summary>
    public async Task PublishAsync(SquadEvent evt)
    {
        Func<SquadEvent, ValueTask>[] snapshot;
        lock (_lock) snapshot = [.. _handlers];

        foreach (var handler in snapshot)
        {
            try { await handler(evt); }
            catch { /* isolate faulting subscribers */ }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_lock) _handlers.Clear();
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private bool _disposed;
        public void Dispose() { if (!_disposed) { _disposed = true; dispose(); } }
    }
}
