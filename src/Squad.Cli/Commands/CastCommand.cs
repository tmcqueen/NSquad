using Spectre.Console.Cli;

namespace Squad.Cli.Commands;

public sealed class CastCommand : AsyncCommand<CastCommand.Settings>
{
    public sealed class Settings : CommandSettings { }
    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) => Task.FromResult(0);
}
