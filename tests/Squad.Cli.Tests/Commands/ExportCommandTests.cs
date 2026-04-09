using System.Text.Json;
using Squad.Cli.Commands;
using Squad.Sdk.Config;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class ExportCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        // Create minimal .squad/ structure
        var squadDir = Path.Combine(_tempDir, ".squad");
        Directory.CreateDirectory(squadDir);
        Directory.CreateDirectory(Path.Combine(squadDir, "casting"));
        Directory.CreateDirectory(Path.Combine(squadDir, "agents", "builder"));
        File.WriteAllText(Path.Combine(squadDir, "team.md"), "# Squad Team");
        File.WriteAllText(Path.Combine(squadDir, "agents", "builder", "charter.md"), "# Builder");
        File.WriteAllText(Path.Combine(squadDir, "casting", "registry.json"), """{"universe":"mcqueen"}""");
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task BuildManifest_includes_agents()
    {
        var manifest = await ExportCommand.BuildManifestAsync(_tempDir);
        manifest.Agents.ShouldContainKey("builder");
        manifest.Agents["builder"].Charter?.ShouldContain("Builder");
    }

    [Test]
    public async Task BuildManifest_includes_casting()
    {
        var manifest = await ExportCommand.BuildManifestAsync(_tempDir);
        manifest.Casting.ShouldContainKey("registry");
    }

    [Test]
    public async Task BuildManifest_version_is_1_0()
    {
        var manifest = await ExportCommand.BuildManifestAsync(_tempDir);
        manifest.Version.ShouldBe("1.0");
    }

    [Test]
    public async Task BuildManifest_includes_skills_when_present()
    {
        var skillsDir = Path.Combine(_tempDir, ".squad", "skills", "my-skill");
        Directory.CreateDirectory(skillsDir);
        File.WriteAllText(Path.Combine(skillsDir, "SKILL.md"), "# My Skill");

        var manifest = await ExportCommand.BuildManifestAsync(_tempDir);
        manifest.Skills.ShouldNotBeEmpty();
    }
}
