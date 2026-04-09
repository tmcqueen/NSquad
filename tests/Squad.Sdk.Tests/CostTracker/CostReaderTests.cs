using Squad.Sdk.CostTracker;
using Shouldly;

namespace Squad.Sdk.Tests.CostTracker;

public class CostReaderTests
{
    private string _squadDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _squadDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), ".squad");
        Directory.CreateDirectory(Path.Combine(_squadDir, "costs"));
    }

    [After(Test)]
    public void Cleanup()
    {
        var parent = Path.GetDirectoryName(_squadDir)!;
        if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
    }

    [Test]
    public async Task LoadEntriesAsync_returns_empty_when_no_costs_dir()
    {
        var noSquad = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), ".squad");

        var result = await CostReader.LoadEntriesAsync(noSquad);

        result.ShouldBeEmpty();
    }

    [Test]
    public async Task LoadEntriesAsync_reads_cost_json_files()
    {
        var json = """
            {
              "agent": "builder",
              "inputTokens": 1000,
              "outputTokens": 500,
              "estimatedCost": 0.0025,
              "timestamp": "2026-04-08T10:00:00Z"
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_squadDir, "costs", "entry1.json"), json);

        var result = await CostReader.LoadEntriesAsync(_squadDir);

        result.Count.ShouldBe(1);
        result[0].Agent.ShouldBe("builder");
        result[0].InputTokens.ShouldBe(1000);
        result[0].OutputTokens.ShouldBe(500);
        result[0].EstimatedCost.ShouldBe(0.0025m);
    }

    [Test]
    public async Task Summarize_groups_by_agent()
    {
        var entries = new[]
        {
            new CostEntry { Agent = "builder", InputTokens = 100, OutputTokens = 50, EstimatedCost = 0.001m, Timestamp = DateTimeOffset.UtcNow },
            new CostEntry { Agent = "builder", InputTokens = 200, OutputTokens = 100, EstimatedCost = 0.002m, Timestamp = DateTimeOffset.UtcNow },
            new CostEntry { Agent = "tester", InputTokens = 50, OutputTokens = 25, EstimatedCost = 0.0005m, Timestamp = DateTimeOffset.UtcNow },
        };

        var summary = CostReader.Summarize(entries);

        summary.Count.ShouldBe(2);
        summary["builder"].TotalInputTokens.ShouldBe(300);
        summary["builder"].TotalOutputTokens.ShouldBe(150);
        summary["builder"].TotalCost.ShouldBe(0.003m);
        summary["tester"].TotalCost.ShouldBe(0.0005m);
    }
}
