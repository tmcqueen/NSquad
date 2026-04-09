using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Ralph;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands;

public sealed class WatchCommand : AsyncCommand<WatchCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--interval")]
        [Description("Polling interval in minutes. Default: 5.")]
        public int Interval { get; init; } = 5;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken ct)
    {
        try { ValidateInterval(settings.Interval); }
        catch (ArgumentException ex)
        {
            AnsiConsole.MarkupLine("[red]✗[/] {0}", ex.Message);
            return 1;
        }

        var cwd = Directory.GetCurrentDirectory();
        var squadDir = PathResolver.ResolveSquadDir(cwd);
        if (squadDir == null || !File.Exists(Path.Combine(squadDir, "team.md")))
        {
            AnsiConsole.MarkupLine("[red]✗[/] No squad found — run init first.");
            return 1;
        }

        var teamMdContent = await File.ReadAllTextAsync(Path.Combine(squadDir, "team.md"), ct);
        var roster = IssueTriager.ParseRoster(teamMdContent);
        if (roster.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]✗[/] No squad members found in team.md.");
            return 1;
        }

        // Simple routing rules: one rule per member (match by name)
        var rules = roster.Select(m => new IssueRoutingRule(m.Name, m.Name)).ToList();

        AnsiConsole.MarkupLine("\n[bold]🔄 Ralph — Watch Mode[/]");
        AnsiConsole.MarkupLine("[dim]Polling every {0} minute(s) for squad work. Ctrl+C to stop.[/]\n",
            settings.Interval);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.Interval));
        var round = 0;

        // Run immediately
        round++;
        await RunCheckAsync(roster, rules, round, ct);

        while (!ct.IsCancellationRequested)
        {
            try { await timer.WaitForNextTickAsync(ct); }
            catch (OperationCanceledException) { break; }
            round++;
            await RunCheckAsync(roster, rules, round, ct);
        }

        AnsiConsole.MarkupLine("\n[dim]🔄 Ralph — Watch stopped.[/]");
        return 0;
    }

    private static async Task RunCheckAsync(
        IReadOnlyList<RosterMember> roster,
        IReadOnlyList<IssueRoutingRule> rules,
        int round,
        CancellationToken ct)
    {
        int untriaged = 0, assigned = 0, triaged = 0;
        var timestamp = DateTime.Now.ToLongTimeString();

        try
        {
            var issues = await FetchIssuesAsync(ct);
            var memberLabels = roster.Select(m => m.Label).ToHashSet();

            foreach (var issue in issues)
            {
                var issueLabels = issue.Labels;
                if (memberLabels.Any(ml => issueLabels.Contains(ml))) { assigned++; continue; }
                untriaged++;

                var result = IssueTriager.Triage(issue.Title, null, issueLabels, rules, roster);
                if (result != null)
                {
                    var ok = await AddLabelAsync(issue.Number, result.Agent.Label, ct);
                    if (ok)
                    {
                        triaged++;
                        AnsiConsole.MarkupLine("[green]✓[/] [{0}] Triaged #{1} \"{2}\" → {3}",
                            timestamp, issue.Number, Markup.Escape(issue.Title), result.Agent.Name);
                    }
                }
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            AnsiConsole.MarkupLine("[red]✗[/] [{0}] Check failed: {1}", timestamp, Markup.Escape(ex.Message));
        }

        var lines = new[]
        {
            FormatBoardLine("Untriaged", untriaged),
            FormatBoardLine("Assigned", assigned),
            FormatBoardLine("Triaged this round", triaged),
        }.Where(l => l != null).ToList();

        if (lines.Count > 0)
        {
            AnsiConsole.MarkupLine("\n[bold]Round {0}[/]", round);
            foreach (var l in lines) AnsiConsole.MarkupLine(l!);
        }
        else
            AnsiConsole.MarkupLine("[dim][{0}] Board is clear — Ralph is idling.[/]", timestamp);
    }

    private sealed record GhIssue(int Number, string Title, IReadOnlyList<string> Labels);

    private static async Task<IReadOnlyList<GhIssue>> FetchIssuesAsync(CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("gh",
            "issue list --label squad --state open --limit 20 --json number,title,labels")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var proc = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("gh CLI not found.");
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0) return Array.Empty<GhIssue>();

        var raw = JsonSerializer.Deserialize<List<JsonElement>>(stdout) ?? new();
        return raw.Select(e => new GhIssue(
            e.GetProperty("number").GetInt32(),
            e.GetProperty("title").GetString() ?? "",
            e.GetProperty("labels").EnumerateArray()
                .Select(l => l.GetProperty("name").GetString() ?? "").ToList()
        )).ToList();
    }

    private static async Task<bool> AddLabelAsync(int number, string label, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("gh",
            $"issue edit {number} --add-label \"{label}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc == null) return false;
        // Read stdout and stderr concurrently to prevent pipe buffer deadlock
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);
        await proc.WaitForExitAsync(ct);
        return proc.ExitCode == 0;
    }

    public static void ValidateInterval(int minutes)
    {
        if (minutes < 1)
            throw new ArgumentException("--interval must be a positive number of minutes.");
    }

    public static string? FormatBoardLine(string label, int count) =>
        count > 0 ? $"  {label}: {count}" : null;
}
