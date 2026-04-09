using System.Text.RegularExpressions;
using Squad.Sdk.Config;

namespace Squad.Sdk.Coordinator;

/// <summary>
/// Compiles routing rules and matches messages to agent names.
/// Pattern is a regex tested case-insensitively against the message text.
/// </summary>
public sealed class RoutingEngine
{
    private readonly IReadOnlyList<(Regex Pattern, string Agent)> _compiled;

    /// <summary>Compile the given routing rules into fast regex matchers.</summary>
    public RoutingEngine(IEnumerable<RoutingRule> rules)
    {
        _compiled = rules
            .Select(r =>
            {
                var regex = new Regex(r.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                return (regex, r.Agent);
            })
            .ToList();
    }

    /// <summary>
    /// Returns the agent name for the first matching rule, or null if none match.
    /// </summary>
    public string? Match(string message)
    {
        foreach (var (pattern, agent) in _compiled)
        {
            if (pattern.IsMatch(message))
                return agent;
        }
        return null;
    }
}
