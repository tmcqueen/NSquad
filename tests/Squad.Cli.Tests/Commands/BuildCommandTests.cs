using Squad.Cli.Commands;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class BuildCommandTests
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

    private async Task WriteConfig(string json)
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "squad.config.json"), json);
    }

    [Test]
    public async Task Build_creates_team_md()
    {
        await WriteConfig("""
            {
              "team": { "name": "Test Squad" },
              "agents": [{ "name": "builder", "role": "dev" }]
            }
            """);

        var result = await BuildCommand.BuildAsync(_tempDir);
        result.Written.ShouldBeGreaterThan(0);
        File.Exists(Path.Combine(_tempDir, ".squad", "team.md")).ShouldBeTrue();
    }

    [Test]
    public async Task Build_check_mode_returns_false_when_files_missing()
    {
        await WriteConfig("""
            {
              "team": { "name": "Test Squad" },
              "agents": [{ "name": "builder", "role": "dev" }]
            }
            """);

        var hasDrift = await BuildCommand.CheckDriftAsync(_tempDir);
        hasDrift.ShouldBeTrue(); // Files don't exist yet = drift
    }

    [Test]
    public async Task Build_check_mode_returns_true_after_build()
    {
        await WriteConfig("""
            {
              "team": { "name": "Test Squad" },
              "agents": [{ "name": "builder", "role": "dev" }]
            }
            """);

        await BuildCommand.BuildAsync(_tempDir);
        var hasDrift = await BuildCommand.CheckDriftAsync(_tempDir);
        hasDrift.ShouldBeFalse();
    }

    [Test]
    public async Task Build_throws_when_no_config()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => BuildCommand.BuildAsync(_tempDir));
    }
}
