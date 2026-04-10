using Orleans;

namespace Squad.Server.Models;

[GenerateSerializer, Alias(nameof(AgentGrainState))]
public class AgentGrainState
{
    [Id(0)] public string CharterPath { get; set; } = "";
    [Id(1)] public string AgentName { get; set; } = "";
    [Id(2)] public List<ChatMessage> MessageHistory { get; set; } = [];
    [Id(3)] public string? SessionId { get; set; }
    [Id(4)] public AgentStatus Status { get; set; } = AgentStatus.Suspended;
}

[GenerateSerializer]
public enum AgentStatus { Suspended, Idle, Processing, Error }
