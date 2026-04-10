using Orleans;

namespace Squad.Server.Models;

[GenerateSerializer, Alias(nameof(ChatMessage))]
public sealed record ChatMessage
{
    [Id(0)] public string Role { get; set; } = "";
    [Id(1)] public string Content { get; set; } = "";
    [Id(2)] public DateTime Timestamp { get; set; }
}
