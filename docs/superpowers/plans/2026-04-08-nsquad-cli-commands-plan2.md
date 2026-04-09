# NSquad CLI Commands — Plan 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement 17 remaining non-interactive `squad` CLI commands: `build`, `export`, `import`, `migrate`, `copilot`, `link`, `init-remote`, `economy`, `roles`, `personal`, `plugin`, `schedule`, `upstream`, `streams`, `discover`, `delegate`, `watch`, and `extract`.

**Architecture:** All commands are `AsyncCommand<TSettings>` subclasses in `src/Squad.Cli/Commands/`. SDK types and helpers live in `src/Squad.Sdk/`. Each command exposes at least one `public static` method for unit testing without Spectre.Console wiring. Commands with subcommands use `AddBranch` in Program.cs.

**Tech Stack:** .NET 10, C# 13, Spectre.Console.Cli 0.55.0, TUnit 1.29.0, Shouldly 4.3.0. TypeScript reference source: `~/squad/packages/squad-cli/src/cli/commands/`. .NET Copilot SDK docs: `~/copilot-sdk/docs/copilot-sdk-csharp.instructions.md`.

---

## Context for Agentic Workers

- Working directory: `~/NSquad` — already a git repo on branch `main`
- Plan 1 was completed: solution has `src/Squad.Sdk/` and `src/Squad.Cli/` projects, 48 tests pass
- Existing SDK types: `SquadConfig`, `AgentConfig`, `TeamConfig`, `RoutingConfig`, `BudgetConfig`, `ModelConfig`, `CastingConfig`, `TelemetryConfig` in `Squad.Sdk.Config`
- Existing SDK utilities: `ConfigLoader`, `PathResolver`, `EventBus`, `CostReader`, `SkillLoader`, `SquadClient`, `SquadSession`, `SquadCoordinator`, `RoutingEngine`
- `PathResolver.GetGlobalSquadPath()` returns `~/.config/squad` (pure calculation, no side effects)
- `PathResolver.ResolveGlobalSquadPath()` calculates + creates the dir
- `PathResolver.ResolveSquadDir(string? startDir)` walks up to find `.squad/`, returns null if not found
- `PathResolver.DetectMode(string squadDir)` reads `.squad/config.json` for `teamRoot` → returns `SquadMode.Remote/Local/Hub`
- `PathResolver.ResolvePersonalSquadDir()` returns null if `SQUAD_NO_PERSONAL` env var is set
- Existing CLI commands: `DoctorCommand`, `CastCommand`, `CostCommand` in `src/Squad.Cli/Commands/`
- Test pattern: TUnit tests in `tests/Squad.Sdk.Tests/` and `tests/Squad.Cli.Tests/`, run with `dotnet run --project tests/...`
- **Never use `dotnet test`** — TUnit requires `dotnet run --project`
- Commit frequently — after each task

---

## File Map

```
src/Squad.Sdk/
  Config/
    LocalSquadConfig.cs       — .squad/config.json schema (teamRoot, economyMode)
    ExportManifest.cs         — squad-export.json schema
    UpstreamConfig.cs         — upstream.json schema
    MarketplacesRegistry.cs   — plugins/marketplaces.json schema
    SubSquadsConfig.cs        — streams.json schema
    ScheduleManifest.cs       — schedule.json schema + ScheduleState
  Generation/
    SquadMarkdownGenerator.cs — generates .squad/ markdown from SquadConfig
  Roles/
    RoleDefinition.cs         — RoleDefinition record + BuiltinRoles static class
  Ralph/
    IssueTriager.cs           — parse roster, triage issues against routing rules
  PersonalSquad/
    LicenseDetector.cs        — detect license type from LICENSE file content
    StagedLearnings.cs        — read/merge staged learnings from .squad/extract/
  Discovery/
    SquadDiscovery.cs         — discover squads via upstream.json

src/Squad.Cli/
  Infrastructure/
    TeamMdHelper.cs           — read/write copilot section in team.md
  Commands/
    BuildCommand.cs
    ExportCommand.cs
    ImportCommand.cs
    MigrateCommand.cs
    CopilotCommand.cs
    LinkCommand.cs
    InitRemoteCommand.cs
    EconomyCommand.cs
    RolesCommand.cs
    Personal/
      PersonalInitCommand.cs
      PersonalListCommand.cs
      PersonalAddCommand.cs
      PersonalRemoveCommand.cs
    Plugin/
      MarketplaceAddCommand.cs
      MarketplaceRemoveCommand.cs
      MarketplaceListCommand.cs
      MarketplaceBrowseCommand.cs
    ScheduleCommand.cs        — routes to subcommands internally
    UpstreamCommand.cs        — routes to subcommands internally
    StreamsCommand.cs         — routes to subcommands internally
    WatchCommand.cs
    DiscoverCommand.cs
    DelegateCommand.cs
    ExtractCommand.cs

tests/Squad.Sdk.Tests/
  Generation/
    SquadMarkdownGeneratorTests.cs
  Roles/
    BuiltinRolesTests.cs
  Ralph/
    IssueTriagerTests.cs
  PersonalSquad/
    LicenseDetectorTests.cs
    StagedLearningsTests.cs
  Discovery/
    SquadDiscoveryTests.cs

tests/Squad.Cli.Tests/
  Commands/
    BuildCommandTests.cs
    ExportCommandTests.cs
    ImportCommandTests.cs
    MigrateCommandTests.cs
    CopilotCommandTests.cs
    LinkCommandTests.cs
    EconomyCommandTests.cs
    RolesCommandTests.cs
    PersonalCommandTests.cs
    PluginCommandTests.cs
    ScheduleCommandTests.cs
    UpstreamCommandTests.cs
    StreamsCommandTests.cs
    WatchCommandTests.cs
    ExtractCommandTests.cs
```

---

## Task 1: New SDK Record Types

**Files:**
- Create: `src/Squad.Sdk/Config/LocalSquadConfig.cs`
- Create: `src/Squad.Sdk/Config/ExportManifest.cs`
- Create: `src/Squad.Sdk/Config/UpstreamConfig.cs`
- Create: `src/Squad.Sdk/Config/MarketplacesRegistry.cs`
- Create: `src/Squad.Sdk/Config/SubSquadsConfig.cs`
- Create: `src/Squad.Sdk/Config/ScheduleManifest.cs`

No tests needed for pure record types — they'll be exercised by command tests.

- [ ] **Step 1: Create LocalSquadConfig.cs**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

/// <summary>
/// Schema for .squad/config.json — machine-local settings (never committed).
/// </summary>
public sealed record LocalSquadConfig
{
    public int Version { get; init; } = 1;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TeamRoot { get; init; }
    public bool EconomyMode { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectKey { get; init; }

    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static LocalSquadConfig Load(string squadDir)
    {
        var path = Path.Combine(squadDir, "config.json");
        if (!File.Exists(path)) return new LocalSquadConfig();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LocalSquadConfig>(json, _opts) ?? new LocalSquadConfig();
        }
        catch
        {
            return new LocalSquadConfig();
        }
    }

    public void Save(string squadDir)
    {
        Directory.CreateDirectory(squadDir);
        var path = Path.Combine(squadDir, "config.json");
        File.WriteAllText(path, JsonSerializer.Serialize(this, _opts) + "\n");
    }
}
```

- [ ] **Step 2: Create ExportManifest.cs**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

public sealed record AgentExportData(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Charter,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? History);

public sealed record ExportManifest
{
    public string Version { get; init; } = "1.0";
    [JsonPropertyName("exported_at")]
    public string ExportedAt { get; init; } = DateTimeOffset.UtcNow.ToString("O");
    [JsonPropertyName("squad_version")]
    public string SquadVersion { get; init; } = "0.1.0";
    public Dictionary<string, JsonElement> Casting { get; init; } = new();
    public Dictionary<string, AgentExportData> Agents { get; init; } = new();
    public List<string> Skills { get; init; } = new();
}
```

- [ ] **Step 3: Create UpstreamConfig.cs**

```csharp
using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

public sealed record UpstreamSource
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "local"; // "local" | "git" | "export"
    public string Source { get; init; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ref { get; init; }
    [JsonPropertyName("added_at")]
    public string AddedAt { get; init; } = DateTimeOffset.UtcNow.ToString("O");
    [JsonPropertyName("last_synced")]
    public string? LastSynced { get; init; }
}

public sealed record UpstreamConfig(List<UpstreamSource> Upstreams)
{
    public UpstreamConfig() : this(new List<UpstreamSource>()) { }
}
```

- [ ] **Step 4: Create MarketplacesRegistry.cs**

```csharp
using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

public sealed record Marketplace(
    string Name,
    string Source,
    [property: JsonPropertyName("added_at")] string AddedAt);

public sealed record MarketplacesRegistry(List<Marketplace> Marketplaces)
{
    public MarketplacesRegistry() : this(new List<Marketplace>()) { }
}
```

- [ ] **Step 5: Create SubSquadsConfig.cs**

```csharp
using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

public sealed record SubSquadDefinition
{
    public string Name { get; init; } = "";
    public string LabelFilter { get; init; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Workflow { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
    public List<string> FolderScope { get; init; } = new();
}

public sealed record SubSquadsConfig
{
    public List<SubSquadDefinition> Workstreams { get; init; } = new();
    public string DefaultWorkflow { get; init; } = "feature";
}
```

- [ ] **Step 6: Create ScheduleManifest.cs**

```csharp
using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

public sealed record ScheduleTrigger
{
    public string Type { get; init; } = "cron"; // "cron" | "interval" | "event" | "startup"
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cron { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? IntervalSeconds { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Event { get; init; }
}

public sealed record ScheduleTask(string Type, string Ref);

public sealed record ScheduleRetry(int MaxRetries, int BackoffSeconds);

public sealed record ScheduleEntry
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public ScheduleTrigger Trigger { get; init; } = new();
    public ScheduleTask Task { get; init; } = new("print", "echo hello");
    public List<string> Providers { get; init; } = new();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScheduleRetry? Retry { get; init; }
}

public sealed record ScheduleManifest(List<ScheduleEntry> Schedules)
{
    public ScheduleManifest() : this(new List<ScheduleEntry>()) { }
}

public sealed record ScheduleRun(string LastRun, string Status, string? Error = null);

public sealed record ScheduleState(Dictionary<string, ScheduleRun> Runs)
{
    public ScheduleState() : this(new Dictionary<string, ScheduleRun>()) { }
}
```

- [ ] **Step 7: Build and verify**

```bash
cd ~/NSquad && dotnet build
```

Expected: 0 errors.

- [ ] **Step 8: Commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/Config/LocalSquadConfig.cs src/Squad.Sdk/Config/ExportManifest.cs \
    src/Squad.Sdk/Config/UpstreamConfig.cs src/Squad.Sdk/Config/MarketplacesRegistry.cs \
    src/Squad.Sdk/Config/SubSquadsConfig.cs src/Squad.Sdk/Config/ScheduleManifest.cs
