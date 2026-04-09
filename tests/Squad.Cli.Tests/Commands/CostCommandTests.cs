using Squad.Cli.Commands;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class CostCommandTests
{
    [Test]
    public void FormatCost_formats_small_amounts_correctly()
    {
        CostCommand.FormatCost(0.0025m).ShouldBe("$0.0025");
    }

    [Test]
    public void FormatCost_formats_larger_amounts_to_two_decimals()
    {
        CostCommand.FormatCost(1.5m).ShouldBe("$1.50");
    }

    [Test]
    public void FormatCost_formats_zero()
    {
        CostCommand.FormatCost(0m).ShouldBe("$0.00");
    }

    [Test]
    public void FormatTokens_abbreviates_thousands()
    {
        CostCommand.FormatTokens(1500).ShouldBe("1.5k");
        CostCommand.FormatTokens(10_000).ShouldBe("10.0k");
    }

    [Test]
    public void FormatTokens_does_not_abbreviate_below_thousand()
    {
        CostCommand.FormatTokens(999).ShouldBe("999");
        CostCommand.FormatTokens(0).ShouldBe("0");
    }
}
