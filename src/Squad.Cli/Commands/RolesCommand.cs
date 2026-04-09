using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Roles;

namespace Squad.Cli.Commands;

public sealed class RolesCommand : AsyncCommand<RolesCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--category")]
        [Description("Filter by category (engineering, quality, product, etc.)")]
        public string? Category { get; init; }

        [CommandOption("--search")]
        [Description("Search roles by keyword.")]
        public string? Search { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var roles = BuiltinRoles.Filter(settings.Category, settings.Search);

        if (roles.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No roles found.[/]");
            return 0;
        }

        if (settings.Search != null || settings.Category != null)
        {
            // Compact listing for filtered results
            foreach (var r in roles)
                AnsiConsole.MarkupLine("  {0} [bold]{1}[/]  [dim]\"{2}\"[/]", r.Emoji, r.Id.PadRight(22), r.Vibe);
            return 0;
        }

        // Full grouped listing
        AnsiConsole.MarkupLine("\n[bold]📦 Built-in Roles ({0} base roles)[/]", BuiltinRoles.All.Count);
        AnsiConsole.MarkupLine("[dim]   Adapted from agency-agents by AgentLand Contributors (MIT)[/]\n");

        var softwareCats = new HashSet<string> { "engineering", "quality" };
        var swRoles = roles.Where(r => softwareCats.Contains(r.Category)).ToList();
        var bizRoles = roles.Where(r => !softwareCats.Contains(r.Category)).ToList();

        if (swRoles.Count > 0)
        {
            AnsiConsole.MarkupLine("  [bold]Software Development:[/]");
            foreach (var r in swRoles)
                AnsiConsole.MarkupLine("    {0} [bold]{1}[/]  {2}  [dim]\"{3}\"[/]",
                    r.Emoji, r.Id.PadRight(22), r.Title.PadRight(24), r.Vibe);
            AnsiConsole.WriteLine();
        }

        if (bizRoles.Count > 0)
        {
            AnsiConsole.MarkupLine("  [bold]Business & Operations:[/]");
            foreach (var r in bizRoles)
                AnsiConsole.MarkupLine("    {0} [bold]{1}[/]  {2}  [dim]\"{3}\"[/]",
                    r.Emoji, r.Id.PadRight(22), r.Title.PadRight(24), r.Vibe);
        }

        return 0;
    }
}
