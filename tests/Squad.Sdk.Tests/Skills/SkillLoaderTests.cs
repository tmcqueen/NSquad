using Squad.Sdk.Skills;
using Shouldly;

namespace Squad.Sdk.Tests.Skills;

public class SkillLoaderTests
{
    private string _squadDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _squadDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), ".squad");
        Directory.CreateDirectory(Path.Combine(_squadDir, "skills", "my-skill"));
    }

    [After(Test)]
    public void Cleanup()
    {
        var parent = Path.GetDirectoryName(_squadDir)!;
        if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
    }

    [Test]
    public async Task DiscoverAsync_returns_empty_when_no_skills_dir()
    {
        var noSkills = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), ".squad");

        var result = await SkillLoader.DiscoverAsync(noSkills);

        result.ShouldBeEmpty();
    }

    [Test]
    public async Task DiscoverAsync_finds_skills_with_SKILL_md()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_squadDir, "skills", "my-skill", "SKILL.md"),
            "# my-skill\n\nA skill.");

        var result = await SkillLoader.DiscoverAsync(_squadDir);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("my-skill");
    }

    [Test]
    public async Task DiscoverAsync_reads_skill_content()
    {
        var content = "# my-skill\n\nDoes something useful.";
        await File.WriteAllTextAsync(
            Path.Combine(_squadDir, "skills", "my-skill", "SKILL.md"),
            content);

        var result = await SkillLoader.DiscoverAsync(_squadDir);

        result[0].Content.ShouldBe(content);
    }

    [Test]
    public async Task DiscoverAsync_skips_dirs_without_SKILL_md()
    {
        Directory.CreateDirectory(Path.Combine(_squadDir, "skills", "incomplete-skill"));

        var result = await SkillLoader.DiscoverAsync(_squadDir);

        result.ShouldBeEmpty();
    }
}
