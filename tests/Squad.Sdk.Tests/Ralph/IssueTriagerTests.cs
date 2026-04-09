using Squad.Sdk.Ralph;
using Shouldly;

namespace Squad.Sdk.Tests.Ralph;

public class IssueTriagerTests
{
    private static readonly string SampleTeamMd = """
        # Squad Team — Test

        ## Members

        | Name | Role | Charter | Status |
        |------|------|---------|--------|
        | Builder | Feature Dev | `.squad/agents/builder/charter.md` | ✅ Active |
        | Tester | Test Engineer | `.squad/agents/tester/charter.md` | ✅ Active |
        """;

    [Test]
    public void ParseRoster_returns_members()
    {
        var roster = IssueTriager.ParseRoster(SampleTeamMd);
        roster.Count.ShouldBe(2);
        roster.ShouldContain(m => m.Name == "builder");
        roster.ShouldContain(m => m.Name == "tester");
    }

    [Test]
    public void ParseRoster_assigns_squad_labels()
    {
        var roster = IssueTriager.ParseRoster(SampleTeamMd);
        var builder = roster.First(m => m.Name == "builder");
        builder.Label.ShouldBe("squad:builder");
    }

    [Test]
    public void Triage_assigns_tester_for_test_issue()
    {
        var roster = IssueTriager.ParseRoster(SampleTeamMd);
        var rules = new List<IssueRoutingRule>
        {
            new("test", "tester"),
            new("feature", "builder"),
        };

        var result = IssueTriager.Triage("Add test coverage", null, new[] { "squad" }, rules, roster);
        result.ShouldNotBeNull();
        result!.Agent.Name.ShouldBe("tester");
    }

    [Test]
    public void Triage_returns_null_when_no_match()
    {
        var roster = IssueTriager.ParseRoster(SampleTeamMd);
        var rules = new List<IssueRoutingRule> { new("test", "tester") };

        var result = IssueTriager.Triage("Random unrelated issue", null, new[] { "squad" }, rules, roster);
        result.ShouldBeNull();
    }

    [Test]
    public void Triage_returns_null_when_roster_empty()
    {
        var result = IssueTriager.Triage("Add feature", null, new[] { "squad" },
            new List<IssueRoutingRule>(), new List<RosterMember>());
        result.ShouldBeNull();
    }
}
