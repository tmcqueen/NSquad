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
