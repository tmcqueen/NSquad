using System.Text.Json;
using Squad.Cli.Commands;
using Squad.Sdk.Config;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class StreamsCommandTests
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
    public async Task LoadConfig_returns_empty_when_no_streams_json()
    {
        var cfg = await StreamsCommand.LoadConfigAsync(_tempDir);
        cfg.Workstreams.ShouldBeEmpty();
    }

    [Test]
    public async Task LoadConfig_parses_streams_json()
    {
        var json = JsonSerializer.Serialize(new
        {
            workstreams = new[]
            {
                new { name = "feature", labelFilter = "squad:feature", workflow = "feature" }
            },
            defaultWorkflow = "feature"
        });
        var path = Path.Combine(_tempDir, ".squad", "streams.json");
        await File.WriteAllTextAsync(path, json);

        var cfg = await StreamsCommand.LoadConfigAsync(_tempDir);
        cfg.Workstreams.Count.ShouldBe(1);
        cfg.Workstreams[0].Name.ShouldBe("feature");
    }

    [Test]
    public void GetActiveStream_returns_null_when_no_workstream_file()
    {
        StreamsCommand.GetActiveStream(_tempDir).ShouldBeNull();
    }

    [Test]
    public void GetActiveStream_returns_name_from_file()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".squad-workstream"), "feature\n");
        StreamsCommand.GetActiveStream(_tempDir).ShouldBe("feature");
    }

    [Test]
    public void ActivateStream_writes_workstream_file()
    {
        StreamsCommand.ActivateStream(_tempDir, "bugfix");
        var active = StreamsCommand.GetActiveStream(_tempDir);
        active.ShouldBe("bugfix");
    }
}
