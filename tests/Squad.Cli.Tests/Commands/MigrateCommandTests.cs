using Squad.Cli.Commands;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class MigrateCommandTests
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
    public void DetectMode_none_when_empty()
    {
        MigrateCommand.DetectMode(_tempDir).ShouldBe("none");
    }

    [Test]
    public void DetectMode_sdk_when_config_json_exists()
    {
        File.WriteAllText(Path.Combine(_tempDir, "squad.config.json"), "{}");
        MigrateCommand.DetectMode(_tempDir).ShouldBe("sdk");
    }

    [Test]
    public void DetectMode_markdown_when_squad_dir_exists()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad"));
        MigrateCommand.DetectMode(_tempDir).ShouldBe("markdown");
    }

    [Test]
    public void DetectMode_legacy_when_ai_team_dir_exists()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".ai-team"));
        MigrateCommand.DetectMode(_tempDir).ShouldBe("legacy");
    }

    [Test]
    public void MigrateFromAiTeam_renames_directory()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".ai-team"));
        File.WriteAllText(Path.Combine(_tempDir, ".ai-team", "team.md"), "# Team");

        MigrateCommand.MigrateFromAiTeam(_tempDir);

        Directory.Exists(Path.Combine(_tempDir, ".squad")).ShouldBeTrue();
        Directory.Exists(Path.Combine(_tempDir, ".ai-team")).ShouldBeFalse();
        File.Exists(Path.Combine(_tempDir, ".squad", "team.md")).ShouldBeTrue();
    }

    [Test]
    public void GenerateConfigJson_contains_team_name()
    {
        var teamMd = """
            # Squad Team — Alpha Squad

            ## Members

            | Name | Role | Charter | Status |
            |------|------|---------|--------|
            | Builder | Feature Dev | `.squad/agents/builder/charter.md` | ✅ Active |
            """;
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad"));
        File.WriteAllText(Path.Combine(_tempDir, ".squad", "team.md"), teamMd);

        var json = MigrateCommand.GenerateConfigJson(_tempDir);
        json.ShouldContain("Alpha Squad");
        json.ShouldContain("builder");
    }
}
