using Squad.Cli.Commands;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class UpstreamCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad"));
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void DetectSourceType_git_url()
    {
        UpstreamCommand.DetectSourceType("https://github.com/owner/repo.git").ShouldBe("git");
    }

    [Test]
    public void DetectSourceType_export_json()
    {
        var jsonFile = Path.Combine(_tempDir, "squad-export.json");
        File.WriteAllText(jsonFile, "{}");
        UpstreamCommand.DetectSourceType(jsonFile).ShouldBe("export");
    }

    [Test]
    public void DetectSourceType_local_directory()
    {
        UpstreamCommand.DetectSourceType(_tempDir).ShouldBe("local");
    }

    [Test]
    public void IsValidGitRef_accepts_valid_refs()
    {
        UpstreamCommand.IsValidGitRef("main").ShouldBeTrue();
        UpstreamCommand.IsValidGitRef("feature/my-branch").ShouldBeTrue();
        UpstreamCommand.IsValidGitRef("v1.0.0").ShouldBeTrue();
    }

    [Test]
    public void IsValidGitRef_rejects_shell_metacharacters()
    {
        UpstreamCommand.IsValidGitRef("main; rm -rf /").ShouldBeFalse();
        UpstreamCommand.IsValidGitRef("$(evil)").ShouldBeFalse();
    }

    [Test]
    public async Task AddUpstream_adds_entry_to_upstream_json()
    {
        await UpstreamCommand.AddAsync(_tempDir, _tempDir, name: "local-team");
        var config = await UpstreamCommand.ReadConfigAsync(_tempDir);
        config.Upstreams.ShouldContain(u => u.Name == "local-team");
    }

    [Test]
    public async Task RemoveUpstream_removes_entry()
    {
        await UpstreamCommand.AddAsync(_tempDir, _tempDir, name: "local-team");
        await UpstreamCommand.RemoveAsync(_tempDir, "local-team");
        var config = await UpstreamCommand.ReadConfigAsync(_tempDir);
        config.Upstreams.ShouldNotContain(u => u.Name == "local-team");
    }
}
