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
        string cwd = Directory.GetCurrentDirectory();
        try
        {
            await ImportAsync(cwd, settings.ImportFile, settings.Force, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine("[red]✗[/] {0}", Markup.Escape(ex.Message));
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
        string resolvedPath = Path.GetFullPath(importPath);
        if (!File.Exists(resolvedPath))
            throw new InvalidOperationException($"Import file not found: {importPath}");

        ExportManifest manifest;
        try
        {
            string json = await File.ReadAllTextAsync(resolvedPath, ct);
            manifest = JsonSerializer.Deserialize<ExportManifest>(json, _opts)
                ?? throw new InvalidOperationException("Empty import file.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid JSON: {ex.Message}");
        }

        ValidateManifest(manifest);

        string squadDir = Path.Combine(cwd, ".squad");
        if (Directory.Exists(squadDir))
        {
            if (!force)
                throw new InvalidOperationException(
                    "A squad already exists here. Use --force to replace (current squad will be archived).");

            string ts = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss");
            string archiveDir = Path.Combine(cwd, $".squad-archive-{ts}");
            Directory.Move(squadDir, archiveDir);
            AnsiConsole.MarkupLine("[dim]Archived existing squad to {0}[/]", Markup.Escape(Path.GetFileName(archiveDir)));
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
        string importDate = DateTimeOffset.UtcNow.ToString("O");
        string sourceProject = Path.GetFileNameWithoutExtension(resolvedPath);
        foreach (var (name, data) in manifest.Agents)
        {
            string agentDir = Path.Combine(squadDir, "agents", name);
            Directory.CreateDirectory(agentDir);

            if (data.Charter != null)
                await File.WriteAllTextAsync(Path.Combine(agentDir, "charter.md"), data.Charter, ct);

            string historyContent = data.History != null
                ? HistorySplitter.Split(data.History, sourceProject)
                : "";
            historyContent = $"📌 Imported from {sourceProject} on {importDate}. Portable knowledge carried over.\n\n"
                           + historyContent;
            await File.WriteAllTextAsync(Path.Combine(agentDir, "history.md"), historyContent, ct);
        }

        // Write skills
        string skillsBase = Path.Combine(cwd, ".copilot", "skills");
        foreach (string skillContent in manifest.Skills)
        {
            var nameMatch = System.Text.RegularExpressions.Regex.Match(
                skillContent, @"^name:\s*[""']?(.+?)[""']?\s*$", System.Text.RegularExpressions.RegexOptions.Multiline);
            string skillName = nameMatch.Success
                ? nameMatch.Groups[1].Value.Trim().ToLowerInvariant().Replace(" ", "-")
                : $"skill-{manifest.Skills.IndexOf(skillContent)}";
            string skillDir = Path.Combine(skillsBase, skillName);
            Directory.CreateDirectory(skillDir);
            await File.WriteAllTextAsync(Path.Combine(skillDir, "SKILL.md"), skillContent, ct);
        }

        AnsiConsole.MarkupLine("[green]✓[/] Imported squad from [bold]{0}[/]", Markup.Escape(Path.GetFileName(resolvedPath)));
        AnsiConsole.MarkupLine("  {0} agents: {1}", manifest.Agents.Count, Markup.Escape(string.Join(", ", manifest.Agents.Keys)));
        AnsiConsole.MarkupLine("  {0} skills imported", manifest.Skills.Count);
        AnsiConsole.MarkupLine("[yellow]⚠[/] Project-specific learnings are marked in agent histories — review if needed.");
    }
}
