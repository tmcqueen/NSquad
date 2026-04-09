using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Cli.Infrastructure;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands;

public sealed class CopilotCommand : AsyncCommand<CopilotCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--off")]
        [Description("Remove @copilot from the team.")]
        public bool Off { get; init; }

        [CommandOption("--auto-assign")]
        [Description("Auto-assign @copilot on squad-labeled issues.")]
        public bool AutoAssign { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        var squadDir = PathResolver.ResolveSquadDir(cwd);
        if (squadDir == null || !Directory.Exists(squadDir))
        {
            AnsiConsole.MarkupLine("[red]✗[/] No squad found — run init first.");
            return 1;
        }

        var content = TeamMdHelper.ReadTeamMd(squadDir);
        var hasCopilot = TeamMdHelper.HasCopilot(content);

        if (settings.Off)
        {
            if (!hasCopilot)
            {
                AnsiConsole.MarkupLine("[dim]@copilot is not on the team — nothing to remove.[/]");
                return 0;
            }
            content = TeamMdHelper.RemoveCopilotSection(content);
            TeamMdHelper.WriteTeamMd(squadDir, content);
            AnsiConsole.MarkupLine("[green]✓[/] Removed @copilot from team roster.");

            var instructionsPath = Path.Combine(cwd, ".github", "copilot-instructions.md");
            if (File.Exists(instructionsPath))
            {
                File.Delete(instructionsPath);
                AnsiConsole.MarkupLine("[green]✓[/] Removed .github/copilot-instructions.md.");
            }
            return 0;
        }

        if (hasCopilot)
        {
            AnsiConsole.MarkupLine("[dim]@copilot is already on the team.[/]");
            return 0;
        }

        content = TeamMdHelper.InsertCopilotSection(content, settings.AutoAssign);
        TeamMdHelper.WriteTeamMd(squadDir, content);
        AnsiConsole.MarkupLine("[green]✓[/] Added @copilot (Coding Agent) to team roster.");

        // Write copilot-instructions.md
        var destPath = Path.Combine(cwd, ".github", "copilot-instructions.md");
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.WriteAllText(destPath, GenerateCopilotInstructions());
        AnsiConsole.MarkupLine("[green]✓[/] Created .github/copilot-instructions.md.");
        return 0;
    }

    private static string GenerateCopilotInstructions() =>
        """
        # GitHub Copilot Instructions

        This project uses Squad multi-agent coordination.
        Copilot (the coding agent) handles issues labeled `squad:copilot`.

        ## Workflow

        1. Pick up squad-labeled issues
        2. Create a feature branch
        3. Implement and test
        4. Open a PR for human review
        """;
}
