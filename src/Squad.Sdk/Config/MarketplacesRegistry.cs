using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

public sealed record Marketplace(
    string Name,
    string Source,
    [property: JsonPropertyName("added_at")] string AddedAt);

public sealed record MarketplacesRegistry(List<Marketplace> Marketplaces)
{
    public MarketplacesRegistry() : this(new List<Marketplace>()) { }
}
