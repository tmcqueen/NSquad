using System.Runtime.InteropServices;
using System.Text.Json;

namespace Squad.Sdk.Resolution;

public enum SquadMode { Local, Remote, Hub }

public static class PathResolver
{
    private static readonly string[] SquadDirNames = [".squad", ".ai-team"];

    /// <summary>
    /// Walk up from startDir to find a .squad/ (or legacy .ai-team/) directory.
    /// Stops at the .git root boundary.
    /// Returns the absolute path to the squad dir, or null if not found.
    /// </summary>
    public static string? ResolveSquadDir(string? startDir = null)
    {
        var current = Path.GetFullPath(startDir ?? Directory.GetCurrentDirectory());

        while (true)
        {
            foreach (var name in SquadDirNames)
            {
                var candidate = Path.Combine(current, name);
                if (Directory.Exists(candidate))
                    return candidate;
            }

            var gitMarker = Path.Combine(current, ".git");
            if (Path.Exists(gitMarker))
                return null; // reached repo boundary — not found

            var parent = Path.GetDirectoryName(current);
            if (parent is null || parent == current)
                return null; // filesystem root

            current = parent;
        }
    }

    /// <summary>
    /// Detect the squad mode for a resolved squad directory.
    /// Local = no config.json or no teamRoot. Remote = config.json with teamRoot.
    /// </summary>
    public static SquadMode DetectMode(string squadDir)
    {
        var parentDir = Path.GetDirectoryName(squadDir);
        if (parentDir is not null && File.Exists(Path.Combine(parentDir, "squad-hub.json")))
            return SquadMode.Hub;

        var configPath = Path.Combine(squadDir, "config.json");
        if (!File.Exists(configPath))
            return SquadMode.Local;

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("teamRoot", out var teamRoot)
                && teamRoot.GetString() is { Length: > 0 })
                return SquadMode.Remote;
        }
        catch (JsonException) { /* malformed — treat as local */ }

        return SquadMode.Local;
    }

    /// <summary>
    /// Calculate the platform-specific global squad config path without creating it.
    /// </summary>
    public static string GetGlobalSquadPath()
    {
        string base_;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            base_ = Environment.GetEnvironmentVariable("APPDATA")
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming");
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            base_ = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");
        else
            base_ = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        return Path.Combine(base_, "squad");
    }

    /// <summary>
    /// Platform-specific global squad config directory.
    /// Windows: %APPDATA%/squad/   macOS: ~/Library/Application Support/squad/
    /// Linux: $XDG_CONFIG_HOME/squad/ (default ~/.config/squad/)
    /// </summary>
    /// <summary>
    /// Return the global squad config directory, creating it if it does not exist.
    /// </summary>
    public static string ResolveGlobalSquadPath()
    {
        var globalDir = GetGlobalSquadPath();
        Directory.CreateDirectory(globalDir);
        return globalDir;
    }

    /// <summary>
    /// Returns the personal squad directory, or null if SQUAD_NO_PERSONAL is set
    /// or the directory does not exist.
    /// </summary>
    public static string? ResolvePersonalSquadDir()
    {
        if (Environment.GetEnvironmentVariable("SQUAD_NO_PERSONAL") is not null)
            return null;

        var personalDir = Path.Combine(GetGlobalSquadPath(), "personal-squad");
        return Directory.Exists(personalDir) ? personalDir : null;
    }
}
