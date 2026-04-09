using Squad.Cli.Commands;
using Squad.Sdk.Ralph;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class WatchCommandTests
{
    private static readonly string SampleTeamMd = """
        # Squad Team — Test

        ## Members

        | Name | Role | Charter | Status |
        |------|------|---------|--------|
        | Builder | Feature Dev | `.squad/agents/builder/charter.md` | ✅ Active |
        """;

    [Test]
    public void ParseRoster_uses_IssueTriager()
    {
        var roster = IssueTriager.ParseRoster(SampleTeamMd);
        roster.ShouldNotBeEmpty();
    }

    [Test]
    public void ValidateInterval_throws_for_zero()
    {
        Should.Throw<ArgumentException>(() => WatchCommand.ValidateInterval(0));
    }

    [Test]
    public void ValidateInterval_throws_for_negative()
    {
        Should.Throw<ArgumentException>(() => WatchCommand.ValidateInterval(-1));
    }

    [Test]
    public void ValidateInterval_accepts_positive()
    {
        WatchCommand.ValidateInterval(5); // Should not throw
    }

    [Test]
    public void FormatBoardLine_returns_string_for_positive_count()
    {
        WatchCommand.FormatBoardLine("Untriaged", 3).ShouldContain("3");
    }

    [Test]
    public void FormatBoardLine_returns_null_for_zero()
    {
        WatchCommand.FormatBoardLine("Untriaged", 0).ShouldBeNull();
    }
}