git commit -m "feat(sdk): add Plan 2 record types (export, upstream, marketplaces, streams, schedule)"
```

---

## Task 2: `link` and `init-remote` Commands

**Files:**
- Create: `src/Squad.Cli/Commands/LinkCommand.cs`
- Create: `src/Squad.Cli/Commands/InitRemoteCommand.cs`
- Create: `tests/Squad.Cli.Tests/Commands/LinkCommandTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Squad.Cli.Tests/Commands/LinkCommandTests.cs`:
```csharp
using Squad.Cli.Commands;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class LinkCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void WriteRemoteConfig_creates_config_json_with_relative_path()
    {
        // Create a fake team repo
        var teamDir = Path.Combine(_tempDir, "team-repo");
        Directory.CreateDirectory(Path.Combine(teamDir, ".squad"));

        LinkCommand.WriteRemoteConfig(_tempDir, teamDir);

        var configPath = Path.Combine(_tempDir, ".squad", "config.json");
        File.Exists(configPath).ShouldBeTrue();
        var json = File.ReadAllText(configPath);
        json.ShouldContain("teamRoot");
        // Should store relative path
        json.ShouldContain("team-repo");
    }

    [Test]
    public void WriteRemoteConfig_adds_gitignore_entry()
    {
        var teamDir = Path.Combine(_tempDir, "team-repo");
        Directory.CreateDirectory(Path.Combine(teamDir, ".squad"));

        LinkCommand.WriteRemoteConfig(_tempDir, teamDir);

        var gitignorePath = Path.Combine(_tempDir, ".gitignore");
        File.Exists(gitignorePath).ShouldBeTrue();
        File.ReadAllText(gitignorePath).ShouldContain(".squad/config.json");
    }

    [Test]
    public void WriteRemoteConfig_throws_if_target_does_not_exist()
    {
        Should.Throw<InvalidOperationException>(() =>
            LinkCommand.WriteRemoteConfig(_tempDir, Path.Combine(_tempDir, "nonexistent")));
    }

    [Test]
    public void WriteRemoteConfig_throws_if_target_has_no_squad_dir()
    {
        var emptyDir = Path.Combine(_tempDir, "empty-dir");
        Directory.CreateDirectory(emptyDir);

        Should.Throw<InvalidOperationException>(() =>
            LinkCommand.WriteRemoteConfig(_tempDir, emptyDir));
    }

    [Test]
    public void WriteRemoteConfig_does_not_duplicate_gitignore_entry()
    {
        var teamDir = Path.Combine(_tempDir, "team-repo");
        Directory.CreateDirectory(Path.Combine(teamDir, ".squad"));

        // Write entry twice
        LinkCommand.WriteRemoteConfig(_tempDir, teamDir);
        LinkCommand.WriteRemoteConfig(_tempDir, teamDir);

        var content = File.ReadAllText(Path.Combine(_tempDir, ".gitignore"));
        content.Split('\n').Count(l => l.Trim() == ".squad/config.json").ShouldBe(1);
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: compile error — `LinkCommand` not found.

- [ ] **Step 3: Implement LinkCommand**

Create `src/Squad.Cli/Commands/LinkCommand.cs`:
```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Config;

namespace Squad.Cli.Commands;

public sealed class LinkCommand : AsyncCommand<LinkCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<team-repo-path>")]
        [Description("Path (relative or absolute) to the team repository.")]
        public string TeamRepoPath { get; init; } = "";
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        try
        {
            WriteRemoteConfig(cwd, settings.TeamRepoPath);
            AnsiConsole.MarkupLine("[green]✓[/] Linked to team root: [dim]{0}[/]",
                Path.GetRelativePath(cwd, Path.GetFullPath(settings.TeamRepoPath)));
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine("[red]✗[/] {0}", ex.Message);
            return 1;
        }
        return 0;
    }

    /// <summary>Write .squad/config.json with teamRoot. Exposed for testing.</summary>
    public static void WriteRemoteConfig(string projectDir, string teamRepoPath)
    {
        var absoluteTeam = Path.GetFullPath(Path.Combine(projectDir, teamRepoPath));

        if (!Directory.Exists(absoluteTeam))
            throw new InvalidOperationException($"Target path does not exist: {absoluteTeam}");

        var hasSquad = Directory.Exists(Path.Combine(absoluteTeam, ".squad"));
        var hasAiTeam = Directory.Exists(Path.Combine(absoluteTeam, ".ai-team"));
        if (!hasSquad && !hasAiTeam)
            throw new InvalidOperationException($"Target does not contain a .squad/ directory: {absoluteTeam}");

        var squadDir = Path.Combine(projectDir, ".squad");
        var relativePath = Path.GetRelativePath(projectDir, absoluteTeam);

        var cfg = new LocalSquadConfig { TeamRoot = relativePath };
        cfg.Save(squadDir);

        EnsureGitignoreEntry(projectDir, ".squad/config.json");
    }

    private static void EnsureGitignoreEntry(string repoDir, string entry)
    {
        var gitignorePath = Path.Combine(repoDir, ".gitignore");
        var existing = File.Exists(gitignorePath) ? File.ReadAllText(gitignorePath) : "";
        if (existing.Split('\n').Any(l => l.Trim() == entry)) return;

        var nl = existing.Length > 0 && !existing.EndsWith('\n') ? "\n" : "";
        File.AppendAllText(gitignorePath,
            nl + "# Squad: local config (machine-specific paths, never commit)\n" + entry + "\n");
    }
}
```

Create `src/Squad.Cli/Commands/InitRemoteCommand.cs`:
```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Squad.Cli.Commands;

public sealed class InitRemoteCommand : AsyncCommand<InitRemoteCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<team-repo-path>")]
        [Description("Path (relative or absolute) to the team repository.")]
        public string TeamRepoPath { get; init; } = "";
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        try
        {
            LinkCommand.WriteRemoteConfig(cwd, settings.TeamRepoPath);
            AnsiConsole.MarkupLine("[green]✓[/] Remote config written.");
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine("[red]✗[/] {0}", ex.Message);
            return 1;
        }
        return 0;
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass (previous + 5 new).

- [ ] **Step 5: Commit**

```bash
cd ~/NSquad
git add src/Squad.Cli/Commands/LinkCommand.cs src/Squad.Cli/Commands/InitRemoteCommand.cs \
    tests/Squad.Cli.Tests/Commands/LinkCommandTests.cs
git commit -m "feat(cli): add link and init-remote commands"
```

---

## Task 3: `economy` and `roles` Commands

**Files:**
- Create: `src/Squad.Sdk/Roles/RoleDefinition.cs`
- Create: `src/Squad.Cli/Commands/EconomyCommand.cs`
- Create: `src/Squad.Cli/Commands/RolesCommand.cs`
- Create: `tests/Squad.Cli.Tests/Commands/EconomyCommandTests.cs`
- Create: `tests/Squad.Cli.Tests/Commands/RolesCommandTests.cs`

- [ ] **Step 1: Create RoleDefinition.cs in Squad.Sdk**

Create `src/Squad.Sdk/Roles/RoleDefinition.cs`:
```csharp
namespace Squad.Sdk.Roles;

public sealed record RoleDefinition(
    string Id,
    string Title,
    string Vibe,
    string Category,
    string Emoji);

/// <summary>
/// Embedded built-in role catalog (20 roles).
/// Attribution: Adapted from agency-agents by AgentLand Contributors (MIT).
/// </summary>
public static class BuiltinRoles
{
    public static readonly IReadOnlyList<RoleDefinition> All = new List<RoleDefinition>
    {
        // Engineering
        new("lead",       "Lead / Architect",    "Designs systems that survive the team that built them.",            "engineering", "🏗️"),
        new("frontend",   "Frontend Developer",  "Builds responsive, accessible web apps with pixel-perfect precision.", "engineering", "⚛️"),
        new("backend",    "Backend Developer",   "Designs the systems that hold everything up — databases, APIs, cloud, scale.", "engineering", "🔧"),
        new("fullstack",  "Full-Stack Developer","Sees the full picture — from the database to the pixel.",           "engineering", "💻"),
        new("security",   "Security Engineer",   "Models threats, reviews code, and designs security architecture that actually holds.", "engineering", "🔒"),
        new("data",       "Data Engineer",       "Thinks in tables and queries. Normalizes first, denormalizes when the numbers demand it.", "engineering", "📊"),
        new("ai",         "AI / ML Engineer",    "Builds intelligent systems that learn, reason, and adapt.",         "engineering", "🤖"),
        // Quality
        new("reviewer",   "Code Reviewer",       "Reviews code like a mentor, not a gatekeeper.",                    "quality",      "👁️"),
        new("tester",     "Test Engineer",        "Breaks your API before your users do.",                            "quality",      "🧪"),
        // Operations
        new("devops",     "DevOps Engineer",     "Automates infrastructure so your team ships faster and sleeps better.", "operations", "⚙️"),
        // Design
        new("designer",   "UI/UX Designer",      "Pixel-aware and user-obsessed. If it looks off by one, it is off by one.", "design", "🎨"),
        // Product
        new("docs",       "Technical Writer",    "Turns complexity into clarity. If the docs are wrong, the product is wrong.", "product", "📝"),
        new("product-manager", "Product Manager","Shapes what gets built and why — every feature earns its place.",  "product",      "📋"),
        // Marketing
        new("marketing-strategist", "Marketing Strategist", "Drives growth through content and channels — every post has a purpose.", "marketing", "📣"),
        // Sales
        new("sales-strategist", "Sales Strategist", "Closes deals with strategic precision — understand the buyer before pitching.", "sales", "💼"),
        // Operations
        new("project-manager", "Project Manager","Keeps the train on the tracks — scope, schedule, and sanity.",     "operations",   "📅"),
        // Support
        new("support-specialist", "Support Specialist", "First line of defense for users — solve fast, document everything.", "support", "🎧"),
        // Game Dev
        new("game-developer", "Game Developer",  "Builds worlds players want to live in.",                           "game-dev",     "🎮"),
        // Media
        new("media-buyer", "Media Buyer",        "Maximizes ROI across ad channels — every dollar tracked.",         "media",        "📺"),
        // Compliance
        new("compliance-legal", "Compliance & Legal", "Ensures you ship safely and legally — compliance is a feature.", "compliance", "⚖️"),
    };

    public static IReadOnlyList<RoleDefinition> Filter(string? category = null, string? search = null)
    {
        var roles = All.AsEnumerable();
        if (category != null)
            roles = roles.Where(r => r.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        if (search != null)
        {
            var q = search.ToLower();
            roles = roles.Where(r =>
                r.Id.Contains(q) || r.Title.ToLower().Contains(q) || r.Vibe.ToLower().Contains(q));
        }
        return roles.ToList();
    }

    public static IReadOnlyList<string> Categories =>
        All.Select(r => r.Category).Distinct().Order().ToList();
}
```

- [ ] **Step 2: Write failing tests**

Create `tests/Squad.Cli.Tests/Commands/EconomyCommandTests.cs`:
```csharp
using Squad.Cli.Commands;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class EconomyCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad"));
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void SetEconomyMode_on_writes_true_to_config()
    {
        EconomyCommand.SetEconomyMode(_tempDir, true);

        var configPath = Path.Combine(_tempDir, ".squad", "config.json");
        var json = File.ReadAllText(configPath);
        json.ShouldContain("true");
    }

    [Test]
    public void GetEconomyMode_returns_false_when_no_config()
    {
        EconomyCommand.GetEconomyMode(_tempDir).ShouldBeFalse();
    }

    [Test]
    public void SetEconomyMode_on_then_off_returns_false()
    {
        EconomyCommand.SetEconomyMode(_tempDir, true);
        EconomyCommand.SetEconomyMode(_tempDir, false);
        EconomyCommand.GetEconomyMode(_tempDir).ShouldBeFalse();
    }
}
```

Create `tests/Squad.Cli.Tests/Commands/RolesCommandTests.cs`:
```csharp
using Squad.Cli.Commands;
using Squad.Sdk.Roles;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class RolesCommandTests
{
    [Test]
    public void BuiltinRoles_All_has_20_entries()
    {
        BuiltinRoles.All.Count.ShouldBe(20);
    }

    [Test]
    public void Filter_by_category_returns_only_matching()
    {
        var engineering = BuiltinRoles.Filter(category: "engineering");
        engineering.ShouldNotBeEmpty();
        engineering.ShouldAllBe(r => r.Category == "engineering");
    }

    [Test]
    public void Filter_by_search_returns_matching()
    {
        var results = BuiltinRoles.Filter(search: "backend");
        results.ShouldNotBeEmpty();
        results.ShouldContain(r => r.Id == "backend");
    }

    [Test]
    public void Filter_no_args_returns_all()
    {
        BuiltinRoles.Filter().Count.ShouldBe(BuiltinRoles.All.Count);
    }

    [Test]
    public void Categories_are_unique_and_sorted()
    {
        var cats = BuiltinRoles.Categories;
        cats.Count.ShouldBe(cats.Distinct().Count());
        cats.ShouldBe(cats.Order().ToList());
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: compile error — `EconomyCommand` not found.

- [ ] **Step 4: Implement EconomyCommand and RolesCommand**

Create `src/Squad.Cli/Commands/EconomyCommand.cs`:
```csharp
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

        switch (settings.Mode?.ToLowerInvariant())
        {
            case "on":
                SetEconomyMode(squadDir, true);
                AnsiConsole.MarkupLine("[green]✓[/] Economy mode [bold]enabled[/].");
                break;
            case "off":
                SetEconomyMode(squadDir, false);
                AnsiConsole.MarkupLine("[green]✓[/] Economy mode [bold]disabled[/].");
                break;
            case null:
                var enabled = GetEconomyMode(squadDir);
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

    public static void SetEconomyMode(string squadDir, bool enabled)
    {
        var cfg = LocalSquadConfig.Load(squadDir);
        cfg = cfg with { EconomyMode = enabled };
        cfg.Save(squadDir);
    }

    public static bool GetEconomyMode(string squadDir)
    {
        return LocalSquadConfig.Load(squadDir).EconomyMode;
    }
}
```

Create `src/Squad.Cli/Commands/RolesCommand.cs`:
```csharp
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
```

- [ ] **Step 5: Run tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/Roles/RoleDefinition.cs src/Squad.Cli/Commands/EconomyCommand.cs \
    src/Squad.Cli/Commands/RolesCommand.cs \
    tests/Squad.Cli.Tests/Commands/EconomyCommandTests.cs \
    tests/Squad.Cli.Tests/Commands/RolesCommandTests.cs
git commit -m "feat(cli): add economy and roles commands"
```

---

## Task 4: `copilot` Command + TeamMdHelper

**Files:**
- Create: `src/Squad.Cli/Infrastructure/TeamMdHelper.cs`
- Create: `src/Squad.Cli/Commands/CopilotCommand.cs`
- Create: `tests/Squad.Cli.Tests/Commands/CopilotCommandTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Squad.Cli.Tests/Commands/CopilotCommandTests.cs`:
```csharp
using Squad.Cli.Infrastructure;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class CopilotCommandTests
{
    private static readonly string SampleTeamMd = """
        # Squad Team — Test Squad

        ## Members

        | Name | Role | Charter | Status |
        |------|------|---------|--------|
        | Builder | Feature Dev | `.squad/agents/builder/charter.md` | ✅ Active |
        """;

    [Test]
    public void HasCopilot_returns_false_when_not_present()
    {
        TeamMdHelper.HasCopilot(SampleTeamMd).ShouldBeFalse();
    }

    [Test]
    public void InsertCopilotSection_adds_copilot_row()
    {
        var result = TeamMdHelper.InsertCopilotSection(SampleTeamMd, autoAssign: false);
        result.ShouldContain("Copilot");
        TeamMdHelper.HasCopilot(result).ShouldBeTrue();
    }

    [Test]
    public void RemoveCopilotSection_removes_copilot_row()
    {
        var withCopilot = TeamMdHelper.InsertCopilotSection(SampleTeamMd, autoAssign: false);
        var result = TeamMdHelper.RemoveCopilotSection(withCopilot);
        TeamMdHelper.HasCopilot(result).ShouldBeFalse();
    }

    [Test]
    public void InsertCopilotSection_with_auto_assign_adds_marker()
    {
        var result = TeamMdHelper.InsertCopilotSection(SampleTeamMd, autoAssign: true);
        result.ShouldContain("copilot-auto-assign: true");
    }

    [Test]
    public void RemoveCopilotSection_is_idempotent_on_content_without_copilot()
    {
        var result = TeamMdHelper.RemoveCopilotSection(SampleTeamMd);
        result.ShouldBe(SampleTeamMd);
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: compile error — `TeamMdHelper` not found.

- [ ] **Step 3: Implement TeamMdHelper**

Create `src/Squad.Cli/Infrastructure/TeamMdHelper.cs`:
```csharp
namespace Squad.Cli.Infrastructure;

/// <summary>
/// Reads and writes the @copilot section in .squad/team.md.
/// </summary>
public static class TeamMdHelper
{
    private const string CopilotRowMarker = "@copilot";
    private const string AutoAssignComment = "<!-- copilot-auto-assign: true -->";

    public static bool HasCopilot(string content) =>
        content.Contains(CopilotRowMarker, StringComparison.OrdinalIgnoreCase) ||
        content.Contains("🤖 Coding Agent", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Insert the Copilot Coding Agent row into the Members table.
    /// If autoAssign is true, also inserts a comment marker.
    /// </summary>
    public static string InsertCopilotSection(string content, bool autoAssign)
    {
        // Add Copilot row before the closing of the Members table
        const string copilotRow = "| @copilot | 🤖 Coding Agent | — | ✅ Active |";
        var lines = content.Split('\n').ToList();

        // Find the last table row in the Members section
        int lastTableRow = -1;
        bool inMembers = false;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("## Members")) { inMembers = true; continue; }
            if (inMembers && lines[i].TrimStart().StartsWith("##")) { inMembers = false; break; }
            if (inMembers && lines[i].TrimStart().StartsWith("|") && !lines[i].Contains("---"))
                lastTableRow = i;
        }

        if (lastTableRow >= 0)
            lines.Insert(lastTableRow + 1, copilotRow);
        else
            lines.Add(copilotRow);

        if (autoAssign)
            lines.Add(AutoAssignComment);

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Remove the Copilot row and auto-assign comment from team.md content.
    /// </summary>
    public static string RemoveCopilotSection(string content)
    {
        var lines = content.Split('\n')
            .Where(l => !l.Contains(CopilotRowMarker, StringComparison.OrdinalIgnoreCase)
                     && !l.Contains("🤖 Coding Agent", StringComparison.OrdinalIgnoreCase)
                     && l.Trim() != AutoAssignComment)
            .ToList();
        return string.Join('\n', lines);
    }

    public static string ReadTeamMd(string squadDir)
    {
        var path = Path.Combine(squadDir, "team.md");
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    public static void WriteTeamMd(string squadDir, string content)
    {
        File.WriteAllText(Path.Combine(squadDir, "team.md"), content);
    }
}
```

- [ ] **Step 4: Implement CopilotCommand**

Create `src/Squad.Cli/Commands/CopilotCommand.cs`:
```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Cli.Infrastructure;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands;

public sealed class CopilotCommand : AsyncCommand<CopilotCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--off")]
        [Description("Remove @copilot from the team.")]
        public bool Off { get; init; }

        [CommandOption("--auto-assign")]
        [Description("Auto-assign @copilot on squad-labeled issues.")]
        public bool AutoAssign { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        var squadDir = PathResolver.ResolveSquadDir(cwd);
        if (squadDir == null || !Directory.Exists(squadDir))
        {
            AnsiConsole.MarkupLine("[red]✗[/] No squad found — run init first.");
            return 1;
        }

        var content = TeamMdHelper.ReadTeamMd(squadDir);
        var hasCopilot = TeamMdHelper.HasCopilot(content);

        if (settings.Off)
        {
            if (!hasCopilot)
            {
                AnsiConsole.MarkupLine("[dim]@copilot is not on the team — nothing to remove.[/]");
                return 0;
            }
            content = TeamMdHelper.RemoveCopilotSection(content);
            TeamMdHelper.WriteTeamMd(squadDir, content);
            AnsiConsole.MarkupLine("[green]✓[/] Removed @copilot from team roster.");

            var instructionsPath = Path.Combine(cwd, ".github", "copilot-instructions.md");
            if (File.Exists(instructionsPath))
            {
                File.Delete(instructionsPath);
                AnsiConsole.MarkupLine("[green]✓[/] Removed .github/copilot-instructions.md.");
            }
            return 0;
        }

        if (hasCopilot)
        {
            AnsiConsole.MarkupLine("[dim]@copilot is already on the team.[/]");
            return 0;
        }

        content = TeamMdHelper.InsertCopilotSection(content, settings.AutoAssign);
        TeamMdHelper.WriteTeamMd(squadDir, content);
        AnsiConsole.MarkupLine("[green]✓[/] Added @copilot (Coding Agent) to team roster.");

        // Write copilot-instructions.md
        var destPath = Path.Combine(cwd, ".github", "copilot-instructions.md");
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.WriteAllText(destPath, GenerateCopilotInstructions());
        AnsiConsole.MarkupLine("[green]✓[/] Created .github/copilot-instructions.md.");
        return 0;
    }

    private static string GenerateCopilotInstructions() =>
        """
        # GitHub Copilot Instructions

        This project uses Squad multi-agent coordination.
        Copilot (the coding agent) handles issues labeled `squad:copilot`.

        ## Workflow

        1. Pick up squad-labeled issues
        2. Create a feature branch
        3. Implement and test
        4. Open a PR for human review
        """;
}
```

- [ ] **Step 5: Run tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
cd ~/NSquad
git add src/Squad.Cli/Infrastructure/TeamMdHelper.cs src/Squad.Cli/Commands/CopilotCommand.cs \
    tests/Squad.Cli.Tests/Commands/CopilotCommandTests.cs
git commit -m "feat(cli): add copilot command and TeamMdHelper"
```

---

## Task 5: `export` Command

**Files:**
- Create: `src/Squad.Cli/Commands/ExportCommand.cs`
- Create: `tests/Squad.Cli.Tests/Commands/ExportCommandTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Squad.Cli.Tests/Commands/ExportCommandTests.cs`:
```csharp
using System.Text.Json;
using Squad.Cli.Commands;
using Squad.Sdk.Config;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class ExportCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        // Create minimal .squad/ structure
        var squadDir = Path.Combine(_tempDir, ".squad");
        Directory.CreateDirectory(squadDir);
        Directory.CreateDirectory(Path.Combine(squadDir, "casting"));
        Directory.CreateDirectory(Path.Combine(squadDir, "agents", "builder"));
        File.WriteAllText(Path.Combine(squadDir, "team.md"), "# Squad Team");
        File.WriteAllText(Path.Combine(squadDir, "agents", "builder", "charter.md"), "# Builder");
        File.WriteAllText(Path.Combine(squadDir, "casting", "registry.json"), """{"universe":"mcqueen"}""");
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task BuildManifest_includes_agents()
    {
        var manifest = await ExportCommand.BuildManifestAsync(_tempDir);
        manifest.Agents.ShouldContainKey("builder");
        manifest.Agents["builder"].Charter.ShouldContain("Builder");
    }

    [Test]
    public async Task BuildManifest_includes_casting()
    {
        var manifest = await ExportCommand.BuildManifestAsync(_tempDir);
        manifest.Casting.ShouldContainKey("registry");
    }

    [Test]
    public async Task BuildManifest_version_is_1_0()
    {
        var manifest = await ExportCommand.BuildManifestAsync(_tempDir);
        manifest.Version.ShouldBe("1.0");
    }

    [Test]
    public async Task BuildManifest_includes_skills_when_present()
    {
        var skillsDir = Path.Combine(_tempDir, ".squad", "skills", "my-skill");
        Directory.CreateDirectory(skillsDir);
        File.WriteAllText(Path.Combine(skillsDir, "SKILL.md"), "# My Skill");

        var manifest = await ExportCommand.BuildManifestAsync(_tempDir);
        manifest.Skills.ShouldNotBeEmpty();
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: compile error.

- [ ] **Step 3: Implement ExportCommand**

Create `src/Squad.Cli/Commands/ExportCommand.cs`:
```csharp
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Config;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands;

public sealed class ExportCommand : AsyncCommand<ExportCommand.Settings>
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-o|--output")]
        [Description("Output file path. Defaults to squad-export.json in current directory.")]
        public string? Output { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        var squadDir = PathResolver.ResolveSquadDir(cwd);
        if (squadDir == null || !File.Exists(Path.Combine(squadDir, "team.md")))
        {
            AnsiConsole.MarkupLine("[red]✗[/] No squad found — run init first.");
            return 1;
        }

        var manifest = await BuildManifestAsync(cwd, cancellationToken);
        var outPath = settings.Output ?? Path.Combine(cwd, "squad-export.json");
        await File.WriteAllTextAsync(outPath, JsonSerializer.Serialize(manifest, _opts) + "\n", cancellationToken);

        AnsiConsole.MarkupLine("[green]✓[/] Exported squad to [bold]{0}[/]",
            Path.GetRelativePath(cwd, outPath));
        AnsiConsole.MarkupLine("[yellow]⚠[/] Review agent histories before sharing — they may contain project-specific information.");
        return 0;
    }

    public static async Task<ExportManifest> BuildManifestAsync(
        string cwd, CancellationToken ct = default)
    {
        var squadDir = Path.Combine(cwd, ".squad");
        var manifest = new ExportManifest();

        // Casting
        var castingDir = Path.Combine(squadDir, "casting");
        if (Directory.Exists(castingDir))
        {
            foreach (var file in new[] { "registry.json", "policy.json", "history.json" })
            {
                var filePath = Path.Combine(castingDir, file);
                if (!File.Exists(filePath)) continue;
                try
                {
                    var json = await File.ReadAllTextAsync(filePath, ct);
                    manifest.Casting[Path.GetFileNameWithoutExtension(file)] =
                        JsonSerializer.Deserialize<JsonElement>(json);
                }
                catch { /* skip unreadable */ }
            }
        }

        // Agents
        var agentsDir = Path.Combine(squadDir, "agents");
        if (Directory.Exists(agentsDir))
        {
            foreach (var entry in Directory.GetDirectories(agentsDir))
            {
                var name = Path.GetFileName(entry);
                var charterPath = Path.Combine(entry, "charter.md");
                var historyPath = Path.Combine(entry, "history.md");
                manifest.Agents[name] = new AgentExportData(
                    File.Exists(charterPath) ? await File.ReadAllTextAsync(charterPath, ct) : null,
                    File.Exists(historyPath) ? await File.ReadAllTextAsync(historyPath, ct) : null);
            }
        }

        // Skills (nested layout: .squad/skills/<name>/SKILL.md)
        var skillsDir = Path.Combine(squadDir, "skills");
        if (Directory.Exists(skillsDir))
        {
            foreach (var entry in Directory.GetDirectories(skillsDir))
            {
                var skillFile = Path.Combine(entry, "SKILL.md");
                if (File.Exists(skillFile))
                    manifest.Skills.Add(await File.ReadAllTextAsync(skillFile, ct));
            }
        }

        return manifest;
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
cd ~/NSquad
git add src/Squad.Cli/Commands/ExportCommand.cs tests/Squad.Cli.Tests/Commands/ExportCommandTests.cs
git commit -m "feat(cli): add export command"
```

---

## Task 6: `import` Command + HistorySplitter

**Files:**
- Create: `src/Squad.Sdk/PersonalSquad/HistorySplitter.cs`
- Create: `src/Squad.Cli/Commands/ImportCommand.cs`
- Create: `tests/Squad.Sdk.Tests/PersonalSquad/HistorySplitterTests.cs`
- Create: `tests/Squad.Cli.Tests/Commands/ImportCommandTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Squad.Sdk.Tests/PersonalSquad/HistorySplitterTests.cs`:
```csharp
using Squad.Sdk.PersonalSquad;
using Shouldly;

namespace Squad.Sdk.Tests.PersonalSquad;

public class HistorySplitterTests
{
    [Test]
    public void Split_puts_project_sections_in_learnings()
    {
        var history = """
            ## Portable Knowledge

            Uses async/await throughout.

            ## Key File Paths

            /src/main.cs is the entry point.
            """;

        var result = HistorySplitter.Split(history, "my-project");

        result.ShouldContain("Portable Knowledge");
        result.ShouldContain("Project Learnings (from import — my-project)");
        result.ShouldContain("Key File Paths");
    }

    [Test]
    public void Split_with_no_project_sections_returns_unchanged()
    {
        var history = "## Learnings\n\nUses pattern X.";
        var result = HistorySplitter.Split(history, "proj");
        result.ShouldContain("Uses pattern X");
        result.ShouldNotContain("Project Learnings");
    }

    [Test]
    public void Split_handles_empty_input()
    {
        var result = HistorySplitter.Split("", "proj");
        result.ShouldNotBeNull();
    }
}
```

Create `tests/Squad.Cli.Tests/Commands/ImportCommandTests.cs`:
```csharp
using System.Text.Json;
using Squad.Cli.Commands;
using Squad.Sdk.Config;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class ImportCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private static string MakeManifestJson(string agentName = "builder") =>
        JsonSerializer.Serialize(new
        {
            version = "1.0",
            exported_at = DateTimeOffset.UtcNow.ToString("O"),
            casting = new { registry = new { universe = "mcqueen" } },
            agents = new Dictionary<string, object>
            {
                [agentName] = new { charter = "# Builder", history = "" }
            },
            skills = Array.Empty<string>()
        });

    [Test]
    public async Task ValidateManifest_accepts_valid_manifest()
    {
        var json = MakeManifestJson();
        var manifest = JsonSerializer.Deserialize<ExportManifest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        // Should not throw
        ImportCommand.ValidateManifest(manifest);
    }

    [Test]
    public async Task ImportManifest_creates_agent_directory()
    {
        var json = MakeManifestJson("edie");
        var manifestPath = Path.Combine(_tempDir, "squad-export.json");
        await File.WriteAllTextAsync(manifestPath, json);

        await ImportCommand.ImportAsync(_tempDir, manifestPath, force: true);

        var agentDir = Path.Combine(_tempDir, ".squad", "agents", "edie");
        Directory.Exists(agentDir).ShouldBeTrue();
        File.Exists(Path.Combine(agentDir, "charter.md")).ShouldBeTrue();
    }

    [Test]
    public async Task ImportManifest_fails_for_wrong_version()
    {
        var json = """{"version":"2.0","casting":{},"agents":{},"skills":[]}""";
        var manifestPath = Path.Combine(_tempDir, "bad.json");
        await File.WriteAllTextAsync(manifestPath, json);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            ImportCommand.ImportAsync(_tempDir, manifestPath, force: false));
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Sdk.Tests
```

Expected: compile error — `HistorySplitter` not found.

- [ ] **Step 3: Implement HistorySplitter**

Create `src/Squad.Sdk/PersonalSquad/HistorySplitter.cs`:
```csharp
using System.Text.RegularExpressions;

namespace Squad.Sdk.PersonalSquad;

/// <summary>
/// Separates portable knowledge from project-specific sections in history.md content.
/// </summary>
public static class HistorySplitter
{
    private static readonly Regex[] ProjectPatterns =
    {
        new(@"^#{1,3}\s*key file paths", RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^#{1,3}\s*sprint",         RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^#{1,3}\s*pr\s*#",         RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^#{1,3}\s*file system",    RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^#{1,3}\s*session",        RegexOptions.IgnoreCase | RegexOptions.Multiline),
    };

    private static readonly Regex[] PortablePatterns =
    {
        new(@"^#{1,3}\s*learnings",           RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^#{1,3}\s*portable knowledge",  RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^#{1,3}\s*team updates",        RegexOptions.IgnoreCase | RegexOptions.Multiline),
    };

    public static string Split(string history, string sourceProject)
    {
        if (string.IsNullOrEmpty(history)) return history;

        var lines = history.Split('\n');
        var portable = new List<string>();
        var projectLearnings = new List<string>();
        var inProjectSection = false;

        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"^#{1,3}\s"))
            {
                if (ProjectPatterns.Any(p => p.IsMatch(line)))
                    inProjectSection = true;
                else if (PortablePatterns.Any(p => p.IsMatch(line)))
                    inProjectSection = false;
            }

            if (inProjectSection)
                projectLearnings.Add(line);
            else
                portable.Add(line);
        }

        var result = string.Join('\n', portable);
        if (projectLearnings.Count > 0)
            result += $"\n\n## Project Learnings (from import — {sourceProject})\n\n"
                    + string.Join('\n', projectLearnings);

        return result;
    }
}
```

- [ ] **Step 4: Implement ImportCommand**

Create `src/Squad.Cli/Commands/ImportCommand.cs`:
```csharp
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Config;
using Squad.Sdk.PersonalSquad;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands;

public sealed class ImportCommand : AsyncCommand<ImportCommand.Settings>
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<import-file>")]
        [Description("Path to squad-export.json file.")]
        public string ImportFile { get; init; } = "";

        [CommandOption("-f|--force")]
        [Description("Overwrite existing squad (archives current squad first).")]
        public bool Force { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        try
        {
            await ImportAsync(cwd, settings.ImportFile, settings.Force, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine("[red]✗[/] {0}", ex.Message);
            return 1;
        }
        return 0;
    }

    public static void ValidateManifest(ExportManifest manifest)
    {
        if (manifest.Version != "1.0")
            throw new InvalidOperationException(
                $"Unsupported export version: {manifest.Version} (expected 1.0)");
    }

    public static async Task ImportAsync(
        string cwd, string importPath, bool force, CancellationToken ct = default)
    {
        var resolvedPath = Path.GetFullPath(importPath);
        if (!File.Exists(resolvedPath))
            throw new InvalidOperationException($"Import file not found: {importPath}");

        ExportManifest manifest;
        try
        {
            var json = await File.ReadAllTextAsync(resolvedPath, ct);
            manifest = JsonSerializer.Deserialize<ExportManifest>(json, _opts)
                ?? throw new InvalidOperationException("Empty import file.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid JSON: {ex.Message}");
        }

        ValidateManifest(manifest);

        var squadDir = Path.Combine(cwd, ".squad");
        if (Directory.Exists(squadDir))
        {
            if (!force)
                throw new InvalidOperationException(
                    "A squad already exists here. Use --force to replace (current squad will be archived).");

            var ts = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss");
            var archiveDir = Path.Combine(cwd, $".squad-archive-{ts}");
            Directory.Move(squadDir, archiveDir);
            AnsiConsole.MarkupLine("[dim]Archived existing squad to {0}[/]", Path.GetFileName(archiveDir));
        }

        // Create directory structure
        Directory.CreateDirectory(Path.Combine(squadDir, "casting"));
        Directory.CreateDirectory(Path.Combine(squadDir, "agents"));
        File.WriteAllText(Path.Combine(squadDir, "decisions.md"), "");
        File.WriteAllText(Path.Combine(squadDir, "team.md"), "");

        // Write casting
        foreach (var (key, value) in manifest.Casting)
        {
            await File.WriteAllTextAsync(
                Path.Combine(squadDir, "casting", $"{key}.json"),
                JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) + "\n", ct);
        }

        // Write agents
        var importDate = DateTimeOffset.UtcNow.ToString("O");
        var sourceProject = Path.GetFileNameWithoutExtension(resolvedPath);
        foreach (var (name, data) in manifest.Agents)
        {
            var agentDir = Path.Combine(squadDir, "agents", name);
            Directory.CreateDirectory(agentDir);

            if (data.Charter != null)
                await File.WriteAllTextAsync(Path.Combine(agentDir, "charter.md"), data.Charter, ct);

            var historyContent = data.History != null
                ? HistorySplitter.Split(data.History, sourceProject)
                : "";
            historyContent = $"📌 Imported from {sourceProject} on {importDate}. Portable knowledge carried over.\n\n"
                           + historyContent;
            await File.WriteAllTextAsync(Path.Combine(agentDir, "history.md"), historyContent, ct);
        }

        // Write skills
        var skillsBase = Path.Combine(cwd, ".copilot", "skills");
        foreach (var skillContent in manifest.Skills)
        {
            var nameMatch = System.Text.RegularExpressions.Regex.Match(
                skillContent, @"^name:\s*[""']?(.+?)[""']?\s*$", System.Text.RegularExpressions.RegexOptions.Multiline);
            var skillName = nameMatch.Success
                ? nameMatch.Groups[1].Value.Trim().ToLowerInvariant().Replace(" ", "-")
                : $"skill-{manifest.Skills.IndexOf(skillContent)}";
            var skillDir = Path.Combine(skillsBase, skillName);
            Directory.CreateDirectory(skillDir);
            await File.WriteAllTextAsync(Path.Combine(skillDir, "SKILL.md"), skillContent, ct);
        }

        AnsiConsole.MarkupLine("[green]✓[/] Imported squad from [bold]{0}[/]", Path.GetFileName(resolvedPath));
        AnsiConsole.MarkupLine("  {0} agents: {1}", manifest.Agents.Count, string.Join(", ", manifest.Agents.Keys));
        AnsiConsole.MarkupLine("  {0} skills imported", manifest.Skills.Count);
        AnsiConsole.MarkupLine("[yellow]⚠[/] Project-specific learnings are marked in agent histories — review if needed.");
    }
}
```

- [ ] **Step 5: Run tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Sdk.Tests && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/PersonalSquad/HistorySplitter.cs src/Squad.Cli/Commands/ImportCommand.cs \
    tests/Squad.Sdk.Tests/PersonalSquad/HistorySplitterTests.cs \
    tests/Squad.Cli.Tests/Commands/ImportCommandTests.cs
git commit -m "feat(cli): add import command + HistorySplitter"
```

---

## Task 7: `personal` Commands

**Files:**
- Create: `src/Squad.Cli/Commands/Personal/PersonalInitCommand.cs`
- Create: `src/Squad.Cli/Commands/Personal/PersonalListCommand.cs`
- Create: `src/Squad.Cli/Commands/Personal/PersonalAddCommand.cs`
- Create: `src/Squad.Cli/Commands/Personal/PersonalRemoveCommand.cs`
- Create: `tests/Squad.Cli.Tests/Commands/PersonalCommandTests.cs`

The TypeScript `personal` command uses `resolveGlobalSquadPath()` and `resolvePersonalSquadDir()` — these already exist in `Squad.Sdk.Resolution.PathResolver`.

- [ ] **Step 1: Write failing tests**

Create `tests/Squad.Cli.Tests/Commands/PersonalCommandTests.cs`:
```csharp
using Squad.Cli.Commands.Personal;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class PersonalCommandTests
{
    private string _personalDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _personalDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "personal-squad");
    }

    [After(Test)]
    public void Cleanup()
    {
        var parent = Path.GetDirectoryName(_personalDir)!;
        if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
    }

    [Test]
    public void InitPersonal_creates_directory_and_config()
    {
        PersonalHelper.Init(_personalDir);
        Directory.Exists(_personalDir).ShouldBeTrue();
        File.Exists(Path.Combine(_personalDir, "config.json")).ShouldBeTrue();
    }

    [Test]
    public void AddAgent_creates_charter_and_history()
    {
        PersonalHelper.Init(_personalDir);
        PersonalHelper.AddAgent(_personalDir, "ripley", "lead");

        var agentDir = Path.Combine(_personalDir, "agents", "ripley");
        Directory.Exists(agentDir).ShouldBeTrue();
        File.Exists(Path.Combine(agentDir, "charter.md")).ShouldBeTrue();
        File.Exists(Path.Combine(agentDir, "history.md")).ShouldBeTrue();
    }

    [Test]
    public void AddAgent_charter_contains_name_and_role()
    {
        PersonalHelper.Init(_personalDir);
        PersonalHelper.AddAgent(_personalDir, "ripley", "lead");

        var charter = File.ReadAllText(Path.Combine(_personalDir, "agents", "ripley", "charter.md"));
        charter.ShouldContain("ripley");
        charter.ShouldContain("lead");
    }

    [Test]
    public void ListAgents_returns_added_agents()
    {
        PersonalHelper.Init(_personalDir);
        PersonalHelper.AddAgent(_personalDir, "ripley", "lead");
        PersonalHelper.AddAgent(_personalDir, "kane", "backend");

        var agents = PersonalHelper.ListAgents(_personalDir);
        agents.ShouldContain("ripley");
        agents.ShouldContain("kane");
    }

    [Test]
    public void RemoveAgent_deletes_agent_directory()
    {
        PersonalHelper.Init(_personalDir);
        PersonalHelper.AddAgent(_personalDir, "ripley", "lead");
        PersonalHelper.RemoveAgent(_personalDir, "ripley");

        var agentDir = Path.Combine(_personalDir, "agents", "ripley");
        Directory.Exists(agentDir).ShouldBeFalse();
    }

    [Test]
    public void RemoveAgent_throws_if_not_found()
    {
        PersonalHelper.Init(_personalDir);
        Should.Throw<InvalidOperationException>(() => PersonalHelper.RemoveAgent(_personalDir, "nobody"));
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: compile error — `PersonalHelper` not found.

- [ ] **Step 3: Implement PersonalHelper and commands**

Create `src/Squad.Cli/Commands/Personal/PersonalInitCommand.cs`:
```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands.Personal;

/// <summary>
/// Shared logic for personal squad operations. Exposed for testing.
/// </summary>
public static class PersonalHelper
{
    public static void Init(string personalDir)
    {
        if (Directory.Exists(personalDir))
            throw new InvalidOperationException($"Personal squad already initialized at {personalDir}");

        Directory.CreateDirectory(Path.Combine(personalDir, "agents"));
        var config = """{"defaultModel":"auto","ghostProtocol":true}""";
        File.WriteAllText(Path.Combine(personalDir, "config.json"), config + "\n");
    }

    public static void AddAgent(string personalDir, string name, string role)
    {
        var agentDir = Path.Combine(personalDir, "agents", name);
        if (Directory.Exists(agentDir))
            throw new InvalidOperationException($"Agent '{name}' already exists.");

        Directory.CreateDirectory(agentDir);
        File.WriteAllText(Path.Combine(agentDir, "charter.md"), GenerateCharter(name, role));
        File.WriteAllText(Path.Combine(agentDir, "history.md"), "# History\n\n<!-- Agent activity log -->\n");
    }

    public static IReadOnlyList<string> ListAgents(string personalDir)
    {
        var agentsDir = Path.Combine(personalDir, "agents");
        if (!Directory.Exists(agentsDir)) return Array.Empty<string>();
        return Directory.GetDirectories(agentsDir).Select(Path.GetFileName).ToList()!;
    }

    public static void RemoveAgent(string personalDir, string name)
    {
        var agentDir = Path.Combine(personalDir, "agents", name);
        if (!Directory.Exists(agentDir))
            throw new InvalidOperationException($"Agent '{name}' not found in personal squad.");
        Directory.Delete(agentDir, recursive: true);
    }

    private static string GenerateCharter(string name, string role) =>
        $"""
        # {name} — {role}

        > Your one-line personality statement — what makes you tick

        ## Identity

        - **Name:** {name}
        - **Role:** {role}
        - **Expertise:** [Your 2-3 specific skills]
        - **Style:** [How you communicate]

        ## Model

        - **Preferred:** auto

        ## Collaboration

        This is a personal agent — ambient across all projects.
        Ghost protocol is enforced in project contexts.
        """;
}

public sealed class PersonalInitCommand : AsyncCommand
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var globalDir = PathResolver.ResolveGlobalSquadPath();
        var personalDir = Path.Combine(globalDir, "personal-squad");
        try
        {
            PersonalHelper.Init(personalDir);
            AnsiConsole.MarkupLine("[green]✓[/] Personal squad initialized at [dim]{0}[/]", personalDir);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine("[yellow]⚠[/] {0}", ex.Message);
        }
        return 0;
    }
}
```

Create `src/Squad.Cli/Commands/Personal/PersonalListCommand.cs`:
```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands.Personal;

public sealed class PersonalListCommand : AsyncCommand
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var personalDir = PathResolver.ResolvePersonalSquadDir();
        if (personalDir == null)
        {
            AnsiConsole.MarkupLine("[dim]No personal squad found. Run [bold]squad personal init[/].[/]");
            return 0;
        }

        var agents = PersonalHelper.ListAgents(personalDir);
        if (agents.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No personal agents. Use [bold]squad personal add <name> --role <role>[/].[/]");
            return 0;
        }

        var table = new Spectre.Console.Table()
            .AddColumn("Name")
            .AddColumn("Charter Path");

        foreach (var name in agents)
            table.AddRow(name, Path.Combine(personalDir, "agents", name, "charter.md"));

        AnsiConsole.Write(table);
        return 0;
    }
}
```

Create `src/Squad.Cli/Commands/Personal/PersonalAddCommand.cs`:
```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands.Personal;

public sealed class PersonalAddCommand : AsyncCommand<PersonalAddCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        public string Name { get; init; } = "";

        [CommandOption("--role")]
        [Description("Agent role (e.g., lead, backend, tester).")]
        public string Role { get; init; } = "agent";
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var globalDir = PathResolver.ResolveGlobalSquadPath();
        var personalDir = Path.Combine(globalDir, "personal-squad");

        if (!Directory.Exists(personalDir))
        {
            AnsiConsole.MarkupLine("[red]✗[/] Personal squad not initialized. Run [bold]squad personal init[/] first.");
            return 1;
        }

        try
        {
            PersonalHelper.AddAgent(personalDir, settings.Name, settings.Role);
            AnsiConsole.MarkupLine("[green]✓[/] Added personal agent: [bold]{0}[/] (role: {1})", settings.Name, settings.Role);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine("[yellow]⚠[/] {0}", ex.Message);
        }
        return 0;
    }
}
```

Create `src/Squad.Cli/Commands/Personal/PersonalRemoveCommand.cs`:
```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands.Personal;

public sealed class PersonalRemoveCommand : AsyncCommand<PersonalRemoveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        public string Name { get; init; } = "";
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var personalDir = PathResolver.ResolvePersonalSquadDir();
        if (personalDir == null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Personal squad not initialized.");
            return 1;
        }

        try
        {
            PersonalHelper.RemoveAgent(personalDir, settings.Name);
            AnsiConsole.MarkupLine("[green]✓[/] Removed personal agent: [bold]{0}[/]", settings.Name);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine("[red]✗[/] {0}", ex.Message);
            return 1;
        }
        return 0;
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
cd ~/NSquad
git add src/Squad.Cli/Commands/Personal/ tests/Squad.Cli.Tests/Commands/PersonalCommandTests.cs
git commit -m "feat(cli): add personal commands (init/list/add/remove)"
```

---

## Task 8: `plugin` Marketplace Commands

**Files:**
- Create: `src/Squad.Cli/Commands/Plugin/MarketplaceCommand.cs` (contains Add/Remove/List + PluginHelper)
- Create: `tests/Squad.Cli.Tests/Commands/PluginCommandTests.cs`

Note: `browse` requires `gh` CLI — provide a stub that calls `gh api`. Test only the registry read/write.

- [ ] **Step 1: Write failing tests**

Create `tests/Squad.Cli.Tests/Commands/PluginCommandTests.cs`:
```csharp
using Squad.Cli.Commands.Plugin;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class PluginCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad"));
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task AddMarketplace_adds_to_registry()
    {
        await MarketplaceHelper.AddAsync(_tempDir, "owner/my-marketplace");
        var reg = await MarketplaceHelper.ReadAsync(_tempDir);
        reg.Marketplaces.ShouldContain(m => m.Source == "owner/my-marketplace");
    }

    [Test]
    public async Task AddMarketplace_is_idempotent()
    {
        await MarketplaceHelper.AddAsync(_tempDir, "owner/my-marketplace");
        await MarketplaceHelper.AddAsync(_tempDir, "owner/my-marketplace"); // duplicate
        var reg = await MarketplaceHelper.ReadAsync(_tempDir);
        reg.Marketplaces.Count(m => m.Source == "owner/my-marketplace").ShouldBe(1);
    }

    [Test]
    public async Task RemoveMarketplace_removes_from_registry()
    {
        await MarketplaceHelper.AddAsync(_tempDir, "owner/my-marketplace");
        await MarketplaceHelper.RemoveAsync(_tempDir, "my-marketplace");
        var reg = await MarketplaceHelper.ReadAsync(_tempDir);
        reg.Marketplaces.ShouldNotContain(m => m.Name == "my-marketplace");
    }

    [Test]
    public async Task RemoveMarketplace_throws_if_not_found()
    {
        await Should.ThrowAsync<InvalidOperationException>(() =>
            MarketplaceHelper.RemoveAsync(_tempDir, "nonexistent"));
    }

    [Test]
    public async Task ReadMarketplaces_returns_empty_when_no_file()
    {
        var reg = await MarketplaceHelper.ReadAsync(_tempDir);
        reg.Marketplaces.ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: compile error.

- [ ] **Step 3: Implement MarketplaceHelper and commands**

Create `src/Squad.Cli/Commands/Plugin/MarketplaceCommand.cs`:
```csharp
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Config;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands.Plugin;

public static class MarketplaceHelper
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static string RegistryPath(string cwd)
    {
        var squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");
        return Path.Combine(squadDir, "plugins", "marketplaces.json");
    }

    public static async Task<MarketplacesRegistry> ReadAsync(string cwd, CancellationToken ct = default)
    {
        var path = RegistryPath(cwd);
        if (!File.Exists(path)) return new MarketplacesRegistry();
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<MarketplacesRegistry>(json, _opts) ?? new MarketplacesRegistry();
        }
        catch { return new MarketplacesRegistry(); }
    }

    public static async Task WriteAsync(string cwd, MarketplacesRegistry reg, CancellationToken ct = default)
    {
        var path = RegistryPath(cwd);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(reg, _opts) + "\n", ct);
    }

    public static async Task AddAsync(string cwd, string source, CancellationToken ct = default)
    {
        var reg = await ReadAsync(cwd, ct);
        if (reg.Marketplaces.Any(m => m.Source == source)) return; // idempotent

        var name = source.Split('/').Last();
        var updated = reg with
        {
            Marketplaces = reg.Marketplaces
                .Append(new Marketplace(name, source, DateTimeOffset.UtcNow.ToString("O")))
                .ToList()
        };
        await WriteAsync(cwd, updated, ct);
    }

    public static async Task RemoveAsync(string cwd, string name, CancellationToken ct = default)
    {
        var reg = await ReadAsync(cwd, ct);
        var filtered = reg.Marketplaces.Where(m => m.Name != name).ToList();
        if (filtered.Count == reg.Marketplaces.Count)
            throw new InvalidOperationException($"Marketplace \"{name}\" not found.");
        await WriteAsync(cwd, reg with { Marketplaces = filtered }, ct);
    }
}

public sealed class MarketplaceAddCommand : AsyncCommand<MarketplaceAddCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<owner/repo>")]
        [Description("GitHub repo in owner/repo format.")]
        public string Source { get; init; } = "";
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!settings.Source.Contains('/'))
        {
            AnsiConsole.MarkupLine("[red]✗[/] Source must be in [bold]owner/repo[/] format.");
            return 1;
        }
        var cwd = Directory.GetCurrentDirectory();
        await MarketplaceHelper.AddAsync(cwd, settings.Source, cancellationToken);
        AnsiConsole.MarkupLine("[green]✓[/] Registered marketplace: [bold]{0}[/]", settings.Source);
        return 0;
    }
}

