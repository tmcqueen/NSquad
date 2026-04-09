using Microsoft.Extensions.DependencyInjection;
using Squad.Sdk.Config;
using Squad.Sdk.Coordinator;
using Squad.Sdk.Events;

namespace Squad.Sdk;

public static class SquadSdkServiceExtensions
{
    /// <summary>
    /// Register Squad SDK services.
    /// Requires squad.config.json to be loadable from the working directory.
    /// </summary>
    public static IServiceCollection AddSquadSdk(
        this IServiceCollection services,
        string? configDirectory = null)
    {
        services.AddSingleton<EventBus>();
        services.AddSingleton(sp =>
        {
            var dir = configDirectory ?? Directory.GetCurrentDirectory();
            return ConfigLoader.Load(dir)
                ?? throw new InvalidOperationException(
                    $"squad.config.json not found in {dir}. " +
                    "Create a squad.config.json before calling AddSquadSdk().");
        });
        services.AddSingleton(sp =>
            new SquadCoordinator(sp.GetRequiredService<SquadConfig>(), sp.GetRequiredService<EventBus>()));

        return services;
    }
}
