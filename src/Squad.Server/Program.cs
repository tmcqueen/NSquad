using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Squad.Sdk.Config;
using Squad.Server.Grains;
using Squad.Server.Hubs;
using Squad.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Load squad config — required before building
var squadConfig = ConfigLoader.Load(Directory.GetCurrentDirectory())
    ?? throw new InvalidOperationException(
        "squad.config.json not found. Run 'squad init' in the project directory first.");

// Configure Orleans Silo (single silo, in-memory everything for 0.4.0)
builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddMemoryGrainStorage("agentStore");
    siloBuilder.AddMemoryGrainStorage("PubSubStore");
    siloBuilder.AddMemoryStreams("AgentStreams");
});

// Register services
builder.Services.AddSingleton(squadConfig);
builder.Services.AddSingleton<ISquadClientFactory, SquadClientFactory>();
builder.Services.AddSingleton<ISquadConfigProvider, SquadConfigProvider>();
builder.Services.AddSignalR();

var app = builder.Build();

// Serve minimal web frontend
app.UseStaticFiles();
app.MapHub<SquadHub>("/hub");
app.MapFallbackToFile("index.html");

// Wake core agents on startup (best-effort — server starts even if wake fails)
var grainFactory = app.Services.GetRequiredService<IGrainFactory>();
foreach (var coreName in new[] { "ralph", "scribe", "squadleader" })
{
    try
    {
        var grain = AgentGrainResolver.Resolve(grainFactory, coreName);
        await grain.WakeAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to wake core agent {AgentName} on startup", coreName);
    }
}

await app.RunAsync();
