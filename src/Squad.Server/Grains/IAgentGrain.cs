using Orleans;
using Orleans.Concurrency;
using Squad.Server.Models;

namespace Squad.Server.Grains;

public interface IAgentGrain : IGrainWithStringKey
{
    Task SendAsync(string prompt);
    Task WakeAsync();
    Task SuspendAsync();
    [AlwaysInterleave]
    Task<AgentStatus> GetStatusAsync();

    Task<IReadOnlyList<ChatMessage>> GetHistoryAsync();
}
