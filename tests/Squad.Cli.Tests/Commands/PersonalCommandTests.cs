using Squad.Cli.Commands.Personal;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class PersonalCommandTests
{
    private string _personalDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _personalDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "personal-squad");
    }

    [After(Test)]
    public void Cleanup()
    {
        var parent = Path.GetDirectoryName(_personalDir)!;
        if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
    }

    [Test]
    public void InitPersonal_creates_directory_and_config()
    {
        PersonalHelper.Init(_personalDir);
        Directory.Exists(_personalDir).ShouldBeTrue();
        File.Exists(Path.Combine(_personalDir, "config.json")).ShouldBeTrue();
    }

    [Test]
    public void AddAgent_creates_charter_and_history()
    {
        PersonalHelper.Init(_personalDir);
        PersonalHelper.AddAgent(_personalDir, "ripley", "lead");

        var agentDir = Path.Combine(_personalDir, "agents", "ripley");
        Directory.Exists(agentDir).ShouldBeTrue();
        File.Exists(Path.Combine(agentDir, "charter.md")).ShouldBeTrue();
        File.Exists(Path.Combine(agentDir, "history.md")).ShouldBeTrue();
    }

    [Test]
    public void AddAgent_charter_contains_name_and_role()
    {
        PersonalHelper.Init(_personalDir);
        PersonalHelper.AddAgent(_personalDir, "ripley", "lead");

        var charter = File.ReadAllText(Path.Combine(_personalDir, "agents", "ripley", "charter.md"));
        charter.ShouldContain("ripley");
        charter.ShouldContain("lead");
    }

    [Test]
    public void ListAgents_returns_added_agents()
    {
        PersonalHelper.Init(_personalDir);
        PersonalHelper.AddAgent(_personalDir, "ripley", "lead");
        PersonalHelper.AddAgent(_personalDir, "kane", "backend");

        var agents = PersonalHelper.ListAgents(_personalDir);
        agents.ShouldContain("ripley");
        agents.ShouldContain("kane");
    }

    [Test]
    public void RemoveAgent_deletes_agent_directory()
    {
        PersonalHelper.Init(_personalDir);
        PersonalHelper.AddAgent(_personalDir, "ripley", "lead");
        PersonalHelper.RemoveAgent(_personalDir, "ripley");

        var agentDir = Path.Combine(_personalDir, "agents", "ripley");
        Directory.Exists(agentDir).ShouldBeFalse();
    }

    [Test]
    public void RemoveAgent_throws_if_not_found()
    {
        PersonalHelper.Init(_personalDir);
        Should.Throw<InvalidOperationException>(() => PersonalHelper.RemoveAgent(_personalDir, "nobody"));
    }
}
