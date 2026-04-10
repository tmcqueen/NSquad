using Orleans;
using Squad.Server.Models;

namespace Squad.Server.Grains;

public interface IAgentGrain : IGrainWithStringKey
{
    Task SendAsync(string prompt);
    Task WakeAsync();
    Task SuspendAsync();
    Task<AgentStatus> GetStatusAsync();
    Task<IReadOnlyList<ChatMessage>> GetHistoryAsync();
}
