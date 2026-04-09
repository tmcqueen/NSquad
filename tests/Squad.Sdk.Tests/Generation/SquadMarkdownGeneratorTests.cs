using Squad.Sdk.Config;
using Squad.Sdk.Generation;
using Shouldly;

namespace Squad.Sdk.Tests.Generation;

public class SquadMarkdownGeneratorTests
{
    private static SquadConfig MakeConfig(string teamName = "Alpha Team") =>
        new()
        {
            Version = "1.0",
            Team = new TeamConfig { Name = teamName, Description = "A test team" },
            Agents = new List<AgentConfig>
            {
                new() { Name = "builder", Role = "feature-dev", Model = "claude-sonnet-4.5" }
            },
            Routing = new RoutingConfig
            {
                Rules = new List<RoutingRule>
                {
                    new() { Pattern = "test", Agent = "tester", WorkTypes = new List<string> { "testing" }, Priority = 10 }
                },
                DefaultAgent = "builder",
                FallbackAgent = null
            }
        };

    [Test]
    public void Build_generates_team_md()
    {
        var files = SquadMarkdownGenerator.Build(MakeConfig());
        files.ShouldContain(f => f.RelPath == ".squad/team.md");
        var teamMd = files.First(f => f.RelPath == ".squad/team.md").Content;
        teamMd.ShouldContain("Alpha Team");
        teamMd.ShouldContain("## Members");
        teamMd.ShouldContain("builder");
    }

    [Test]
    public void Build_generates_routing_md_when_routing_exists()
    {
        var files = SquadMarkdownGenerator.Build(MakeConfig());
        files.ShouldContain(f => f.RelPath == ".squad/routing.md");
        var routingMd = files.First(f => f.RelPath == ".squad/routing.md").Content;
        routingMd.ShouldContain("builder");
    }

    [Test]
    public void Build_generates_charter_per_agent()
    {
        var files = SquadMarkdownGenerator.Build(MakeConfig());
        files.ShouldContain(f => f.RelPath == ".squad/agents/builder/charter.md");
        var charterMd = files.First(f => f.RelPath == ".squad/agents/builder/charter.md").Content;
        charterMd.ShouldContain("builder");
        charterMd.ShouldContain("feature-dev");
    }

    [Test]
    public void Build_does_not_generate_routing_md_when_no_routing()
    {
        var cfg = MakeConfig() with { Routing = null };
        var files = SquadMarkdownGenerator.Build(cfg);
        files.ShouldNotContain(f => f.RelPath == ".squad/routing.md");
    }

    [Test]
    public void CheckDrift_returns_true_when_files_match()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var files = SquadMarkdownGenerator.Build(MakeConfig());
            SquadMarkdownGenerator.WriteFiles(tempDir, files);
            SquadMarkdownGenerator.CheckDrift(tempDir, files).ShouldBeTrue();
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Test]
    public void CheckDrift_returns_false_when_file_missing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempDir);
            var files = SquadMarkdownGenerator.Build(MakeConfig());
            // Don't write files — all are missing
            SquadMarkdownGenerator.CheckDrift(tempDir, files).ShouldBeFalse();
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }
}