public sealed class MarketplaceRemoveCommand : AsyncCommand<MarketplaceRemoveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        public string Name { get; init; } = "";
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        try
        {
            await MarketplaceHelper.RemoveAsync(cwd, settings.Name, cancellationToken);
            AnsiConsole.MarkupLine("[green]✓[/] Removed marketplace: [bold]{0}[/]", settings.Name);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine("[red]✗[/] {0}", ex.Message);
            return 1;
        }
        return 0;
    }
}

public sealed class MarketplaceListCommand : AsyncCommand
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        var reg = await MarketplaceHelper.ReadAsync(cwd, cancellationToken);
        if (reg.Marketplaces.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No marketplaces registered.[/]");
            AnsiConsole.MarkupLine("\nAdd one with: [bold]squad plugin marketplace add <owner/repo>[/]");
            return 0;
        }
        AnsiConsole.MarkupLine("\n[bold]Registered marketplaces:[/]\n");
        foreach (var m in reg.Marketplaces)
            AnsiConsole.MarkupLine("  [bold]{0}[/]  →  [dim]{1}[/]", m.Name, m.Source);
        return 0;
    }
}

public sealed class MarketplaceBrowseCommand : AsyncCommand<MarketplaceBrowseCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Marketplace name to browse.")]
        public string Name { get; init; } = "";
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cwd = Directory.GetCurrentDirectory();
        var reg = await MarketplaceHelper.ReadAsync(cwd, cancellationToken);
        var mp = reg.Marketplaces.FirstOrDefault(m => m.Name == settings.Name);
        if (mp == null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Marketplace \"{0}\" not found.", settings.Name);
            return 1;
        }

        // Invoke gh api to list top-level directories
        var psi = new System.Diagnostics.ProcessStartInfo("gh",
            $"api repos/{mp.Source}/contents --jq \"[.[] | select(.type == \\\"dir\\\") | .name]\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc == null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] gh CLI not found.");
            return 1;
        }
        var output = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
        await proc.WaitForExitAsync(cancellationToken);

        if (proc.ExitCode != 0)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Could not browse {0}.", mp.Source);
            return 1;
        }

        var entries = JsonSerializer.Deserialize<List<string>>(output.Trim()) ?? new();
        AnsiConsole.MarkupLine("\n[bold]Plugins in {0}[/] ({1}):\n", mp.Name, mp.Source);
        foreach (var e in entries)
            AnsiConsole.MarkupLine("  📦 {0}", e);
        return 0;
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
cd ~/NSquad
git add src/Squad.Cli/Commands/Plugin/ tests/Squad.Cli.Tests/Commands/PluginCommandTests.cs
git commit -m "feat(cli): add plugin marketplace commands"
```

---

## Task 9: `upstream` Command

**Files:**
- Create: `src/Squad.Cli/Commands/UpstreamCommand.cs`
- Create: `tests/Squad.Cli.Tests/Commands/UpstreamCommandTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Squad.Cli.Tests/Commands/UpstreamCommandTests.cs`:
```csharp
using Squad.Cli.Commands;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class UpstreamCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad"));
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void DetectSourceType_git_url()
    {
        UpstreamCommand.DetectSourceType("https://github.com/owner/repo.git").ShouldBe("git");
    }

    [Test]
    public void DetectSourceType_export_json()
    {
        var jsonFile = Path.Combine(_tempDir, "squad-export.json");
        File.WriteAllText(jsonFile, "{}");
        UpstreamCommand.DetectSourceType(jsonFile).ShouldBe("export");
    }

    [Test]
    public void DetectSourceType_local_directory()
    {
        UpstreamCommand.DetectSourceType(_tempDir).ShouldBe("local");
    }

    [Test]
    public void IsValidGitRef_accepts_valid_refs()
    {
        UpstreamCommand.IsValidGitRef("main").ShouldBeTrue();
        UpstreamCommand.IsValidGitRef("feature/my-branch").ShouldBeTrue();
        UpstreamCommand.IsValidGitRef("v1.0.0").ShouldBeTrue();
    }

    [Test]
    public void IsValidGitRef_rejects_shell_metacharacters()
    {
        UpstreamCommand.IsValidGitRef("main; rm -rf /").ShouldBeFalse();
        UpstreamCommand.IsValidGitRef("$(evil)").ShouldBeFalse();
    }

    [Test]
    public async Task AddUpstream_adds_entry_to_upstream_json()
    {
        await UpstreamCommand.AddAsync(_tempDir, _tempDir, name: "local-team");
        var config = await UpstreamCommand.ReadConfigAsync(_tempDir);
        config.Upstreams.ShouldContain(u => u.Name == "local-team");
    }

    [Test]
    public async Task RemoveUpstream_removes_entry()
    {
        await UpstreamCommand.AddAsync(_tempDir, _tempDir, name: "local-team");
        await UpstreamCommand.RemoveAsync(_tempDir, "local-team");
        var config = await UpstreamCommand.ReadConfigAsync(_tempDir);
        config.Upstreams.ShouldNotContain(u => u.Name == "local-team");
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: compile error.

- [ ] **Step 3: Implement UpstreamCommand**

Create `src/Squad.Cli/Commands/UpstreamCommand.cs`:
```csharp
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Config;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands;

public sealed class UpstreamCommand : AsyncCommand<UpstreamCommand.Settings>
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[action]")]
        [Description("add | remove | list | sync")]
        public string? Action { get; init; }

        [CommandArgument(1, "[source-or-name]")]
        public string? SourceOrName { get; init; }

        [CommandOption("--name")]
        public string? Name { get; init; }

        [CommandOption("--ref")]
        public string? Ref { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken ct)
    {
        var cwd = Directory.GetCurrentDirectory();
        var squadDir = PathResolver.ResolveSquadDir(cwd);
        if (squadDir == null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] No squad found.");
            return 1;
        }

        switch (settings.Action?.ToLowerInvariant())
        {
            case "add":
                if (settings.SourceOrName == null)
                {
                    AnsiConsole.MarkupLine("[red]✗[/] Usage: squad upstream add <source> [--name <name>] [--ref <branch>]");
                    return 1;
                }
                try
                {
                    await AddAsync(cwd, settings.SourceOrName, settings.Name, settings.Ref, ct);
                    AnsiConsole.MarkupLine("[green]✓[/] Added upstream: [bold]{0}[/]", settings.Name ?? settings.SourceOrName);
                }
                catch (Exception ex) { AnsiConsole.MarkupLine("[red]✗[/] {0}", ex.Message); return 1; }
                break;

            case "remove":
                if (settings.SourceOrName == null)
                {
                    AnsiConsole.MarkupLine("[red]✗[/] Usage: squad upstream remove <name>");
                    return 1;
                }
                try
                {
                    await RemoveAsync(cwd, settings.SourceOrName, ct);
                    AnsiConsole.MarkupLine("[green]✓[/] Removed upstream: [bold]{0}[/]", settings.SourceOrName);
                }
                catch (Exception ex) { AnsiConsole.MarkupLine("[red]✗[/] {0}", ex.Message); return 1; }
                break;

            case "list":
                await ListAsync(cwd, ct);
                break;

            case "sync":
                await SyncAsync(cwd, settings.SourceOrName, ct);
                break;

            default:
                AnsiConsole.MarkupLine("[red]✗[/] Usage: squad upstream add|remove|list|sync");
                return 1;
        }
        return 0;
    }

    // -----------------------------------------------------------------------
    // Public helpers (for testing)
    // -----------------------------------------------------------------------

    public static string DetectSourceType(string source)
    {
        if (source.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.GetFullPath(source)))
            return "export";
        if (source.StartsWith("http://") || source.StartsWith("https://")
            || source.StartsWith("file://") || source.EndsWith(".git"))
            return "git";
        if (Directory.Exists(Path.GetFullPath(source)))
            return "local";
        if (source.Contains('/') && !source.Contains('\\'))
            return "git";
        throw new InvalidOperationException($"Cannot determine source type for \"{source}\".");
    }

    public static bool IsValidGitRef(string gitRef) =>
        Regex.IsMatch(gitRef, @"^[a-zA-Z0-9._\-/]+$");

    private static bool IsValidUpstreamName(string name) =>
        Regex.IsMatch(name, @"^[a-zA-Z0-9._-]+$");

    private static string DeriveName(string source, string type)
    {
        if (type == "export") return Path.GetFileNameWithoutExtension(source).Replace("squad-export", "upstream");
        if (type == "git") return (source.TrimEnd('/').TrimSuffix(".git").Split('/').LastOrDefault() ?? "upstream");
        return Path.GetFileName(Path.GetFullPath(source)) ?? "upstream";
    }

    public static async Task<UpstreamConfig> ReadConfigAsync(string cwd, CancellationToken ct = default)
    {
        var squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");
        var path = Path.Combine(squadDir, "upstream.json");
        if (!File.Exists(path)) return new UpstreamConfig();
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<UpstreamConfig>(json, _opts) ?? new UpstreamConfig();
        }
        catch { return new UpstreamConfig(); }
    }

    public static async Task WriteConfigAsync(string cwd, UpstreamConfig config, CancellationToken ct = default)
    {
        var squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");
        Directory.CreateDirectory(squadDir);
        await File.WriteAllTextAsync(Path.Combine(squadDir, "upstream.json"),
            JsonSerializer.Serialize(config, _opts) + "\n", ct);
    }

    public static async Task AddAsync(string cwd, string source, string? name = null,
        string? gitRef = null, CancellationToken ct = default)
    {
        var type = DetectSourceType(source);
        var derivedName = name ?? DeriveName(source, type);

        if (!IsValidUpstreamName(derivedName))
            throw new InvalidOperationException($"Invalid upstream name \"{derivedName}\".");
        if (gitRef != null && !IsValidGitRef(gitRef))
            throw new InvalidOperationException($"Invalid git ref \"{gitRef}\".");

        var config = await ReadConfigAsync(cwd, ct);
        if (config.Upstreams.Any(u => u.Name == derivedName))
            throw new InvalidOperationException($"Upstream \"{derivedName}\" already exists.");

        var entry = new UpstreamSource
        {
            Name = derivedName,
            Type = type,
            Source = type is "local" or "export" ? Path.GetFullPath(source) : source,
            Ref = type == "git" ? (gitRef ?? "main") : null,
            AddedAt = DateTimeOffset.UtcNow.ToString("O"),
        };

        await WriteConfigAsync(cwd, config with { Upstreams = config.Upstreams.Append(entry).ToList() }, ct);
    }

    public static async Task RemoveAsync(string cwd, string name, CancellationToken ct = default)
    {
        var config = await ReadConfigAsync(cwd, ct);
        var filtered = config.Upstreams.Where(u => u.Name != name).ToList();
        if (filtered.Count == config.Upstreams.Count)
            throw new InvalidOperationException($"Upstream \"{name}\" not found.");

        // Clean up cached clone
        var squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");
        var cloneDir = Path.Combine(squadDir, "_upstream_repos", name);
        if (Directory.Exists(cloneDir)) Directory.Delete(cloneDir, recursive: true);

        await WriteConfigAsync(cwd, config with { Upstreams = filtered }, ct);
    }

    private static async Task ListAsync(string cwd, CancellationToken ct)
    {
        var config = await ReadConfigAsync(cwd, ct);
        if (config.Upstreams.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No upstreams configured.[/]");
            return;
        }
        AnsiConsole.MarkupLine("\n[bold]Configured upstreams:[/]\n");
        foreach (var u in config.Upstreams)
        {
            var synced = u.LastSynced != null ? $"synced {u.LastSynced.Split('T')[0]}" : "never synced";
            var refStr = u.Ref != null ? $" (ref: {u.Ref})" : "";
            AnsiConsole.MarkupLine("  [bold]{0}[/]  →  {1}: {2}{3}  [dim]({4})[/]",
                u.Name, u.Type, u.Source, refStr, synced);
        }
    }

    private static async Task SyncAsync(string cwd, string? specificName, CancellationToken ct)
    {
        var config = await ReadConfigAsync(cwd, ct);
        var toSync = specificName != null
            ? config.Upstreams.Where(u => u.Name == specificName).ToList()
            : config.Upstreams.ToList();

        if (toSync.Count == 0)
        {
            AnsiConsole.MarkupLine(specificName != null
                ? $"[red]✗[/] Upstream \"{specificName}\" not found."
                : "[dim]No upstreams configured.[/]");
            return;
        }

        AnsiConsole.MarkupLine("\nSyncing {0} upstream(s)...\n", toSync.Count);
        var synced = 0;
        var squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");

        foreach (var upstream in toSync)
        {
            if (upstream.Type is "local" or "export")
            {
                if (!Path.Exists(upstream.Source)) { AnsiConsole.MarkupLine("[yellow]⚠[/] {0}: not found", upstream.Name); continue; }
                synced++;
                AnsiConsole.MarkupLine("[green]✓[/] {0} (read live): validated", upstream.Name);
            }
            else if (upstream.Type == "git")
            {
                var reposDir = Path.Combine(squadDir, "_upstream_repos");
                var cloneDir = Path.Combine(reposDir, upstream.Name);
                Directory.CreateDirectory(reposDir);

                var (cmd, args) = Directory.Exists(Path.Combine(cloneDir, ".git"))
                    ? ("git", new[] { "-C", cloneDir, "pull", "--ff-only" })
                    : ("git", new[] { "clone", "--depth", "1", "--branch", upstream.Ref ?? "main", "--single-branch", upstream.Source, cloneDir });

                var psi = new System.Diagnostics.ProcessStartInfo(cmd)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                foreach (var a in args) psi.ArgumentList.Add(a);
                using var proc = System.Diagnostics.Process.Start(psi);
                await proc!.WaitForExitAsync(ct);

                if (proc.ExitCode == 0)
                {
                    synced++;
                    AnsiConsole.MarkupLine("[green]✓[/] {0} (git — synced)", upstream.Name);
                }
                else
                    AnsiConsole.MarkupLine("[yellow]⚠[/] {0}: git sync failed", upstream.Name);
            }
        }

        AnsiConsole.MarkupLine("\n{0}/{1} upstream(s) synced.\n", synced, toSync.Count);
    }
}

file static class StringExtensions
{
    public static string TrimSuffix(this string s, string suffix) =>
        s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? s[..^suffix.Length] : s;
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
cd ~/NSquad
git add src/Squad.Cli/Commands/UpstreamCommand.cs tests/Squad.Cli.Tests/Commands/UpstreamCommandTests.cs
git commit -m "feat(cli): add upstream command (add/remove/list/sync)"
```

---

## Task 10: `streams` Command (SubSquads)

**Files:**
- Create: `src/Squad.Cli/Commands/StreamsCommand.cs`
- Create: `tests/Squad.Cli.Tests/Commands/StreamsCommandTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Squad.Cli.Tests/Commands/StreamsCommandTests.cs`:
```csharp
using System.Text.Json;
using Squad.Cli.Commands;
using Squad.Sdk.Config;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class StreamsCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad"));
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task LoadConfig_returns_empty_when_no_streams_json()
    {
        var cfg = await StreamsCommand.LoadConfigAsync(_tempDir);
        cfg.Workstreams.ShouldBeEmpty();
    }

    [Test]
    public async Task LoadConfig_parses_streams_json()
    {
        var json = JsonSerializer.Serialize(new
        {
            workstreams = new[]
            {
                new { name = "feature", labelFilter = "squad:feature", workflow = "feature" }
            },
            defaultWorkflow = "feature"
        });
        var path = Path.Combine(_tempDir, ".squad", "streams.json");
        await File.WriteAllTextAsync(path, json);

        var cfg = await StreamsCommand.LoadConfigAsync(_tempDir);
        cfg.Workstreams.Count.ShouldBe(1);
        cfg.Workstreams[0].Name.ShouldBe("feature");
    }

    [Test]
    public void GetActiveStream_returns_null_when_no_workstream_file()
    {
        StreamsCommand.GetActiveStream(_tempDir).ShouldBeNull();
    }

    [Test]
    public void GetActiveStream_returns_name_from_file()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".squad-workstream"), "feature\n");
        StreamsCommand.GetActiveStream(_tempDir).ShouldBe("feature");
    }

    [Test]
    public void ActivateStream_writes_workstream_file()
    {
        StreamsCommand.ActivateStream(_tempDir, "bugfix");
        var active = StreamsCommand.GetActiveStream(_tempDir);
        active.ShouldBe("bugfix");
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: compile error.

- [ ] **Step 3: Implement StreamsCommand**

Create `src/Squad.Cli/Commands/StreamsCommand.cs`:
```csharp
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
        var cwd = Directory.GetCurrentDirectory();
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
                AnsiConsole.MarkupLine("[green]✓[/] Activated SubSquad: [bold]{0}[/]", settings.Name);
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
        var squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");
        var path = Path.Combine(squadDir, "streams.json");
        if (!File.Exists(path)) return new SubSquadsConfig();
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<SubSquadsConfig>(json, _opts) ?? new SubSquadsConfig();
        }
        catch { return new SubSquadsConfig(); }
    }

    public static string? GetActiveStream(string cwd)
    {
        var path = Path.Combine(cwd, ".squad-workstream");
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
        var active = GetActiveStream(cwd);

        if (cfg.Workstreams.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No SubSquads configured. Create .squad/streams.json to define SubSquads.[/]");
            return;
        }

        AnsiConsole.MarkupLine("\n[bold]Configured SubSquads[/]\n");
        AnsiConsole.MarkupLine("  Default workflow: {0}\n", cfg.DefaultWorkflow);

        foreach (var ws in cfg.Workstreams)
        {
            var isActive = active == ws.Name;
            var marker = isActive ? "[green]● active[/]" : "[dim]○[/]";
            AnsiConsole.MarkupLine("  {0}  [bold]{1}[/]", marker, ws.Name);
            AnsiConsole.MarkupLine("       Label: {0}", ws.LabelFilter);
            AnsiConsole.MarkupLine("       Workflow: {0}", ws.Workflow ?? cfg.DefaultWorkflow);
            if (!string.IsNullOrEmpty(ws.Description))
                AnsiConsole.MarkupLine("       [dim]{0}[/]", ws.Description);
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
        var active = GetActiveStream(cwd);

        foreach (var ws in cfg.Workstreams)
        {
            var isActive = active == ws.Name;
            var marker = isActive ? "[green]●[/]" : "[dim]○[/]";
            AnsiConsole.MarkupLine("  {0} [bold]{1}[/] ({2})", marker, ws.Name, ws.LabelFilter);

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
                var stdout = await proc!.StandardOutput.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);
                if (proc.ExitCode == 0)
                {
                    var prs = JsonSerializer.Deserialize<List<JsonElement>>(stdout) ?? new();
                    if (prs.Count > 0)
                        foreach (var pr in prs)
                            AnsiConsole.MarkupLine("    PR #{0}: {1}",
                                pr.GetProperty("number").GetInt32(),
                                pr.GetProperty("title").GetString() ?? "");
                    else
                        AnsiConsole.MarkupLine("    [dim]No open PRs[/]");
                }
            }
            catch { AnsiConsole.MarkupLine("    [dim](gh CLI not available)[/]"); }
            AnsiConsole.WriteLine();
        }
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
cd ~/NSquad
git add src/Squad.Cli/Commands/StreamsCommand.cs tests/Squad.Cli.Tests/Commands/StreamsCommandTests.cs
git commit -m "feat(cli): add streams command (list/status/activate)"
```

---

## Task 11: `schedule` Command

**Files:**
- Create: `src/Squad.Cli/Commands/ScheduleCommand.cs`
- Create: `tests/Squad.Cli.Tests/Commands/ScheduleCommandTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Squad.Cli.Tests/Commands/ScheduleCommandTests.cs`:
```csharp
using System.Text.Json;
using Squad.Cli.Commands;
using Squad.Sdk.Config;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class ScheduleCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad"));
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task Init_creates_schedule_json()
    {
        await ScheduleCommand.InitAsync(_tempDir);
        var path = Path.Combine(_tempDir, ".squad", "schedule.json");
        File.Exists(path).ShouldBeTrue();
    }

    [Test]
    public async Task Init_is_idempotent()
    {
        await ScheduleCommand.InitAsync(_tempDir);
        await ScheduleCommand.InitAsync(_tempDir); // second call should not throw
    }

    [Test]
    public async Task LoadSchedule_returns_template_entries()
    {
        await ScheduleCommand.InitAsync(_tempDir);
        var manifest = await ScheduleCommand.LoadScheduleAsync(_tempDir);
        manifest.Schedules.ShouldNotBeEmpty();
    }

    [Test]
    public async Task LoadSchedule_throws_when_no_file()
    {
        await Should.ThrowAsync<InvalidOperationException>(() =>
            ScheduleCommand.LoadScheduleAsync(_tempDir));
    }

    [Test]
    public void FormatTrigger_cron()
    {
        var entry = new ScheduleEntry { Trigger = new ScheduleTrigger { Type = "cron", Cron = "0 9 * * 1" } };
        ScheduleCommand.FormatTrigger(entry).ShouldContain("0 9 * * 1");
    }

    [Test]
    public void FormatTrigger_interval()
    {
        var entry = new ScheduleEntry { Trigger = new ScheduleTrigger { Type = "interval", IntervalSeconds = 3600 } };
        ScheduleCommand.FormatTrigger(entry).ShouldContain("3600");
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: compile error.

- [ ] **Step 3: Implement ScheduleCommand**

Create `src/Squad.Cli/Commands/ScheduleCommand.cs`:
```csharp
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
        var cwd = Directory.GetCurrentDirectory();
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

    // -----------------------------------------------------------------------
    // Public helpers
    // -----------------------------------------------------------------------

    public static async Task InitAsync(string cwd, CancellationToken ct = default)
    {
        var squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");
        var path = Path.Combine(squadDir, "schedule.json");
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
        var squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");
        var path = Path.Combine(squadDir, "schedule.json");
        if (!File.Exists(path))
            throw new InvalidOperationException("No schedule.json found — run 'squad schedule init' to create one.");
        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<ScheduleManifest>(json, _opts) ?? new ScheduleManifest();
    }

    private static async Task<ScheduleState> LoadStateAsync(string cwd, CancellationToken ct = default)
    {
        var squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");
        var path = Path.Combine(squadDir, ".schedule-state.json");
        if (!File.Exists(path)) return new ScheduleState();
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<ScheduleState>(json, _opts) ?? new ScheduleState();
        }
        catch { return new ScheduleState(); }
    }

    private static async Task SaveStateAsync(string cwd, ScheduleState state, CancellationToken ct = default)
    {
        var squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");
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
        catch (InvalidOperationException ex) { AnsiConsole.MarkupLine("[red]✗[/] {0}", ex.Message); return; }

        if (manifest.Schedules.Count == 0) { AnsiConsole.MarkupLine("[dim]No schedules configured.[/]"); return; }

        AnsiConsole.MarkupLine("\n[bold]Configured Schedules[/] ({0}):\n", manifest.Schedules.Count);
        foreach (var e in manifest.Schedules)
        {
            var status = e.Enabled ? "[green]● enabled[/]" : "[dim]○ disabled[/]";
            AnsiConsole.MarkupLine("  [bold]{0}[/] — {1}", e.Id, e.Name);
            AnsiConsole.MarkupLine("    {0}  │  {1}  │  {2}:{3}", status, FormatTrigger(e), e.Task.Type, e.Task.Ref);
        }
    }

    private static async Task StatusAsync(string cwd, CancellationToken ct)
    {
        ScheduleManifest manifest;
        try { manifest = await LoadScheduleAsync(cwd, ct); }
        catch (InvalidOperationException ex) { AnsiConsole.MarkupLine("[red]✗[/] {0}", ex.Message); return; }

        var state = await LoadStateAsync(cwd, ct);
        AnsiConsole.MarkupLine("\n[bold]Schedule Status[/]\n");

        foreach (var e in manifest.Schedules)
        {
            state.Runs.TryGetValue(e.Id, out var run);
            var statusStr = run == null ? "[dim]– never run[/]"
                : run.Status == "success" ? "[green]✓ success[/]"
                : run.Status == "running" ? "[yellow]⟳ running[/]"
                : "[red]✗ failure[/]";
            var enabledStr = e.Enabled ? "" : " [dim](disabled)[/]";
            AnsiConsole.MarkupLine("  [bold]{0}[/]{1}", e.Id, enabledStr);
            AnsiConsole.MarkupLine("    {0}  │  last: {1}", statusStr, run?.LastRun ?? "–");
            if (run?.Error != null) AnsiConsole.MarkupLine("    [red]error: {0}[/]", run.Error);
        }
    }

    private static async Task<bool> RunAsync(string cwd, string id, CancellationToken ct)
    {
        ScheduleManifest manifest;
        try { manifest = await LoadScheduleAsync(cwd, ct); }
        catch (InvalidOperationException ex) { AnsiConsole.MarkupLine("[red]✗[/] {0}", ex.Message); return false; }

        var entry = manifest.Schedules.FirstOrDefault(s => s.Id == id);
        if (entry == null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Schedule '{0}' not found.", id);
            return false;
        }

        AnsiConsole.MarkupLine("Running schedule: [bold]{0}[/] ({1})...", entry.Name, entry.Id);
        var state = await LoadStateAsync(cwd, ct);

        // Execute: for "print" tasks, just output the ref; for others, echo
        AnsiConsole.MarkupLine("[dim]{0}[/]", entry.Task.Ref);

        state.Runs[id] = new ScheduleRun(DateTimeOffset.UtcNow.ToString("O"), "success");
        await SaveStateAsync(cwd, state, ct);
        AnsiConsole.MarkupLine("[green]✓[/] {0} completed.", entry.Name);
        return true;
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
cd ~/NSquad
git add src/Squad.Cli/Commands/ScheduleCommand.cs tests/Squad.Cli.Tests/Commands/ScheduleCommandTests.cs
git commit -m "feat(cli): add schedule command (init/list/status/run)"
```

---

## Task 12: SquadMarkdownGenerator + `build` Command

**Files:**
- Create: `src/Squad.Sdk/Generation/SquadMarkdownGenerator.cs`
- Create: `src/Squad.Cli/Commands/BuildCommand.cs`
- Create: `tests/Squad.Sdk.Tests/Generation/SquadMarkdownGeneratorTests.cs`
- Create: `tests/Squad.Cli.Tests/Commands/BuildCommandTests.cs`

The `squad build` command reads `squad.config.json` (via `ConfigLoader.LoadAsync()`) and generates `.squad/` markdown.
In .NET, `squad.config.json` is the source-of-truth SDK format (no TypeScript layer needed).

- [ ] **Step 1: Write failing SDK tests**

Create `tests/Squad.Sdk.Tests/Generation/SquadMarkdownGeneratorTests.cs`:
```csharp
using Squad.Sdk.Config;
using Squad.Sdk.Generation;
using Shouldly;

namespace Squad.Sdk.Tests.Generation;

public class SquadMarkdownGeneratorTests
{
    private static SquadConfig MakeConfig(string teamName = "Alpha Team") =>
        new(
            Version: "1.0",
            Team: new TeamConfig(teamName, "A test team"),
            Agents: new List<AgentConfig>
            {
                new("builder", Role: "feature-dev", Model: "claude-sonnet-4.5",
                    Charter: null, Skills: new(), Metadata: new()),
            },
            Routing: new RoutingConfig(
                Rules: new List<RoutingRule>
                {
                    new("test", "tester", new List<string> { "testing" }, 10)
                },
                DefaultAgent: "builder",
                FallbackAgent: null),
            Models: null, Budget: null, Casting: null, Telemetry: null);

    [Test]
    public void Build_generates_team_md()
    {
        var files = SquadMarkdownGenerator.Build(MakeConfig());
        files.ShouldContain(f => f.RelPath == ".squad/team.md");
        var teamMd = files.First(f => f.RelPath == ".squad/team.md").Content;
        teamMd.ShouldContain("Alpha Team");
        teamMd.ShouldContain("## Members");
        teamMd.ShouldContain("builder");
    }

    [Test]
    public void Build_generates_routing_md_when_routing_exists()
    {
        var files = SquadMarkdownGenerator.Build(MakeConfig());
        files.ShouldContain(f => f.RelPath == ".squad/routing.md");
        var routingMd = files.First(f => f.RelPath == ".squad/routing.md").Content;
        routingMd.ShouldContain("builder");
    }

    [Test]
    public void Build_generates_charter_per_agent()
    {
        var files = SquadMarkdownGenerator.Build(MakeConfig());
        files.ShouldContain(f => f.RelPath == ".squad/agents/builder/charter.md");
        var charterMd = files.First(f => f.RelPath == ".squad/agents/builder/charter.md").Content;
        charterMd.ShouldContain("builder");
        charterMd.ShouldContain("feature-dev");
    }

    [Test]
    public void Build_does_not_generate_routing_md_when_no_routing()
    {
        var cfg = MakeConfig() with { Routing = null };
        var files = SquadMarkdownGenerator.Build(cfg);
        files.ShouldNotContain(f => f.RelPath == ".squad/routing.md");
    }

    [Test]
    public void CheckDrift_returns_true_when_files_match()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var files = SquadMarkdownGenerator.Build(MakeConfig());
            SquadMarkdownGenerator.WriteFiles(tempDir, files);
            SquadMarkdownGenerator.CheckDrift(tempDir, files).ShouldBeTrue();
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }

    [Test]
    public void CheckDrift_returns_false_when_file_missing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempDir);
            var files = SquadMarkdownGenerator.Build(MakeConfig());
            // Don't write files — all are missing
            SquadMarkdownGenerator.CheckDrift(tempDir, files).ShouldBeFalse();
        }
        finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
    }
}
```

- [ ] **Step 2: Run SDK tests — expect compile failure**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Sdk.Tests
```

Expected: compile error — `SquadMarkdownGenerator` not found.

- [ ] **Step 3: Implement SquadMarkdownGenerator**

Create `src/Squad.Sdk/Generation/SquadMarkdownGenerator.cs`:
```csharp
using Squad.Sdk.Config;

namespace Squad.Sdk.Generation;

public sealed record GeneratedFile(string RelPath, string Content);

/// <summary>
/// Generates .squad/ markdown from a SquadConfig (squad.config.json).
/// This is the .NET equivalent of the TypeScript "squad build" pipeline.
/// </summary>
public static class SquadMarkdownGenerator
{
    private const string GeneratedHeader = "<!-- generated by squad build — do not edit -->\n\n";
    private static readonly HashSet<string> ProtectedFiles =
        new(StringComparer.OrdinalIgnoreCase) { "decisions.md", "decisions-archive.md", "history.md" };

    public static IReadOnlyList<GeneratedFile> Build(SquadConfig config)
    {
        var files = new List<GeneratedFile>();

        files.Add(new(".squad/team.md", GenerateTeamMd(config)));

        if (config.Routing != null)
            files.Add(new(".squad/routing.md", GenerateRoutingMd(config)));

        foreach (var agent in config.Agents)
            files.Add(new($".squad/agents/{agent.Name}/charter.md", GenerateCharterMd(agent, config)));

        return files;
    }

    private static string GenerateTeamMd(SquadConfig config)
    {
        var sb = new System.Text.StringBuilder(GeneratedHeader);
        sb.AppendLine($"# Squad Team — {config.Team.Name}\n");
        if (!string.IsNullOrEmpty(config.Team.Description))
            sb.AppendLine($"> {config.Team.Description}\n");

        sb.AppendLine("## Members\n");
        sb.AppendLine("| Name | Role | Charter | Status |");
        sb.AppendLine("|------|------|---------|--------|");
        foreach (var agent in config.Agents)
        {
            var name = char.ToUpper(agent.Name[0]) + agent.Name[1..];
            var role = agent.Role ?? agent.Name;
            sb.AppendLine($"| {name} | {role} | `.squad/agents/{agent.Name}/charter.md` | ✅ Active |");
        }
        return sb.ToString();
    }

    private static string GenerateRoutingMd(SquadConfig config)
    {
        var routing = config.Routing!;
        var sb = new System.Text.StringBuilder(GeneratedHeader);
        sb.AppendLine($"# Routing Rules — {config.Team.Name}\n");
        if (routing.DefaultAgent != null)
            sb.AppendLine($"**Default agent:** {routing.DefaultAgent}\n");

        sb.AppendLine("## Rules\n");
        foreach (var rule in routing.Rules)
            sb.AppendLine($"- `{rule.Pattern}` → {rule.Agent}");

        return sb.ToString();
    }

    private static string GenerateCharterMd(AgentConfig agent, SquadConfig config)
    {
        var sb = new System.Text.StringBuilder(GeneratedHeader);
        var name = char.ToUpper(agent.Name[0]) + agent.Name[1..];
        var role = agent.Role ?? agent.Name;
        sb.AppendLine($"# {name} — {role}\n");

        if (!string.IsNullOrEmpty(agent.Charter))
            sb.AppendLine(agent.Charter + "\n");

        if (!string.IsNullOrEmpty(agent.Model))
        {
            sb.AppendLine("## Model\n");
            sb.AppendLine($"**Preferred:** {agent.Model}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns true if all generated files match what is currently on disk.
    /// </summary>
    public static bool CheckDrift(string cwd, IReadOnlyList<GeneratedFile> files)
    {
        foreach (var file in files)
        {
            if (ProtectedFiles.Contains(Path.GetFileName(file.RelPath))) continue;
            var absPath = Path.Combine(cwd, file.RelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absPath)) return false;
            if (File.ReadAllText(absPath) != file.Content) return false;
        }
        return true;
    }

    /// <summary>
    /// Write generated files to disk, skipping protected files.
    /// </summary>
    public static void WriteFiles(string cwd, IReadOnlyList<GeneratedFile> files)
    {
        foreach (var file in files)
        {
            if (ProtectedFiles.Contains(Path.GetFileName(file.RelPath))) continue;
            var absPath = Path.Combine(cwd, file.RelPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);
            File.WriteAllText(absPath, file.Content);
        }
    }
}
```

- [ ] **Step 4: Write CLI build command tests**

Create `tests/Squad.Cli.Tests/Commands/BuildCommandTests.cs`:
```csharp
using Squad.Cli.Commands;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class BuildCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private async Task WriteConfig(string json)
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "squad.config.json"), json);
    }

    [Test]
    public async Task Build_creates_team_md()
    {
        await WriteConfig("""
            {
              "team": { "name": "Test Squad" },
              "agents": [{ "name": "builder", "role": "dev" }]
            }
            """);

        var result = await BuildCommand.BuildAsync(_tempDir);
        result.Written.ShouldBeGreaterThan(0);
        File.Exists(Path.Combine(_tempDir, ".squad", "team.md")).ShouldBeTrue();
    }

    [Test]
    public async Task Build_check_mode_returns_false_when_files_missing()
    {
        await WriteConfig("""
            {
              "team": { "name": "Test Squad" },
              "agents": [{ "name": "builder", "role": "dev" }]
            }
            """);

        var hasDrift = await BuildCommand.CheckDriftAsync(_tempDir);
        hasDrift.ShouldBeTrue(); // Files don't exist yet = drift
    }

    [Test]
    public async Task Build_check_mode_returns_true_after_build()
    {
        await WriteConfig("""
            {
              "team": { "name": "Test Squad" },
              "agents": [{ "name": "builder", "role": "dev" }]
            }
            """);

        await BuildCommand.BuildAsync(_tempDir);
        var hasDrift = await BuildCommand.CheckDriftAsync(_tempDir);
        hasDrift.ShouldBeFalse();
    }

    [Test]
    public async Task Build_throws_when_no_config()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => BuildCommand.BuildAsync(_tempDir));
    }
}
```

- [ ] **Step 5: Implement BuildCommand**

Create `src/Squad.Cli/Commands/BuildCommand.cs`:
```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Config;
using Squad.Sdk.Generation;

