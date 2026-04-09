using Squad.Sdk.PersonalSquad;
using Shouldly;

namespace Squad.Sdk.Tests.PersonalSquad;

public class HistorySplitterTests
{
    [Test]
    public void Split_puts_project_sections_in_learnings()
    {
        var history = """
            ## Portable Knowledge

            Uses async/await throughout.

            ## Key File Paths

            /src/main.cs is the entry point.
            """;

        var result = HistorySplitter.Split(history, "my-project");

        result.ShouldContain("Portable Knowledge");
        result.ShouldContain("Project Learnings (from import — my-project)");
        result.ShouldContain("Key File Paths");
    }

    [Test]
    public void Split_with_no_project_sections_returns_unchanged()
    {
        var history = "## Learnings\n\nUses pattern X.";
        var result = HistorySplitter.Split(history, "proj");
        result.ShouldContain("Uses pattern X");
        result.ShouldNotContain("Project Learnings");
    }

    [Test]
    public void Split_handles_empty_input()
    {
        var result = HistorySplitter.Split("", "proj");
        result.ShouldNotBeNull();
    }
}
