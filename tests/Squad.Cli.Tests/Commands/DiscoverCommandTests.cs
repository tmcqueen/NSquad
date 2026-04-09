using Squad.Sdk.Discovery;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class DiscoverCommandTests
{
    [Test]
    public void FormatTable_returns_message_for_empty()
    {
        SquadDiscovery.FormatTable(Array.Empty<DiscoveredSquad>()).ShouldContain("No squads");
    }

    [Test]
    public void FormatTable_includes_squad_name()
    {
        var manifest = new SquadManifest("alpha-team", new List<string> { "issues" },
            new SquadContact("owner/alpha", new List<string> { "squad" }));
        var squads = new[] { new DiscoveredSquad(".squad", manifest) };
        SquadDiscovery.FormatTable(squads).ShouldContain("alpha-team");
    }
}
