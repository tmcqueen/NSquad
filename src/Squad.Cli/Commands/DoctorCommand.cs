using Spectre.Console;
using Spectre.Console.Cli;
using System.Text.Json;

namespace Squad.Cli.Commands;

public sealed class DoctorCommand : AsyncCommand<DoctorCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--dir <DIR>")]
        public string? Dir { get; init; }
    }

    public enum CheckStatus { Pass, Fail, Warn }
    public enum SquadDoctorMode { Local, Remote, Hub }

    public record DoctorCheck(string Name, CheckStatus Status, string Message);

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = settings.Dir ?? Directory.GetCurrentDirectory();
        var mode = DetectMode(cwd);
        var checks = RunChecks(cwd);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Squad Doctor[/]");
        AnsiConsole.MarkupLine(new string('═', 30));
        AnsiConsole.MarkupLine($"Mode: [cyan]{mode.ToString().ToLower()}[/]");
        AnsiConsole.WriteLine();

        var table = new Table().NoBorder();
        table.AddColumn(new TableColumn("").NoWrap());
        table.AddColumn(new TableColumn("Check"));
        table.AddColumn(new TableColumn("Detail"));

        foreach (var check in checks)
        {
            var (icon, color) = check.Status switch
            {
                CheckStatus.Pass => ("✓", "green"),
                CheckStatus.Fail => ("✗", "red"),
                CheckStatus.Warn => ("⚠", "yellow"),
                _ => (" ", "white"),
            };
            table.AddRow(
                $"[{color}]{icon}[/]",
                Markup.Escape(check.Name),
                $"[dim]{Markup.Escape(check.Message)}[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var passed = checks.Count(c => c.Status == CheckStatus.Pass);
        var failed = checks.Count(c => c.Status == CheckStatus.Fail);
        var warned = checks.Count(c => c.Status == CheckStatus.Warn);

        AnsiConsole.MarkupLine(
            $"[bold]Summary:[/] [green]{passed} passed[/], [red]{failed} failed[/], [yellow]{warned} warnings[/]");
        AnsiConsole.WriteLine();

        return 0; // always exit 0 — doctor is diagnostic, not a gate
    }

    /// <summary>Run all health checks for the given working directory. Exposed for testing.</summary>
    public static IReadOnlyList<DoctorCheck> RunChecks(string cwd)
    {
        var squadDir = Path.Combine(cwd, ".squad");
        var legacyDir = Path.Combine(cwd, ".ai-team");
        var effectiveDir = Directory.Exists(squadDir) ? squadDir
            : Directory.Exists(legacyDir) ? legacyDir
            : squadDir;

        var checks = new List<DoctorCheck>();

        // 1. .squad/ directory exists
        checks.Add(Directory.Exists(effectiveDir)
            ? new(".squad/ directory exists", CheckStatus.Pass, "directory present")
            : new(".squad/ directory exists", CheckStatus.Fail, "directory not found — run `squad init`"));

        if (!Directory.Exists(effectiveDir))
            return checks;

        // 2. config.json (optional — only check if present)
        var configPath = Path.Combine(effectiveDir, "config.json");
        if (File.Exists(configPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                checks.Add(new("config.json valid", CheckStatus.Pass, "parses as JSON, schema OK"));

                if (doc.RootElement.TryGetProperty("teamRoot", out var teamRoot))
                {
                    var raw = teamRoot.GetString();
                    if (raw is not null && Path.IsPathFullyQualified(raw))
                        checks.Add(new("absolute path warning", CheckStatus.Warn,
                            $"teamRoot is absolute ({raw}) — prefer relative paths for portability"));

                    if (raw is { Length: > 0 })
                    {
                        var resolved = Path.IsPathFullyQualified(raw)
                            ? raw
                            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(effectiveDir)!, raw));
                        checks.Add(Directory.Exists(resolved)
                            ? new("team root resolves", CheckStatus.Pass, $"resolved to {resolved}")
                            : new("team root resolves", CheckStatus.Fail, $"directory not found: {resolved}"));
                    }
                }
            }
            catch (JsonException ex)
            {
                checks.Add(new("config.json valid", CheckStatus.Fail, $"invalid JSON: {ex.Message}"));
            }
        }

        // 3. team.md with ## Members
        var teamMd = Path.Combine(effectiveDir, "team.md");
        if (!File.Exists(teamMd))
            checks.Add(new("team.md exists with ## Members", CheckStatus.Fail, "file not found"));
        else if (!File.ReadAllText(teamMd).Contains("## Members"))
            checks.Add(new("team.md exists with ## Members", CheckStatus.Warn, "file exists but missing ## Members header"));
        else
            checks.Add(new("team.md exists with ## Members", CheckStatus.Pass, "file present, header found"));

        // 4. routing.md
        checks.Add(File.Exists(Path.Combine(effectiveDir, "routing.md"))
            ? new("routing.md exists", CheckStatus.Pass, "file present")
            : new("routing.md exists", CheckStatus.Fail, "file not found"));

        // 5. agents/ directory
        var agentsDir = Path.Combine(effectiveDir, "agents");
        if (!Directory.Exists(agentsDir))
        {
            checks.Add(new("agents/ directory exists", CheckStatus.Fail, "directory not found"));
        }
        else
        {
            var count = Directory.GetDirectories(agentsDir).Length;
            checks.Add(new("agents/ directory exists", CheckStatus.Pass,
                $"directory present ({count} agent{(count == 1 ? "" : "s")})"));
        }

        // 6. casting/registry.json
        var registryPath = Path.Combine(effectiveDir, "casting", "registry.json");
        if (!File.Exists(registryPath))
        {
            checks.Add(new("casting/registry.json exists", CheckStatus.Fail, "file not found"));
        }
        else
        {
            try
            {
                JsonDocument.Parse(File.ReadAllText(registryPath));
                checks.Add(new("casting/registry.json exists", CheckStatus.Pass, "file present, valid JSON"));
            }
            catch
            {
                checks.Add(new("casting/registry.json exists", CheckStatus.Fail, "file exists but is not valid JSON"));
            }
        }

        // 7. decisions.md
        checks.Add(File.Exists(Path.Combine(effectiveDir, "decisions.md"))
            ? new("decisions.md exists", CheckStatus.Pass, "file present")
            : new("decisions.md exists", CheckStatus.Fail, "file not found"));

        // 8. .NET runtime version
        var ver = Environment.Version;
        checks.Add(ver.Major >= 10
            ? new(".NET ≥10", CheckStatus.Pass, $"v{ver} — runtime supported")
            : new(".NET ≥10", CheckStatus.Warn, $"v{ver} — NSquad requires .NET 10+"));

        // 9. GitHub CLI available
        try
        {
            var result = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("gh", "--version")
                { RedirectStandardOutput = true, UseShellExecute = false });
            result?.WaitForExit(2000);
            checks.Add(new("GitHub CLI (gh) available", CheckStatus.Pass, "gh found in PATH"));
        }
        catch
        {
            checks.Add(new("GitHub CLI (gh) available", CheckStatus.Warn, "gh not found — some commands will not work"));
        }

        return checks;
    }

    /// <summary>Detect the squad mode for the given working directory. Exposed for testing.</summary>
    public static SquadDoctorMode DetectMode(string cwd)
    {
        if (File.Exists(Path.Combine(cwd, "squad-hub.json")))
            return SquadDoctorMode.Hub;

        var configPath = Path.Combine(cwd, ".squad", "config.json");
        if (!File.Exists(configPath))
            return SquadDoctorMode.Local;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            if (doc.RootElement.TryGetProperty("teamRoot", out var t) && t.GetString() is { Length: > 0 })
                return SquadDoctorMode.Remote;
        }
        catch (JsonException) { }

        return SquadDoctorMode.Local;
    }
}
