using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Json;
using System.ComponentModel;

namespace Squad.Cli.Commands;

public sealed class MigrateCommand : AsyncCommand<MigrateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--to")]
        [Description("Target format: sdk | markdown")]
        public string? To { get; init; }

        [CommandOption("--from")]
        [Description("Source format: ai-team")]
        public string? From { get; init; }

        [CommandOption("--dry-run")]
        [Description("Preview changes without writing.")]
        public bool DryRun { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken ct)
    {
        string cwd = Directory.GetCurrentDirectory();
        string mode = DetectMode(cwd);

        // --from ai-team
        if (settings.From?.ToLowerInvariant() == "ai-team")
        {
            if (mode != "legacy")
            {
                AnsiConsole.MarkupLine("[red]✗[/] --from ai-team requires a .ai-team/ directory.");
                return 1;
            }
            if (!settings.DryRun)
            {
                MigrateFromAiTeam(cwd);
                AnsiConsole.MarkupLine("[green]✓[/] Renamed .ai-team/ → .squad/");
            }
            else
                AnsiConsole.MarkupLine("[dim][[DRY RUN]][/] Would rename .ai-team/ → .squad/");
            return 0;
        }

        // --to markdown (generate .squad/ from squad.config.json)
        if (settings.To?.ToLowerInvariant() == "markdown")
        {
            if (mode != "sdk")
            {
                AnsiConsole.MarkupLine("[red]✗[/] --to markdown requires a squad.config.json file.");
                return 1;
            }
            if (!settings.DryRun)
            {
                try
                {
                    var result = await BuildCommand.BuildAsync(cwd, ct);
                    AnsiConsole.MarkupLine("[green]✓[/] Generated {0} markdown file(s) from squad.config.json.", result.Written);
                    AnsiConsole.MarkupLine("[dim].squad/ is now up to date.[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine("[red]✗[/] Build failed: {0}", Markup.Escape(ex.Message));
                    return 1;
                }
            }
            else
                AnsiConsole.MarkupLine("[dim][[DRY RUN]][/] Would run squad build to generate .squad/ markdown.");
            return 0;
        }

        // --to sdk (parse .squad/ markdown → generate squad.config.json)
        if (settings.To?.ToLowerInvariant() == "sdk")
        {
            if (mode == "none") { AnsiConsole.MarkupLine("[red]✗[/] No squad found."); return 1; }
            if (mode == "legacy") { AnsiConsole.MarkupLine("[red]✗[/] Run [bold]squad migrate --from ai-team[/] first."); return 1; }
            if (mode == "sdk") { AnsiConsole.MarkupLine("[red]✗[/] Already in SDK mode (squad.config.json exists)."); return 1; }

            string json;
            try { json = GenerateConfigJson(cwd); }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("[red]✗[/] Migration failed: {0}", Markup.Escape(ex.Message));
                return 1;
            }
            if (settings.DryRun)
            {
                AnsiConsole.MarkupLine("[dim][[DRY RUN]][/] Generated squad.config.json:\n");
                AnsiConsole.Write(new JsonText(json));
                return 0;
            }

            string configPath = Path.Combine(cwd, "squad.config.json");
            await File.WriteAllTextAsync(configPath, json, ct);
            AnsiConsole.MarkupLine("[green]✓[/] Created squad.config.json");
            AnsiConsole.MarkupLine("\nNext steps:");
            AnsiConsole.MarkupLine("  1. Review [dim]squad.config.json[/]");
            AnsiConsole.MarkupLine("  2. Run [bold]squad build[/] to verify");
            return 0;
        }

        // No mode — show current state
        AnsiConsole.MarkupLine("\n[bold]Squad Migrate[/] — current mode: [bold]{0}[/]\n", Markup.Escape(mode));
        switch (mode)
        {
            case "none":
                AnsiConsole.MarkupLine("No squad found. Run [bold]squad init[/] to create one.");
                break;
            case "legacy":
                AnsiConsole.MarkupLine("Run: [bold]squad migrate --from ai-team[/]");
                break;
            case "markdown":
                AnsiConsole.MarkupLine(".squad/ is the source of truth.");
                AnsiConsole.MarkupLine("To convert to SDK mode: [bold]squad migrate --to sdk[/]");
                break;
            case "sdk":
                AnsiConsole.MarkupLine("squad.config.json is the source of truth.");
                AnsiConsole.MarkupLine("To update .squad/ markdown: [bold]squad migrate --to markdown[/]");
                break;
        }
        return 0;
    }

    // -----------------------------------------------------------------------
    // Public helpers (for testing)
    // -----------------------------------------------------------------------

    public static string DetectMode(string cwd)
    {
        if (File.Exists(Path.Combine(cwd, "squad.config.json"))) return "sdk";
        if (Directory.Exists(Path.Combine(cwd, ".squad"))) return "markdown";
        if (Directory.Exists(Path.Combine(cwd, ".ai-team"))) return "legacy";
        return "none";
    }

    public static void MigrateFromAiTeam(string cwd)
    {
        string src = Path.Combine(cwd, ".ai-team");
        string dst = Path.Combine(cwd, ".squad");
        if (Directory.Exists(dst))
            throw new InvalidOperationException(
                ".squad/ already exists. Remove it manually before migrating from .ai-team/.");
        Directory.Move(src, dst);
    }

    public static string GenerateConfigJson(string cwd)
    {
        string squadDir = Path.Combine(cwd, ".squad");
        string teamName = ParseTeamName(squadDir);
        var members = ParseMembers(squadDir);
        string defaultAgent = members.FirstOrDefault() ?? "builder";
        var routingRules = ParseRoutingRules(squadDir);

        var config = new
        {
            version = "1.0",
            team = new { name = teamName },
            agents = members.Select(m => new { name = m, role = ParseAgentRole(squadDir, m) }).ToList(),
            routing = new
            {
                defaultAgent,
                rules = routingRules.Select(r => new { pattern = r.pattern, agent = r.agent }).ToList()
            }
        };

        return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    private static string ParseTeamName(string squadDir)
    {
        string path = Path.Combine(squadDir, "team.md");
        if (!File.Exists(path)) return "untitled-squad";
        foreach (string line in File.ReadAllLines(path))
        {
            var m = Regex.Match(line, @"^# Squad Team — (.+)");
            if (m.Success) return m.Groups[1].Value.Trim();
        }
        return "untitled-squad";
    }

    private static List<string> ParseMembers(string squadDir)
    {
        string path = Path.Combine(squadDir, "team.md");
        if (!File.Exists(path)) return new();
        var members = new List<string>();
        bool inMembers = false;
        foreach (string line in File.ReadAllLines(path))
        {
            if (line.TrimStart().StartsWith("## Members")) { inMembers = true; continue; }
            if (inMembers && line.TrimStart().StartsWith("##")) break;
            if (inMembers && line.StartsWith('|') && !line.Contains("---") && !line.Contains("Name"))
            {
                string[] cells = line.Split('|').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
                if (cells.Length >= 4 && cells[3].Contains("Active"))
                    members.Add(cells[0].ToLowerInvariant());
            }
        }
        return members;
    }

    private static string ParseAgentRole(string squadDir, string agentName)
    {
        string charterPath = Path.Combine(squadDir, "agents", agentName, "charter.md");
        if (!File.Exists(charterPath)) return agentName;
        string first = File.ReadAllLines(charterPath).FirstOrDefault(l => l.StartsWith("# ")) ?? "";
        var m = Regex.Match(first, @"^# \w+ — (.+)");
        return m.Success ? m.Groups[1].Value.Trim() : agentName;
    }

    private static List<(string pattern, string agent)> ParseRoutingRules(string squadDir)
    {
        string path = Path.Combine(squadDir, "routing.md");
        if (!File.Exists(path)) return new();
        var rules = new List<(string, string)>();
        foreach (string line in File.ReadAllLines(path))
        {
            var m = Regex.Match(line, @"^-\s+`(.+?)`\s*→\s*(\S+)");
            if (m.Success) rules.Add((m.Groups[1].Value, m.Groups[2].Value));
        }
        return rules;
    }
}
