using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

/// <summary>A registered plugin marketplace.</summary>
/// <param name="Name">Display name derived from the owner/repo slug.</param>
/// <param name="Source">GitHub owner/repo slug (e.g. acme/squad-plugins).</param>
/// <param name="AddedAt">ISO 8601 registration timestamp.</param>
public sealed record Marketplace(
    string Name,
    string Source,
    [property: JsonPropertyName("added_at")] string AddedAt);

/// <summary>Registry of all configured plugin marketplaces, persisted to .squad/marketplaces.json.</summary>
/// <param name="Marketplaces">Ordered list of registered marketplaces.</param>
public sealed record MarketplacesRegistry(List<Marketplace> Marketplaces)
{
    /// <summary>Create an empty registry.</summary>
    public MarketplacesRegistry() : this(new List<Marketplace>()) { }
}
