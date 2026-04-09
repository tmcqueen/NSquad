using Squad.Cli.Commands.Plugin;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class PluginCommandTests
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
    public async Task AddMarketplace_adds_to_registry()
    {
        await MarketplaceHelper.AddAsync(_tempDir, "owner/my-marketplace");
        var reg = await MarketplaceHelper.ReadAsync(_tempDir);
        reg.Marketplaces.ShouldContain(m => m.Source == "owner/my-marketplace");
    }

    [Test]
    public async Task AddMarketplace_is_idempotent()
    {
        await MarketplaceHelper.AddAsync(_tempDir, "owner/my-marketplace");
        await MarketplaceHelper.AddAsync(_tempDir, "owner/my-marketplace"); // duplicate
        var reg = await MarketplaceHelper.ReadAsync(_tempDir);
        reg.Marketplaces.Count(m => m.Source == "owner/my-marketplace").ShouldBe(1);
    }

    [Test]
    public async Task RemoveMarketplace_removes_from_registry()
    {
        await MarketplaceHelper.AddAsync(_tempDir, "owner/my-marketplace");
        await MarketplaceHelper.RemoveAsync(_tempDir, "my-marketplace");
        var reg = await MarketplaceHelper.ReadAsync(_tempDir);
        reg.Marketplaces.ShouldNotContain(m => m.Name == "my-marketplace");
    }

    [Test]
    public async Task RemoveMarketplace_throws_if_not_found()
    {
        await Should.ThrowAsync<InvalidOperationException>(() =>
            MarketplaceHelper.RemoveAsync(_tempDir, "nonexistent"));
    }

    [Test]
    public async Task ReadMarketplaces_returns_empty_when_no_file()
    {
        var reg = await MarketplaceHelper.ReadAsync(_tempDir);
        reg.Marketplaces.ShouldBeEmpty();
    }
}
