using Squad.Cli.Commands;
using Squad.Sdk.PersonalSquad;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class ExtractCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad", "extract"));

        // Write consult mode config
        File.WriteAllText(Path.Combine(_tempDir, ".squad", "config.json"),
            """{"version":1,"consultMode":true,"sourceSquad":"/tmp/personal-squad"}""");
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void IsConsultMode_returns_true_when_flag_set()
    {
        ExtractCommand.IsConsultMode(Path.Combine(_tempDir, ".squad")).ShouldBeTrue();
    }

    [Test]
    public void IsConsultMode_returns_false_when_no_config()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(emptyDir, ".squad"));
        ExtractCommand.IsConsultMode(Path.Combine(emptyDir, ".squad")).ShouldBeFalse();
        if (Directory.Exists(emptyDir)) Directory.Delete(emptyDir, true);
    }

    [Test]
    public void FormatLearningPreview_truncates_at_50_chars()
    {
        var content = new string('x', 100);
        var preview = ExtractCommand.FormatLearningPreview(content);
        preview.Length.ShouldBeLessThanOrEqualTo(60); // 50 + "..."
    }

    [Test]
    public async Task ExtractLearnings_with_yes_flag_merges_without_prompt()
    {
        var personalDir = Path.Combine(_tempDir, "personal-squad");
        Directory.CreateDirectory(personalDir);
        File.WriteAllText(Path.Combine(personalDir, "decisions.md"), "# Decisions\n");

        // Stage a learning
        File.WriteAllText(Path.Combine(_tempDir, ".squad", "extract", "pattern.md"),
            "## Pattern X\n\nUse pattern X always.");

        await ExtractCommand.ExtractAsync(_tempDir, personalDir, yes: true);

        var decisions = File.ReadAllText(Path.Combine(personalDir, "decisions.md"));
        decisions.ShouldContain("Use pattern X always.");
    }
}
