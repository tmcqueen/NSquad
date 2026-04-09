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
            Markup.Escape(Path.GetRelativePath(cwd, outPath)));
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
