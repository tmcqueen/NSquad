using Spectre.Console;
using Spectre.Console.Cli;
using Squad.Sdk.CostTracker;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands;

public sealed class CostCommand : AsyncCommand<CostCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--dir <DIR>")]
        public string? Dir { get; init; }

        [CommandOption("--all")]
        public bool ShowAll { get; init; }

        [CommandOption("--agent <AGENT>")]
        public string? AgentFilter { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = settings.Dir ?? Directory.GetCurrentDirectory();
        var squadDir = PathResolver.ResolveSquadDir(cwd);

        if (squadDir is null)
        {
            AnsiConsole.MarkupLine("[red]No .squad/ directory found.[/]");
            return 1;
        }

        var entries = await CostReader.LoadEntriesAsync(squadDir, cancellationToken);

        if (!string.IsNullOrEmpty(settings.AgentFilter))
            entries = entries
                .Where(e => e.Agent.Contains(settings.AgentFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (entries.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No cost data found in .squad/costs/[/]");
            return 0;
        }

        var summary = CostReader.Summarize(entries);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Token Usage & Cost[/]");
        AnsiConsole.MarkupLine(new string('═', 55));
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Agent");
        table.AddColumn(new TableColumn("[dim]Input[/]").RightAligned());
        table.AddColumn(new TableColumn("[dim]Output[/]").RightAligned());
        table.AddColumn(new TableColumn("[dim]Total Tokens[/]").RightAligned());
        table.AddColumn(new TableColumn("Est. Cost").RightAligned());

        foreach (var (_, s) in summary.OrderByDescending(kv => kv.Value.TotalCost))
        {
            table.AddRow(
                $"[bold]{Markup.Escape(s.Agent)}[/]",
                $"[dim]{FormatTokens(s.TotalInputTokens)}[/]",
                $"[dim]{FormatTokens(s.TotalOutputTokens)}[/]",
                FormatTokens(s.TotalInputTokens + s.TotalOutputTokens),
                $"[green]{FormatCost(s.TotalCost)}[/]");
        }

        var totalInput = summary.Values.Sum(s => s.TotalInputTokens);
        var totalOutput = summary.Values.Sum(s => s.TotalOutputTokens);
        var totalCost = summary.Values.Sum(s => s.TotalCost);

        table.AddEmptyRow();
        table.AddRow(
            "[bold]TOTAL[/]",
            $"[bold]{FormatTokens(totalInput)}[/]",
            $"[bold]{FormatTokens(totalOutput)}[/]",
            $"[bold]{FormatTokens(totalInput + totalOutput)}[/]",
            $"[bold green]{FormatCost(totalCost)}[/]");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        return 0;
    }

    /// <summary>Format a cost value. Exposed for testing.</summary>
    public static string FormatCost(decimal cost)
        => (cost >= 1m || cost == 0m) ? $"${cost:F2}" : $"${cost:G4}";

    /// <summary>Format a token count, abbreviating thousands. Exposed for testing.</summary>
    public static string FormatTokens(int tokens)
        => tokens >= 1000 ? $"{tokens / 1000.0:F1}k" : tokens.ToString();
}
