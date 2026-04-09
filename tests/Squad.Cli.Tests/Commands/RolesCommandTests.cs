using Squad.Cli.Commands;
using Squad.Sdk.Roles;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class RolesCommandTests
{
    [Test]
    public void BuiltinRoles_All_has_20_entries()
    {
        BuiltinRoles.All.Count.ShouldBe(20);
    }

    [Test]
    public void Filter_by_category_returns_only_matching()
    {
        var engineering = BuiltinRoles.Filter(category: "engineering");
        engineering.ShouldNotBeEmpty();
        engineering.ShouldAllBe(r => r.Category == "engineering");
    }

    [Test]
    public void Filter_by_search_returns_matching()
    {
        var results = BuiltinRoles.Filter(search: "backend");
        results.ShouldNotBeEmpty();
        results.ShouldContain(r => r.Id == "backend");
    }

    [Test]
    public void Filter_no_args_returns_all()
    {
        BuiltinRoles.Filter().Count.ShouldBe(BuiltinRoles.All.Count);
    }

    [Test]
    public void Categories_are_unique_and_sorted()
    {
        var cats = BuiltinRoles.Categories;
        cats.Count.ShouldBe(cats.Distinct().Count());
        cats.ShouldBe(cats.Order().ToList());
    }
}
