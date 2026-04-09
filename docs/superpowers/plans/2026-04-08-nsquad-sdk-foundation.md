# NSquad SDK Foundation + CLI Scaffold Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the `Squad.Sdk` library (Copilot session management, config loading, event bus, coordinator, cost tracker) and a `Squad.Cli` dotnet tool with three working commands: `doctor`, `cast`, and `cost`.

**Architecture:** `Squad.Sdk` is a pure .NET 10 library wrapping the `GitHub.Copilot.SDK` with Squad's config-driven multi-agent coordinator, event bus, and streaming pipeline. `Squad.Cli` is a `dotnet tool` built on Spectre.Console.Cli that reads `.squad/` directories — no Copilot session required for the commands in this plan. A second plan covers remaining CLI commands.

**Tech Stack:** .NET 10, GitHub.Copilot.SDK 0.2.2-preview.0, Microsoft.Extensions.AI 10.4.1, Microsoft.Extensions.Hosting 10.0.5, Spectre.Console 0.55.0, Spectre.Console.Cli 0.55.0, TUnit 1.29.0, Shouldly 4.3.0, OpenTelemetry 1.15.2

---

## Reference Material

- TypeScript source: `~/squad/` (the repo being ported)
- .NET Copilot SDK docs: `~/copilot-sdk/docs/copilot-sdk-csharp.instructions.md`
- Key TypeScript files to reference:
  - `~/squad/packages/squad-sdk/src/resolution.ts` — path resolver logic
  - `~/squad/packages/squad-cli/src/cli/commands/doctor.ts` — doctor checks
  - `~/squad/packages/squad-cli/src/cli/commands/cast.ts` — cast display
  - `~/squad/packages/squad-cli/src/cli/commands/cost.ts` — cost parsing

---

## File Map

```
NSquad.sln
src/
  Squad.Sdk/
    Squad.Sdk.csproj
    Config/
      SquadConfig.cs          — all config record types (SquadConfig, AgentConfig, TeamConfig, RoutingConfig, etc.)
      ConfigLoader.cs         — find + deserialize squad.config.json
    Resolution/
      PathResolver.cs         — walk-up .squad/ detection, global/personal dir resolution
    Events/
      SquadEvent.cs           — event type hierarchy (abstract base + concrete sealed records)
      EventBus.cs             — Channel<SquadEvent>-backed pub/sub with IDisposable subscriptions
    Client/
      SquadClient.cs          — wraps CopilotClient; creates/lists/deletes sessions
      SquadSession.cs         — wraps CopilotSession; typed event subscription, streaming, tool wiring
    Streaming/
      StreamingPipeline.cs    — translates SDK events → typed Squad streaming events
    Coordinator/
      RoutingEngine.cs        — compile + match routing rules against messages
      SquadCoordinator.cs     — direct/single/multi strategy decision + fan-out
    CostTracker/
      CostEntry.cs            — record type for one cost log entry
      CostReader.cs           — read + aggregate cost JSON files from .squad/costs/
    Skills/
      SkillLoader.cs          — discover + read skill markdown files from .squad/skills/
    Telemetry/
      TelemetrySetup.cs       — OTel provider registration helpers
  Squad.Cli/
    Squad.Cli.csproj
    Program.cs                — Spectre.Console CommandApp entry point + DI bootstrap
    Infrastructure/
      CliServiceExtensions.cs — IServiceCollection wiring for CLI services
    Commands/
      DoctorCommand.cs        — validate .squad/ setup, print table
      CastCommand.cs          — show merged agent cast
      CostCommand.cs          — show cost totals per agent
tests/
  Squad.Sdk.Tests/
    Squad.Sdk.Tests.csproj
    Config/
      ConfigLoaderTests.cs
    Resolution/
      PathResolverTests.cs
    Events/
      EventBusTests.cs
    Coordinator/
      RoutingEngineTests.cs
      CoordinatorTests.cs
    CostTracker/
      CostReaderTests.cs
  Squad.Cli.Tests/
    Squad.Cli.Tests.csproj
    Commands/
      DoctorCommandTests.cs
      CastCommandTests.cs
      CostCommandTests.cs
```

---

## Task 1: Solution Scaffold

**Files:**
- Create: `NSquad.sln`
- Create: `src/Squad.Sdk/Squad.Sdk.csproj`
- Create: `src/Squad.Cli/Squad.Cli.csproj`
- Create: `tests/Squad.Sdk.Tests/Squad.Sdk.Tests.csproj`
- Create: `tests/Squad.Cli.Tests/Squad.Cli.Tests.csproj`
- Create: `.gitignore` (already exists but needs .NET entries)
- Create: `Directory.Build.props`

- [ ] **Step 1: Create solution and projects**

```bash
cd ~/NSquad
dotnet new sln -n NSquad --force
dotnet new classlib -n Squad.Sdk -o src/Squad.Sdk --framework net10.0
dotnet new console -n Squad.Cli -o src/Squad.Cli --framework net10.0
mkdir -p tests/Squad.Sdk.Tests tests/Squad.Cli.Tests
```

- [ ] **Step 2: Write test project files (TUnit requires OutputType=Exe)**

Create `tests/Squad.Sdk.Tests/Squad.Sdk.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="TUnit" Version="1.29.0" />
    <PackageReference Include="Shouldly" Version="4.3.0" />
    <ProjectReference Include="..\..\src\Squad.Sdk\Squad.Sdk.csproj" />
  </ItemGroup>
</Project>
```

Create `tests/Squad.Cli.Tests/Squad.Cli.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="TUnit" Version="1.29.0" />
    <PackageReference Include="Shouldly" Version="4.3.0" />
    <ProjectReference Include="..\..\src\Squad.Cli\Squad.Cli.csproj" />
    <ProjectReference Include="..\..\src\Squad.Sdk\Squad.Sdk.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Write SDK project file**

Overwrite `src/Squad.Sdk/Squad.Sdk.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>Squad.Sdk</AssemblyName>
    <RootNamespace>Squad.Sdk</RootNamespace>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="GitHub.Copilot.SDK" Version="0.2.2-preview.0" />
    <PackageReference Include="Microsoft.Extensions.AI" Version="10.4.1" />
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.5" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.5" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.5" />
    <PackageReference Include="OpenTelemetry" Version="1.15.2" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.15.2" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Write CLI project file**

Overwrite `src/Squad.Cli/Squad.Cli.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>Squad.Cli</AssemblyName>
    <RootNamespace>Squad.Cli</RootNamespace>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>squad</ToolCommandName>
    <PackageId>Squad.Cli</PackageId>
    <Version>0.1.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Spectre.Console" Version="0.55.0" />
    <PackageReference Include="Spectre.Console.Cli" Version="0.55.0" />
    <ProjectReference Include="..\Squad.Sdk\Squad.Sdk.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Write shared Directory.Build.props**

Create `Directory.Build.props` in `~/NSquad/`:
```xml
<Project>
  <PropertyGroup>
    <LangVersion>preview</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 6: Add projects to solution**

```bash
cd ~/NSquad
dotnet sln add src/Squad.Sdk/Squad.Sdk.csproj
dotnet sln add src/Squad.Cli/Squad.Cli.csproj
dotnet sln add tests/Squad.Sdk.Tests/Squad.Sdk.Tests.csproj
dotnet sln add tests/Squad.Cli.Tests/Squad.Cli.Tests.csproj
```

- [ ] **Step 7: Update .gitignore with .NET entries**

Append to existing `.gitignore`:
```
# .NET
bin/
obj/
*.user
.vs/
*.nupkg
```

- [ ] **Step 8: Verify solution builds**

```bash
cd ~/NSquad
dotnet build
```

