using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Config;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands;

public sealed class StreamsCommand : AsyncCommand<StreamsCommand.Settings>
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[action]")]
        [Description("list | status | activate <name>. Defaults to list.")]
        public string? Action { get; init; }

        [CommandArgument(1, "[name]")]
        [Description("SubSquad name (for activate).")]
        public string? Name { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken ct)
    {
        string cwd = Directory.GetCurrentDirectory();
        switch (settings.Action?.ToLowerInvariant() ?? "list")
        {
            case "list":
                await ListAsync(cwd, ct);
                break;
            case "status":
                await StatusAsync(cwd, ct);
                break;
            case "activate":
                if (settings.Name == null)
                {
                    AnsiConsole.MarkupLine("[red]✗[/] Usage: squad streams activate <name>");
                    return 1;
                }
                ActivateStream(cwd, settings.Name);
                AnsiConsole.MarkupLine("[green]✓[/] Activated SubSquad: [bold]{0}[/]", Markup.Escape(settings.Name));
                AnsiConsole.MarkupLine("[dim]  Written to .squad-workstream (gitignored — local to your machine)[/]");
                break;
            default:
                AnsiConsole.MarkupLine("[red]✗[/] Unknown action. Use: list | status | activate <name>");
                return 1;
        }
        return 0;
    }

    public static async Task<SubSquadsConfig> LoadConfigAsync(string cwd, CancellationToken ct = default)
    {
        string squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");
        string path = Path.Combine(squadDir, "streams.json");
        if (!File.Exists(path)) return new SubSquadsConfig();
        try
        {
            string json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<SubSquadsConfig>(json, _opts) ?? new SubSquadsConfig();
        }
        catch { return new SubSquadsConfig(); }
    }

    public static string? GetActiveStream(string cwd)
    {
        string path = Path.Combine(cwd, ".squad-workstream");
        if (!File.Exists(path)) return null;
        return File.ReadAllText(path).Trim();
    }

    public static void ActivateStream(string cwd, string name)
    {
        File.WriteAllText(Path.Combine(cwd, ".squad-workstream"), name + "\n");
    }

    private static async Task ListAsync(string cwd, CancellationToken ct)
    {
        var cfg = await LoadConfigAsync(cwd, ct);
        string? active = GetActiveStream(cwd);

        if (cfg.Workstreams.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No SubSquads configured. Create .squad/streams.json to define SubSquads.[/]");
            return;
        }

        AnsiConsole.MarkupLine("\n[bold]Configured SubSquads[/]\n");
        AnsiConsole.MarkupLine("  Default workflow: {0}\n", Markup.Escape(cfg.DefaultWorkflow));

        foreach (var ws in cfg.Workstreams)
        {
            bool isActive = active == ws.Name;
            string marker = isActive ? "[green]● active[/]" : "[dim]○[/]";
            AnsiConsole.MarkupLine("  {0}  [bold]{1}[/]", marker, Markup.Escape(ws.Name));
            AnsiConsole.MarkupLine("       Label: {0}", Markup.Escape(ws.LabelFilter));
            AnsiConsole.MarkupLine("       Workflow: {0}", Markup.Escape(ws.Workflow ?? cfg.DefaultWorkflow));
            if (!string.IsNullOrEmpty(ws.Description))
                AnsiConsole.MarkupLine("       [dim]{0}[/]", Markup.Escape(ws.Description));
            AnsiConsole.WriteLine();
        }
    }

    private static async Task StatusAsync(string cwd, CancellationToken ct)
    {
        var cfg = await LoadConfigAsync(cwd, ct);
        if (cfg.Workstreams.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No SubSquads configured.[/]");
            return;
        }

        AnsiConsole.MarkupLine("\n[bold]SubSquad Status[/]\n");
        string? active = GetActiveStream(cwd);

        foreach (var ws in cfg.Workstreams)
        {
            bool isActive = active == ws.Name;
            string marker = isActive ? "[green]●[/]" : "[dim]○[/]";
            AnsiConsole.MarkupLine("  {0} [bold]{1}[/] ({2})", marker, Markup.Escape(ws.Name), Markup.Escape(ws.LabelFilter));

            // Best-effort gh pr list
            var psi = new System.Diagnostics.ProcessStartInfo("gh",
                $"pr list --label {ws.LabelFilter} --json number,title,state --limit 5")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            try
            {
                using var proc = System.Diagnostics.Process.Start(psi);
                string stdout = await proc!.StandardOutput.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);
                if (proc.ExitCode == 0)
                {
                    var prs = JsonSerializer.Deserialize<List<JsonElement>>(stdout) ?? new();
                    if (prs.Count > 0)
                        foreach (var pr in prs)
                            AnsiConsole.MarkupLine("    PR #{0}: {1}",
                                pr.GetProperty("number").GetInt32(),
                                Markup.Escape(pr.GetProperty("title").GetString() ?? ""));
                    else
                        AnsiConsole.MarkupLine("    [dim]No open PRs[/]");
                }
            }
            catch { AnsiConsole.MarkupLine("    [dim](gh CLI not available)[/]"); }
            AnsiConsole.WriteLine();
        }
    }
}
