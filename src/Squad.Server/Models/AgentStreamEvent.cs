using Orleans;

namespace Squad.Server.Models;

[GenerateSerializer, Alias(nameof(AgentStreamEvent))]
public sealed record AgentStreamEvent
{
    public AgentStreamEvent(
        AgentStreamEventType Type,
        string? Text = null,
        AgentStatus? Status = null)
    {
        this.Type = Type;
        this.Text = Text;
        this.Status = Status;
    }

    [Id(0)] public AgentStreamEventType Type { get; init; }
    [Id(1)] public string? Text { get; init; }
    [Id(2)] public AgentStatus? Status { get; init; }
}

[GenerateSerializer]
public enum AgentStreamEventType { Delta, Completed, StatusChanged, Error }
