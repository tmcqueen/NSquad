namespace Squad.Sdk.Roles;

/// <summary>A single built-in agent role definition.</summary>
public sealed record RoleDefinition(
    /// <summary>Unique slug identifier (e.g. <c>lead</c>, <c>frontend</c>).</summary>
    string Id,
    /// <summary>Human-readable display title (e.g. <c>Lead / Architect</c>).</summary>
    string Title,
    /// <summary>One-line personality or work-style description for the role.</summary>
    string Vibe,
    /// <summary>Category grouping (e.g. <c>engineering</c>, <c>quality</c>, <c>design</c>).</summary>
    string Category,
    /// <summary>Emoji representing the role in UI contexts.</summary>
    string Emoji);

/// <summary>
/// Embedded built-in role catalog (20 roles).
/// Attribution: Adapted from agency-agents by AgentLand Contributors (MIT).
/// </summary>
public static class BuiltinRoles
{
    /// <summary>All 20 built-in role definitions.</summary>
    public static readonly IReadOnlyList<RoleDefinition> All = new List<RoleDefinition>
    {
        // Engineering
        new("lead",       "Lead / Architect",    "Designs systems that survive the team that built them.",            "engineering", "🏗️"),
        new("frontend",   "Frontend Developer",  "Builds responsive, accessible web apps with pixel-perfect precision.", "engineering", "⚛️"),
        new("backend",    "Backend Developer",   "Designs the systems that hold everything up — databases, APIs, cloud, scale.", "engineering", "🔧"),
        new("fullstack",  "Full-Stack Developer","Sees the full picture — from the database to the pixel.",           "engineering", "💻"),
        new("security",   "Security Engineer",   "Models threats, reviews code, and designs security architecture that actually holds.", "engineering", "🔒"),
        new("data",       "Data Engineer",       "Thinks in tables and queries. Normalizes first, denormalizes when the numbers demand it.", "engineering", "📊"),
        new("ai",         "AI / ML Engineer",    "Builds intelligent systems that learn, reason, and adapt.",         "engineering", "🤖"),
        // Quality
        new("reviewer",   "Code Reviewer",       "Reviews code like a mentor, not a gatekeeper.",                    "quality",      "👁️"),
        new("tester",     "Test Engineer",        "Breaks your API before your users do.",                            "quality",      "🧪"),
        // Operations
        new("devops",     "DevOps Engineer",     "Automates infrastructure so your team ships faster and sleeps better.", "operations", "⚙️"),
        // Design
        new("designer",   "UI/UX Designer",      "Pixel-aware and user-obsessed. If it looks off by one, it is off by one.", "design", "🎨"),
        // Product
        new("docs",       "Technical Writer",    "Turns complexity into clarity. If the docs are wrong, the product is wrong.", "product", "📝"),
        new("product-manager", "Product Manager","Shapes what gets built and why — every feature earns its place.",  "product",      "📋"),
        // Marketing
        new("marketing-strategist", "Marketing Strategist", "Drives growth through content and channels — every post has a purpose.", "marketing", "📣"),
        // Sales
        new("sales-strategist", "Sales Strategist", "Closes deals with strategic precision — understand the buyer before pitching.", "sales", "💼"),
        // Operations
        new("project-manager", "Project Manager","Keeps the train on the tracks — scope, schedule, and sanity.",     "operations",   "📅"),
        // Support
        new("support-specialist", "Support Specialist", "First line of defense for users — solve fast, document everything.", "support", "🎧"),
        // Game Dev
        new("game-developer", "Game Developer",  "Builds worlds players want to live in.",                           "game-dev",     "🎮"),
        // Media
        new("media-buyer", "Media Buyer",        "Maximizes ROI across ad channels — every dollar tracked.",         "media",        "📺"),
        // Compliance
        new("compliance-legal", "Compliance & Legal", "Ensures you ship safely and legally — compliance is a feature.", "compliance", "⚖️"),
    };

    /// <summary>Filter roles by optional category and/or search term (matched against id, title, and vibe).</summary>
    public static IReadOnlyList<RoleDefinition> Filter(string? category = null, string? search = null)
    {
        var roles = All.AsEnumerable();
        if (category != null)
            roles = roles.Where(r => r.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        if (search != null)
        {
            var q = search.ToLower();
            roles = roles.Where(r =>
                r.Id.Contains(q) || r.Title.ToLower().Contains(q) || r.Vibe.ToLower().Contains(q));
        }
        return roles.ToList();
    }

    /// <summary>Distinct sorted list of all category names present in <see cref="All"/>.</summary>
    public static IReadOnlyList<string> Categories =>
        All.Select(r => r.Category).Distinct().Order().ToList();
}
