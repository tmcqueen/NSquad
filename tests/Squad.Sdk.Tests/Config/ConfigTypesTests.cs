using Squad.Sdk.Config;
using Shouldly;

namespace Squad.Sdk.Tests.Config;

public class ConfigTypesTests
{
    [Test]
    public void AgentConfig_defaults_are_sensible()
    {
        var agent = new AgentConfig { Name = "archer" };

        agent.Name.ShouldBe("archer");
        agent.Skills.ShouldBeEmpty();
        agent.Metadata.ShouldBeEmpty();
    }

    [Test]
    public void SquadConfig_can_be_constructed_with_init()
    {
        var config = new SquadConfig
        {
            Team = new TeamConfig { Name = "Delta" },
            Agents = [new AgentConfig { Name = "striker", Role = "feature-dev" }]
        };

        config.Team.Name.ShouldBe("Delta");
        config.Agents.Count.ShouldBe(1);
        config.Agents[0].Role.ShouldBe("feature-dev");
    }

    [Test]
    public void RoutingRule_requires_pattern_and_agent()
    {
        var rule = new RoutingRule { Pattern = "bug.*", Agent = "fixer" };
        rule.Pattern.ShouldBe("bug.*");
        rule.Agent.ShouldBe("fixer");
    }
}
