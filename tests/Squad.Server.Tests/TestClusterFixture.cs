using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.TestingHost;
using Squad.Sdk.Client;
using Squad.Sdk.Config;
using Squad.Server.Services;

namespace Squad.Server.Tests;

public class TestSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("agentStore");
        siloBuilder.AddMemoryGrainStorage("PubSubStore");
        siloBuilder.AddMemoryStreams("AgentStreams");
        siloBuilder.ConfigureServices(services =>
        {
            services.AddSingleton<ISquadClientFactory, StubSquadClientFactory>();
            services.AddSingleton(new SquadConfig());
            services.AddSingleton<ISquadConfigProvider, StubSquadConfigProvider>();
        });
    }
}

public sealed class StubSquadClientFactory : ISquadClientFactory
{
    public Task<SquadClient> CreateAsync(CancellationToken ct = default)
        => throw new InvalidOperationException("No real copilot in unit tests");
}

// Minimal stub for ISquadConfigProvider (Task 4 provides the real one)
public sealed class StubSquadConfigProvider : ISquadConfigProvider
{
    public Squad.Server.Models.SquadMemberConfiguration? GetMemberConfig(string agentName) => null;
    public IReadOnlyList<string> GetAllAgentNames() => [];
}
