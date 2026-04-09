using Squad.Sdk.Discovery;
using Shouldly;

namespace Squad.Sdk.Tests.Discovery;

public class SquadDiscoveryTests
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
    public async Task Discover_returns_empty_when_no_upstream_json()
    {
        var squads = await SquadDiscovery.DiscoverAsync(_tempDir);
        squads.ShouldBeEmpty();
    }

    [Test]
    public async Task Discover_finds_squads_with_manifest()
    {
        // Create a fake upstream squad directory with manifest
        var upstreamDir = Path.Combine(_tempDir, "other-squad");
        Directory.CreateDirectory(Path.Combine(upstreamDir, ".squad"));
        var manifest = """{"name":"other-team","accepts":["issues"],"contact":{"repo":"owner/other","labels":["squad"]}}""";
        await File.WriteAllTextAsync(Path.Combine(upstreamDir, ".squad", "squad.manifest.json"), manifest);

        // Write upstream.json pointing to it
        var upstreamJson = $$$"""
            {
              "upstreams": [
                {"name":"other","type":"local","source":"{{{upstreamDir}}}","addedAt":"2026-01-01T00:00:00Z"}
              ]
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".squad", "upstream.json"), upstreamJson);

        var squads = await SquadDiscovery.DiscoverAsync(_tempDir);
        squads.Count.ShouldBe(1);
        squads[0].Manifest.Name.ShouldBe("other-team");
    }
}
