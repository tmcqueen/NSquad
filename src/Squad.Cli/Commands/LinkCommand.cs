using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Config;

namespace Squad.Cli.Commands;

public sealed class LinkCommand : AsyncCommand<LinkCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<team-repo-path>")]
        [Description("Path (relative or absolute) to the team repository.")]
        public string TeamRepoPath { get; init; } = "";
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        try
        {
            WriteRemoteConfig(cwd, settings.TeamRepoPath);
            AnsiConsole.MarkupLine("[green]✓[/] Linked to team root: [dim]{0}[/]",
                Markup.Escape(Path.GetRelativePath(cwd, Path.GetFullPath(settings.TeamRepoPath))));
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine("[red]✗[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
        return 0;
    }

    /// <summary>Write .squad/config.json with teamRoot. Exposed for testing.</summary>
    public static void WriteRemoteConfig(string projectDir, string teamRepoPath)
    {
        var absoluteTeam = Path.GetFullPath(Path.Combine(projectDir, teamRepoPath));

        if (!Directory.Exists(absoluteTeam))
            throw new InvalidOperationException($"Target path does not exist: {absoluteTeam}");

        var hasSquad = Directory.Exists(Path.Combine(absoluteTeam, ".squad"));
        var hasAiTeam = Directory.Exists(Path.Combine(absoluteTeam, ".ai-team"));
        if (!hasSquad && !hasAiTeam)
            throw new InvalidOperationException($"Target does not contain a .squad/ directory: {absoluteTeam}");

        var squadDir = Path.Combine(projectDir, ".squad");
        var relativePath = Path.GetRelativePath(projectDir, absoluteTeam);

        var cfg = new LocalSquadConfig { TeamRoot = relativePath };
        cfg.Save(squadDir);

        EnsureGitignoreEntry(projectDir, ".squad/config.json");
    }

    private static void EnsureGitignoreEntry(string repoDir, string entry)
    {
        var gitignorePath = Path.Combine(repoDir, ".gitignore");
        var existing = File.Exists(gitignorePath) ? File.ReadAllText(gitignorePath) : "";
        if (existing.Split('\n').Any(l => l.Trim() == entry)) return;

        var nl = existing.Length > 0 && !existing.EndsWith('\n') ? "\n" : "";
        File.AppendAllText(gitignorePath,
            nl + "# Squad: local config (machine-specific paths, never commit)\n" + entry + "\n");
    }
}
