using Squad.Sdk.Client;

namespace Squad.Server.Services;

/// <summary>
/// Creates SquadClient instances using Squad.Sdk defaults.
/// Each call starts a new copilot CLI process.
/// </summary>
public sealed class SquadClientFactory : ISquadClientFactory
{
    public Task<SquadClient> CreateAsync(CancellationToken ct = default)
        => SquadClient.CreateAsync(ct: ct);
}