Expected: Build succeeded, 0 errors. Each project compiles (they'll just have placeholder content from templates).

- [ ] **Step 9: Commit**

```bash
cd ~/NSquad
git add .
git commit -m "chore: scaffold NSquad solution with Squad.Sdk, Squad.Cli, and test projects"
```

---

## Task 2: Config Types

**Files:**
- Create: `src/Squad.Sdk/Config/SquadConfig.cs`
- Create: `tests/Squad.Sdk.Tests/Config/ConfigTypesTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Squad.Sdk.Tests/Config/ConfigTypesTests.cs`:
```csharp
using Squad.Sdk.Config;
using Shouldly;

namespace Squad.Sdk.Tests.Config;

public class ConfigTypesTests
{
    [Test]
    public void AgentConfig_defaults_are_sensible()
    {
        var agent = new AgentConfig { Name = "archer" };

        agent.Name.ShouldBe("archer");
        agent.Skills.ShouldBeEmpty();
        agent.Metadata.ShouldBeEmpty();
    }

    [Test]
    public void SquadConfig_can_be_constructed_with_init()
    {
        var config = new SquadConfig
        {
            Team = new TeamConfig { Name = "Delta" },
            Agents = [new AgentConfig { Name = "striker", Role = "feature-dev" }]
        };

        config.Team.Name.ShouldBe("Delta");
        config.Agents.Count.ShouldBe(1);
        config.Agents[0].Role.ShouldBe("feature-dev");
    }

    [Test]
    public void RoutingRule_requires_pattern_and_agent()
    {
        var rule = new RoutingRule { Pattern = "bug.*", Agent = "fixer" };
        rule.Pattern.ShouldBe("bug.*");
        rule.Agent.ShouldBe("fixer");
    }
}
```

- [ ] **Step 2: Run tests — expect failure (types don't exist yet)**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Sdk.Tests
```

Expected: compile error — `Squad.Sdk.Config` namespace not found.

- [ ] **Step 3: Create the config types**

Create `src/Squad.Sdk/Config/SquadConfig.cs`:
```csharp
namespace Squad.Sdk.Config;

/// <summary>Top-level squad configuration loaded from squad.config.json.</summary>
public record SquadConfig
{
    public string? Version { get; init; }
    public TeamConfig Team { get; init; } = new();
    public List<AgentConfig> Agents { get; init; } = [];
    public RoutingConfig? Routing { get; init; }
    public ModelConfig? Models { get; init; }
    public BudgetConfig? Budget { get; init; }
    public CastingConfig? Casting { get; init; }
    public TelemetryConfig? Telemetry { get; init; }
}

public record TeamConfig
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public record AgentConfig
{
    public string Name { get; init; } = string.Empty;
    public string? Role { get; init; }
    public string? Model { get; init; }
    public string? Charter { get; init; }
    public List<string> Skills { get; init; } = [];
    public Dictionary<string, object?> Metadata { get; init; } = [];
}

public record RoutingConfig
{
    public List<RoutingRule> Rules { get; init; } = [];
    public string? DefaultAgent { get; init; }
    public string? FallbackAgent { get; init; }
}

public record RoutingRule
{
    public string Pattern { get; init; } = string.Empty;
    public string Agent { get; init; } = string.Empty;
    public List<string> WorkTypes { get; init; } = [];
    public int Priority { get; init; }
}

public record ModelConfig
{
    public string DefaultModel { get; init; } = "claude-sonnet-4.5";
    public string DefaultTier { get; init; } = "standard";
}

public record BudgetConfig
{
    public decimal? MaxCostPerSession { get; init; }
    public decimal? MaxCostPerDay { get; init; }
    public int? MaxTokensPerSession { get; init; }
}

public record CastingConfig
{
    public Dictionary<string, string> Assignments { get; init; } = [];
}

public record TelemetryConfig
{
    public bool Enabled { get; init; }
    public string? OtlpEndpoint { get; init; }
    public string? ServiceName { get; init; }
}
```

- [ ] **Step 4: Delete the placeholder Class1.cs from Squad.Sdk**

```bash
rm ~/NSquad/src/Squad.Sdk/Class1.cs
```

- [ ] **Step 5: Run tests — expect pass**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Sdk.Tests
```

Expected: 3 tests pass, 0 fail.

- [ ] **Step 6: Commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/Config/SquadConfig.cs tests/Squad.Sdk.Tests/Config/ConfigTypesTests.cs
git rm src/Squad.Sdk/Class1.cs
git commit -m "feat(sdk): add Squad config record types"
```

---

## Task 3: Config Loader

**Files:**
- Create: `src/Squad.Sdk/Config/ConfigLoader.cs`
- Create: `tests/Squad.Sdk.Tests/Config/ConfigLoaderTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Squad.Sdk.Tests/Config/ConfigLoaderTests.cs`:
```csharp
using Squad.Sdk.Config;
using Shouldly;

namespace Squad.Sdk.Tests.Config;

public class ConfigLoaderTests
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
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task LoadAsync_reads_squad_config_json()
    {
        var json = """
            {
              "version": "1.0",
              "team": { "name": "Alpha Team" },
              "agents": [
                { "name": "builder", "role": "feature-dev", "model": "claude-sonnet-4.5" }
              ]
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "squad.config.json"), json);

        var result = await ConfigLoader.LoadAsync(_tempDir);

        result.ShouldNotBeNull();
        result.Team.Name.ShouldBe("Alpha Team");
        result.Agents.Count.ShouldBe(1);
        result.Agents[0].Name.ShouldBe("builder");
        result.Agents[0].Model.ShouldBe("claude-sonnet-4.5");
    }

    [Test]
    public async Task LoadAsync_returns_null_when_no_config_file()
    {
        var result = await ConfigLoader.LoadAsync(_tempDir);
        result.ShouldBeNull();
    }

    [Test]
    public async Task LoadAsync_throws_on_malformed_json()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "squad.config.json"), "{ invalid }");

        await Should.ThrowAsync<ConfigLoadException>(() => ConfigLoader.LoadAsync(_tempDir));
    }

    [Test]
    public async Task LoadAsync_handles_minimal_config()
    {
        var json = """{ "team": { "name": "Solo" }, "agents": [] }""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "squad.config.json"), json);

        var result = await ConfigLoader.LoadAsync(_tempDir);

        result.ShouldNotBeNull();
        result.Agents.ShouldBeEmpty();
        result.Routing.ShouldBeNull();
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Sdk.Tests
```

Expected: compile error — `ConfigLoader` and `ConfigLoadException` not found.

- [ ] **Step 3: Implement ConfigLoader**

Create `src/Squad.Sdk/Config/ConfigLoader.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Squad.Sdk.Config;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Load squad.config.json from the given directory.
    /// Returns null if no config file exists.
    /// Throws <see cref="ConfigLoadException"/> on invalid JSON.
    /// </summary>
    public static async Task<SquadConfig?> LoadAsync(string directory, CancellationToken ct = default)
    {
        var path = Path.Combine(directory, "squad.config.json");
        if (!File.Exists(path))
            return null;

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, ct);
        }
        catch (IOException ex)
        {
            throw new ConfigLoadException($"Could not read {path}", ex);
        }

        try
        {
            var config = JsonSerializer.Deserialize<SquadConfig>(json, _options);
            return config;
        }
        catch (JsonException ex)
        {
            throw new ConfigLoadException($"Invalid JSON in {path}: {ex.Message}", ex);
        }
    }

    /// <summary>Synchronous version for contexts where async is inconvenient.</summary>
    public static SquadConfig? Load(string directory)
    {
        var path = Path.Combine(directory, "squad.config.json");
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SquadConfig>(json, _options);
        }
        catch (JsonException ex)
        {
            throw new ConfigLoadException($"Invalid JSON in {path}: {ex.Message}", ex);
        }
    }
}

public sealed class ConfigLoadException : Exception
{
    public ConfigLoadException(string message, Exception? inner = null)
        : base(message, inner) { }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Sdk.Tests
```

Expected: all 7 tests pass (3 from Task 2 + 4 from this task).

- [ ] **Step 5: Commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/Config/ConfigLoader.cs tests/Squad.Sdk.Tests/Config/ConfigLoaderTests.cs
git commit -m "feat(sdk): add ConfigLoader with squad.config.json deserialization"
```

---

## Task 4: Path Resolver

**Files:**
- Create: `src/Squad.Sdk/Resolution/PathResolver.cs`
- Create: `tests/Squad.Sdk.Tests/Resolution/PathResolverTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Squad.Sdk.Tests/Resolution/PathResolverTests.cs`:
```csharp
using Squad.Sdk.Resolution;
using Shouldly;

namespace Squad.Sdk.Tests.Resolution;

public class PathResolverTests
{
    private string _root = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_root);
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void ResolveSquadDir_finds_squad_dir_in_start_dir()
    {
        var squadDir = Path.Combine(_root, ".squad");
        Directory.CreateDirectory(squadDir);
        // Simulate a git root so walk-up stops here
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

        var result = PathResolver.ResolveSquadDir(_root);

        result.ShouldBe(squadDir);
    }

    [Test]
    public void ResolveSquadDir_walks_up_to_find_squad_dir()
    {
        var squadDir = Path.Combine(_root, ".squad");
        Directory.CreateDirectory(squadDir);
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        var subDir = Path.Combine(_root, "src", "app");
        Directory.CreateDirectory(subDir);

        var result = PathResolver.ResolveSquadDir(subDir);

        result.ShouldBe(squadDir);
    }

    [Test]
    public void ResolveSquadDir_returns_null_when_not_found()
    {
        // No .squad/ and .git stops the walk
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

        var result = PathResolver.ResolveSquadDir(_root);

        result.ShouldBeNull();
    }

    [Test]
    public void ResolveSquadDir_stops_at_git_boundary()
    {
        // .squad exists ABOVE the .git root — should not be found
        var outerSquad = Path.Combine(_root, ".squad");
        Directory.CreateDirectory(outerSquad);
        var innerRepo = Path.Combine(_root, "inner");
        Directory.CreateDirectory(innerRepo);
        Directory.CreateDirectory(Path.Combine(innerRepo, ".git"));

        var result = PathResolver.ResolveSquadDir(innerRepo);

        result.ShouldBeNull();
    }

    [Test]
    public void ResolveSquadDir_accepts_legacy_ai_team_dir()
    {
        var legacyDir = Path.Combine(_root, ".ai-team");
        Directory.CreateDirectory(legacyDir);
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

        var result = PathResolver.ResolveSquadDir(_root);

        result.ShouldBe(legacyDir);
    }

    [Test]
    public void DetectMode_returns_local_when_no_config_json()
    {
        var squadDir = Path.Combine(_root, ".squad");
        Directory.CreateDirectory(squadDir);

        var mode = PathResolver.DetectMode(squadDir);

        mode.ShouldBe(SquadMode.Local);
    }

    [Test]
    public void DetectMode_returns_remote_when_config_json_has_team_root()
    {
        var squadDir = Path.Combine(_root, ".squad");
        Directory.CreateDirectory(squadDir);
        File.WriteAllText(
            Path.Combine(squadDir, "config.json"),
            """{ "version": 1, "teamRoot": "../team", "projectKey": null }""");

        var mode = PathResolver.DetectMode(squadDir);

        mode.ShouldBe(SquadMode.Remote);
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Sdk.Tests
```

Expected: compile error — `PathResolver` and `SquadMode` not found.

- [ ] **Step 3: Implement PathResolver**

Create `src/Squad.Sdk/Resolution/PathResolver.cs`:
```csharp
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Squad.Sdk.Resolution;

public enum SquadMode { Local, Remote, Hub }

public static class PathResolver
{
    private static readonly string[] SquadDirNames = [".squad", ".ai-team"];

    /// <summary>
    /// Walk up from startDir to find a .squad/ (or legacy .ai-team/) directory.
    /// Stops at the .git root boundary.
    /// Returns the absolute path to the squad dir, or null if not found.
    /// </summary>
    public static string? ResolveSquadDir(string? startDir = null)
    {
        var current = Path.GetFullPath(startDir ?? Directory.GetCurrentDirectory());

        while (true)
        {
            foreach (var name in SquadDirNames)
            {
                var candidate = Path.Combine(current, name);
                if (Directory.Exists(candidate))
                    return candidate;
            }

            var gitMarker = Path.Combine(current, ".git");
            if (Path.Exists(gitMarker))
                return null; // reached repo boundary — not found

            var parent = Path.GetDirectoryName(current);
            if (parent is null || parent == current)
                return null; // filesystem root

            current = parent;
        }
    }

    /// <summary>
    /// Detect the squad mode for a resolved squad directory.
    /// Local = no config.json or no teamRoot. Remote = config.json with teamRoot.
    /// </summary>
    public static SquadMode DetectMode(string squadDir)
    {
        var hubFile = Path.Combine(Path.GetDirectoryName(squadDir)!, "squad-hub.json");
        if (File.Exists(hubFile))
            return SquadMode.Hub;

        var configPath = Path.Combine(squadDir, "config.json");
        if (!File.Exists(configPath))
            return SquadMode.Local;

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("teamRoot", out var teamRoot)
                && teamRoot.GetString() is { Length: > 0 })
                return SquadMode.Remote;
        }
        catch (JsonException) { /* malformed — treat as local */ }

        return SquadMode.Local;
    }

    /// <summary>
    /// Platform-specific global squad config directory.
    /// Windows: %APPDATA%/squad/   macOS: ~/Library/Application Support/squad/
    /// Linux: $XDG_CONFIG_HOME/squad/ (default ~/.config/squad/)
    /// </summary>
    public static string ResolveGlobalSquadPath()
    {
        string base_;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            base_ = Environment.GetEnvironmentVariable("APPDATA")
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming");
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            base_ = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");
        else
            base_ = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        var globalDir = Path.Combine(base_, "squad");
        Directory.CreateDirectory(globalDir);
        return globalDir;
    }

    /// <summary>
    /// Returns the personal squad directory, or null if SQUAD_NO_PERSONAL is set
    /// or the directory does not exist.
    /// </summary>
    public static string? ResolvePersonalSquadDir()
    {
        if (Environment.GetEnvironmentVariable("SQUAD_NO_PERSONAL") is not null)
            return null;

        var personalDir = Path.Combine(ResolveGlobalSquadPath(), "personal-squad");
        return Directory.Exists(personalDir) ? personalDir : null;
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Sdk.Tests
```

Expected: all 14 tests pass.

- [ ] **Step 5: Commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/Resolution/PathResolver.cs tests/Squad.Sdk.Tests/Resolution/PathResolverTests.cs
git commit -m "feat(sdk): add PathResolver with walk-up .squad/ detection and mode detection"
```

---

## Task 5: Event Bus

**Files:**
- Create: `src/Squad.Sdk/Events/SquadEvent.cs`
- Create: `src/Squad.Sdk/Events/EventBus.cs`
- Create: `tests/Squad.Sdk.Tests/Events/EventBusTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Squad.Sdk.Tests/Events/EventBusTests.cs`:
```csharp
using Squad.Sdk.Events;
using Shouldly;

namespace Squad.Sdk.Tests.Events;

public class EventBusTests
{
    [Test]
    public async Task Subscribe_receives_published_events()
    {
        var bus = new EventBus();
        var received = new List<SquadEvent>();

        using var _ = bus.Subscribe(evt => received.Add(evt));
        await bus.PublishAsync(new SessionCreatedEvent("s1", "agent1"));

        received.Count.ShouldBe(1);
        received[0].ShouldBeOfType<SessionCreatedEvent>();
    }