namespace Squad.Cli.Commands;

public sealed class BuildCommand : AsyncCommand<BuildCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--check")]
        [Description("Validate without writing. Exit 1 if drift detected.")]
        public bool Check { get; init; }

        [CommandOption("--dry-run")]
        [Description("Show what would be generated without writing.")]
        public bool DryRun { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken ct)
    {
        var cwd = Directory.GetCurrentDirectory();

        if (settings.Check)
        {
            var hasDrift = await CheckDriftAsync(cwd, ct);
            if (!hasDrift)
            {
                AnsiConsole.MarkupLine("[green]✓[/] All generated files match disk — no drift.");
                return 0;
            }
            AnsiConsole.MarkupLine("[red]✗[/] Drift detected. Run [bold]squad build[/] to regenerate.");
            return 1;
        }

        if (settings.DryRun)
        {
            var config = await LoadConfigOrThrow(cwd, ct);
            var files = SquadMarkdownGenerator.Build(config);
            AnsiConsole.MarkupLine("\n[bold]Dry run[/] — would generate {0} file(s):\n", files.Count);
            foreach (var f in files)
            {
                var exists = File.Exists(Path.Combine(cwd, f.RelPath));
                AnsiConsole.MarkupLine("  {0}  {1}",
                    exists ? "[yellow]overwrite[/]" : "[green]create[/]", f.RelPath);
            }
            return 0;
        }

        var result = await BuildAsync(cwd, ct);
        AnsiConsole.MarkupLine("[green]✓[/] squad build complete — generated [bold]{0}[/] file(s).", result.Written);
        return 0;
    }

    public static async Task<BuildResult> BuildAsync(string cwd, CancellationToken ct = default)
    {
        var config = await LoadConfigOrThrow(cwd, ct);
        var files = SquadMarkdownGenerator.Build(config);
        SquadMarkdownGenerator.WriteFiles(cwd, files);
        return new BuildResult(files.Count);
    }

    /// <summary>Returns true if there IS drift (files missing or changed).</summary>
    public static async Task<bool> CheckDriftAsync(string cwd, CancellationToken ct = default)
    {
        var config = await LoadConfigOrThrow(cwd, ct);
        var files = SquadMarkdownGenerator.Build(config);
        return !SquadMarkdownGenerator.CheckDrift(cwd, files);
    }

    private static async Task<SquadConfig> LoadConfigOrThrow(string cwd, CancellationToken ct)
    {
        var config = await ConfigLoader.LoadAsync(cwd, ct);
        if (config == null)
            throw new InvalidOperationException(
                $"No squad.config.json found in {cwd}. Create one first.");
        return config;
    }

    public sealed record BuildResult(int Written);
}
```

- [ ] **Step 6: Run all tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Sdk.Tests && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/Generation/SquadMarkdownGenerator.cs src/Squad.Cli/Commands/BuildCommand.cs \
    tests/Squad.Sdk.Tests/Generation/SquadMarkdownGeneratorTests.cs \
    tests/Squad.Cli.Tests/Commands/BuildCommandTests.cs
git commit -m "feat(sdk,cli): add SquadMarkdownGenerator and build command"
```

