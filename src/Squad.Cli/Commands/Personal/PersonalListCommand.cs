using Spectre.Console;
using Spectre.Console.Cli;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands.Personal;

public sealed class PersonalListCommand : AsyncCommand
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        string? personalDir = PathResolver.ResolvePersonalSquadDir();
        if (personalDir == null)
        {
            AnsiConsole.MarkupLine("[dim]No personal squad found. Run [bold]squad personal init[/].[/]");
            return 0;
        }

        var agents = PersonalHelper.ListAgents(personalDir);
        if (agents.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No personal agents. Use [bold]squad personal add <name> --role <role>[/].[/]");
            return 0;
        }

        var table = new Spectre.Console.Table()
            .AddColumn("Name")
            .AddColumn("Charter Path");

        foreach (string name in agents)
            table.AddRow(name, Path.Combine(personalDir, "agents", name, "charter.md"));

        AnsiConsole.Write(table);
        return 0;
    }
}
