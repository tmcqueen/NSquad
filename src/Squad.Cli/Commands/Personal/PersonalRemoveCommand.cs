using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands.Personal;

public sealed class PersonalRemoveCommand : AsyncCommand<PersonalRemoveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        public string Name { get; init; } = "";
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        string? personalDir = PathResolver.ResolvePersonalSquadDir();
        if (personalDir == null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Personal squad not initialized.");
            return 1;
        }

        try
        {
            PersonalHelper.RemoveAgent(personalDir, settings.Name);
            AnsiConsole.MarkupLine("[green]✓[/] Removed personal agent: [bold]{0}[/]", Markup.Escape(settings.Name));
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine("[red]✗[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
        return 0;
    }
}
