using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Orleans;
using Shouldly;
using Squad.Server.Grains;
using Squad.Server.Hubs;
using Squad.Server.Models;
using Squad.Server.Services;

namespace Squad.Server.Tests.Hubs;

public class SquadHubTests
{
    [Test]
    public async Task WakeAgent_calls_WakeAsync_on_resolved_grain()
    {
        var (hub, grain, _) = MakeHub(["ralph"]);
        SetupHubContext(hub);

        await hub.WakeAgent("ralph");

        await grain.Received(1).WakeAsync();
    }

    [Test]
    public async Task SuspendAgent_calls_SuspendAsync_on_resolved_grain()
    {
        var (hub, grain, _) = MakeHub(["ralph"]);
        SetupHubContext(hub);

        await hub.SuspendAgent("ralph");

        await grain.Received(1).SuspendAsync();
    }

    [Test]
    public async Task GetAgentStatus_returns_status_for_all_configured_agents()
    {
        var (hub, grain, _) = MakeHub(["ralph", "scribe"]);
        grain.GetStatusAsync().Returns(AgentStatus.Idle);
        SetupHubContext(hub);

        var result = await hub.GetAgentStatus();

        result.Keys.ShouldContain("ralph");
        result.Keys.ShouldContain("scribe");
        result["ralph"].ShouldBe("Idle");
        result["scribe"].ShouldBe("Idle");
    }

    [Test]
    public async Task GetHistory_returns_history_from_grain()
    {
        var (hub, grain, _) = MakeHub(["ralph"]);
        var expectedHistory = new List<ChatMessage>
        {
            new ChatMessage { Role = "user", Content = "hello", Timestamp = DateTime.UtcNow }
        };
        grain.GetHistoryAsync().Returns(expectedHistory);
        SetupHubContext(hub);

        var result = await hub.GetHistory("ralph");

        result.ShouldBe(expectedHistory);
    }

    // --- Helpers ---

    private static (SquadHub hub, IAgentGrain grain, IClusterClient clusterClient) MakeHub(
        IEnumerable<string> agentNames)
    {
        var grain = Substitute.For<IAgentGrain>();
        var grainFactory = Substitute.For<IGrainFactory>();
        grainFactory
            .GetGrain<IAgentGrain>(Arg.Any<string>(), Arg.Any<string>())
            .Returns(grain);

        var configProvider = Substitute.For<ISquadConfigProvider>();
        configProvider.GetAllAgentNames().Returns(agentNames.ToList());

        var clusterClient = Substitute.For<IClusterClient>();

        var hub = new SquadHub(grainFactory, clusterClient, configProvider);
        return (hub, grain, clusterClient);
    }

    private static void SetupHubContext(Hub hub)
    {
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("test-conn-id");
        hub.Context = context;

        var clients = Substitute.For<IHubCallerClients>();
        clients.Caller.Returns(Substitute.For<ISingleClientProxy>());
        clients.All.Returns(Substitute.For<IClientProxy>());
        hub.Clients = clients;
    }
}
