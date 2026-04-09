using Squad.Sdk.PersonalSquad;
using Shouldly;

namespace Squad.Sdk.Tests.PersonalSquad;

public class StagedLearningsTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad", "extract"));
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void Load_returns_empty_when_no_extract_dir()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(emptyDir, ".squad"));
        var result = StagedLearnings.Load(emptyDir);
        result.ShouldBeEmpty();
    }

    [Test]
    public void Load_returns_md_files_from_extract_dir()
    {
        var extractDir = Path.Combine(_tempDir, ".squad", "extract");
        File.WriteAllText(Path.Combine(extractDir, "learning1.md"), "# Pattern X\n\nAlways use X.");
        File.WriteAllText(Path.Combine(extractDir, "learning2.md"), "# Pattern Y\n\nPrefer Y.");

        var learnings = StagedLearnings.Load(_tempDir);
        learnings.Count.ShouldBe(2);
        learnings.ShouldContain(l => l.Filename == "learning1.md");
    }

    [Test]
    public async Task MergeToPersonalSquad_appends_to_decisions_md()
    {
        var personalDir = Path.Combine(_tempDir, "personal-squad");
        Directory.CreateDirectory(personalDir);
        File.WriteAllText(Path.Combine(personalDir, "decisions.md"), "# Decisions\n");

        var learnings = new List<StagedLearning>
        {
            new("test.md", "/tmp/test.md", "## New Learning\n\nUse async/await.")
        };

        var result = await StagedLearnings.MergeAsync(learnings, personalDir);
        result.Decisions.ShouldBe(1);

        var decisions = File.ReadAllText(Path.Combine(personalDir, "decisions.md"));
        decisions.ShouldContain("Use async/await.");
    }
}
