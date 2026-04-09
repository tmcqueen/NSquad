using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands.Personal;

public sealed class PersonalAddCommand : AsyncCommand<PersonalAddCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        public string Name { get; init; } = "";

        [CommandOption("--role")]
        [Description("Agent role (e.g., lead, backend, tester).")]
        public string Role { get; init; } = "agent";
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        string globalDir = PathResolver.ResolveGlobalSquadPath();
        string personalDir = Path.Combine(globalDir, "personal-squad");

        if (!Directory.Exists(personalDir))
        {
            AnsiConsole.MarkupLine("[red]✗[/] Personal squad not initialized. Run [bold]squad personal init[/] first.");
            return 1;
        }

        try
        {
            PersonalHelper.AddAgent(personalDir, settings.Name, settings.Role);
            AnsiConsole.MarkupLine("[green]✓[/] Added personal agent: [bold]{0}[/] (role: {1})", Markup.Escape(settings.Name), Markup.Escape(settings.Role));
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine("[yellow]⚠[/] {0}", Markup.Escape(ex.Message));
        }
        return 0;
    }
}
