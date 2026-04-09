using Squad.Sdk.Events;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Infrastructure;

public static class CliServiceExtensions
{
    /// <summary>
    /// Find the .squad/ dir starting from the given directory (or cwd).
    /// Throws with a friendly message if not found.
    /// </summary>
    public static string RequireSquadDir(string? cwd = null)
    {
        var dir = PathResolver.ResolveSquadDir(cwd ?? Directory.GetCurrentDirectory());
        if (dir is null)
            throw new InvalidOperationException(
                "No .squad/ directory found. Run from inside a repository that has been initialized with Squad.");
        return dir;
    }
}
