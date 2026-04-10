using Orleans.TestingHost;
using Shouldly;
using Squad.Server.Grains;
using Squad.Server.Models;
using Squad.Server.Tests;

namespace Squad.Server.Tests.Grains;

public class AgentGrainLifecycleTests
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
    public async Task GetStatusAsync_returns_Suspended_before_Wake()
    {
        var grain = _cluster.GrainFactory.GetGrain<ITestAgentGrain>("test-lc-1");
        var status = await grain.GetStatusAsync();
        status.ShouldBe(AgentStatus.Suspended);
    }

    [Test]
    public async Task WakeAsync_transitions_status_to_Idle()
    {
        var grain = _cluster.GrainFactory.GetGrain<ITestAgentGrain>("test-lc-2");
        await grain.WakeAsync();
        var status = await grain.GetStatusAsync();
        status.ShouldBe(AgentStatus.Idle);
    }

    [Test]
    public async Task SuspendAsync_transitions_status_to_Suspended()
    {
        var grain = _cluster.GrainFactory.GetGrain<ITestAgentGrain>("test-lc-3");
        await grain.WakeAsync();
        await grain.SuspendAsync();
        var status = await grain.GetStatusAsync();
        status.ShouldBe(AgentStatus.Suspended);
    }

    [Test]
    public async Task GetHistoryAsync_returns_empty_initially()
    {
        var grain = _cluster.GrainFactory.GetGrain<ITestAgentGrain>("test-lc-4");
        var history = await grain.GetHistoryAsync();
        history.ShouldBeEmpty();
    }
}
