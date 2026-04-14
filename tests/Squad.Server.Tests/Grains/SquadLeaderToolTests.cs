using Orleans.TestingHost;
using Shouldly;
using Squad.Server.Grains;
using Squad.Server.Tests;

namespace Squad.Server.Tests.Grains;

public class SquadLeaderToolTests
{
    private static TestCluster _cluster = null!;

    [Before(Class)]
    public static async Task SetUp()
    {
        _cluster = new TestClusterBuilder()
            .AddSiloBuilderConfigurator<TestSiloConfigurator>()
            .Build();
        await _cluster.DeployAsync();
    }

    [After(Class)]
    public static async Task TearDown()
    {
        await _cluster.StopAllSilosAsync();
        _cluster.Dispose();
    }

    [Test]
    public async Task SquadLeader_exposes_four_management_tools()
    {
        var grain = _cluster.GrainFactory.GetGrain<ITestSquadLeaderGrain>("squadleader");
        var toolNames = await grain.GetToolNamesAsync();

        toolNames.ShouldContain("WakeAgent");
        toolNames.ShouldContain("SuspendAgent");
        toolNames.ShouldContain("SendTo");
        toolNames.ShouldContain("GetAgentStatus");
    }
}
