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
                Markup.Escape(settings.SquadName),
                names.Length > 0 ? $" Known squads: {Markup.Escape(names)}" : " No squads discovered.");
            return 1;
        }

        if (!target.Manifest.Accepts.Contains("issues"))
        {
            AnsiConsole.MarkupLine("[red]✗[/] Squad \"{0}\" does not accept issues. Accepts: {1}",
                Markup.Escape(settings.SquadName), Markup.Escape(string.Join(", ", target.Manifest.Accepts)));
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
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);
        await proc.WaitForExitAsync(ct);
        var url = stdoutTask.Result.Trim();

        if (proc.ExitCode == 0)
            AnsiConsole.MarkupLine("[green]✓[/] Created cross-squad issue: {0}", Markup.Escape(url));
        else
        {
            AnsiConsole.MarkupLine("[red]✗[/] Failed to create issue.");
            return 1;
        }
        return 0;
    }

    private static string EscapeArg(string s) => s.Replace("\"", "\\\"").Replace("\n", "\\n");
}
