using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Config;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands;

public sealed class EconomyCommand : AsyncCommand<EconomyCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[mode]")]
        [Description("on | off — toggle economy mode. Omit to show status.")]
        public string? Mode { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        var squadDir = PathResolver.ResolveSquadDir(cwd);
        if (squadDir == null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] No squad found. Run [bold]squad init[/] first.");
            return 1;
        }

        var rootDir = Path.GetDirectoryName(squadDir)!;

        switch (settings.Mode?.ToLowerInvariant())
        {
            case "on":
                SetEconomyMode(rootDir, true);
                AnsiConsole.MarkupLine("[green]✓[/] Economy mode [bold]enabled[/].");
                break;
            case "off":
                SetEconomyMode(rootDir, false);
                AnsiConsole.MarkupLine("[green]✓[/] Economy mode [bold]disabled[/].");
                break;
            case null:
                var enabled = GetEconomyMode(rootDir);
                AnsiConsole.MarkupLine("\n[bold]Economy Mode[/]\n");
                AnsiConsole.MarkupLine("  Status: " + (enabled ? "[green]enabled[/]" : "[dim]disabled[/]"));
                if (!enabled) AnsiConsole.MarkupLine("  Usage: [bold]squad economy on | off[/]\n");
                break;
            default:
                AnsiConsole.MarkupLine("[red]✗[/] Unknown mode '{0}'. Use [bold]on[/] or [bold]off[/].", settings.Mode);
                return 1;
        }
        return 0;
    }

    public static void SetEconomyMode(string rootDir, bool enabled)
    {
        var squadDir = Path.Combine(rootDir, ".squad");
        var cfg = LocalSquadConfig.Load(squadDir);
        cfg = cfg with { EconomyMode = enabled };
        cfg.Save(squadDir);
    }

    public static bool GetEconomyMode(string rootDir)
    {
        var squadDir = Path.Combine(rootDir, ".squad");
        return LocalSquadConfig.Load(squadDir).EconomyMode;
    }
}
