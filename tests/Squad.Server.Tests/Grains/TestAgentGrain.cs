using Orleans;
using Squad.Server.Grains;
using Squad.Server.Models;

namespace Squad.Server.Tests.Grains;

/// <summary>
/// Minimal Orleans grain implementing ITestAgentGrain directly (no AgentGrain base)
/// so lifecycle tests can run without a real copilot process.
/// </summary>
public sealed class TestAgentGrain : Grain, ITestAgentGrain
{
    private AgentStatus _status = AgentStatus.Suspended;
    private readonly List<ChatMessage> _history = [];

    public Task<AgentStatus> GetStatusAsync() => Task.FromResult(_status);
    public Task<IReadOnlyList<ChatMessage>> GetHistoryAsync()
        => Task.FromResult<IReadOnlyList<ChatMessage>>(_history);

    public Task WakeAsync() { _status = AgentStatus.Idle; return Task.CompletedTask; }
    public Task SuspendAsync() { _status = AgentStatus.Suspended; return Task.CompletedTask; }
    public Task SendAsync(string prompt) => throw new NotImplementedException("not tested here");
}
