using Spectre.Console.Cli;

namespace Squad.Cli.Commands;

public sealed class CostCommand : AsyncCommand<CostCommand.Settings>
{
    public sealed class Settings : CommandSettings { }
    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) => Task.FromResult(0);
}