    [Test]
    public async Task Unsubscribe_stops_receiving_events()
    {
        var bus = new EventBus();
        var received = new List<SquadEvent>();

        var sub = bus.Subscribe(evt => received.Add(evt));
        await bus.PublishAsync(new SessionCreatedEvent("s1", "agent1"));
        sub.Dispose();
        await bus.PublishAsync(new SessionCreatedEvent("s2", "agent2"));

        received.Count.ShouldBe(1);
    }

    [Test]
    public async Task Multiple_subscribers_all_receive_events()
    {
        var bus = new EventBus();
        var count1 = 0;
        var count2 = 0;

        using var _ = bus.Subscribe(_ => count1++);
        using var __ = bus.Subscribe(_ => count2++);

        await bus.PublishAsync(new SessionIdleEvent("s1", null));

        count1.ShouldBe(1);
        count2.ShouldBe(1);
    }

    [Test]
    public async Task Faulting_subscriber_does_not_affect_others()
    {
        var bus = new EventBus();
        var received = new List<SquadEvent>();

        using var _ = bus.Subscribe(_ => throw new InvalidOperationException("boom"));
        using var __ = bus.Subscribe(evt => received.Add(evt));

        await Should.NotThrowAsync(() => bus.PublishAsync(new SessionIdleEvent("s1", null)));
        received.Count.ShouldBe(1);
    }

    [Test]
    public async Task Typed_subscribe_only_receives_matching_events()
    {
        var bus = new EventBus();
        var idleEvents = new List<SessionIdleEvent>();

        using var _ = bus.Subscribe<SessionIdleEvent>(idleEvents.Add);
        await bus.PublishAsync(new SessionCreatedEvent("s1", "a"));
        await bus.PublishAsync(new SessionIdleEvent("s2", "b"));
        await bus.PublishAsync(new SessionErrorEvent("s3", "c", "oops"));

        idleEvents.Count.ShouldBe(1);
        idleEvents[0].SessionId.ShouldBe("s2");
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Sdk.Tests
```

Expected: compile error — event types not found.

- [ ] **Step 3: Create event types**

Create `src/Squad.Sdk/Events/SquadEvent.cs`:
```csharp
namespace Squad.Sdk.Events;

/// <summary>Base type for all Squad events.</summary>
public abstract record SquadEvent(
    string? SessionId,
    string? AgentName,
    DateTimeOffset Timestamp);

// ── Lifecycle Events ────────────────────────────────────────────────

public sealed record SessionCreatedEvent(string SessionId, string? AgentName)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

public sealed record SessionIdleEvent(string SessionId, string? AgentName)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

public sealed record SessionErrorEvent(string SessionId, string? AgentName, string Message)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

public sealed record SessionDestroyedEvent(string SessionId, string? AgentName)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

// ── Streaming Events ────────────────────────────────────────────────

public sealed record StreamDeltaEvent(
    string SessionId,
    string? AgentName,
    string Content,
    int Index)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

public sealed record ReasoningDeltaEvent(
    string SessionId,
    string? AgentName,
    string Content,
    int Index)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

public sealed record UsageEvent(
    string SessionId,
    string? AgentName,
    string Model,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCost)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);

// ── Coordinator Events ──────────────────────────────────────────────

public sealed record CoordinatorRoutingEvent(
    string? SessionId,
    string? AgentName,
    string Strategy,
    string Message)
    : SquadEvent(SessionId, AgentName, DateTimeOffset.UtcNow);
```

- [ ] **Step 4: Create EventBus**

Create `src/Squad.Sdk/Events/EventBus.cs`:
```csharp
namespace Squad.Sdk.Events;

/// <summary>
/// In-process pub/sub event bus for Squad session lifecycle events.
/// Handlers run in subscription order. Faulting handlers are isolated
/// via try/catch — they cannot disrupt other subscribers.
/// </summary>
public sealed class EventBus : IDisposable
{
    private readonly Lock _lock = new();
    private readonly List<Func<SquadEvent, ValueTask>> _handlers = [];
    private bool _disposed;

    /// <summary>Subscribe to all events. Returns an IDisposable to unsubscribe.</summary>
    public IDisposable Subscribe(Func<SquadEvent, ValueTask> handler)
    {
        lock (_lock) _handlers.Add(handler);
        return new Subscription(() => { lock (_lock) _handlers.Remove(handler); });
    }

    /// <summary>Subscribe with a synchronous handler.</summary>
    public IDisposable Subscribe(Action<SquadEvent> handler)
        => Subscribe(evt => { handler(evt); return ValueTask.CompletedTask; });

    /// <summary>Subscribe to a specific event type only.</summary>
    public IDisposable Subscribe<T>(Action<T> handler) where T : SquadEvent
        => Subscribe(evt => { if (evt is T typed) handler(typed); });

