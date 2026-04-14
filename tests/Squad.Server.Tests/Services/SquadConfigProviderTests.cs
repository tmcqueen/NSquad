using Squad.Sdk.Config;
using Squad.Server.Models;
using Squad.Server.Services;
using Shouldly;

namespace Squad.Server.Tests.Services;

public class SquadConfigProviderTests
{
    private static SquadConfig MakeConfig(params AgentConfig[] agents)
        => new SquadConfig { Agents = [.. agents] };

    [Test]
    public void GetMemberConfig_returns_config_for_known_agent()
    {
        var config = MakeConfig(
            new AgentConfig { Name = "booster", Role = "backend", Charter = "templates/booster/charter.md" });
        var provider = new SquadConfigProvider(config);

        var result = provider.GetMemberConfig("booster");

        result.ShouldNotBeNull();
        result!.CharterPath.ShouldBe("templates/booster/charter.md");
        result.Role.ShouldBe("backend");
    }

    [Test]
    public void GetMemberConfig_is_case_insensitive()
    {
        var config = MakeConfig(new AgentConfig { Name = "Booster" });
        var provider = new SquadConfigProvider(config);

        provider.GetMemberConfig("booster").ShouldNotBeNull();
        provider.GetMemberConfig("BOOSTER").ShouldNotBeNull();
    }

    [Test]
    public void GetMemberConfig_returns_null_for_unknown_agent()
    {
        var config = MakeConfig(new AgentConfig { Name = "booster" });
        var provider = new SquadConfigProvider(config);

        provider.GetMemberConfig("ghost").ShouldBeNull();
    }

    [Test]
    public void GetAllAgentNames_returns_all_configured_names()
    {
        var config = MakeConfig(
            new AgentConfig { Name = "ralph" },
            new AgentConfig { Name = "scribe" },
            new AgentConfig { Name = "booster" });
        var provider = new SquadConfigProvider(config);

        var names = provider.GetAllAgentNames();
        names.ShouldBe(["ralph", "scribe", "booster"], ignoreOrder: true);
    }
}
