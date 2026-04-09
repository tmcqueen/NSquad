using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

/// <summary>A registered plugin marketplace.</summary>
public sealed record Marketplace(
    /// <summary>Display name derived from the owner/repo slug.</summary>
    string Name,
    /// <summary>GitHub owner/repo slug (e.g. acme/squad-plugins).</summary>
    string Source,
    /// <summary>ISO 8601 registration timestamp.</summary>
    [property: JsonPropertyName("added_at")] string AddedAt);

/// <summary>Registry of all configured plugin marketplaces, persisted to .squad/marketplaces.json.</summary>
public sealed record MarketplacesRegistry(
    /// <summary>Ordered list of registered marketplaces.</summary>
    List<Marketplace> Marketplaces)
{
    /// <summary>Create an empty registry.</summary>
    public MarketplacesRegistry() : this(new List<Marketplace>()) { }
}