    /// <summary>Publish an event to all current subscribers.</summary>
    public async Task PublishAsync(SquadEvent evt)
    {
        Func<SquadEvent, ValueTask>[] snapshot;
        lock (_lock) snapshot = [.. _handlers];

        foreach (var handler in snapshot)
        {
            try { await handler(evt); }
            catch { /* isolate faulting subscribers */ }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_lock) _handlers.Clear();
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private bool _disposed;
        public void Dispose() { if (!_disposed) { _disposed = true; dispose(); } }
    }
}
```

- [ ] **Step 5: Run tests — expect pass**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Sdk.Tests
```

Expected: all 19 tests pass.

- [ ] **Step 6: Commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/Events/ tests/Squad.Sdk.Tests/Events/
git commit -m "feat(sdk): add EventBus with typed subscriptions and fault isolation"
```

---

## Task 6: Cost Reader

**Files:**
- Create: `src/Squad.Sdk/CostTracker/CostEntry.cs`
- Create: `src/Squad.Sdk/CostTracker/CostReader.cs`
- Create: `tests/Squad.Sdk.Tests/CostTracker/CostReaderTests.cs`

This is needed before CLI commands since `cost` is one of the first commands.
Reference: `~/squad/packages/squad-cli/src/cli/commands/cost.ts` for file format.

- [ ] **Step 1: Write the failing tests**

Create `tests/Squad.Sdk.Tests/CostTracker/CostReaderTests.cs`:
```csharp
using Squad.Sdk.CostTracker;
using Shouldly;

namespace Squad.Sdk.Tests.CostTracker;

public class CostReaderTests
{
    private string _squadDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _squadDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), ".squad");
        Directory.CreateDirectory(Path.Combine(_squadDir, "costs"));
    }

    [After(Test)]
    public void Cleanup()
    {
        var parent = Path.GetDirectoryName(_squadDir)!;
        if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
    }

    [Test]
    public async Task LoadEntriesAsync_returns_empty_when_no_costs_dir()
    {
        var noSquad = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), ".squad");

        var result = await CostReader.LoadEntriesAsync(noSquad);

        result.ShouldBeEmpty();
    }

    [Test]
    public async Task LoadEntriesAsync_reads_cost_json_files()
    {
        var json = """
            {
              "agent": "builder",
              "inputTokens": 1000,
              "outputTokens": 500,
              "estimatedCost": 0.0025,
              "timestamp": "2026-04-08T10:00:00Z"
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_squadDir, "costs", "entry1.json"), json);

        var result = await CostReader.LoadEntriesAsync(_squadDir);

        result.Count.ShouldBe(1);
        result[0].Agent.ShouldBe("builder");
        result[0].InputTokens.ShouldBe(1000);
        result[0].OutputTokens.ShouldBe(500);
        result[0].EstimatedCost.ShouldBe(0.0025m);
    }

    [Test]
    public async Task Summarize_groups_by_agent()
    {
        var entries = new[]
        {
            new CostEntry { Agent = "builder", InputTokens = 100, OutputTokens = 50, EstimatedCost = 0.001m, Timestamp = DateTimeOffset.UtcNow },
            new CostEntry { Agent = "builder", InputTokens = 200, OutputTokens = 100, EstimatedCost = 0.002m, Timestamp = DateTimeOffset.UtcNow },
            new CostEntry { Agent = "tester", InputTokens = 50, OutputTokens = 25, EstimatedCost = 0.0005m, Timestamp = DateTimeOffset.UtcNow },
        };

        var summary = CostReader.Summarize(entries);

        summary.Count.ShouldBe(2);
        summary["builder"].TotalInputTokens.ShouldBe(300);
        summary["builder"].TotalOutputTokens.ShouldBe(150);
        summary["builder"].TotalCost.ShouldBe(0.003m);
        summary["tester"].TotalCost.ShouldBe(0.0005m);
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Sdk.Tests
```

Expected: compile error — `CostEntry`, `CostReader` not found.

- [ ] **Step 3: Implement cost types and reader**

Create `src/Squad.Sdk/CostTracker/CostEntry.cs`:
```csharp
namespace Squad.Sdk.CostTracker;

public record CostEntry
{
    public string Agent { get; init; } = string.Empty;
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public decimal EstimatedCost { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string? Model { get; init; }
    public string? SessionId { get; init; }
}

public record AgentCostSummary
{
    public string Agent { get; init; } = string.Empty;
    public int TotalInputTokens { get; init; }
    public int TotalOutputTokens { get; init; }
    public decimal TotalCost { get; init; }
    public int SessionCount { get; init; }
}
```

Create `src/Squad.Sdk/CostTracker/CostReader.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Squad.Sdk.CostTracker;

public static class CostReader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Read all cost log entries from .squad/costs/*.json</summary>
    public static async Task<IReadOnlyList<CostEntry>> LoadEntriesAsync(
        string squadDir,
        CancellationToken ct = default)
    {
        var costsDir = Path.Combine(squadDir, "costs");
        if (!Directory.Exists(costsDir))
            return [];

        var entries = new List<CostEntry>();
        foreach (var file in Directory.GetFiles(costsDir, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var entry = JsonSerializer.Deserialize<CostEntry>(json, _options);
                if (entry is not null) entries.Add(entry);
            }
            catch { /* skip malformed files */ }
        }

        return entries.OrderBy(e => e.Timestamp).ToList();
    }

    /// <summary>Aggregate cost entries by agent name.</summary>
    public static IReadOnlyDictionary<string, AgentCostSummary> Summarize(
        IEnumerable<CostEntry> entries)
    {
        return entries
            .GroupBy(e => e.Agent)
            .ToDictionary(
                g => g.Key,
                g => new AgentCostSummary
                {
                    Agent = g.Key,
                    TotalInputTokens = g.Sum(e => e.InputTokens),
                    TotalOutputTokens = g.Sum(e => e.OutputTokens),
                    TotalCost = g.Sum(e => e.EstimatedCost),
                    SessionCount = g.Select(e => e.SessionId).Distinct().Count(),
                });
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Sdk.Tests
```

Expected: all 22 tests pass.

- [ ] **Step 5: Commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/CostTracker/ tests/Squad.Sdk.Tests/CostTracker/
git commit -m "feat(sdk): add CostReader for reading .squad/costs/*.json log entries"
```

---

## Task 7: Squad Client + Session Wrapper

**Files:**
- Create: `src/Squad.Sdk/Client/SquadClient.cs`
- Create: `src/Squad.Sdk/Client/SquadSession.cs`
- Create: `src/Squad.Sdk/Client/SquadClientOptions.cs`
- Create: `src/Squad.Sdk/Client/SquadSessionOptions.cs`

No unit tests for the client wrapper — it's a thin adapter over the Copilot SDK (which requires an external process). Integration tests come later.

- [ ] **Step 1: Create client options types**

Create `src/Squad.Sdk/Client/SquadClientOptions.cs`:
```csharp
namespace Squad.Sdk.Client;

public sealed class SquadClientOptions
{
    /// <summary>Path to the copilot CLI executable. Defaults to "copilot" on PATH.</summary>
    public string? CliPath { get; init; }
    /// <summary>Working directory for the CLI process.</summary>
    public string? WorkingDirectory { get; init; }
    /// <summary>Auto-restart CLI process on crash.</summary>
    public bool AutoRestart { get; init; } = true;
    /// <summary>Log level passed to the CLI.</summary>
    public string LogLevel { get; init; } = "info";
    /// <summary>Extra environment variables for the CLI process.</summary>
    public Dictionary<string, string> Environment { get; init; } = [];
}

public sealed class SquadSessionOptions
{
    public string? AgentName { get; init; }
    public string? Model { get; init; }
    public string? SessionId { get; init; }
    public bool Streaming { get; init; } = true;
    public string? SystemMessageAppend { get; init; }
    public List<string> AvailableTools { get; init; } = [];
    public List<string> ExcludedTools { get; init; } = [];
}
```

- [ ] **Step 2: Create SquadSession wrapper**

Create `src/Squad.Sdk/Client/SquadSession.cs`:
```csharp
using GitHub.Copilot.SDK;
using Squad.Sdk.Events;

namespace Squad.Sdk.Client;

/// <summary>
/// Wraps a CopilotSession, bridging SDK events to the Squad EventBus
/// and providing a typed streaming interface.
/// </summary>
public sealed class SquadSession : IAsyncDisposable
{
    private readonly CopilotSession _inner;
    private readonly EventBus? _eventBus;

    public string SessionId { get; }
    public string? AgentName { get; }

    internal SquadSession(CopilotSession inner, string? agentName, EventBus? eventBus = null)
    {
        _inner = inner;
        _eventBus = eventBus;
        SessionId = inner.SessionId;
        AgentName = agentName;

        WireEvents();
    }

    /// <summary>Send a message and return when the session goes idle.</summary>
    public async Task<string> SendAsync(string prompt, CancellationToken ct = default)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new System.Text.StringBuilder();

        using var sub = _inner.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg:
                    response.Append(msg.Data.Content);
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
                case SessionErrorEvent err:
                    done.TrySetException(new SquadSessionException(err.Data.Message));
                    break;
            }
        });

        ct.Register(() => done.TrySetCanceled(ct));
        await _inner.SendAsync(new MessageOptions { Prompt = prompt });
        await done.Task;

        return response.ToString();
    }

    /// <summary>
    /// Send a message and stream delta chunks via the provided callback.
    /// Awaits completion (SessionIdle) before returning.
    /// </summary>
    public async Task SendStreamingAsync(
        string prompt,
        Action<string> onDelta,
        CancellationToken ct = default)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = _inner.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    onDelta(delta.Data.DeltaContent);
                    _eventBus?.PublishAsync(new StreamDeltaEvent(SessionId, AgentName, delta.Data.DeltaContent, 0));
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
                case SessionErrorEvent err:
                    done.TrySetException(new SquadSessionException(err.Data.Message));
                    break;
            }
        });

        ct.Register(() => done.TrySetCanceled(ct));
        await _inner.SendAsync(new MessageOptions { Prompt = prompt });
        await done.Task;
    }

    /// <summary>Subscribe directly to the underlying CopilotSession events.</summary>
    public IDisposable OnEvent(Action<object> handler) => _inner.On(handler);

    public async Task AbortAsync() => await _inner.AbortAsync();

    public async ValueTask DisposeAsync()
    {
        await _eventBus?.PublishAsync(new SessionDestroyedEvent(SessionId, AgentName))!;
        await _inner.DisposeAsync();
    }

    private void WireEvents()
    {
        if (_eventBus is null) return;
        _eventBus.PublishAsync(new SessionCreatedEvent(SessionId, AgentName));

        _inner.On(evt =>
        {
            switch (evt)
            {
                case GitHub.Copilot.SDK.SessionIdleEvent:
                    _eventBus.PublishAsync(new Events.SessionIdleEvent(SessionId, AgentName));
                    break;
                case GitHub.Copilot.SDK.SessionErrorEvent err:
                    _eventBus.PublishAsync(new Events.SessionErrorEvent(SessionId, AgentName, err.Data.Message));
                    break;
            }
        });
    }
}

public sealed class SquadSessionException(string message) : Exception(message);
```

- [ ] **Step 3: Create SquadClient wrapper**

Create `src/Squad.Sdk/Client/SquadClient.cs`:
```csharp
using GitHub.Copilot.SDK;
using Squad.Sdk.Events;

namespace Squad.Sdk.Client;

/// <summary>
/// Wraps CopilotClient. The single entry point for creating Squad sessions.
/// Use CreateAsync() for normal usage; Dispose when done.
/// </summary>
public sealed class SquadClient : IAsyncDisposable
{
    private readonly CopilotClient _inner;
    private readonly EventBus? _eventBus;
    private bool _disposed;

    private SquadClient(CopilotClient inner, EventBus? eventBus)
    {
        _inner = inner;
        _eventBus = eventBus;
    }

    /// <summary>Create and start a SquadClient.</summary>
    public static async Task<SquadClient> CreateAsync(
        SquadClientOptions? options = null,
        EventBus? eventBus = null,
        CancellationToken ct = default)
    {
        var clientOptions = new CopilotClientOptions
        {
            CliPath = options?.CliPath,
            Cwd = options?.WorkingDirectory,
            AutoRestart = options?.AutoRestart ?? true,
            LogLevel = options?.LogLevel ?? "info",
            Environment = options?.Environment,
        };

        var client = new CopilotClient(clientOptions);
        await client.StartAsync(ct);
        return new SquadClient(client, eventBus);
    }

    /// <summary>Create a new Squad session for an agent.</summary>
    public async Task<SquadSession> CreateSessionAsync(
        SquadSessionOptions? options = null,
        CancellationToken ct = default)
    {
        var sessionConfig = new SessionConfig
        {
            OnPermissionRequest = PermissionHandler.ApproveAll,
            Model = options?.Model,
            SessionId = options?.SessionId,
            Streaming = options?.Streaming ?? true,
            AvailableTools = options?.AvailableTools?.Count > 0
                ? [.. options.AvailableTools]
                : null,
            ExcludedTools = options?.ExcludedTools?.Count > 0
                ? [.. options.ExcludedTools]
                : null,
        };

        if (options?.SystemMessageAppend is { Length: > 0 } append)
        {
            sessionConfig = sessionConfig with
            {
                SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Append,
                    Content = append,
                }
            };
        }

        var inner = await _inner.CreateSessionAsync(sessionConfig, ct);
        return new SquadSession(inner, options?.AgentName, _eventBus);
    }

    /// <summary>Resume an existing session by ID.</summary>
    public async Task<SquadSession> ResumeSessionAsync(
        string sessionId,
        string? agentName = null,
        CancellationToken ct = default)
    {
        var inner = await _inner.ResumeSessionAsync(sessionId, new ResumeSessionConfig
        {
            OnPermissionRequest = PermissionHandler.ApproveAll,
        }, ct);
        return new SquadSession(inner, agentName, _eventBus);
    }

    public async Task<IReadOnlyList<SessionMetadata>> ListSessionsAsync(CancellationToken ct = default)
        => await _inner.ListSessionsAsync(ct);

    public async Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
        => await _inner.DeleteSessionAsync(sessionId, ct);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _inner.StopAsync();
    }
}
```

- [ ] **Step 4: Verify solution compiles**

```bash
cd ~/NSquad
dotnet build
```

Expected: Build succeeded. (No runtime test here — live Copilot SDK requires the CLI process.)

- [ ] **Step 5: Commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/Client/
git commit -m "feat(sdk): add SquadClient and SquadSession wrappers over GitHub.Copilot.SDK"
```

---

## Task 8: Coordinator and Routing Engine

**Files:**
- Create: `src/Squad.Sdk/Coordinator/RoutingEngine.cs`
- Create: `src/Squad.Sdk/Coordinator/SquadCoordinator.cs`
- Create: `src/Squad.Sdk/Coordinator/CoordinatorResult.cs`
- Create: `tests/Squad.Sdk.Tests/Coordinator/RoutingEngineTests.cs`
- Create: `tests/Squad.Sdk.Tests/Coordinator/CoordinatorTests.cs`

- [ ] **Step 1: Write failing tests for routing**

Create `tests/Squad.Sdk.Tests/Coordinator/RoutingEngineTests.cs`:
```csharp
using Squad.Sdk.Config;
using Squad.Sdk.Coordinator;
using Shouldly;

namespace Squad.Sdk.Tests.Coordinator;

public class RoutingEngineTests
{
    [Test]
    public void Match_returns_null_when_no_rules()
    {
        var engine = new RoutingEngine([]);
        engine.Match("fix the login bug").ShouldBeNull();
    }

    [Test]
    public void Match_finds_rule_by_keyword_pattern()
    {
        var rules = new[]
        {
            new RoutingRule { Pattern = "bug|fix|error", Agent = "debugger" },
            new RoutingRule { Pattern = "feature|add|implement", Agent = "builder" },
        };
        var engine = new RoutingEngine(rules);

        engine.Match("fix the login bug").ShouldBe("debugger");
        engine.Match("implement new feature").ShouldBe("builder");
    }

    [Test]
    public void Match_is_case_insensitive()
    {
        var rules = new[] { new RoutingRule { Pattern = "test", Agent = "tester" } };
        var engine = new RoutingEngine(rules);

        engine.Match("Write Tests for the API").ShouldBe("tester");
    }

    [Test]
    public void Match_returns_first_rule_when_multiple_match()
    {
        var rules = new[]
        {
            new RoutingRule { Pattern = "bug", Agent = "first" },
            new RoutingRule { Pattern = "bug", Agent = "second" },
        };
        var engine = new RoutingEngine(rules);

        engine.Match("bug report").ShouldBe("first");
    }

    [Test]
    public void Match_returns_null_when_no_pattern_matches()
    {
        var rules = new[] { new RoutingRule { Pattern = "test", Agent = "tester" } };
        var engine = new RoutingEngine(rules);

        engine.Match("unrelated query").ShouldBeNull();
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Sdk.Tests
```

Expected: compile error — `RoutingEngine` not found.

- [ ] **Step 3: Implement RoutingEngine**

Create `src/Squad.Sdk/Coordinator/RoutingEngine.cs`:
```csharp
using System.Text.RegularExpressions;
using Squad.Sdk.Config;

namespace Squad.Sdk.Coordinator;

/// <summary>
/// Compiles routing rules and matches messages to agent names.
/// Pattern is a regex tested case-insensitively against the message text.
/// </summary>
public sealed class RoutingEngine
{
    private readonly IReadOnlyList<(Regex Pattern, string Agent)> _compiled;

    public RoutingEngine(IEnumerable<RoutingRule> rules)
    {
        _compiled = rules
            .Select(r =>
            {
                var regex = new Regex(r.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                return (regex, r.Agent);
            })
            .ToList();
    }

    /// <summary>
    /// Returns the agent name for the first matching rule, or null if none match.
    /// </summary>
    public string? Match(string message)
    {
        foreach (var (pattern, agent) in _compiled)
        {
            if (pattern.IsMatch(message))
                return agent;
        }
        return null;
    }
}
```

- [ ] **Step 4: Create CoordinatorResult types**

Create `src/Squad.Sdk/Coordinator/CoordinatorResult.cs`:
```csharp
namespace Squad.Sdk.Coordinator;

public enum SpawnStrategy { Direct, Single, Multi, Fallback }

public sealed record CoordinatorResult
{
    public required SpawnStrategy Strategy { get; init; }
    public string? MatchedAgent { get; init; }
    public IReadOnlyList<string> SpawnedAgents { get; init; } = [];
    public IReadOnlyList<string> Responses { get; init; } = [];
    public long DurationMs { get; init; }
    public string? Error { get; init; }
}
```

- [ ] **Step 5: Write coordinator test**

Create `tests/Squad.Sdk.Tests/Coordinator/CoordinatorTests.cs`:
```csharp
using Squad.Sdk.Config;
using Squad.Sdk.Coordinator;
using Squad.Sdk.Events;
using Shouldly;

namespace Squad.Sdk.Tests.Coordinator;

public class CoordinatorTests
{
    private static SquadConfig MinimalConfig(string defaultAgent = "worker") => new()
    {
        Team = new TeamConfig { Name = "Test" },
        Agents = [new AgentConfig { Name = defaultAgent }],
        Routing = new RoutingConfig
        {
            DefaultAgent = defaultAgent,
            Rules = [new RoutingRule { Pattern = "bug|fix", Agent = defaultAgent }]
        }
    };

    [Test]
    public void RouteMessage_returns_default_agent_when_no_match()
    {
        var coordinator = new SquadCoordinator(MinimalConfig(), new EventBus());

        var agent = coordinator.RouteMessage("unrelated query");

        agent.ShouldBe("worker");
    }

    [Test]
    public void RouteMessage_uses_routing_rule_when_pattern_matches()
    {
        var config = new SquadConfig
        {
            Team = new TeamConfig { Name = "Test" },
            Agents =
            [
                new AgentConfig { Name = "fixer" },
                new AgentConfig { Name = "builder" },
            ],
            Routing = new RoutingConfig
            {
                DefaultAgent = "builder",
                Rules = [new RoutingRule { Pattern = "bug|fix", Agent = "fixer" }]
            }
        };
        var coordinator = new SquadCoordinator(config, new EventBus());

        var agent = coordinator.RouteMessage("fix the login bug");

        agent.ShouldBe("fixer");
    }

    [Test]
    public void RouteMessage_falls_back_to_first_agent_when_no_default()
    {
        var config = new SquadConfig
        {
            Team = new TeamConfig { Name = "Test" },
            Agents = [new AgentConfig { Name = "only-agent" }],
            Routing = new RoutingConfig { Rules = [] }
        };
        var coordinator = new SquadCoordinator(config, new EventBus());

        var agent = coordinator.RouteMessage("anything");

        agent.ShouldBe("only-agent");
    }
}
```

- [ ] **Step 6: Implement SquadCoordinator**

Create `src/Squad.Sdk/Coordinator/SquadCoordinator.cs`:
```csharp
using Squad.Sdk.Config;
using Squad.Sdk.Events;

namespace Squad.Sdk.Coordinator;

/// <summary>
/// Routes messages to agents based on config-driven routing rules.
/// In this phase, RouteMessage() selects the agent name — actual spawning
/// happens in the SquadClient layer once sessions are live.
/// </summary>
public sealed class SquadCoordinator
{
    private readonly SquadConfig _config;
    private readonly EventBus _eventBus;
    private readonly RoutingEngine _router;

    public SquadCoordinator(SquadConfig config, EventBus eventBus)
    {
        _config = config;
        _eventBus = eventBus;
        _router = new RoutingEngine(config.Routing?.Rules ?? []);
    }

    /// <summary>
    /// Determine which agent should handle the given message.
    /// Returns the agent name from routing rules, the configured default, 
    /// or the first agent in the config as final fallback.
    /// </summary>
    public string RouteMessage(string message)
    {
        var matched = _router.Match(message);
        if (matched is not null)
        {
            _ = _eventBus.PublishAsync(new CoordinatorRoutingEvent(null, matched, "single", message));
            return matched;
        }

        var fallback = _config.Routing?.DefaultAgent
                       ?? _config.Agents.FirstOrDefault()?.Name
                       ?? throw new InvalidOperationException("No agents configured.");

        _ = _eventBus.PublishAsync(new CoordinatorRoutingEvent(null, fallback, "fallback", message));
        return fallback;
    }
}
```

- [ ] **Step 7: Run all tests — expect pass**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Sdk.Tests
```

Expected: all tests pass (routing engine + coordinator tests included).

- [ ] **Step 8: Commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/Coordinator/ tests/Squad.Sdk.Tests/Coordinator/
git commit -m "feat(sdk): add RoutingEngine and SquadCoordinator for message-to-agent routing"
```

---

## Task 9: CLI Project Setup + Program Entry Point

**Files:**
- Modify: `src/Squad.Cli/Program.cs`
- Create: `src/Squad.Cli/Infrastructure/CliServiceExtensions.cs`

- [ ] **Step 1: Write Program.cs**

Overwrite `src/Squad.Cli/Program.cs`:
```csharp
using Spectre.Console.Cli;
using Squad.Cli.Commands;
using Squad.Cli.Infrastructure;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("squad");
    config.SetApplicationVersion("0.1.0");
    config.AddCommand<DoctorCommand>("doctor")
          .WithDescription("Validate .squad/ setup and report health checks.");
    config.AddCommand<CastCommand>("cast")
          .WithDescription("Show the current session cast (project + personal agents).");
    config.AddCommand<CostCommand>("cost")
          .WithDescription("Show token usage and estimated cost per agent.");
});

return app.Run(args);
```

- [ ] **Step 2: Create CliServiceExtensions (DI helpers for later)**

Create `src/Squad.Cli/Infrastructure/CliServiceExtensions.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Squad.Sdk.Events;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Infrastructure;

public static class CliServiceExtensions
{
    public static IServiceCollection AddSquadCli(
        this IServiceCollection services,
        string? workingDirectory = null)
    {
        services.AddSingleton<EventBus>();
        return services;
    }

    /// <summary>
    /// Find the .squad/ dir starting from the given directory (or cwd).
    /// Throws with a friendly message if not found.
    /// </summary>
    public static string RequireSquadDir(string? cwd = null)
    {
        var dir = PathResolver.ResolveSquadDir(cwd ?? Directory.GetCurrentDirectory());
        if (dir is null)
            throw new InvalidOperationException(
                "No .squad/ directory found. Run from inside a repository that has been initialized with Squad.");
        return dir;
    }
}
```

- [ ] **Step 3: Verify CLI builds (commands will fail to compile until Task 10)**

Add temporary placeholder commands so the project compiles before we write real ones:

Create `src/Squad.Cli/Commands/DoctorCommand.cs` (stub):
```csharp
using Spectre.Console.Cli;

namespace Squad.Cli.Commands;

public sealed class DoctorCommand : Command<DoctorCommand.Settings>
{
    public sealed class Settings : CommandSettings { }
    public override int Execute(CommandContext context, Settings settings) => 0;
}
```

Create `src/Squad.Cli/Commands/CastCommand.cs` (stub):
```csharp
using Spectre.Console.Cli;

namespace Squad.Cli.Commands;

public sealed class CastCommand : Command<CastCommand.Settings>
{
    public sealed class Settings : CommandSettings { }
    public override int Execute(CommandContext context, Settings settings) => 0;
}
```

Create `src/Squad.Cli/Commands/CostCommand.cs` (stub):
```csharp
using Spectre.Console.Cli;

namespace Squad.Cli.Commands;

public sealed class CostCommand : Command<CostCommand.Settings>
{
    public sealed class Settings : CommandSettings { }
    public override int Execute(CommandContext context, Settings settings) => 0;
}
```

Delete `src/Squad.Cli/Program.cs` placeholder from the template (was already overwritten).

- [ ] **Step 4: Delete Class1.cs from Squad.Cli if it exists**

```bash
rm -f ~/NSquad/src/Squad.Cli/Class1.cs
```

- [ ] **Step 5: Build and smoke-test CLI**

```bash
cd ~/NSquad
dotnet build
dotnet run --project src/Squad.Cli -- --help
```

Expected: help text listing `doctor`, `cast`, `cost` commands.

- [ ] **Step 6: Commit**

```bash
cd ~/NSquad
git add src/Squad.Cli/
git commit -m "feat(cli): scaffold Spectre.Console.Cli app with stub commands"
```

---

## Task 10: `doctor` Command

**Files:**
- Modify: `src/Squad.Cli/Commands/DoctorCommand.cs` (replace stub)
- Create: `tests/Squad.Cli.Tests/Commands/DoctorCommandTests.cs`

Reference: `~/squad/packages/squad-cli/src/cli/commands/doctor.ts`

- [ ] **Step 1: Write failing tests**

Create `tests/Squad.Cli.Tests/Commands/DoctorCommandTests.cs`:
```csharp
using Squad.Cli.Commands;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class DoctorCommandTests
{
    private string _root = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, ".git")); // repo boundary
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void RunChecks_fails_when_no_squad_dir()
    {
        var checks = DoctorCommand.RunChecks(_root);

        var squadCheck = checks.Single(c => c.Name == ".squad/ directory exists");
        squadCheck.Status.ShouldBe(DoctorCommand.CheckStatus.Fail);
    }

    [Test]
    public void RunChecks_passes_squad_dir_check_when_dir_exists()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".squad"));

        var checks = DoctorCommand.RunChecks(_root);

        var squadCheck = checks.Single(c => c.Name == ".squad/ directory exists");
        squadCheck.Status.ShouldBe(DoctorCommand.CheckStatus.Pass);
    }

    [Test]
    public void RunChecks_warns_on_missing_team_md()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".squad"));

        var checks = DoctorCommand.RunChecks(_root);

        var teamCheck = checks.Single(c => c.Name == "team.md exists with ## Members");
        teamCheck.Status.ShouldBe(DoctorCommand.CheckStatus.Fail);
    }

    [Test]
    public void RunChecks_passes_team_md_when_file_has_members_header()
    {
        var squadDir = Path.Combine(_root, ".squad");
        Directory.CreateDirectory(squadDir);
        File.WriteAllText(Path.Combine(squadDir, "team.md"), "# Team\n\n## Members\n\n- Agent 1");

        var checks = DoctorCommand.RunChecks(_root);

        var teamCheck = checks.Single(c => c.Name == "team.md exists with ## Members");
        teamCheck.Status.ShouldBe(DoctorCommand.CheckStatus.Pass);
    }

    [Test]
    public void RunChecks_detects_local_mode()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".squad"));

        var mode = DoctorCommand.DetectMode(_root);

        mode.ShouldBe(DoctorCommand.SquadDoctorMode.Local);
    }

    [Test]
    public void RunChecks_detects_remote_mode_when_config_has_team_root()
    {
        var squadDir = Path.Combine(_root, ".squad");
        Directory.CreateDirectory(squadDir);
        File.WriteAllText(
            Path.Combine(squadDir, "config.json"),
            """{ "version": 1, "teamRoot": "../team", "projectKey": null }""");

        var mode = DoctorCommand.DetectMode(_root);

        mode.ShouldBe(DoctorCommand.SquadDoctorMode.Remote);
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Cli.Tests
```

Expected: compile error — `DoctorCommand.RunChecks`, `DoctorCommand.CheckStatus`, `DoctorCommand.DetectMode` not found.

- [ ] **Step 3: Implement DoctorCommand**

Overwrite `src/Squad.Cli/Commands/DoctorCommand.cs`:
```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using Squad.Sdk.Resolution;
using System.Text.Json;

namespace Squad.Cli.Commands;

public sealed class DoctorCommand : Command<DoctorCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--dir <DIR>")]
        public string? Dir { get; init; }
    }

    public enum CheckStatus { Pass, Fail, Warn }
    public enum SquadDoctorMode { Local, Remote, Hub }

    public record DoctorCheck(string Name, CheckStatus Status, string Message);

    public override int Execute(CommandContext context, Settings settings)
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

    /// <summary>Run all checks for the given working directory. Exposed for testing.</summary>
    public static IReadOnlyList<DoctorCheck> RunChecks(string cwd)
    {
        var squadDir = Path.Combine(cwd, ".squad");
        var legacyDir = Path.Combine(cwd, ".ai-team");
        var effectiveDir = Directory.Exists(squadDir) ? squadDir
            : Directory.Exists(legacyDir) ? legacyDir
            : squadDir; // use canonical path even for fail message

        var checks = new List<DoctorCheck>();

        // 1. .squad/ directory exists
        checks.Add(Directory.Exists(effectiveDir)
            ? new(".squad/ directory exists", CheckStatus.Pass, "directory present")
            : new(".squad/ directory exists", CheckStatus.Fail, "directory not found — run `squad init`"));

        if (!Directory.Exists(effectiveDir))
            return checks; // no point running further checks

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
                    if (Path.IsPathFullyQualified(raw ?? string.Empty))
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
            checks.Add(new("casting/registry.json exists", CheckStatus.Fail, "file not found"));
        else
        {
            try { JsonDocument.Parse(File.ReadAllText(registryPath)); checks.Add(new("casting/registry.json exists", CheckStatus.Pass, "file present, valid JSON")); }
            catch { checks.Add(new("casting/registry.json exists", CheckStatus.Fail, "file exists but is not valid JSON")); }
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

    /// <summary>Detect mode for the given working directory. Exposed for testing.</summary>
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
```

- [ ] **Step 4: Run tests — expect pass**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Cli.Tests
```

Expected: all 6 doctor tests pass.

- [ ] **Step 5: Smoke-test the command against a real directory**

```bash
cd ~/NSquad
dotnet run --project src/Squad.Cli -- doctor --dir ~/squad
```

Expected: table of checks printed to terminal, some pass/fail/warn.

- [ ] **Step 6: Commit**

```bash
cd ~/NSquad
git add src/Squad.Cli/Commands/DoctorCommand.cs tests/Squad.Cli.Tests/Commands/DoctorCommandTests.cs
git commit -m "feat(cli): implement doctor command with .squad/ health checks"
```

---

## Task 11: `cast` Command

**Files:**
- Modify: `src/Squad.Cli/Commands/CastCommand.cs` (replace stub)
- Create: `tests/Squad.Cli.Tests/Commands/CastCommandTests.cs`

Reference: `~/squad/packages/squad-cli/src/cli/commands/cast.ts`

- [ ] **Step 1: Write failing tests**

Create `tests/Squad.Cli.Tests/Commands/CastCommandTests.cs`:
```csharp
using Squad.Cli.Commands;
using Squad.Sdk.Config;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class CastCommandTests
{
    private string _root = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_root, ".squad"));
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Test]
    public async Task LoadCastAsync_returns_agents_from_config()
    {
        var json = """
            {
              "team": { "name": "Alpha" },
              "agents": [
                { "name": "striker", "role": "feature-dev" },
                { "name": "keeper", "role": "testing" }
              ]
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_root, "squad.config.json"), json);

        var agents = await CastCommand.LoadCastAsync(_root);

        agents.Count.ShouldBe(2);
        agents.ShouldContain(a => a.Name == "striker");
        agents.ShouldContain(a => a.Name == "keeper");
    }

    [Test]
    public async Task LoadCastAsync_returns_empty_when_no_config()
    {
        var agents = await CastCommand.LoadCastAsync(_root);
        agents.ShouldBeEmpty();
    }

    [Test]
    public async Task LoadCastAsync_includes_agent_role_and_model()
    {
        var json = """
            {
              "team": { "name": "Beta" },
              "agents": [{ "name": "maker", "role": "architecture", "model": "claude-sonnet-4.5" }]
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_root, "squad.config.json"), json);

        var agents = await CastCommand.LoadCastAsync(_root);

        agents[0].Role.ShouldBe("architecture");
        agents[0].Model.ShouldBe("claude-sonnet-4.5");
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Cli.Tests
```

Expected: compile error — `CastCommand.LoadCastAsync` not found.

- [ ] **Step 3: Implement CastCommand**

Overwrite `src/Squad.Cli/Commands/CastCommand.cs`:
```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using Squad.Sdk.Config;

namespace Squad.Cli.Commands;

public sealed class CastCommand : AsyncCommand<CastCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--dir <DIR>")]
        public string? Dir { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var cwd = settings.Dir ?? Directory.GetCurrentDirectory();
        var agents = await LoadCastAsync(cwd);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Session Cast[/]");
        AnsiConsole.MarkupLine(new string('═', 40));
        AnsiConsole.WriteLine();

        if (agents.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No agents found. Add a squad.config.json to this directory.[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("Agent");
        table.AddColumn("Role");
        table.AddColumn("Model");
        table.AddColumn("Skills");

        foreach (var agent in agents)
        {
            table.AddRow(
                $"[bold]{Markup.Escape(agent.Name)}[/]",
                agent.Role is { Length: > 0 } ? Markup.Escape(agent.Role) : "[dim]—[/]",
                agent.Model is { Length: > 0 } ? Markup.Escape(agent.Model) : "[dim]default[/]",
                agent.Skills.Count > 0 ? Markup.Escape(string.Join(", ", agent.Skills)) : "[dim]none[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]{agents.Count} agent{(agents.Count == 1 ? "" : "s")} in session cast[/]");

        return 0;
    }

    /// <summary>Load agents from squad.config.json in the given directory. Exposed for testing.</summary>
    public static async Task<IReadOnlyList<AgentConfig>> LoadCastAsync(string cwd)
    {
        var config = await ConfigLoader.LoadAsync(cwd);
        return config?.Agents ?? [];
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Cli.Tests
```

Expected: all CLI tests pass (doctor + cast).

- [ ] **Step 5: Smoke-test against squad repo**

```bash
cd ~/NSquad
dotnet run --project src/Squad.Cli -- cast --dir ~/squad
```

Expected: table showing agents from ~/squad/squad.config.ts (note: squad.config.ts won't be read — it'll show "no agents" which is correct; the .NET port uses squad.config.json).

- [ ] **Step 6: Commit**

```bash
cd ~/NSquad
git add src/Squad.Cli/Commands/CastCommand.cs tests/Squad.Cli.Tests/Commands/CastCommandTests.cs
git commit -m "feat(cli): implement cast command showing agent roster from squad.config.json"
```

---

## Task 12: `cost` Command

**Files:**
- Modify: `src/Squad.Cli/Commands/CostCommand.cs` (replace stub)
- Create: `tests/Squad.Cli.Tests/Commands/CostCommandTests.cs`

Reference: `~/squad/packages/squad-cli/src/cli/commands/cost.ts`

- [ ] **Step 1: Write failing tests**

Create `tests/Squad.Cli.Tests/Commands/CostCommandTests.cs`:
```csharp
using Squad.Cli.Commands;
using Squad.Sdk.CostTracker;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class CostCommandTests
{
    [Test]
    public void FormatCost_formats_small_amounts_in_cents()
    {
        CostCommand.FormatCost(0.0025m).ShouldBe("$0.0025");
    }

    [Test]
    public void FormatCost_formats_larger_amounts_to_two_decimals()
    {
        CostCommand.FormatCost(1.5m).ShouldBe("$1.50");
    }

    [Test]
    public void FormatCost_returns_zero_string_for_zero()
    {
        CostCommand.FormatCost(0m).ShouldBe("$0.00");
    }

    [Test]
    public void FormatTokens_abbreviates_thousands()
    {
        CostCommand.FormatTokens(1500).ShouldBe("1.5k");
        CostCommand.FormatTokens(999).ShouldBe("999");
        CostCommand.FormatTokens(10_000).ShouldBe("10.0k");
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Cli.Tests
```

Expected: compile error — `CostCommand.FormatCost`, `CostCommand.FormatTokens` not found.

- [ ] **Step 3: Implement CostCommand**

Overwrite `src/Squad.Cli/Commands/CostCommand.cs`:
```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using Squad.Sdk.CostTracker;
using Squad.Sdk.Resolution;

namespace Squad.Cli.Commands;

public sealed class CostCommand : AsyncCommand<CostCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--dir <DIR>")]
        public string? Dir { get; init; }

        [CommandOption("--all")]
        public bool ShowAll { get; init; }

        [CommandOption("--agent <AGENT>")]
        public string? AgentFilter { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var cwd = settings.Dir ?? Directory.GetCurrentDirectory();
        var squadDir = PathResolver.ResolveSquadDir(cwd);

        if (squadDir is null)
        {
            AnsiConsole.MarkupLine("[red]No .squad/ directory found.[/]");
            return 1;
        }

        var entries = await CostReader.LoadEntriesAsync(squadDir);

        if (!string.IsNullOrEmpty(settings.AgentFilter))
            entries = entries.Where(e => e.Agent.Contains(settings.AgentFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        if (entries.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No cost data found in .squad/costs/[/]");
            return 0;
        }

        var summary = CostReader.Summarize(entries);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Token Usage & Cost[/]");
        AnsiConsole.MarkupLine(new string('═', 55));
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Agent");
        table.AddColumn(new TableColumn("[dim]Input[/]").RightAligned());
        table.AddColumn(new TableColumn("[dim]Output[/]").RightAligned());
        table.AddColumn(new TableColumn("[dim]Total Tokens[/]").RightAligned());
        table.AddColumn(new TableColumn("Est. Cost").RightAligned());

        foreach (var (_, s) in summary.OrderByDescending(kv => kv.Value.TotalCost))
        {
            table.AddRow(
                $"[bold]{Markup.Escape(s.Agent)}[/]",
                $"[dim]{FormatTokens(s.TotalInputTokens)}[/]",
                $"[dim]{FormatTokens(s.TotalOutputTokens)}[/]",
                FormatTokens(s.TotalInputTokens + s.TotalOutputTokens),
                $"[green]{FormatCost(s.TotalCost)}[/]");
        }

        // Totals row
        var totalInput = summary.Values.Sum(s => s.TotalInputTokens);
        var totalOutput = summary.Values.Sum(s => s.TotalOutputTokens);
        var totalCost = summary.Values.Sum(s => s.TotalCost);

        table.AddEmptyRow();
        table.AddRow(
            "[bold]TOTAL[/]",
            $"[bold]{FormatTokens(totalInput)}[/]",
            $"[bold]{FormatTokens(totalOutput)}[/]",
            $"[bold]{FormatTokens(totalInput + totalOutput)}[/]",
            $"[bold green]{FormatCost(totalCost)}[/]");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        return 0;
    }

    /// <summary>Format a cost value. Exposed for testing.</summary>
    public static string FormatCost(decimal cost)
        => cost >= 1m ? $"${cost:F2}" : $"${cost:G4}";

    /// <summary>Format a token count, abbreviating thousands. Exposed for testing.</summary>
    public static string FormatTokens(int tokens)
        => tokens >= 1000 ? $"{tokens / 1000.0:F1}k" : tokens.ToString();
}
```

- [ ] **Step 4: Run all tests — expect pass**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Sdk.Tests
dotnet run --project tests/Squad.Cli.Tests
```

Expected: all tests pass in both test projects.

- [ ] **Step 5: Full build verification**

```bash
cd ~/NSquad
dotnet build
dotnet run --project src/Squad.Cli -- --help
dotnet run --project src/Squad.Cli -- doctor --help
dotnet run --project src/Squad.Cli -- cast --help
dotnet run --project src/Squad.Cli -- cost --help
```

Expected: all help text prints correctly.

- [ ] **Step 6: Commit**

```bash
cd ~/NSquad
git add src/Squad.Cli/Commands/CostCommand.cs tests/Squad.Cli.Tests/Commands/CostCommandTests.cs
git commit -m "feat(cli): implement cost command with per-agent token and cost summary"
```

---

## Task 13: Skills Loader

**Files:**
- Create: `src/Squad.Sdk/Skills/SkillDefinition.cs`
- Create: `src/Squad.Sdk/Skills/SkillLoader.cs`
- Create: `tests/Squad.Sdk.Tests/Skills/SkillLoaderTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Squad.Sdk.Tests/Skills/SkillLoaderTests.cs`:
```csharp
using Squad.Sdk.Skills;
using Shouldly;

namespace Squad.Sdk.Tests.Skills;

public class SkillLoaderTests
{
    private string _squadDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _squadDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), ".squad");
        Directory.CreateDirectory(Path.Combine(_squadDir, "skills", "my-skill"));
    }

    [After(Test)]
    public void Cleanup()
    {
        var parent = Path.GetDirectoryName(_squadDir)!;
        if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
    }

    [Test]
    public async Task DiscoverAsync_returns_empty_when_no_skills_dir()
    {
        var noSkills = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), ".squad");
        var result = await SkillLoader.DiscoverAsync(noSkills);
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task DiscoverAsync_finds_skills_with_SKILL_md()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_squadDir, "skills", "my-skill", "SKILL.md"),
            "# my-skill\n\nA skill.");

        var result = await SkillLoader.DiscoverAsync(_squadDir);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("my-skill");
    }

    [Test]
    public async Task DiscoverAsync_reads_skill_content()
    {
        var content = "# my-skill\n\nDoes something useful.";
        await File.WriteAllTextAsync(
            Path.Combine(_squadDir, "skills", "my-skill", "SKILL.md"),
            content);

        var result = await SkillLoader.DiscoverAsync(_squadDir);

        result[0].Content.ShouldBe(content);
    }

    [Test]
    public async Task DiscoverAsync_skips_dirs_without_SKILL_md()
    {
        // has a subdir but no SKILL.md
        Directory.CreateDirectory(Path.Combine(_squadDir, "skills", "incomplete-skill"));

        var result = await SkillLoader.DiscoverAsync(_squadDir);

        result.ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Sdk.Tests
```

Expected: compile error — `SkillLoader`, `SkillDefinition` not found.

- [ ] **Step 3: Implement skills types and loader**

Create `src/Squad.Sdk/Skills/SkillDefinition.cs`:
```csharp
namespace Squad.Sdk.Skills;

public record SkillDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
}
```

Create `src/Squad.Sdk/Skills/SkillLoader.cs`:
```csharp
namespace Squad.Sdk.Skills;

public static class SkillLoader
{
    /// <summary>
    /// Discover all skills in .squad/skills/ — each sub-directory containing
    /// a SKILL.md file is a skill. Returns skill name and content.
    /// </summary>
    public static async Task<IReadOnlyList<SkillDefinition>> DiscoverAsync(
        string squadDir,
        CancellationToken ct = default)
    {
        var skillsDir = System.IO.Path.Combine(squadDir, "skills");
        if (!Directory.Exists(skillsDir))
            return [];

        var skills = new List<SkillDefinition>();
        foreach (var dir in Directory.GetDirectories(skillsDir))
        {
            var skillMd = System.IO.Path.Combine(dir, "SKILL.md");
            if (!File.Exists(skillMd)) continue;

            var content = await File.ReadAllTextAsync(skillMd, ct);
            skills.Add(new SkillDefinition
            {
                Name = System.IO.Path.GetFileName(dir),
                Content = content,
                Path = skillMd,
            });
        }

        return skills;
    }
}
```

- [ ] **Step 4: Run all tests — expect pass**

```bash
cd ~/NSquad
dotnet run --project tests/Squad.Sdk.Tests
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/Skills/ tests/Squad.Sdk.Tests/Skills/
git commit -m "feat(sdk): add SkillLoader for discovering .squad/skills/ definitions"
```

---

## Task 14: Final Wiring + README

**Files:**
- Modify: `README.md`
- Create: `src/Squad.Sdk/SquadSdkServiceExtensions.cs`

- [ ] **Step 1: Add IServiceCollection extension for hosting scenarios**

Create `src/Squad.Sdk/SquadSdkServiceExtensions.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Squad.Sdk.Client;
using Squad.Sdk.Config;
using Squad.Sdk.Coordinator;
using Squad.Sdk.Events;
using Squad.Sdk.Skills;

namespace Squad.Sdk;

public static class SquadSdkServiceExtensions
{
    /// <summary>
    /// Register Squad SDK services.
    /// Requires squad.config.json to be loadable from the working directory.
    /// </summary>
    public static IServiceCollection AddSquadSdk(
        this IServiceCollection services,
        string? configDirectory = null)
    {
        services.AddSingleton<EventBus>();
        services.AddSingleton(sp =>
        {
            var dir = configDirectory ?? Directory.GetCurrentDirectory();
            return ConfigLoader.Load(dir)
                ?? throw new InvalidOperationException(
                    $"squad.config.json not found in {dir}. " +
                    "Create a squad.config.json before calling AddSquadSdk().");
        });
        services.AddSingleton(sp =>
            new SquadCoordinator(sp.GetRequiredService<SquadConfig>(), sp.GetRequiredService<EventBus>()));

        return services;
    }
}
```

- [ ] **Step 2: Verify everything still builds and tests pass**

```bash
cd ~/NSquad
dotnet build
dotnet run --project tests/Squad.Sdk.Tests
dotnet run --project tests/Squad.Cli.Tests
```

Expected: build success, all tests pass.

- [ ] **Step 3: Update README.md**

Overwrite `README.md`:
```markdown
# NSquad

.NET 10 port of [Squad](https://github.com/bradygaster/squad) — a programmable multi-agent runtime for GitHub Copilot.

## Status

Early development. SDK foundation + 3 CLI commands implemented.

## Prerequisites

- .NET 10 SDK
- GitHub Copilot CLI installed and authenticated (`copilot --version`)

## Installation

```bash
dotnet tool install --global Squad.Cli
```

## CLI Commands

```bash
squad doctor           # Validate .squad/ setup
squad cast             # Show current agent roster
squad cost             # Show token usage and cost summary
```

## SDK Usage

```csharp
using Squad.Sdk;
using Squad.Sdk.Client;

// Register services
services.AddSquadSdk();

// Or use directly
await using var client = await SquadClient.CreateAsync();
await using var session = await client.CreateSessionAsync(new SquadSessionOptions
{
    AgentName = "builder",
    Model = "claude-sonnet-4.5",
    Streaming = true,
});

var response = await session.SendAsync("Implement the new feature");
Console.WriteLine(response);
```

## Configuration

Create `squad.config.json` at your repo root:

```json
{
  "version": "1.0",
  "team": { "name": "My Team" },
  "agents": [
    { "name": "builder", "role": "feature-dev", "model": "claude-sonnet-4.5" },
    { "name": "tester",  "role": "testing",     "model": "claude-haiku-4.5" }
  ],
  "routing": {
    "defaultAgent": "builder",
    "rules": [
      { "pattern": "test|spec|coverage", "agent": "tester" }
    ]
  }
}
```

## Development

```bash
dotnet build
dotnet run --project tests/Squad.Sdk.Tests   # SDK tests
dotnet run --project tests/Squad.Cli.Tests   # CLI tests
dotnet run --project src/Squad.Cli -- doctor # Run CLI locally
```
```

- [ ] **Step 4: Final commit**

```bash
cd ~/NSquad
git add src/Squad.Sdk/SquadSdkServiceExtensions.cs README.md
git commit -m "feat(sdk): add IServiceCollection extension + update README"
```

---

## What's Next: Plan 2

The second plan covers the remaining ~15 non-interactive CLI commands:

`build`, `export`, `import`, `migrate`, `copilot`, `link`, `init-remote`, `economy`, `roles`, `personal`, `plugin`, `schedule`, `upstream`, `streams`, `discover`/`delegate`, `watch`, `extract`

Each of those follows the same pattern as this plan: read TypeScript source → write failing test → implement → pass. The SDK foundation from this plan is sufficient — Plan 2 is purely CLI command work with no new SDK primitives needed.
