using Squad.Cli.Infrastructure;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class CopilotCommandTests
{
    private static readonly string SampleTeamMd = """
        # Squad Team — Test Squad

        ## Members

        | Name | Role | Charter | Status |
        |------|------|---------|--------|
        | Builder | Feature Dev | `.squad/agents/builder/charter.md` | ✅ Active |
        """;

    [Test]
    public void HasCopilot_returns_false_when_not_present()
    {
        TeamMdHelper.HasCopilot(SampleTeamMd).ShouldBeFalse();
    }

    [Test]
    public void InsertCopilotSection_adds_copilot_row()
    {
        var result = TeamMdHelper.InsertCopilotSection(SampleTeamMd, autoAssign: false);
        result.ShouldContain("Copilot");
        TeamMdHelper.HasCopilot(result).ShouldBeTrue();
    }

    [Test]
    public void RemoveCopilotSection_removes_copilot_row()
    {
        var withCopilot = TeamMdHelper.InsertCopilotSection(SampleTeamMd, autoAssign: false);
        var result = TeamMdHelper.RemoveCopilotSection(withCopilot);
        TeamMdHelper.HasCopilot(result).ShouldBeFalse();
    }

    [Test]
    public void InsertCopilotSection_with_auto_assign_adds_marker()
    {
        var result = TeamMdHelper.InsertCopilotSection(SampleTeamMd, autoAssign: true);
        result.ShouldContain("copilot-auto-assign: true");
    }

    [Test]
    public void RemoveCopilotSection_is_idempotent_on_content_without_copilot()
    {
        var result = TeamMdHelper.RemoveCopilotSection(SampleTeamMd);
        result.ShouldBe(SampleTeamMd);
    }
}
