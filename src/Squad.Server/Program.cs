using Squad.Sdk.Config;
using Squad.Server;
using Squad.Server.Grains;
using Squad.Server.Hubs;
using Squad.Server.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Load squad config — required before building
SquadConfig squadConfig = ConfigLoader.Load(Directory.GetCurrentDirectory())
    ?? throw new InvalidOperationException(
        "squad.config.json not found. Run 'squad init' in the project directory first.");

// Configure Orleans Silo (single silo, in-memory everything for 0.4.0)
builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddMemoryGrainStorage(Constants.AgentStateStore);
    siloBuilder.AddMemoryGrainStorage(Constants.PubSubStore);
    siloBuilder.AddMemoryStreams(Constants.AgentStreams);
});

// Register services
builder.Services.AddSingleton(squadConfig);
builder.Services.AddSingleton<ISquadClientFactory, SquadClientFactory>();
builder.Services.AddSingleton<ISquadConfigProvider, SquadConfigProvider>();
builder.Services.AddSignalR();

WebApplication app = builder.Build();

// Serve minimal web frontend
app.UseStaticFiles();
app.MapHub<SquadHub>("/hub");
app.MapFallbackToFile("index.html");

// Start the host (non-blocking)
await app.StartAsync();

// Wake core agents on startup (best-effort — server starts even if wake fails)
IGrainFactory grainFactory = app.Services.GetRequiredService<IGrainFactory>();
ISquadConfigProvider configProvider = app.Services.GetRequiredService<ISquadConfigProvider>();

// Core agents to wake, using ordinal ignore case to find the correctly-cased names from config
string[] coreAgents = [Constants.Ralph, Constants.Scribe, Constants.SquadLeader];
var agentsToWake = configProvider.GetAllAgentNames()
    .Where(name => coreAgents.Contains(name, StringComparer.OrdinalIgnoreCase));

foreach (string coreName in agentsToWake)
{
    try
    {
        app.Logger.LogInformation("Waking core agent {AgentName}", coreName);
        IAgentGrain grain = AgentGrainResolver.Resolve(grainFactory, coreName);
        await grain.WakeAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to wake core agent {AgentName} on startup", coreName);
    }
}

// Block until a shutdown signal arrives (Ctrl+C, SIGTERM, etc.)
try
{
    await app.WaitForShutdownAsync();
}
finally
{
    // Drain Orleans (streams, grains) before the DI container is disposed.
    // Without this, MemoryAdapterReceiver's polling loop fires into a dead
    // IServiceProvider and throws ObjectDisposedException.
    await app.StopAsync();
    await app.DisposeAsync();
}
