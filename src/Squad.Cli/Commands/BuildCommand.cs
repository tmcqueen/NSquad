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
                    exists ? "[yellow]overwrite[/]" : "[green]create[/]", Markup.Escape(f.RelPath));
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