---

## Task 13: `migrate` Command

**Files:**
- Create: `src/Squad.Cli/Commands/MigrateCommand.cs`
- Create: `tests/Squad.Cli.Tests/Commands/MigrateCommandTests.cs`

Three modes:
- `--from ai-team`: Renames `.ai-team/` → `.squad/`
- `--to markdown`: Runs squad build (writes/updates `.squad/` from `squad.config.json`)
- `--to sdk`: Parses `.squad/` markdown → generates `squad.config.json`
- No mode: Reports current state

- [ ] **Step 1: Write failing tests**

Create `tests/Squad.Cli.Tests/Commands/MigrateCommandTests.cs`:
```csharp
using Squad.Cli.Commands;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class MigrateCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void DetectMode_none_when_empty()
    {
        MigrateCommand.DetectMode(_tempDir).ShouldBe("none");
    }

    [Test]
    public void DetectMode_sdk_when_config_json_exists()
    {
        File.WriteAllText(Path.Combine(_tempDir, "squad.config.json"), "{}");
        MigrateCommand.DetectMode(_tempDir).ShouldBe("sdk");
    }

    [Test]
    public void DetectMode_markdown_when_squad_dir_exists()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad"));
        MigrateCommand.DetectMode(_tempDir).ShouldBe("markdown");
    }

    [Test]
    public void DetectMode_legacy_when_ai_team_dir_exists()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".ai-team"));
        MigrateCommand.DetectMode(_tempDir).ShouldBe("legacy");
    }

    [Test]
    public void MigrateFromAiTeam_renames_directory()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".ai-team"));
        File.WriteAllText(Path.Combine(_tempDir, ".ai-team", "team.md"), "# Team");

        MigrateCommand.MigrateFromAiTeam(_tempDir);

        Directory.Exists(Path.Combine(_tempDir, ".squad")).ShouldBeTrue();
        Directory.Exists(Path.Combine(_tempDir, ".ai-team")).ShouldBeFalse();
        File.Exists(Path.Combine(_tempDir, ".squad", "team.md")).ShouldBeTrue();
    }

    [Test]
    public void GenerateConfigJson_contains_team_name()
    {
        var teamMd = """
            # Squad Team — Alpha Squad

            ## Members

            | Name | Role | Charter | Status |
            |------|------|---------|--------|
            | Builder | Feature Dev | `.squad/agents/builder/charter.md` | ✅ Active |
            """;
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad"));
        File.WriteAllText(Path.Combine(_tempDir, ".squad", "team.md"), teamMd);

        var json = MigrateCommand.GenerateConfigJson(_tempDir);
        json.ShouldContain("Alpha Squad");
        json.ShouldContain("builder");
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: compile error.

- [ ] **Step 3: Implement MigrateCommand**

Create `src/Squad.Cli/Commands/MigrateCommand.cs`:
```csharp
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Config;

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
        var cwd = Directory.GetCurrentDirectory();
        var mode = DetectMode(cwd);

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
                AnsiConsole.MarkupLine("[dim][DRY RUN][/] Would rename .ai-team/ → .squad/");
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
                var result = await BuildCommand.BuildAsync(cwd, ct);
                AnsiConsole.MarkupLine("[green]✓[/] Generated {0} markdown file(s) from squad.config.json.", result.Written);
                AnsiConsole.MarkupLine("[dim].squad/ is now up to date.[/]");
            }
            else
                AnsiConsole.MarkupLine("[dim][DRY RUN][/] Would run squad build to generate .squad/ markdown.");
            return 0;
        }

        // --to sdk (parse .squad/ markdown → generate squad.config.json)
        if (settings.To?.ToLowerInvariant() == "sdk")
        {
            if (mode == "none") { AnsiConsole.MarkupLine("[red]✗[/] No squad found."); return 1; }
            if (mode == "legacy") { AnsiConsole.MarkupLine("[red]✗[/] Run [bold]squad migrate --from ai-team[/] first."); return 1; }
            if (mode == "sdk") { AnsiConsole.MarkupLine("[red]✗[/] Already in SDK mode (squad.config.json exists)."); return 1; }

            var json = GenerateConfigJson(cwd);
            if (settings.DryRun)
            {
                AnsiConsole.MarkupLine("[dim][DRY RUN][/] Generated squad.config.json:\n");
                AnsiConsole.Write(json);
                return 0;
            }

            var configPath = Path.Combine(cwd, "squad.config.json");
            await File.WriteAllTextAsync(configPath, json, ct);
            AnsiConsole.MarkupLine("[green]✓[/] Created squad.config.json");
            AnsiConsole.MarkupLine("\nNext steps:");
            AnsiConsole.MarkupLine("  1. Review [dim]squad.config.json[/]");
            AnsiConsole.MarkupLine("  2. Run [bold]squad build[/] to verify");
            return 0;
        }

        // No mode — show current state
        AnsiConsole.MarkupLine("\n[bold]Squad Migrate[/] — current mode: [bold]{0}[/]\n", mode);
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
        var src = Path.Combine(cwd, ".ai-team");
        var dst = Path.Combine(cwd, ".squad");
        Directory.Move(src, dst);
    }

    public static string GenerateConfigJson(string cwd)
    {
        var squadDir = Path.Combine(cwd, ".squad");
        var teamName = ParseTeamName(squadDir);
        var members = ParseMembers(squadDir);
        var defaultAgent = members.FirstOrDefault() ?? "builder";
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
        var path = Path.Combine(squadDir, "team.md");
        if (!File.Exists(path)) return "untitled-squad";
        foreach (var line in File.ReadAllLines(path))
        {
            var m = Regex.Match(line, @"# Squad Team — (.+)");
            if (m.Success) return m.Groups[1].Value.Trim();
        }
        return "untitled-squad";
    }

    private static List<string> ParseMembers(string squadDir)
    {
        var path = Path.Combine(squadDir, "team.md");
        if (!File.Exists(path)) return new();
        var members = new List<string>();
        var inMembers = false;
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.TrimStart().StartsWith("## Members")) { inMembers = true; continue; }
            if (inMembers && line.TrimStart().StartsWith("##")) break;
            if (inMembers && line.StartsWith('|') && !line.Contains("---") && !line.Contains("Name"))
            {
                var cells = line.Split('|').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
                if (cells.Length >= 1 && (cells.Length < 4 || cells[3].Contains("Active")))
                    members.Add(cells[0].ToLowerInvariant());
            }
        }
        return members;
    }

    private static string ParseAgentRole(string squadDir, string agentName)
    {
        var charterPath = Path.Combine(squadDir, "agents", agentName, "charter.md");
        if (!File.Exists(charterPath)) return agentName;
        var first = File.ReadAllLines(charterPath).FirstOrDefault(l => l.StartsWith("# ")) ?? "";
        var m = Regex.Match(first, @"# \w+ — (.+)");
        return m.Success ? m.Groups[1].Value.Trim() : agentName;
    }

    private static List<(string pattern, string agent)> ParseRoutingRules(string squadDir)
    {
        var path = Path.Combine(squadDir, "routing.md");
        if (!File.Exists(path)) return new();
        var rules = new List<(string, string)>();
        foreach (var line in File.ReadAllLines(path))
        {
            var m = Regex.Match(line, @"^-\s+`(.+?)`\s*→\s*(\S+)");
            if (m.Success) rules.Add((m.Groups[1].Value, m.Groups[2].Value));
        }
        return rules;
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
cd ~/NSquad
git add src/Squad.Cli/Commands/MigrateCommand.cs tests/Squad.Cli.Tests/Commands/MigrateCommandTests.cs
git commit -m "feat(cli): add migrate command (--from ai-team, --to sdk, --to markdown)"
```

---

## Task 14: IssueTriager + `watch` Command

**Files:**
- Create: `src/Squad.Sdk/Ralph/IssueTriager.cs`
- Create: `src/Squad.Cli/Commands/WatchCommand.cs`
- Create: `tests/Squad.Sdk.Tests/Ralph/IssueTriagerTests.cs`
- Create: `tests/Squad.Cli.Tests/Commands/WatchCommandTests.cs`

`watch` is a long-lived polling loop. The command polls every N minutes (default 5) until Ctrl+C.
It uses `gh issue list` to fetch open issues with `squad` label, then triages unassigned ones.

- [ ] **Step 1: Write failing SDK tests**

Create `tests/Squad.Sdk.Tests/Ralph/IssueTriagerTests.cs`:
```csharp
using Squad.Sdk.Ralph;
using Shouldly;

namespace Squad.Sdk.Tests.Ralph;

public class IssueTriagerTests
{
    private static readonly string SampleTeamMd = """
        # Squad Team — Test

        ## Members

        | Name | Role | Charter | Status |
        |------|------|---------|--------|
        | Builder | Feature Dev | `.squad/agents/builder/charter.md` | ✅ Active |
        | Tester | Test Engineer | `.squad/agents/tester/charter.md` | ✅ Active |
        """;

    [Test]
    public void ParseRoster_returns_members()
    {
        var roster = IssueTriager.ParseRoster(SampleTeamMd);
        roster.Count.ShouldBe(2);
        roster.ShouldContain(m => m.Name == "builder");
        roster.ShouldContain(m => m.Name == "tester");
    }

    [Test]
    public void ParseRoster_assigns_squad_labels()
    {
        var roster = IssueTriager.ParseRoster(SampleTeamMd);
        var builder = roster.First(m => m.Name == "builder");
        builder.Label.ShouldBe("squad:builder");
    }

    [Test]
    public void Triage_assigns_tester_for_test_issue()
    {
        var roster = IssueTriager.ParseRoster(SampleTeamMd);
        var rules = new List<IssueRoutingRule>
        {
            new("test", "tester"),
            new("feature", "builder"),
        };

        var result = IssueTriager.Triage("Add test coverage", null, new[] { "squad" }, rules, roster);
        result.ShouldNotBeNull();
        result!.Agent.Name.ShouldBe("tester");
    }

    [Test]
    public void Triage_returns_null_when_no_match()
    {
        var roster = IssueTriager.ParseRoster(SampleTeamMd);
        var rules = new List<IssueRoutingRule> { new("test", "tester") };

        var result = IssueTriager.Triage("Random unrelated issue", null, new[] { "squad" }, rules, roster);
        result.ShouldBeNull();
    }

    [Test]
    public void Triage_returns_null_when_roster_empty()
    {
        var result = IssueTriager.Triage("Add feature", null, new[] { "squad" },
            new List<IssueRoutingRule>(), new List<RosterMember>());
        result.ShouldBeNull();
    }
}
```

- [ ] **Step 2: Run SDK tests — expect compile failure**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Sdk.Tests
```

Expected: compile error — `IssueTriager` not found.

- [ ] **Step 3: Implement IssueTriager**

Create `src/Squad.Sdk/Ralph/IssueTriager.cs`:
```csharp
using System.Text.RegularExpressions;

namespace Squad.Sdk.Ralph;

public sealed record RosterMember(string Name, string Label, string Role);
public sealed record TriageResult(RosterMember Agent, string Reason);
public sealed record IssueRoutingRule(string Pattern, string AgentName);

public static class IssueTriager
{
    /// <summary>
    /// Parse the ## Members table from team.md content.
    /// Returns one RosterMember per active row.
    /// </summary>
    public static IReadOnlyList<RosterMember> ParseRoster(string teamMdContent)
    {
        var members = new List<RosterMember>();
        var inMembers = false;

        foreach (var line in teamMdContent.Split('\n'))
        {
            if (line.TrimStart().StartsWith("## Members")) { inMembers = true; continue; }
            if (inMembers && line.TrimStart().StartsWith("##")) break;
            if (!inMembers || !line.StartsWith('|') || line.Contains("---") || line.Contains("Name")) continue;

            var cells = line.Split('|').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            if (cells.Length < 2) continue;
            var status = cells.Length >= 4 ? cells[3] : "Active";
            if (!status.Contains("Active")) continue;

            var name = cells[0].ToLowerInvariant();
            var role = cells[1];
            members.Add(new RosterMember(name, $"squad:{name}", role));
        }

        return members;
    }

    /// <summary>
    /// Triage an issue against routing rules and roster.
    /// Returns the first matching assignment, or null if no match.
    /// </summary>
    public static TriageResult? Triage(
        string title,
        string? body,
        IEnumerable<string> labels,
        IReadOnlyList<IssueRoutingRule> rules,
        IReadOnlyList<RosterMember> roster)
    {
        if (roster.Count == 0) return null;

        var text = (title + " " + (body ?? "")).ToLowerInvariant();

        foreach (var rule in rules)
        {
            if (!Regex.IsMatch(text, rule.Pattern, RegexOptions.IgnoreCase)) continue;
            var agent = roster.FirstOrDefault(m =>
                m.Name.Equals(rule.AgentName, StringComparison.OrdinalIgnoreCase));
            if (agent != null)
                return new TriageResult(agent, $"matched pattern: {rule.Pattern}");
        }

        return null;
    }
}
```

- [ ] **Step 4: Write watch command tests**

Create `tests/Squad.Cli.Tests/Commands/WatchCommandTests.cs`:
```csharp
using Squad.Cli.Commands;
using Squad.Sdk.Ralph;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class WatchCommandTests
{
    private static readonly string SampleTeamMd = """
        # Squad Team — Test

        ## Members

        | Name | Role | Charter | Status |
        |------|------|---------|--------|
        | Builder | Feature Dev | `.squad/agents/builder/charter.md` | ✅ Active |
        """;

    [Test]
    public void ParseRoster_uses_IssueTriager()
    {
        var roster = IssueTriager.ParseRoster(SampleTeamMd);
        roster.ShouldNotBeEmpty();
    }

    [Test]
    public void ValidateInterval_throws_for_zero()
    {
        Should.Throw<ArgumentException>(() => WatchCommand.ValidateInterval(0));
    }

    [Test]
    public void ValidateInterval_throws_for_negative()
    {
        Should.Throw<ArgumentException>(() => WatchCommand.ValidateInterval(-1));
    }

    [Test]
    public void ValidateInterval_accepts_positive()
    {
        WatchCommand.ValidateInterval(5); // Should not throw
    }

    [Test]
    public void FormatBoardLine_returns_string_for_positive_count()
    {
        WatchCommand.FormatBoardLine("Untriaged", 3).ShouldContain("3");
    }

    [Test]
    public void FormatBoardLine_returns_null_for_zero()
    {
        WatchCommand.FormatBoardLine("Untriaged", 0).ShouldBeNull();
    }
}
```

- [ ] **Step 5: Implement WatchCommand**

Create `src/Squad.Cli/Commands/WatchCommand.cs`:
```csharp
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
                            timestamp, issue.Number, issue.Title, result.Agent.Name);
                    }
                }
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            AnsiConsole.MarkupLine("[red]✗[/] [{0}] Check failed: {1}", timestamp, ex.Message);
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
```

- [ ] **Step 6: Run all tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Sdk.Tests && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/Ralph/IssueTriager.cs src/Squad.Cli/Commands/WatchCommand.cs \
    tests/Squad.Sdk.Tests/Ralph/IssueTriagerTests.cs \
    tests/Squad.Cli.Tests/Commands/WatchCommandTests.cs
git commit -m "feat(sdk,cli): add IssueTriager and watch command"
```

---

## Task 15: `discover` and `delegate` Commands

**Files:**
- Create: `src/Squad.Sdk/Discovery/SquadDiscovery.cs`
- Create: `src/Squad.Cli/Commands/DiscoverCommand.cs`
- Create: `src/Squad.Cli/Commands/DelegateCommand.cs`
- Create: `tests/Squad.Sdk.Tests/Discovery/SquadDiscoveryTests.cs`
- Create: `tests/Squad.Cli.Tests/Commands/DiscoverCommandTests.cs`

Discovery reads upstream.json and loads the `squad.manifest.json` from each upstream's `.squad/` directory.

- [ ] **Step 1: Write failing SDK tests**

Create `tests/Squad.Sdk.Tests/Discovery/SquadDiscoveryTests.cs`:
```csharp
using Squad.Sdk.Discovery;
using Shouldly;

namespace Squad.Sdk.Tests.Discovery;

public class SquadDiscoveryTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad"));
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task Discover_returns_empty_when_no_upstream_json()
    {
        var squads = await SquadDiscovery.DiscoverAsync(_tempDir);
        squads.ShouldBeEmpty();
    }

    [Test]
    public async Task Discover_finds_squads_with_manifest()
    {
        // Create a fake upstream squad directory with manifest
        var upstreamDir = Path.Combine(_tempDir, "other-squad");
        Directory.CreateDirectory(Path.Combine(upstreamDir, ".squad"));
        var manifest = """{"name":"other-team","accepts":["issues"],"contact":{"repo":"owner/other","labels":["squad"]}}""";
        await File.WriteAllTextAsync(Path.Combine(upstreamDir, ".squad", "squad.manifest.json"), manifest);

        // Write upstream.json pointing to it
        var upstreamJson = $$$"""
            {{
              "upstreams": [
                {{"name":"other","type":"local","source":"{{{upstreamDir}}}","addedAt":"2026-01-01T00:00:00Z"}}
              ]
            }}
            """;
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".squad", "upstream.json"), upstreamJson);

        var squads = await SquadDiscovery.DiscoverAsync(_tempDir);
        squads.Count.ShouldBe(1);
        squads[0].Manifest.Name.ShouldBe("other-team");
    }
}
```

- [ ] **Step 2: Run SDK tests — expect compile failure**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Sdk.Tests
```

