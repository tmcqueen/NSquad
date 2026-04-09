using Squad.Sdk.Config;
using Squad.Sdk.Coordinator;
using Shouldly;

namespace Squad.Sdk.Tests.Coordinator;

public class RoutingEngineTests
{
    [Test]
    public void Match_returns_null_when_no_rules()
    {
        var engine = new RoutingEngine([]);
        engine.Match("fix the login bug").ShouldBeNull();
    }

    [Test]
    public void Match_finds_rule_by_keyword_pattern()
    {
        var rules = new[]
        {
            new RoutingRule { Pattern = "bug|fix|error", Agent = "debugger" },
            new RoutingRule { Pattern = "feature|add|implement", Agent = "builder" },
        };
        var engine = new RoutingEngine(rules);

        engine.Match("fix the login bug").ShouldBe("debugger");
        engine.Match("implement new feature").ShouldBe("builder");
    }

    [Test]
    public void Match_is_case_insensitive()
    {
        var rules = new[] { new RoutingRule { Pattern = "test", Agent = "tester" } };
        var engine = new RoutingEngine(rules);

        engine.Match("Write Tests for the API").ShouldBe("tester");
    }

    [Test]
    public void Match_returns_first_rule_when_multiple_match()
    {
        var rules = new[]
        {
            new RoutingRule { Pattern = "bug", Agent = "first" },
            new RoutingRule { Pattern = "bug", Agent = "second" },
        };
        var engine = new RoutingEngine(rules);

        engine.Match("bug report").ShouldBe("first");
    }

    [Test]
    public void Match_returns_null_when_no_pattern_matches()
    {
        var rules = new[] { new RoutingRule { Pattern = "test", Agent = "tester" } };
        var engine = new RoutingEngine(rules);

        engine.Match("unrelated query").ShouldBeNull();
    }
}
