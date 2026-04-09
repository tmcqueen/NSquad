using Spectre.Console;
using Spectre.Console.Cli;
using Squad.Sdk.Discovery;

namespace Squad.Cli.Commands;

public sealed class DiscoverCommand : AsyncCommand
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken ct)
    {
        string cwd = Directory.GetCurrentDirectory();
        var squads = await SquadDiscovery.DiscoverAsync(cwd, ct);
        AnsiConsole.Write(SquadDiscovery.FormatTable(squads));
        return 0;
    }
}