Expected: compile error.

- [ ] **Step 3: Implement SquadDiscovery**

Create `src/Squad.Sdk/Discovery/SquadDiscovery.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Squad.Sdk.Config;

namespace Squad.Sdk.Discovery;

public sealed record SquadContact(string Repo, List<string> Labels);
public sealed record SquadManifest(string Name, List<string> Accepts, SquadContact Contact);
public sealed record DiscoveredSquad(string SquadDir, SquadManifest Manifest);

public static class SquadDiscovery
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Discover squads registered as local upstreams that have a squad.manifest.json.
    /// </summary>
    public static async Task<IReadOnlyList<DiscoveredSquad>> DiscoverAsync(
        string cwd, CancellationToken ct = default)
    {
        var upstreamPath = Path.Combine(cwd, ".squad", "upstream.json");
        if (!File.Exists(upstreamPath)) return Array.Empty<DiscoveredSquad>();

        UpstreamConfig config;
        try
        {
            var json = await File.ReadAllTextAsync(upstreamPath, ct);
            config = JsonSerializer.Deserialize<UpstreamConfig>(json, _opts) ?? new UpstreamConfig();
        }
        catch { return Array.Empty<DiscoveredSquad>(); }

        var discovered = new List<DiscoveredSquad>();
        foreach (var upstream in config.Upstreams.Where(u => u.Type == "local"))
        {
            var manifestPath = Path.Combine(upstream.Source, ".squad", "squad.manifest.json");
            if (!File.Exists(manifestPath)) continue;
            try
            {
                var json = await File.ReadAllTextAsync(manifestPath, ct);
                var manifest = JsonSerializer.Deserialize<SquadManifest>(json, _opts);
                if (manifest != null)
                    discovered.Add(new DiscoveredSquad(Path.Combine(upstream.Source, ".squad"), manifest));
            }
            catch { /* skip unreadable */ }
        }

        return discovered;
    }

    public static string FormatTable(IReadOnlyList<DiscoveredSquad> squads)
    {
        if (squads.Count == 0) return "No squads discovered.";
        var sb = new System.Text.StringBuilder("\nDiscovered Squads:\n\n");
        foreach (var s in squads)
        {
            sb.AppendLine($"  {s.Manifest.Name}");
            sb.AppendLine($"    Accepts: {string.Join(", ", s.Manifest.Accepts)}");
            sb.AppendLine($"    Repo: {s.Manifest.Contact.Repo}");
        }
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Write CLI tests**

Create `tests/Squad.Cli.Tests/Commands/DiscoverCommandTests.cs`:
```csharp
using Squad.Sdk.Discovery;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class DiscoverCommandTests
{
    [Test]
    public void FormatTable_returns_message_for_empty()
    {
        SquadDiscovery.FormatTable(Array.Empty<DiscoveredSquad>()).ShouldContain("No squads");
    }

    [Test]
    public void FormatTable_includes_squad_name()
    {
        var manifest = new SquadManifest("alpha-team", new List<string> { "issues" },
            new SquadContact("owner/alpha", new List<string> { "squad" }));
        var squads = new[] { new DiscoveredSquad(".squad", manifest) };
        SquadDiscovery.FormatTable(squads).ShouldContain("alpha-team");
    }
}
```

- [ ] **Step 5: Implement DiscoverCommand and DelegateCommand**

Create `src/Squad.Cli/Commands/DiscoverCommand.cs`:
```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using Squad.Sdk.Discovery;

