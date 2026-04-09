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
        await WriteIfNotExistsAsync(configPath, GenerateSquadConfig(projectName), created, skipped, ct);

        // ── .squad/team.md ───────────────────────────────────────────────────
        await WriteIfNotExistsAsync(
            Path.Combine(squadDir, "team.md"),
            GenerateTeamMd(projectName),
            created, skipped, ct);

        // ── .squad/decisions.md ──────────────────────────────────────────────
        await WriteIfNotExistsAsync(
            Path.Combine(squadDir, "decisions.md"),
            "# Squad Decisions\n\n## Active Decisions\n\nNo decisions recorded yet.\n\n## Governance\n\n- All meaningful changes require team consensus\n- Document architectural decisions here\n- Keep history focused on work, decisions focused on direction\n",
            created, skipped, ct);

        // ── .squad/routing.md ────────────────────────────────────────────────
        await WriteIfNotExistsAsync(
            Path.Combine(squadDir, "routing.md"),
            GenerateRoutingMd(),
            created, skipped, ct);

        // ── .squad/ceremonies.md ─────────────────────────────────────────────
        await WriteIfNotExistsAsync(
            Path.Combine(squadDir, "ceremonies.md"),
            GenerateCeremoniesMd(),
            created, skipped, ct);

        // ── .github/agents/squad.agent.md ───────────────────────────────────
        var agentDir = Path.Combine(cwd, ".github", "agents");
        Directory.CreateDirectory(agentDir);
        await WriteIfNotExistsAsync(
            Path.Combine(agentDir, "squad.agent.md"),
            GenerateSquadAgentMd(projectName),
            created, skipped, ct);

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

    private static async Task WriteIfNotExistsAsync(
        string path, string content,
        List<string> created, List<string> skipped,
        CancellationToken ct)
    {
        var rel = Path.GetRelativePath(Directory.GetCurrentDirectory(), path);
        if (File.Exists(path))
        {
            skipped.Add(rel);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, ct);
        created.Add(rel);
    }

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

    private static string GenerateRoutingMd() =>
        """
        # Work Routing

        How to decide who handles what.

        ## Routing Table

        | Work Type | Route To | Examples |
        |-----------|----------|----------|
        | Code review | lead | Review PRs, check quality, suggest improvements |
        | Testing | tester | Write tests, find edge cases, verify fixes |
        | Session logging | scribe | Automatic — never needs routing |

        ## Issue Routing

        | Label | Action | Who |
        |-------|--------|-----|
        | `squad` | Triage: analyze issue, assign `squad:{member}` label | Lead |
        | `squad:{name}` | Pick up issue and complete the work | Named member |

        """;

    private static string GenerateCeremoniesMd() =>
        """
        # Ceremonies

        > Team meetings that happen before or after work. Each squad configures their own.

        ## Design Review

        | Field | Value |
        |-------|-------|
        | **Trigger** | auto |
        | **When** | before |
        | **Condition** | multi-agent task involving 2+ agents modifying shared systems |
        | **Facilitator** | lead |
        | **Participants** | all-relevant |
        | **Enabled** | ✅ yes |

        **Agenda:**
        1. Review the task and requirements
        2. Agree on interfaces and contracts between components
        3. Identify risks and edge cases
        4. Assign action items

        ---

        ## Retrospective

        | Field | Value |
        |-------|-------|
        | **Trigger** | auto |
        | **When** | after |
        | **Condition** | build failure, test failure, or reviewer rejection |
        | **Facilitator** | lead |
        | **Participants** | all-involved |
        | **Enabled** | ✅ yes |

        **Agenda:**
        1. What happened? (facts only)
        2. Root cause analysis
        3. What should change?
        4. Action items for next iteration

        """;

    private static string GenerateSquadAgentMd(string projectName) =>
        $"""
        ---
        name: Squad
        description: "Your AI team coordinator for {projectName}."
        ---

        You are **Squad (Coordinator)** — the orchestrator for this project's AI team.

        ### Coordinator Identity

        - **Name:** Squad (Coordinator)
        - **Role:** Agent orchestration, handoff enforcement, reviewer gating
        - **Inputs:** User request, repository state, `.squad/decisions.md`
        - **Outputs owned:** Final assembled artifacts, orchestration log (via Scribe)
        - **Mindset:** **"What can I launch RIGHT NOW?"** — always maximize parallel work

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
        var rel = Path.GetRelativePath(cwd, path);

        var existing = File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : "";
        if (existing.Contains(".squad/decisions.md"))
            return;

        await File.AppendAllTextAsync(path, block, ct);
        created.Add(rel);
    }

    private static async Task AppendGitIgnoreAsync(
        string cwd, List<string> created, CancellationToken ct)
    {
        const string block =
            "\n# Squad runtime state\n.squad/log/\n.squad/inbox/\n.squad/decisions/inbox/\n.squad/.init-prompt\n.squad-workstream\n";

        var path = Path.Combine(cwd, ".gitignore");
        var rel = Path.GetRelativePath(cwd, path);

        var existing = File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : "";
        if (existing.Contains(".squad/log/"))
            return;

        await File.AppendAllTextAsync(path, block, ct);
        created.Add(rel);
    }
}
