using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Config;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands;

public sealed class ScheduleCommand : AsyncCommand<ScheduleCommand.Settings>
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[action]")]
        [Description("list | init | status | run <id>. Defaults to list.")]
        public string? Action { get; init; }

        [CommandArgument(1, "[id]")]
        [Description("Schedule ID (for run).")]
        public string? Id { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken ct)
    {
        string cwd = Directory.GetCurrentDirectory();
        switch (settings.Action?.ToLowerInvariant() ?? "list")
        {
            case "init":
                await InitAsync(cwd, ct);
                AnsiConsole.MarkupLine("[green]✓[/] Created .squad/schedule.json");
                break;
            case "list":
                await ListAsync(cwd, ct);
                break;
            case "status":
                await StatusAsync(cwd, ct);
                break;
            case "run":
                if (settings.Id == null) { AnsiConsole.MarkupLine("[red]✗[/] Usage: squad schedule run <id>"); return 1; }
                return await RunAsync(cwd, settings.Id, ct) ? 0 : 1;
            default:
                AnsiConsole.MarkupLine("[red]✗[/] Unknown action. Use: list | init | status | run <id>");
                return 1;
        }
        return 0;
    }

    public static async Task InitAsync(string cwd, CancellationToken ct = default)
    {
        string squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");
        string path = Path.Combine(squadDir, "schedule.json");
        if (File.Exists(path)) return; // idempotent

        Directory.CreateDirectory(squadDir);
        var template = new ScheduleManifest(new List<ScheduleEntry>
        {
            new()
            {
                Id = "daily-standup",
                Name = "Daily Standup",
                Enabled = false,
                Trigger = new ScheduleTrigger { Type = "cron", Cron = "0 9 * * 1-5" },
                Task = new ScheduleTask("print", "Daily standup reminder"),
                Providers = new List<string> { "local" },
            }
        });
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(template, _opts) + "\n", ct);
    }

    public static async Task<ScheduleManifest> LoadScheduleAsync(string cwd, CancellationToken ct = default)
    {
        string squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");
        string path = Path.Combine(squadDir, "schedule.json");
        if (!File.Exists(path))
            throw new InvalidOperationException("No schedule.json found — run 'squad schedule init' to create one.");
        string json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<ScheduleManifest>(json, _opts) ?? new ScheduleManifest();
    }

    private static async Task<ScheduleState> LoadStateAsync(string cwd, CancellationToken ct = default)
    {
        string squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");
        string path = Path.Combine(squadDir, ".schedule-state.json");
        if (!File.Exists(path)) return new ScheduleState();
        try
        {
            string json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<ScheduleState>(json, _opts) ?? new ScheduleState();
        }
        catch { return new ScheduleState(); }
    }

    private static async Task SaveStateAsync(string cwd, ScheduleState state, CancellationToken ct = default)
    {
        string squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");
        await File.WriteAllTextAsync(Path.Combine(squadDir, ".schedule-state.json"),
            JsonSerializer.Serialize(state, _opts) + "\n", ct);
    }

    public static string FormatTrigger(ScheduleEntry entry) => entry.Trigger.Type switch
    {
        "cron" => $"cron: {entry.Trigger.Cron}",
        "interval" => $"every {entry.Trigger.IntervalSeconds}s",
        "event" => $"on: {entry.Trigger.Event}",
        "startup" => "on startup",
        _ => "unknown"
    };

    private static async Task ListAsync(string cwd, CancellationToken ct)
    {
        ScheduleManifest manifest;
        try { manifest = await LoadScheduleAsync(cwd, ct); }
        catch (InvalidOperationException ex) { AnsiConsole.MarkupLine("[red]✗[/] {0}", Markup.Escape(ex.Message)); return; }

        if (manifest.Schedules.Count == 0) { AnsiConsole.MarkupLine("[dim]No schedules configured.[/]"); return; }

        AnsiConsole.MarkupLine("\n[bold]Configured Schedules[/] ({0}):\n", manifest.Schedules.Count);
        foreach (var e in manifest.Schedules)
        {
            string status = e.Enabled ? "[green]● enabled[/]" : "[dim]○ disabled[/]";
            AnsiConsole.MarkupLine("  [bold]{0}[/] — {1}", Markup.Escape(e.Id), Markup.Escape(e.Name));
            AnsiConsole.MarkupLine("    {0}  │  {1}  │  {2}:{3}", status, Markup.Escape(FormatTrigger(e)),
                Markup.Escape(e.Task?.Type ?? "?"), Markup.Escape(e.Task?.Ref ?? "?"));
        }
    }

    private static async Task StatusAsync(string cwd, CancellationToken ct)
    {
        ScheduleManifest manifest;
        try { manifest = await LoadScheduleAsync(cwd, ct); }
        catch (InvalidOperationException ex) { AnsiConsole.MarkupLine("[red]✗[/] {0}", Markup.Escape(ex.Message)); return; }

        var state = await LoadStateAsync(cwd, ct);
        AnsiConsole.MarkupLine("\n[bold]Schedule Status[/]\n");

        foreach (var e in manifest.Schedules)
        {
            state.Runs.TryGetValue(e.Id, out var run);
            string statusStr = run == null ? "[dim]– never run[/]"
                : run.Status == "success" ? "[green]✓ success[/]"
                : run.Status == "running" ? "[yellow]⟳ running[/]"
                : "[red]✗ failure[/]";
            string enabledStr = e.Enabled ? "" : " [dim](disabled)[/]";
            AnsiConsole.MarkupLine("  [bold]{0}[/]{1}", Markup.Escape(e.Id), enabledStr);
            AnsiConsole.MarkupLine("    {0}  │  last: {1}", statusStr, Markup.Escape(run?.LastRun ?? "–"));
            if (run?.Error != null) AnsiConsole.MarkupLine("    [red]error: {0}[/]", Markup.Escape(run.Error));
        }
    }

    private static async Task<bool> RunAsync(string cwd, string id, CancellationToken ct)
    {
        ScheduleManifest manifest;
        try { manifest = await LoadScheduleAsync(cwd, ct); }
        catch (InvalidOperationException ex) { AnsiConsole.MarkupLine("[red]✗[/] {0}", Markup.Escape(ex.Message)); return false; }

        var entry = manifest.Schedules.FirstOrDefault(s => s.Id == id);
        if (entry == null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Schedule '{0}' not found.", Markup.Escape(id));
            return false;
        }

        AnsiConsole.MarkupLine("Running schedule: [bold]{0}[/] ({1})...", Markup.Escape(entry.Name), Markup.Escape(entry.Id));
        var state = await LoadStateAsync(cwd, ct);

        AnsiConsole.MarkupLine("[dim]{0}[/]", Markup.Escape(entry.Task?.Ref ?? ""));

        state.Runs[id] = new ScheduleRun(DateTimeOffset.UtcNow.ToString("O"), "success");
        await SaveStateAsync(cwd, state, ct);
        AnsiConsole.MarkupLine("[green]✓[/] {0} completed.", Markup.Escape(entry.Name));
        return true;
    }
}
