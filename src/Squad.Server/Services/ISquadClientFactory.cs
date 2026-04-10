using Squad.Sdk.Client;

namespace Squad.Server.Services;

/// <summary>
/// Creates SquadClient instances. Abstracted for testability — grains depend on this
/// interface rather than calling SquadClient.CreateAsync() directly.
/// </summary>
public interface ISquadClientFactory
{
    /// <summary>Create and start a new SquadClient backed by the copilot CLI.</summary>
    Task<SquadClient> CreateAsync(CancellationToken ct = default);
}
