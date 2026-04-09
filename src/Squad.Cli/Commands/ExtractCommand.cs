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
