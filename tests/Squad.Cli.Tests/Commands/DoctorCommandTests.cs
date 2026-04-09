using Squad.Cli.Commands;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class DoctorCommandTests
{
    private string _root = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, ".git")); // repo boundary
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void RunChecks_fails_when_no_squad_dir()
    {
        var checks = DoctorCommand.RunChecks(_root);

        var squadCheck = checks.Single(c => c.Name == ".squad/ directory exists");
        squadCheck.Status.ShouldBe(DoctorCommand.CheckStatus.Fail);
    }

    [Test]
    public void RunChecks_passes_squad_dir_check_when_dir_exists()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".squad"));

        var checks = DoctorCommand.RunChecks(_root);

        var squadCheck = checks.Single(c => c.Name == ".squad/ directory exists");
        squadCheck.Status.ShouldBe(DoctorCommand.CheckStatus.Pass);
    }

    [Test]
    public void RunChecks_warns_on_missing_team_md()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".squad"));

        var checks = DoctorCommand.RunChecks(_root);

        var teamCheck = checks.Single(c => c.Name == "team.md exists with ## Members");
        teamCheck.Status.ShouldBe(DoctorCommand.CheckStatus.Fail);
    }

    [Test]
    public void RunChecks_passes_team_md_when_file_has_members_header()
    {
        var squadDir = Path.Combine(_root, ".squad");
        Directory.CreateDirectory(squadDir);
        File.WriteAllText(Path.Combine(squadDir, "team.md"), "# Team\n\n## Members\n\n- Agent 1");

        var checks = DoctorCommand.RunChecks(_root);

        var teamCheck = checks.Single(c => c.Name == "team.md exists with ## Members");
        teamCheck.Status.ShouldBe(DoctorCommand.CheckStatus.Pass);
    }

    [Test]
    public void RunChecks_detects_local_mode()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".squad"));

        var mode = DoctorCommand.DetectMode(_root);

        mode.ShouldBe(DoctorCommand.SquadDoctorMode.Local);
    }

    [Test]
    public void RunChecks_detects_remote_mode_when_config_has_team_root()
    {
        var squadDir = Path.Combine(_root, ".squad");
        Directory.CreateDirectory(squadDir);
        File.WriteAllText(
            Path.Combine(squadDir, "config.json"),
            """{ "version": 1, "teamRoot": "../team", "projectKey": null }""");

        var mode = DoctorCommand.DetectMode(_root);

        mode.ShouldBe(DoctorCommand.SquadDoctorMode.Remote);
    }
}
