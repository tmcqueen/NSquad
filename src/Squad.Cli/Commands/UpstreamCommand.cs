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
                    AnsiConsole.MarkupLine("[green]✓[/] Added upstream: [bold]{0}[/]", Markup.Escape(settings.Name ?? settings.SourceOrName));
                }
                catch (Exception ex) { AnsiConsole.MarkupLine("[red]✗[/] {0}", Markup.Escape(ex.Message)); return 1; }
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
                    AnsiConsole.MarkupLine("[green]✓[/] Removed upstream: [bold]{0}[/]", Markup.Escape(settings.SourceOrName));
                }
                catch (Exception ex) { AnsiConsole.MarkupLine("[red]✗[/] {0}", Markup.Escape(ex.Message)); return 1; }
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
        if (type == "git")
        {
            var trimmed = source.TrimEnd('/');
            if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[..^4];
            return trimmed.Split('/').LastOrDefault() ?? "upstream";
        }
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
                Markup.Escape(u.Name), Markup.Escape(u.Type), Markup.Escape(u.Source),
                Markup.Escape(refStr), Markup.Escape(synced));
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
            if (specificName != null)
                AnsiConsole.MarkupLine("[red]✗[/] Upstream \"{0}\" not found.", Markup.Escape(specificName));
            else
                AnsiConsole.MarkupLine("[dim]No upstreams configured.[/]");
            return;
        }

        AnsiConsole.MarkupLine("\nSyncing {0} upstream(s)...\n", toSync.Count);
        var synced = 0;
        var squadDir = PathResolver.ResolveSquadDir(cwd) ?? Path.Combine(cwd, ".squad");

        foreach (var upstream in toSync)
        {
            if (upstream.Type is "local" or "export")
            {
                if (!Path.Exists(upstream.Source)) { AnsiConsole.MarkupLine("[yellow]⚠[/] {0}: not found", Markup.Escape(upstream.Name)); continue; }
                synced++;
                AnsiConsole.MarkupLine("[green]✓[/] {0} (read live): validated", Markup.Escape(upstream.Name));
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
                    AnsiConsole.MarkupLine("[green]✓[/] {0} (git — synced)", Markup.Escape(upstream.Name));
                }
                else
                    AnsiConsole.MarkupLine("[yellow]⚠[/] {0}: git sync failed", Markup.Escape(upstream.Name));
            }
        }

        AnsiConsole.MarkupLine("\n{0}/{1} upstream(s) synced.\n", synced, toSync.Count);
    }
}
