using Squad.Sdk.Config;
using Shouldly;

namespace Squad.Sdk.Tests.Config;

public class ConfigLoaderTests
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
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task LoadAsync_reads_squad_config_json()
    {
        var json = """
            {
              "version": "1.0",
              "team": { "name": "Alpha Team" },
              "agents": [
                { "name": "builder", "role": "feature-dev", "model": "claude-sonnet-4.5" }
              ]
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "squad.config.json"), json);

        var result = await ConfigLoader.LoadAsync(_tempDir);

        result.ShouldNotBeNull();
        result.Team.Name.ShouldBe("Alpha Team");
        result.Agents.Count.ShouldBe(1);
        result.Agents[0].Name.ShouldBe("builder");
        result.Agents[0].Model.ShouldBe("claude-sonnet-4.5");
    }

    [Test]
    public async Task LoadAsync_returns_null_when_no_config_file()
    {
        var result = await ConfigLoader.LoadAsync(_tempDir);
        result.ShouldBeNull();
    }

    [Test]
    public async Task LoadAsync_throws_on_malformed_json()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "squad.config.json"), "{ invalid }");

        await Should.ThrowAsync<ConfigLoadException>(() => ConfigLoader.LoadAsync(_tempDir));
    }

    [Test]
    public async Task LoadAsync_handles_minimal_config()
    {
        var json = """{ "team": { "name": "Solo" }, "agents": [] }""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "squad.config.json"), json);

        var result = await ConfigLoader.LoadAsync(_tempDir);

        result.ShouldNotBeNull();
        result.Agents.ShouldBeEmpty();
        result.Routing.ShouldBeNull();
    }
}
