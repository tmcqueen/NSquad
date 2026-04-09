using Squad.Cli.Commands;
using Squad.Sdk.Config;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class CastCommandTests
{
    private string _root = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_root, ".squad"));
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Test]
    public async Task LoadCastAsync_returns_agents_from_config()
    {
        var json = """
            {
              "team": { "name": "Alpha" },
              "agents": [
                { "name": "striker", "role": "feature-dev" },
                { "name": "keeper", "role": "testing" }
              ]
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_root, "squad.config.json"), json);

        var agents = await CastCommand.LoadCastAsync(_root);

        agents.Count.ShouldBe(2);
        agents.ShouldContain(a => a.Name == "striker");
        agents.ShouldContain(a => a.Name == "keeper");
    }

    [Test]
    public async Task LoadCastAsync_returns_empty_when_no_config()
    {
        var agents = await CastCommand.LoadCastAsync(_root);
        agents.ShouldBeEmpty();
    }

    [Test]
    public async Task LoadCastAsync_includes_agent_role_and_model()
    {
        var json = """
            {
              "team": { "name": "Beta" },
              "agents": [{ "name": "maker", "role": "architecture", "model": "claude-sonnet-4.5" }]
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_root, "squad.config.json"), json);

        var agents = await CastCommand.LoadCastAsync(_root);

        agents[0].Role.ShouldBe("architecture");
        agents[0].Model.ShouldBe("claude-sonnet-4.5");
    }
}
