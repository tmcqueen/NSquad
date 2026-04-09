using Spectre.Console;
using Spectre.Console.Cli;
using Squad.Sdk.Config;

namespace Squad.Cli.Commands;

public sealed class CastCommand : AsyncCommand<CastCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--dir <DIR>")]
        public string? Dir { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = settings.Dir ?? Directory.GetCurrentDirectory();
        var agents = await LoadCastAsync(cwd);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Session Cast[/]");
        AnsiConsole.MarkupLine(new string('═', 40));
        AnsiConsole.WriteLine();

        if (agents.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No agents found. Add a squad.config.json to this directory.[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("Agent");
        table.AddColumn("Role");
        table.AddColumn("Model");
        table.AddColumn("Skills");

        foreach (var agent in agents)
        {
            table.AddRow(
                $"[bold]{Markup.Escape(agent.Name)}[/]",
                agent.Role is { Length: > 0 } ? Markup.Escape(agent.Role) : "[dim]—[/]",
                agent.Model is { Length: > 0 } ? Markup.Escape(agent.Model) : "[dim]default[/]",
                agent.Skills.Count > 0 ? Markup.Escape(string.Join(", ", agent.Skills)) : "[dim]none[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]{agents.Count} agent{(agents.Count == 1 ? "" : "s")} in session cast[/]");

        return 0;
    }

    /// <summary>Load agents from squad.config.json in the given directory. Exposed for testing.</summary>
    public static async Task<IReadOnlyList<AgentConfig>> LoadCastAsync(string cwd)
    {
        var config = await ConfigLoader.LoadAsync(cwd);
        return config?.Agents ?? [];
    }
}
