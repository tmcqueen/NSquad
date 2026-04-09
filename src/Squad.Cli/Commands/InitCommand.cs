using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.Json;

namespace Squad.Cli.Commands;

public sealed class InitCommand : AsyncCommand<InitCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[prompt]")]
        [Description("Optional project description stored for REPL auto-casting.")]
        public string? Prompt { get; init; }

        [CommandOption("--roles")]
        [Description("Use built-in base roles catalog during team casting.")]
        public bool Roles { get; init; }

        [CommandOption("--yes|-y")]
        [Description("Skip confirmation when squad.config.json already exists.")]
        public bool Yes { get; init; }
    }

    // Files that go directly into .squad/ (relative to templates dir → .squad/)
    private static readonly string[] SquadTemplateFiles =
    [
        "casting-history.json",
        "casting-policy.json",
        "casting-registry.json",
        "ceremonies.md",
        "constraint-tracking.md",
        "copilot-instructions.md",
        "mcp-config.md",
        "multi-agent-format.md",
        "orchestration-log.md",
        "plugin-marketplace.md",
        "raw-agent-output.md",
        "roster.md",
        "routing.md",
        "run-output.md",
        "skill.md",
    ];

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken ct)
    {
        var cwd = Directory.GetCurrentDirectory();
        var projectName = Path.GetFileName(cwd);
        var squadDir = Path.Combine(cwd, ".squad");
        var configPath = Path.Combine(cwd, "squad.config.json");

        // Guard: existing squad.config.json
        if (File.Exists(configPath) && !settings.Yes)
        {
            AnsiConsole.MarkupLine("[yellow]⚠[/]  squad.config.json already exists.");
            AnsiConsole.MarkupLine("[dim]Run with --yes to re-scaffold missing files.[/]");
            return 0;
        }

        var templatesDir = Path.Combine(AppContext.BaseDirectory, "templates");
        if (!Directory.Exists(templatesDir))
        {
            AnsiConsole.MarkupLine("[red]✗[/] Templates directory not found: {0}", Markup.Escape(templatesDir));
            return 1;
        }

        AnsiConsole.MarkupLine("[dim]Let's build your team.[/]");
        AnsiConsole.WriteLine();

        var created = new List<string>();
        var skipped = new List<string>();

        // ── Directory structure ──────────────────────────────────────────────
        string[] dirs =
        [
            Path.Combine(squadDir, "agents"),
            Path.Combine(squadDir, "casting"),
            Path.Combine(squadDir, "decisions", "inbox"),
            Path.Combine(squadDir, "plugins"),
            Path.Combine(squadDir, "identity"),
            Path.Combine(squadDir, "orchestration-log"),
            Path.Combine(squadDir, "log"),
        ];
        foreach (var dir in dirs)
            Directory.CreateDirectory(dir);

        // ── squad.config.json ────────────────────────────────────────────────
        await WriteIfNotExistsAsync(configPath, GenerateSquadConfig(projectName), created, skipped, cwd, ct);

        // ── .squad/ template files ───────────────────────────────────────────
        foreach (var name in SquadTemplateFiles)
        {
            var src = Path.Combine(templatesDir, name);
            var dest = Path.Combine(squadDir, name);
            if (File.Exists(src))
                await CopyIfNotExistsAsync(src, dest, created, skipped, cwd, ct);
        }

        // ── .squad/team.md (generated — uses project name) ──────────────────
        await WriteIfNotExistsAsync(
            Path.Combine(squadDir, "team.md"),
            GenerateTeamMd(projectName),
            created, skipped, cwd, ct);

        // ── .squad/decisions.md ──────────────────────────────────────────────
        await WriteIfNotExistsAsync(
            Path.Combine(squadDir, "decisions.md"),
            "# Squad Decisions\n\n## Active Decisions\n\nNo decisions recorded yet.\n\n## Governance\n\n- All meaningful changes require team consensus\n- Document architectural decisions here\n- Keep history focused on work, decisions focused on direction\n",
            created, skipped, cwd, ct);

        // ── Default agents: scribe + ralph ───────────────────────────────────
        await ScaffoldAgentAsync("scribe", templatesDir, squadDir, created, skipped, cwd, ct);
        await ScaffoldAgentAsync("ralph", templatesDir, squadDir, created, skipped, cwd, ct);

        // ── .squad/identity/ ─────────────────────────────────────────────────
        var identitySrc = Path.Combine(templatesDir, "identity");
        if (Directory.Exists(identitySrc))
            await CopyDirIfNotExistsAsync(identitySrc, Path.Combine(squadDir, "identity"), created, skipped, cwd, ct);

        // ── .copilot/skills/ ─────────────────────────────────────────────────
        var skillsSrc = Path.Combine(templatesDir, "skills");
        var skillsDest = Path.Combine(cwd, ".copilot", "skills");
        if (Directory.Exists(skillsSrc) && !Directory.Exists(skillsDest))
        {
            CopyDirRecursive(skillsSrc, skillsDest);
            created.Add(Path.GetRelativePath(cwd, skillsDest));
        }

        // ── .github/agents/squad.agent.md ───────────────────────────────────
        var agentSrc = Path.Combine(templatesDir, "squad.agent.md");
        var agentDest = Path.Combine(cwd, ".github", "agents", "squad.agent.md");
        Directory.CreateDirectory(Path.GetDirectoryName(agentDest)!);
        if (File.Exists(agentSrc))
            await CopyIfNotExistsAsync(agentSrc, agentDest, created, skipped, cwd, ct);
        else
            await WriteIfNotExistsAsync(agentDest, GenerateFallbackAgentMd(projectName), created, skipped, cwd, ct);

        // ── .gitattributes ───────────────────────────────────────────────────
        await AppendGitAttributesAsync(cwd, created, ct);

        // ── .gitignore ───────────────────────────────────────────────────────
        await AppendGitIgnoreAsync(cwd, created, ct);

        // ── .squad/.init-prompt ──────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(settings.Prompt))
        {
            var promptPath = Path.Combine(squadDir, ".init-prompt");
            await File.WriteAllTextAsync(promptPath, settings.Prompt.Trim(), ct);
            created.Add(Path.GetRelativePath(cwd, promptPath));
        }

        // ── .squad/.init-roles ───────────────────────────────────────────────
        if (settings.Roles)
        {
            var rolesMarker = Path.Combine(squadDir, ".init-roles");
            await File.WriteAllTextAsync(rolesMarker, "1", ct);
            created.Add(Path.GetRelativePath(cwd, rolesMarker));
        }

        // ── Report ───────────────────────────────────────────────────────────
        foreach (var f in created)
            AnsiConsole.MarkupLine("[green]✓[/] {0}", Markup.Escape(f));

        foreach (var f in skipped)
            AnsiConsole.MarkupLine("[dim]  {0} already exists — skipping[/]", Markup.Escape(f));

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold cyan]◆ SQUAD[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  📁  Team workspace");
        AnsiConsole.MarkupLine("  📋  Skills & ceremonies");
        AnsiConsole.MarkupLine("  🧠  Identity & wisdom");
        AnsiConsole.MarkupLine("  🔧  Workflows & config");
        AnsiConsole.MarkupLine("  🤖  Copilot agent prompt");
        AnsiConsole.WriteLine();

        if (!string.IsNullOrWhiteSpace(settings.Prompt))
            AnsiConsole.MarkupLine("[green]✓[/] .init-prompt stored — team will be cast when you start squad");

        if (settings.Roles)
            AnsiConsole.MarkupLine("[green]✓[/] base roles enabled — team will use built-in role catalog");

        AnsiConsole.MarkupLine("[bold green]Your team is ready.[/] Run [bold cyan]squad[/] to start.");
        AnsiConsole.WriteLine();

        return 0;
    }

    // ── Agent scaffolding ────────────────────────────────────────────────────

    private static async Task ScaffoldAgentAsync(
        string agentName, string templatesDir, string squadDir,
        List<string> created, List<string> skipped, string cwd, CancellationToken ct)
    {
        var agentDir = Path.Combine(squadDir, "agents", agentName);
        Directory.CreateDirectory(agentDir);

        // charter.md: prefer scribe-charter.md for scribe, charter.md for others
        var charterSrc = agentName == "scribe"
            ? Path.Combine(templatesDir, "scribe-charter.md")
            : Path.Combine(templatesDir, "charter.md");

        var charterDest = Path.Combine(agentDir, "charter.md");
        if (File.Exists(charterSrc))
            await CopyIfNotExistsAsync(charterSrc, charterDest, created, skipped, cwd, ct);

        // history.md
        var historySrc = Path.Combine(templatesDir, "history.md");
        var historyDest = Path.Combine(agentDir, "history.md");
        if (File.Exists(historySrc))
            await CopyIfNotExistsAsync(historySrc, historyDest, created, skipped, cwd, ct);
    }

    // ── File helpers ─────────────────────────────────────────────────────────

    private static async Task WriteIfNotExistsAsync(
        string path, string content,
        List<string> created, List<string> skipped, string cwd, CancellationToken ct)
    {
        var rel = Path.GetRelativePath(cwd, path);
        if (File.Exists(path)) { skipped.Add(rel); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, ct);
        created.Add(rel);
    }

    private static async Task CopyIfNotExistsAsync(
        string src, string dest,
        List<string> created, List<string> skipped, string cwd, CancellationToken ct)
    {
        var rel = Path.GetRelativePath(cwd, dest);
        if (File.Exists(dest)) { skipped.Add(rel); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        await using var srcStream = File.OpenRead(src);
        await using var dstStream = File.Create(dest);
        await srcStream.CopyToAsync(dstStream, ct);
        created.Add(rel);
    }

    private static async Task CopyDirIfNotExistsAsync(
        string srcDir, string destDir,
        List<string> created, List<string> skipped, string cwd, CancellationToken ct)
    {
        Directory.CreateDirectory(destDir);
        foreach (var srcFile in Directory.GetFiles(srcDir))
        {
            var dest = Path.Combine(destDir, Path.GetFileName(srcFile));
            await CopyIfNotExistsAsync(srcFile, dest, created, skipped, cwd, ct);
        }
    }

    private static void CopyDirRecursive(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: false);
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirRecursive(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    // ── Content generators ───────────────────────────────────────────────────

    private static string GenerateSquadConfig(string projectName)
    {
        var config = new
        {
            version = "1",
            team = new { name = projectName, description = $"AI team for {projectName}" },
            agents = new[]
            {
                new { name = "scribe", role = "docs" },
                new { name = "ralph", role = "tester" },
            },
        };
        return JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }) + "\n";
    }

    private static string GenerateTeamMd(string projectName) =>
        $"""
        # Squad Team

        > {projectName}

        ## Coordinator

        | Name | Role | Notes |
        |------|------|-------|
        | Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

        ## Members

        | Name | Role | Charter | Status |
        |------|------|---------|--------|

        ## Project Context

        - **Project:** {projectName}
        - **Created:** {DateTimeOffset.UtcNow:yyyy-MM-dd}

        """;

    private static string GenerateFallbackAgentMd(string projectName) =>
        $"""
        ---
        name: Squad
        description: "Your AI team coordinator for {projectName}."
        ---

        You are **Squad (Coordinator)** — the orchestrator for this project's AI team.

        Check: Does `.squad/team.md` exist?
        - **No** → Init Mode
        - **Yes, but `## Members` has zero roster entries** → Init Mode
        - **Yes, with roster entries** → Team Mode

        In Team Mode, route all work to the appropriate agent based on `.squad/routing.md`.

        """;

    private static async Task AppendGitAttributesAsync(
        string cwd, List<string> created, CancellationToken ct)
    {
        const string block = "\n# Squad merge drivers\n.squad/decisions.md merge=union\n";
        var path = Path.Combine(cwd, ".gitattributes");
        var existing = File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : "";
        if (existing.Contains(".squad/decisions.md")) return;
        await File.AppendAllTextAsync(path, block, ct);
        created.Add(Path.GetRelativePath(cwd, path));
    }

    private static async Task AppendGitIgnoreAsync(
        string cwd, List<string> created, CancellationToken ct)
    {
        const string block =
            "\n# Squad runtime state\n.squad/log/\n.squad/inbox/\n.squad/decisions/inbox/\n.squad/.init-prompt\n.squad-workstream\n";
        var path = Path.Combine(cwd, ".gitignore");
        var existing = File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : "";
        if (existing.Contains(".squad/log/")) return;
        await File.AppendAllTextAsync(path, block, ct);
        created.Add(Path.GetRelativePath(cwd, path));
    }
}
