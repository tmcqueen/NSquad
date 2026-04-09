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
            AnsiConsole.MarkupLine("[green]✓[/] Personal squad initialized at [dim]{0}[/]", Markup.Escape(personalDir));
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine("[yellow]⚠[/] {0}", Markup.Escape(ex.Message));
        }
        return 0;
    }
}
