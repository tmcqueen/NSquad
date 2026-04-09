using Squad.Sdk.Events;
using Shouldly;

namespace Squad.Sdk.Tests.Events;

public class EventBusTests
{
    [Test]
    public async Task Subscribe_receives_published_events()
    {
        var bus = new EventBus();
        var received = new List<SquadEvent>();

        using var _ = bus.Subscribe(evt => received.Add(evt));
        await bus.PublishAsync(new SessionCreatedEvent("s1", "agent1"));

        received.Count.ShouldBe(1);
        received[0].ShouldBeOfType<SessionCreatedEvent>();
    }

    [Test]
    public async Task Unsubscribe_stops_receiving_events()
    {
        var bus = new EventBus();
        var received = new List<SquadEvent>();

        var sub = bus.Subscribe(evt => received.Add(evt));
        await bus.PublishAsync(new SessionCreatedEvent("s1", "agent1"));
        sub.Dispose();
        await bus.PublishAsync(new SessionCreatedEvent("s2", "agent2"));

        received.Count.ShouldBe(1);
    }

    [Test]
    public async Task Multiple_subscribers_all_receive_events()
    {
        var bus = new EventBus();
        var count1 = 0;
        var count2 = 0;

        using var _ = bus.Subscribe(_ => count1++);
        using var __ = bus.Subscribe(_ => count2++);

        await bus.PublishAsync(new SessionIdleEvent("s1", null));

        count1.ShouldBe(1);
        count2.ShouldBe(1);
    }

    [Test]
    public async Task Faulting_subscriber_does_not_affect_others()
    {
        var bus = new EventBus();
        var received = new List<SquadEvent>();

        using var _ = bus.Subscribe(_ => throw new InvalidOperationException("boom"));
        using var __ = bus.Subscribe(evt => received.Add(evt));

        await Should.NotThrowAsync(() => bus.PublishAsync(new SessionIdleEvent("s1", null)));
        received.Count.ShouldBe(1);
    }

    [Test]
    public async Task Typed_subscribe_only_receives_matching_events()
    {
        var bus = new EventBus();
        var idleEvents = new List<SessionIdleEvent>();

        using var _ = bus.Subscribe<SessionIdleEvent>(idleEvents.Add);
        await bus.PublishAsync(new SessionCreatedEvent("s1", "a"));
        await bus.PublishAsync(new SessionIdleEvent("s2", "b"));
        await bus.PublishAsync(new SessionErrorEvent("s3", "c", "oops"));

        idleEvents.Count.ShouldBe(1);
        idleEvents[0].SessionId.ShouldBe("s2");
    }
}