namespace Squad.Cli.Commands;

public sealed class DiscoverCommand : AsyncCommand
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken ct)
    {
        var cwd = Directory.GetCurrentDirectory();
        var squads = await SquadDiscovery.DiscoverAsync(cwd, ct);
        AnsiConsole.Write(SquadDiscovery.FormatTable(squads));
        return 0;
    }
}
```

Create `src/Squad.Cli/Commands/DelegateCommand.cs`:
```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.Discovery;

namespace Squad.Cli.Commands;

public sealed class DelegateCommand : AsyncCommand<DelegateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<squad-name>")]
        [Description("Target squad name (from squad discover).")]
        public string SquadName { get; init; } = "";

        [CommandArgument(1, "<description>")]
        [Description("Work description to delegate.")]
        public string Description { get; init; } = "";
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken ct)
    {
        var cwd = Directory.GetCurrentDirectory();
        var squads = await SquadDiscovery.DiscoverAsync(cwd, ct);
        var target = squads.FirstOrDefault(s =>
            s.Manifest.Name.Equals(settings.SquadName, StringComparison.OrdinalIgnoreCase));

        if (target == null)
        {
            var names = string.Join(", ", squads.Select(s => s.Manifest.Name));
            AnsiConsole.MarkupLine("[red]✗[/] Squad \"{0}\" not found.{1}",
                settings.SquadName,
                names.Length > 0 ? $" Known squads: {names}" : " No squads discovered.");
            return 1;
        }

        if (!target.Manifest.Accepts.Contains("issues"))
        {
            AnsiConsole.MarkupLine("[red]✗[/] Squad \"{0}\" does not accept issues. Accepts: {1}",
                settings.SquadName, string.Join(", ", target.Manifest.Accepts));
            return 1;
        }

        var title = $"[cross-squad] {settings.Description}";
        var body = $"""
            ## Cross-Squad Work Request

            **To:** {target.Manifest.Name} ({target.Manifest.Contact.Repo})

            ### Description

            {settings.Description}

            ### Acceptance Criteria

            - [ ] Work completed and verified
            - [ ] Originating squad notified of completion
            """;

        var labels = string.Join(",", target.Manifest.Contact.Labels.Append("cross-squad"));
        var psi = new System.Diagnostics.ProcessStartInfo("gh",
            $"issue create --repo {target.Manifest.Contact.Repo} --title \"{EscapeArg(title)}\" --body \"{EscapeArg(body)}\" --label \"{labels}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc == null) { AnsiConsole.MarkupLine("[red]✗[/] gh CLI not found."); return 1; }
        var url = (await proc.StandardOutput.ReadToEndAsync(ct)).Trim();
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode == 0)
            AnsiConsole.MarkupLine("[green]✓[/] Created cross-squad issue: {0}", url);
        else
        {
            AnsiConsole.MarkupLine("[red]✗[/] Failed to create issue.");
            return 1;
        }
        return 0;
    }

    private static string EscapeArg(string s) => s.Replace("\"", "\\\"").Replace("\n", "\\n");
}
```

- [ ] **Step 6: Run all tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Sdk.Tests && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/Discovery/SquadDiscovery.cs src/Squad.Cli/Commands/DiscoverCommand.cs \
    src/Squad.Cli/Commands/DelegateCommand.cs \
    tests/Squad.Sdk.Tests/Discovery/SquadDiscoveryTests.cs \
    tests/Squad.Cli.Tests/Commands/DiscoverCommandTests.cs
git commit -m "feat(sdk,cli): add SquadDiscovery, discover and delegate commands"
```

---

## Task 16: LicenseDetector + StagedLearnings + `extract` Command

**Files:**
- Create: `src/Squad.Sdk/PersonalSquad/LicenseDetector.cs`
- Create: `src/Squad.Sdk/PersonalSquad/StagedLearnings.cs`
- Create: `src/Squad.Cli/Commands/ExtractCommand.cs`
- Create: `tests/Squad.Sdk.Tests/PersonalSquad/LicenseDetectorTests.cs`
- Create: `tests/Squad.Sdk.Tests/PersonalSquad/StagedLearningsTests.cs`
- Create: `tests/Squad.Cli.Tests/Commands/ExtractCommandTests.cs`

`extract` reads `.squad/extract/*.md`, optionally prompts for selection (skipped with `--yes`), merges to personal squad, logs consultation.

- [ ] **Step 1: Write failing SDK tests**

Create `tests/Squad.Sdk.Tests/PersonalSquad/LicenseDetectorTests.cs`:
```csharp
using Squad.Sdk.PersonalSquad;
using Shouldly;

namespace Squad.Sdk.Tests.PersonalSquad;

public class LicenseDetectorTests
{
    [Test]
    public void Detect_mit_is_permissive()
    {
        var result = LicenseDetector.Detect("MIT License\n\nCopyright...");
        result.Type.ShouldBe("permissive");
        result.SpdxId.ShouldBe("MIT");
    }

    [Test]
    public void Detect_apache2_is_permissive()
    {
        var result = LicenseDetector.Detect("Apache License, Version 2.0");
        result.Type.ShouldBe("permissive");
        result.SpdxId.ShouldBe("Apache-2.0");
    }

    [Test]
    public void Detect_gpl_is_copyleft()
    {
        var result = LicenseDetector.Detect("GNU GENERAL PUBLIC LICENSE\nVersion 3");
        result.Type.ShouldBe("copyleft");
        result.SpdxId.ShouldBe("GPL-3.0");
    }

    [Test]
    public void Detect_gpl2_is_copyleft()
    {
        var result = LicenseDetector.Detect("GNU GENERAL PUBLIC LICENSE\nVersion 2");
        result.Type.ShouldBe("copyleft");
    }

    [Test]
    public void Detect_unknown_on_empty()
    {
        var result = LicenseDetector.Detect("");
        result.Type.ShouldBe("unknown");
        result.SpdxId.ShouldBeNull();
    }

    [Test]
    public void Detect_bsd_is_permissive()
    {
        var result = LicenseDetector.Detect("BSD 3-Clause License");
        result.Type.ShouldBe("permissive");
    }
}
```

Create `tests/Squad.Sdk.Tests/PersonalSquad/StagedLearningsTests.cs`:
```csharp
using Squad.Sdk.PersonalSquad;
using Shouldly;

namespace Squad.Sdk.Tests.PersonalSquad;

public class StagedLearningsTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad", "extract"));
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void Load_returns_empty_when_no_extract_dir()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(emptyDir, ".squad"));
        var result = StagedLearnings.Load(emptyDir);
        result.ShouldBeEmpty();
    }

    [Test]
    public void Load_returns_md_files_from_extract_dir()
    {
        var extractDir = Path.Combine(_tempDir, ".squad", "extract");
        File.WriteAllText(Path.Combine(extractDir, "learning1.md"), "# Pattern X\n\nAlways use X.");
        File.WriteAllText(Path.Combine(extractDir, "learning2.md"), "# Pattern Y\n\nPrefer Y.");

        var learnings = StagedLearnings.Load(_tempDir);
        learnings.Count.ShouldBe(2);
        learnings.ShouldContain(l => l.Filename == "learning1.md");
    }

    [Test]
    public async Task MergeToPersonalSquad_appends_to_decisions_md()
    {
        var personalDir = Path.Combine(_tempDir, "personal-squad");
        Directory.CreateDirectory(personalDir);
        File.WriteAllText(Path.Combine(personalDir, "decisions.md"), "# Decisions\n");

        var learnings = new List<StagedLearning>
        {
            new("test.md", "/tmp/test.md", "## New Learning\n\nUse async/await.")
        };

        var result = await StagedLearnings.MergeAsync(learnings, personalDir);
        result.Decisions.ShouldBe(1);

        var decisions = File.ReadAllText(Path.Combine(personalDir, "decisions.md"));
        decisions.ShouldContain("Use async/await.");
    }
}
```

