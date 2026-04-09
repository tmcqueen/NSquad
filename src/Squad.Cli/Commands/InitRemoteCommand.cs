using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Squad.Cli.Commands;

public sealed class InitRemoteCommand : AsyncCommand<InitRemoteCommand.Settings>
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
        string cwd = Directory.GetCurrentDirectory();
        try
        {
            LinkCommand.WriteRemoteConfig(cwd, settings.TeamRepoPath);
            AnsiConsole.MarkupLine("[green]✓[/] Remote config written.");
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine("[red]✗[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
        return 0;
    }
}
