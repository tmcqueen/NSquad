using Squad.Cli.Commands;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class LinkCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void WriteRemoteConfig_creates_config_json_with_relative_path()
    {
        // Create a fake team repo
        var teamDir = Path.Combine(_tempDir, "team-repo");
        Directory.CreateDirectory(Path.Combine(teamDir, ".squad"));

        LinkCommand.WriteRemoteConfig(_tempDir, teamDir);

        var configPath = Path.Combine(_tempDir, ".squad", "config.json");
        File.Exists(configPath).ShouldBeTrue();
        var json = File.ReadAllText(configPath);
        json.ShouldContain("teamRoot");
        // Should store relative path
        json.ShouldContain("team-repo");
    }

    [Test]
    public void WriteRemoteConfig_adds_gitignore_entry()
    {
        var teamDir = Path.Combine(_tempDir, "team-repo");
        Directory.CreateDirectory(Path.Combine(teamDir, ".squad"));

        LinkCommand.WriteRemoteConfig(_tempDir, teamDir);

        var gitignorePath = Path.Combine(_tempDir, ".gitignore");
        File.Exists(gitignorePath).ShouldBeTrue();
        File.ReadAllText(gitignorePath).ShouldContain(".squad/config.json");
    }

    [Test]
    public void WriteRemoteConfig_throws_if_target_does_not_exist()
    {
        Should.Throw<InvalidOperationException>(() =>
            LinkCommand.WriteRemoteConfig(_tempDir, Path.Combine(_tempDir, "nonexistent")));
    }

    [Test]
    public void WriteRemoteConfig_throws_if_target_has_no_squad_dir()
    {
        var emptyDir = Path.Combine(_tempDir, "empty-dir");
        Directory.CreateDirectory(emptyDir);

        Should.Throw<InvalidOperationException>(() =>
            LinkCommand.WriteRemoteConfig(_tempDir, emptyDir));
    }

    [Test]
    public void WriteRemoteConfig_does_not_duplicate_gitignore_entry()
    {
        var teamDir = Path.Combine(_tempDir, "team-repo");
        Directory.CreateDirectory(Path.Combine(teamDir, ".squad"));

        // Write entry twice
        LinkCommand.WriteRemoteConfig(_tempDir, teamDir);
        LinkCommand.WriteRemoteConfig(_tempDir, teamDir);

        var content = File.ReadAllText(Path.Combine(_tempDir, ".gitignore"));
        content.Split('\n').Count(l => l.Trim() == ".squad/config.json").ShouldBe(1);
    }
}