- [ ] **Step 2: Run SDK tests — expect compile failure**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Sdk.Tests
```

Expected: compile error — `LicenseDetector`, `StagedLearnings` not found.

- [ ] **Step 3: Implement LicenseDetector**

Create `src/Squad.Sdk/PersonalSquad/LicenseDetector.cs`:
```csharp
using System.Text.RegularExpressions;

namespace Squad.Sdk.PersonalSquad;

public sealed record LicenseInfo(string Type, string? SpdxId = null);

/// <summary>
/// Detects license type from LICENSE file content.
/// Type: "permissive" | "copyleft" | "unknown"
/// </summary>
public static class LicenseDetector
{
    public static LicenseInfo Detect(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return new("unknown");

        var upper = content.ToUpperInvariant();

        // Copyleft — check before permissive (GPL text is more specific)
        if (upper.Contains("GNU GENERAL PUBLIC LICENSE"))
        {
            var spdx = upper.Contains("VERSION 3") ? "GPL-3.0"
                     : upper.Contains("VERSION 2") ? "GPL-2.0"
                     : "GPL";
            return new("copyleft", spdx);
        }
        if (upper.Contains("GNU LESSER GENERAL PUBLIC LICENSE") || upper.Contains("LGPL"))
            return new("copyleft", "LGPL");
        if (upper.Contains("GNU AFFERO GENERAL PUBLIC LICENSE"))
            return new("copyleft", "AGPL-3.0");
        if (Regex.IsMatch(upper, @"\bMOZILLA PUBLIC LICENSE\b"))
            return new("copyleft", "MPL-2.0");

        // Permissive
        if (upper.Contains("MIT LICENSE") || Regex.IsMatch(content, @"\bMIT\b"))
            return new("permissive", "MIT");
        if (upper.Contains("APACHE LICENSE") && upper.Contains("2.0"))
            return new("permissive", "Apache-2.0");
        if (upper.Contains("BSD") && (upper.Contains("3-CLAUSE") || upper.Contains("2-CLAUSE")))
            return new("permissive", upper.Contains("3") ? "BSD-3-Clause" : "BSD-2-Clause");
        if (upper.Contains("ISC LICENSE") || upper.Contains("ISC"))
            return new("permissive", "ISC");

        return new("unknown");
    }
}
```

- [ ] **Step 4: Implement StagedLearnings**

Create `src/Squad.Sdk/PersonalSquad/StagedLearnings.cs`:
```csharp
namespace Squad.Sdk.PersonalSquad;

public sealed record StagedLearning(string Filename, string Filepath, string Content);
public sealed record MergeResult(int Decisions);

public static class StagedLearnings
{
    /// <summary>
    /// Load staged learning files from .squad/extract/*.md
    /// </summary>
    public static IReadOnlyList<StagedLearning> Load(string cwd)
    {
        var extractDir = Path.Combine(cwd, ".squad", "extract");
        if (!Directory.Exists(extractDir)) return Array.Empty<StagedLearning>();

        return Directory.GetFiles(extractDir, "*.md")
            .OrderBy(f => f)
            .Select(f => new StagedLearning(Path.GetFileName(f), f, File.ReadAllText(f)))
            .ToList();
    }

    /// <summary>
    /// Merge learnings into personal squad's decisions.md
    /// </summary>
    public static async Task<MergeResult> MergeAsync(
        IEnumerable<StagedLearning> learnings, string personalSquadDir, CancellationToken ct = default)
    {
        var decisionsPath = Path.Combine(personalSquadDir, "decisions.md");
        var existing = File.Exists(decisionsPath) ? await File.ReadAllTextAsync(decisionsPath, ct) : "# Decisions\n";

        var newEntries = new System.Text.StringBuilder();
        var count = 0;
        foreach (var l in learnings)
        {
            newEntries.AppendLine($"\n---\n");
            newEntries.AppendLine($"*Extracted: {DateTimeOffset.UtcNow:O}*\n");
            newEntries.AppendLine(l.Content);
            count++;
        }

        if (count > 0)
        {
            var updated = existing.TrimEnd() + "\n" + newEntries;
            await File.WriteAllTextAsync(decisionsPath, updated, ct);
        }

        return new MergeResult(count);
    }
}
```

- [ ] **Step 5: Write extract command tests**

Create `tests/Squad.Cli.Tests/Commands/ExtractCommandTests.cs`:
```csharp
using Squad.Cli.Commands;
using Squad.Sdk.PersonalSquad;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class ExtractCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad", "extract"));

        // Write consult mode config
        File.WriteAllText(Path.Combine(_tempDir, ".squad", "config.json"),
            """{"version":1,"consultMode":true,"sourceSquad":"/tmp/personal-squad"}""");
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void IsConsultMode_returns_true_when_flag_set()
    {
        ExtractCommand.IsConsultMode(Path.Combine(_tempDir, ".squad")).ShouldBeTrue();
    }

    [Test]
    public void IsConsultMode_returns_false_when_no_config()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(emptyDir, ".squad"));
        ExtractCommand.IsConsultMode(Path.Combine(emptyDir, ".squad")).ShouldBeFalse();
        if (Directory.Exists(emptyDir)) Directory.Delete(emptyDir, true);
    }

    [Test]
    public void FormatLearningPreview_truncates_at_50_chars()
    {
        var content = new string('x', 100);
        var preview = ExtractCommand.FormatLearningPreview(content);
        preview.Length.ShouldBeLessThanOrEqualTo(60); // 50 + "..."
    }

    [Test]
    public async Task ExtractLearnings_with_yes_flag_merges_without_prompt()
    {
        var personalDir = Path.Combine(_tempDir, "personal-squad");
        Directory.CreateDirectory(personalDir);
        File.WriteAllText(Path.Combine(personalDir, "decisions.md"), "# Decisions\n");

        // Stage a learning
        File.WriteAllText(Path.Combine(_tempDir, ".squad", "extract", "pattern.md"),
            "## Pattern X\n\nUse pattern X always.");

        await ExtractCommand.ExtractAsync(_tempDir, personalDir, yes: true);

        var decisions = File.ReadAllText(Path.Combine(personalDir, "decisions.md"));
        decisions.ShouldContain("Use pattern X always.");
    }
}
```

- [ ] **Step 6: Implement ExtractCommand**

Create `src/Squad.Cli/Commands/ExtractCommand.cs`:
```csharp
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Squad.Sdk.PersonalSquad;

namespace Squad.Cli.Commands;

public sealed class ExtractCommand : AsyncCommand<ExtractCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--yes|-y")]
        [Description("Accept all staged learnings without prompting.")]
        public bool Yes { get; init; }

        [CommandOption("--dry-run")]
        [Description("Preview what would be extracted without writing.")]
        public bool DryRun { get; init; }

        [CommandOption("--clean")]
        [Description("Delete .squad/ after extraction.")]
        public bool Clean { get; init; }

        [CommandOption("--accept-risks")]
        [Description("Proceed even with copyleft licenses.")]
        public bool AcceptRisks { get; init; }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken ct)
    {
        var cwd = Directory.GetCurrentDirectory();
        var squadDir = Path.Combine(cwd, ".squad");

        if (!Directory.Exists(squadDir))
        {
            AnsiConsole.MarkupLine("[red]✗[/] No .squad/config.json found. Run [bold]squad consult[/] first.");
            return 1;
        }

        if (!IsConsultMode(squadDir))
        {
            AnsiConsole.MarkupLine("[red]✗[/] Not in consult mode. This command only works after [bold]squad consult[/].");
            return 1;
        }

        var sourceSquad = GetSourceSquad(squadDir);
        if (sourceSquad == null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Missing sourceSquad in .squad/config.json.");
            return 1;
        }

        // License check
        var licensePath = Path.Combine(cwd, "LICENSE");
        var license = File.Exists(licensePath)
            ? LicenseDetector.Detect(await File.ReadAllTextAsync(licensePath, ct))
            : new LicenseInfo("unknown");

        if (license.Type == "copyleft" && !settings.AcceptRisks)
        {
            AnsiConsole.MarkupLine("[red]🚫[/] License: {0} — Extraction blocked. Use --accept-risks to override.",
                license.SpdxId ?? "copyleft");
            return 1;
        }

        // Load staged learnings
        var staged = StagedLearnings.Load(cwd);
        if (staged.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]📭 No learnings staged for extraction.[/]");
            if (settings.Clean && settings.Yes)
            {
                Directory.Delete(squadDir, recursive: true);
                AnsiConsole.MarkupLine("[dim]🗑️  Deleted .squad/[/]");
            }
            return 0;
        }

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[dim]📋 Dry-run — {0} learning(s) staged:[/]", staged.Count);
            foreach (var l in staged)
                AnsiConsole.MarkupLine("   - {0}: \"{1}\"", l.Filename, FormatLearningPreview(l.Content));
            return 0;
        }

        // Select learnings
        IReadOnlyList<StagedLearning> toExtract = staged;
        if (!settings.Yes)
            toExtract = await PromptSelectionAsync(staged, ct);

        if (toExtract.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No learnings selected.[/]");
            return 0;
        }

        await ExtractAsync(cwd, sourceSquad, yes: true, ct, toExtract);
        return 0;
    }

    public static bool IsConsultMode(string squadDir)
    {
        var configPath = Path.Combine(squadDir, "config.json");
        if (!File.Exists(configPath)) return false;
        try
        {
            var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            return doc.RootElement.TryGetProperty("consultMode", out var v) && v.GetBoolean();
        }
        catch { return false; }
    }

    public static string? GetSourceSquad(string squadDir)
    {
        var configPath = Path.Combine(squadDir, "config.json");
        if (!File.Exists(configPath)) return null;
        try
        {
            var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            return doc.RootElement.TryGetProperty("sourceSquad", out var v) ? v.GetString() : null;
        }
        catch { return null; }
    }

    public static string FormatLearningPreview(string content)
    {
        var preview = content.Replace('\n', ' ').Trim();
        return preview.Length > 50 ? preview[..50] + "..." : preview;
    }

    public static async Task ExtractAsync(
        string cwd, string personalSquadDir, bool yes,
        CancellationToken ct = default,
        IReadOnlyList<StagedLearning>? learnings = null)
    {
        learnings ??= StagedLearnings.Load(cwd);
        var result = await StagedLearnings.MergeAsync(learnings, personalSquadDir, ct);

        // Remove extracted files
        foreach (var l in learnings)
            if (File.Exists(l.Filepath)) File.Delete(l.Filepath);

        AnsiConsole.MarkupLine("[green]✓[/] Extraction complete — {0} learning(s) merged.", result.Decisions);
    }

    private static async Task<IReadOnlyList<StagedLearning>> PromptSelectionAsync(
        IReadOnlyList<StagedLearning> learnings, CancellationToken ct)
    {
        AnsiConsole.MarkupLine("[bold]{0} learning(s) staged:[/]\n", learnings.Count);
        for (int i = 0; i < learnings.Count; i++)
            AnsiConsole.MarkupLine("  [{0}] {1}. {2}: \"{3}\"",
                "✓", i + 1, learnings[i].Filename, FormatLearningPreview(learnings[i].Content));

        AnsiConsole.Write("\nAccept all? [Y/n] ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (input == "n" || input == "no") return Array.Empty<StagedLearning>();
        return learnings;
    }
}
```

- [ ] **Step 7: Run all tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Sdk.Tests && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass.

- [ ] **Step 8: Commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/PersonalSquad/LicenseDetector.cs src/Squad.Sdk/PersonalSquad/StagedLearnings.cs \
    src/Squad.Cli/Commands/ExtractCommand.cs \
    tests/Squad.Sdk.Tests/PersonalSquad/LicenseDetectorTests.cs \
    tests/Squad.Sdk.Tests/PersonalSquad/StagedLearningsTests.cs \
    tests/Squad.Cli.Tests/Commands/ExtractCommandTests.cs
git commit -m "feat(sdk,cli): add LicenseDetector, StagedLearnings, and extract command"
```

---

## Task 17: Register All Commands in Program.cs + Final Build Verification

**Files:**
- Modify: `src/Squad.Cli/Program.cs`
- Modify: `src/Squad.Cli/Squad.Cli.csproj` (verify no missing package refs)

Register all 20+ new commands in Spectre.Console.Cli. Commands with subcommands use `AddBranch`.

- [ ] **Step 1: Write the failing test**

Create `tests/Squad.Cli.Tests/Commands/ProgramRegistrationTests.cs`:
```csharp
using Spectre.Console.Cli;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class ProgramRegistrationTests
{
    [Test]
    public void CommandApp_builds_without_exception()
    {
        // If all command types are registered correctly, this will not throw
        var app = SquadCliApp.Build();
        app.ShouldNotBeNull();
    }
}
```

Create `src/Squad.Cli/SquadCliApp.cs`:
```csharp
using Spectre.Console.Cli;
using Squad.Cli.Commands;
using Squad.Cli.Commands.Personal;
using Squad.Cli.Commands.Plugin;

namespace Squad.Cli;

/// <summary>
/// Builds the CommandApp. Extracted from Program.cs for testability.
/// </summary>
public static class SquadCliApp
{
    public static CommandApp Build()
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("squad");
            config.SetApplicationVersion("0.2.0");

            // Plan 1 commands
            config.AddCommand<DoctorCommand>("doctor")
                  .WithDescription("Validate .squad/ setup and report health checks.");
            config.AddCommand<CastCommand>("cast")
                  .WithDescription("Show the current session cast (project + personal agents).");
            config.AddCommand<CostCommand>("cost")
                  .WithDescription("Show token usage and estimated cost per agent.");

            // Plan 2 commands
            config.AddCommand<BuildCommand>("build")
                  .WithDescription("Generate .squad/ markdown from squad.config.json.");
            config.AddCommand<ExportCommand>("export")
                  .WithDescription("Export squad state to squad-export.json.");
            config.AddCommand<ImportCommand>("import")
                  .WithDescription("Import squad from squad-export.json.");
            config.AddCommand<MigrateCommand>("migrate")
                  .WithDescription("Migrate between squad formats (--to sdk | markdown | --from ai-team).");
            config.AddCommand<CopilotCommand>("copilot")
                  .WithDescription("Add/remove @copilot coding agent from team roster.");
            config.AddCommand<LinkCommand>("link")
                  .WithDescription("Link this project to a remote team root.");
            config.AddCommand<InitRemoteCommand>("init-remote")
                  .WithDescription("Write .squad/config.json for remote squad mode.");
            config.AddCommand<EconomyCommand>("economy")
                  .WithDescription("Toggle cost-conscious model selection (on | off).");
            config.AddCommand<RolesCommand>("roles")
                  .WithDescription("List available built-in agent roles.");
            config.AddCommand<UpstreamCommand>("upstream")
                  .WithDescription("Manage upstream squad sources (add|remove|list|sync).");
            config.AddCommand<StreamsCommand>("streams")
                  .WithDescription("Manage SubSquads (list|status|activate).");
            config.AddCommand<ScheduleCommand>("schedule")
                  .WithDescription("Manage scheduled squad tasks (init|list|status|run).");
            config.AddCommand<WatchCommand>("watch")
                  .WithDescription("Run Ralph's local polling loop — triage issues and monitor PRs.");
            config.AddCommand<DiscoverCommand>("discover")
                  .WithDescription("Discover linked squads via upstream.json.");
            config.AddCommand<DelegateCommand>("delegate")
                  .WithDescription("Create a cross-squad work request.");
            config.AddCommand<ExtractCommand>("extract")
                  .WithDescription("Extract learnings from a consult session to personal squad.");

            // personal branch
            config.AddBranch("personal", personal =>
            {
                personal.SetDescription("Manage personal squad agents.");
                personal.AddCommand<PersonalInitCommand>("init").WithDescription("Initialize personal squad.");
                personal.AddCommand<PersonalListCommand>("list").WithDescription("List personal agents.");
                personal.AddCommand<PersonalAddCommand>("add").WithDescription("Add a personal agent.");
                personal.AddCommand<PersonalRemoveCommand>("remove").WithDescription("Remove a personal agent.");
            });

            // plugin branch
            config.AddBranch("plugin", plugin =>
            {
                plugin.SetDescription("Manage squad plugins and marketplaces.");
                plugin.AddBranch("marketplace", mp =>
                {
                    mp.SetDescription("Manage plugin marketplaces.");
                    mp.AddCommand<MarketplaceAddCommand>("add").WithDescription("Register a marketplace.");
                    mp.AddCommand<MarketplaceRemoveCommand>("remove").WithDescription("Remove a marketplace.");
                    mp.AddCommand<MarketplaceListCommand>("list").WithDescription("List registered marketplaces.");
                    mp.AddCommand<MarketplaceBrowseCommand>("browse").WithDescription("Browse marketplace plugins.");
                });
            });
        });
        return app;
    }
}
```

- [ ] **Step 2: Update Program.cs to use SquadCliApp**

Replace the contents of `src/Squad.Cli/Program.cs` with:
```csharp
return Squad.Cli.SquadCliApp.Build().Run(args);
```

- [ ] **Step 3: Run tests — expect pass**

```bash
cd ~/NSquad && dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass.

- [ ] **Step 4: Full build + all test suites**

```bash
cd ~/NSquad
dotnet build
dotnet run --project tests/Squad.Sdk.Tests
dotnet run --project tests/Squad.Cli.Tests
```

Expected: build succeeds, all tests pass.

- [ ] **Step 5: Smoke test a few commands locally**

```bash
cd ~/NSquad
dotnet run --project src/Squad.Cli -- --help
dotnet run --project src/Squad.Cli -- roles
dotnet run --project src/Squad.Cli -- economy
dotnet run --project src/Squad.Cli -- build --help
dotnet run --project src/Squad.Cli -- personal --help
dotnet run --project src/Squad.Cli -- plugin --help
```

Expected: each command prints usage or output without crashing.

- [ ] **Step 6: Commit**

```bash
cd ~/NSquad
git add src/Squad.Cli/SquadCliApp.cs src/Squad.Cli/Program.cs \
    tests/Squad.Cli.Tests/Commands/ProgramRegistrationTests.cs
git commit -m "feat(cli): register all Plan 2 commands, extract SquadCliApp for testability"
```

---

## What's Next: Plan 3

Additional commands not included here (require interactive terminal / PTY):
- `start` — full Copilot session with terminal UI
- `rc` — remote control console
- `rc-tunnel` — SSH tunnel for rc
- `consult` — consult mode session

The .NET aspire dashboard integration (`squad aspire`) would also be a natural Plan 3 addition since .NET Aspire is a first-class .NET concept.
