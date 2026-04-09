using Squad.Cli.Commands;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class EconomyCommandTests
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
    public void SetEconomyMode_on_writes_true_to_config()
    {
        EconomyCommand.SetEconomyMode(_tempDir, true);

        var configPath = Path.Combine(_tempDir, ".squad", "config.json");
        var json = File.ReadAllText(configPath);
        json.ShouldContain("true");
    }

    [Test]
    public void GetEconomyMode_returns_false_when_no_config()
    {
        EconomyCommand.GetEconomyMode(_tempDir).ShouldBeFalse();
    }

    [Test]
    public void SetEconomyMode_on_then_off_returns_false()
    {
        EconomyCommand.SetEconomyMode(_tempDir, true);
        EconomyCommand.SetEconomyMode(_tempDir, false);
        EconomyCommand.GetEconomyMode(_tempDir).ShouldBeFalse();
    }
}
