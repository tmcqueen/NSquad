using Squad.Sdk.Config;
using Squad.Sdk.Coordinator;
using Squad.Sdk.Events;
using Shouldly;

namespace Squad.Sdk.Tests.Coordinator;

public class CoordinatorTests
{
    private static SquadConfig MinimalConfig(string defaultAgent = "worker") => new()
    {
        Team = new TeamConfig { Name = "Test" },
        Agents = [new AgentConfig { Name = defaultAgent }],
        Routing = new RoutingConfig
        {
            DefaultAgent = defaultAgent,
            Rules = [new RoutingRule { Pattern = "bug|fix", Agent = defaultAgent }]
        }
    };

    [Test]
    public void RouteMessage_returns_default_agent_when_no_match()
    {
        var coordinator = new SquadCoordinator(MinimalConfig(), new EventBus());

        var agent = coordinator.RouteMessage("unrelated query");

        agent.ShouldBe("worker");
    }

    [Test]
    public void RouteMessage_uses_routing_rule_when_pattern_matches()
    {
        var config = new SquadConfig
        {
            Team = new TeamConfig { Name = "Test" },
            Agents =
            [
                new AgentConfig { Name = "fixer" },
                new AgentConfig { Name = "builder" },
            ],
            Routing = new RoutingConfig
            {
                DefaultAgent = "builder",
                Rules = [new RoutingRule { Pattern = "bug|fix", Agent = "fixer" }]
            }
        };
        var coordinator = new SquadCoordinator(config, new EventBus());

        var agent = coordinator.RouteMessage("fix the login bug");

        agent.ShouldBe("fixer");
    }

    [Test]
    public void RouteMessage_falls_back_to_first_agent_when_no_default()
    {
        var config = new SquadConfig
        {
            Team = new TeamConfig { Name = "Test" },
            Agents = [new AgentConfig { Name = "only-agent" }],
            Routing = new RoutingConfig { Rules = [] }
        };
        var coordinator = new SquadCoordinator(config, new EventBus());

        var agent = coordinator.RouteMessage("anything");

        agent.ShouldBe("only-agent");
    }
}
