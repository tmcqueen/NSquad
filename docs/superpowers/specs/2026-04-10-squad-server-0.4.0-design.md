# Squad Server 0.4.0 Design Spec

**Date:** 2026-04-10  
**Version:** 0.4.0  
**Feature:** Core Squad Server with Orleans-based agent lifecycle  
**Status:** In Design

---

## Overview

Squad Server is the core unit of the Brigade orchestration platform. It runs as a **Microsoft Orleans 10 Silo** hosted in ASP.NET Core, where each agent is a **virtual actor (Grain)** with persistent state. The server loads agents from `squad.config.json`, maintains always-on grains for the core trio (Ralph, Scribe, SquadLeader), and exposes a **SignalR hub** for clients (TUI, web frontend, future Quartermaster) to send messages and manage agent lifecycle.

**Orleans Version:** This project targets **Microsoft Orleans 10.x** (`Microsoft.Orleans.Server`, `Microsoft.Orleans.Streaming`, etc.). All grain patterns, silo configuration, and streaming APIs must follow Orleans 10 conventions.

**Goals for 0.4.0:**
- Establish server-client communication via SignalR
- Implement agent lifecycle using Orleans Grains (activate = wake, deactivate = suspend)
- Provide SquadLeader's agent management tools (wake, suspend, send-to, status)
- Inter-agent communication via Orleans Streams with BroadcastChannel
- Build a minimal web frontend to test the system end-to-end
- Update agent templates to Military Squad theme (replacing NASA Mission Control)
- No durable persistence — in-memory grain storage only (persistence deferred to 0.4.1)

**Why Orleans?**
Orleans virtual actors map directly to agent lifecycle: activation = wake, deactivation = suspend, state persists automatically within grain lifetime. This eliminates hand-rolled `AgentManager` state tracking, manual session serialization, and ad-hoc lifecycle management. It also provides a clear upgrade path to multi-silo clustering for Headquarters (0.7.0) and Quartermaster (0.8.0).

---

## Architecture

### High-Level Components

```
┌──────────────────────────────────────────────────────┐
│                   Squad Server                        │
│                  (Orleans Silo)                        │
│                                                       │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────────┐  │
│  │ Ralph Grain  │  │ Scribe Grain│  │ SquadLeader  │  │
│  │             │  │             │  │    Grain     │  │
│  │ - charter   │  │ - charter   │  │ - charter    │  │
│  │ - messages  │  │ - messages  │  │ - messages   │  │
│  │ - session   │  │ - session   │  │ - tools      │  │
│  └──────┬──────┘  └──────┬──────┘  └──────┬───────┘  │
│         │                │                │           │
│         └───── Orleans Streams ───────────┘           │
│                (BroadcastChannel)                      │
│                                                       │
│  ┌────────────────────────────────────────────────┐   │
│  │              SquadHub (SignalR)                 │   │
│  │  SendMessage | WakeAgent | SuspendAgent | ...  │   │
│  └───────────────────┬────────────────────────────┘   │
└──────────────────────┼────────────────────────────────┘
                       │ WebSocket
          ┌────────────┼────────────┐
          │            │            │
     ┌────┴────┐  ┌────┴────┐  ┌───┴────┐
     │  TUI    │  │  Web    │  │ Future │
     │ Client  │  │Frontend │  │  HQ    │
     └─────────┘  └─────────┘  └────────┘
```

### Logical Flow

```
Client connects via SignalR
├─ Client sends: SendMessage(agentName, prompt)
│  ├─ SquadHub resolves IAgentGrain from GrainFactory
│  ├─ Grain.SendAsync(prompt) — creates SquadSession if not active
│  ├─ SquadSession.SendStreamingAsync streams tokens
│  ├─ Grain publishes deltas to Orleans Stream
│  ├─ SquadHub subscribes to stream, forwards to client via OnDelta
│  └─ On completion: client receives OnComplete
│
├─ Client sends: WakeAgent(agentName) [SquadLeader tool]
│  ├─ SquadHub calls IAgentGrain.WakeAsync()
│  ├─ Grain activates (OnActivateAsync), creates SquadSession
│  └─ Broadcasts OnAgentStatusChanged("idle") to all clients
│
├─ Client sends: SuspendAgent(agentName)
│  ├─ SquadHub calls IAgentGrain.SuspendAsync()
│  ├─ Grain saves state, disposes SquadSession, deactivates
│  └─ Broadcasts OnAgentStatusChanged("suspended") to all clients
│
└─ Client polls: GetAgentStatus()
   └─ Returns { ralph: "idle", scribe: "processing", squadleader: "idle" }
```

---

## Orleans Grain Design

### Grain Interface

```csharp
public interface IAgentGrain : IGrainWithStringKey
{
    Task SendAsync(string prompt);
    Task WakeAsync();
    Task SuspendAsync();
    Task<AgentStatus> GetStatusAsync();
    Task<IReadOnlyList<ChatMessage>> GetHistoryAsync();
}
```

### Shared Types

```csharp
[GenerateSerializer, Alias(nameof(AgentGrainState))]
public class AgentGrainState
{
    [Id(0)] public string CharterPath { get; set; } = "";
    [Id(1)] public string AgentName { get; set; } = "";
    [Id(2)] public List<ChatMessage> MessageHistory { get; set; } = [];
    [Id(3)] public string? SessionId { get; set; }
    [Id(4)] public AgentStatus Status { get; set; } = AgentStatus.Suspended;
}

[GenerateSerializer]
public enum AgentStatus { Suspended, Idle, Processing, Error }

[GenerateSerializer, Alias(nameof(ChatMessage))]
public sealed record ChatMessage
{
    [Id(0)] public string Role { get; set; } = "";
    [Id(1)] public string Content { get; set; } = "";
    [Id(2)] public DateTime Timestamp { get; set; }
}

[GenerateSerializer, Alias(nameof(AgentStreamEvent))]
public sealed record AgentStreamEvent
{
    public AgentStreamEvent(
        AgentStreamEventType Type, string? Text = null, AgentStatus? Status = null)
    {
        this.Type = Type; this.Text = Text; this.Status = Status;
    }

    [Id(0)] public AgentStreamEventType Type { get; init; }
    [Id(1)] public string? Text { get; init; }
    [Id(2)] public AgentStatus? Status { get; init; }
}

[GenerateSerializer]
public enum AgentStreamEventType { Delta, Completed, StatusChanged, Error }
```

### Grain Hierarchy

```
AgentGrain (abstract)
├── RalphAgentGrain      — persistent collaborator, pre-set charter + tools
├── ScribeAgentGrain     — silent logger/decision merger, pre-set charter + tools
├── SquadLeaderAgentGrain — lead/orchestrator, agent management tools (wake/suspend/send-to/status)
└── SquadMemberGrain     — generic agent, configured via SquadMemberConfiguration
```

The three core grains (Ralph, Scribe, SquadLeader) have **hard-coded** charter paths, system prompts, and tool sets baked into the grain class. `SquadMemberGrain` is the flexible type — it reads its configuration from a `SquadMemberConfiguration` object resolved at activation time, making it suitable for any agent defined in `squad.config.json`.

### Abstract Base: AgentGrain

Contains all shared lifecycle, streaming, and session management logic. Concrete grains override `GetCharterPath()` and `GetTools()` to customize behavior.

```csharp
public abstract class AgentGrain : Grain, IAgentGrain
{
    private readonly IPersistentState<AgentGrainState> _state;
    private readonly ISquadClientFactory _clientFactory;
    private readonly ILogger _logger;

    // NOT serializable — recreated on each activation
    private SquadSession? _session;
    private IAsyncStream<AgentStreamEvent>? _outputStream;

    protected AgentGrain(
        [PersistentState("agent", "agentStore")]
        IPersistentState<AgentGrainState> state,
        ISquadClientFactory clientFactory,
        ILogger logger)
    {
        _state = state;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    // --- Extension points for concrete grains ---

    /// <summary>Path to this agent's charter template.</summary>
    protected abstract string GetCharterPath();

    /// <summary>Tools available to this agent during conversations.</summary>
    protected abstract IReadOnlyList<AgentTool> GetTools();

    /// <summary>Optional hook for subclasses to run logic after session creation.</summary>
    protected virtual Task OnSessionCreatedAsync(SquadSession session) => Task.CompletedTask;

    // --- Lifecycle ---

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var streamProvider = this.GetStreamProvider("AgentStreams");
        var streamId = StreamId.Create("AgentOutput", this.GetPrimaryKeyString());
        _outputStream = streamProvider.GetStream<AgentStreamEvent>(streamId);

        // Ensure charter path is set in state
        _state.State.CharterPath = GetCharterPath();
        _state.State.AgentName = this.GetPrimaryKeyString();

        if (_state.State.Status != AgentStatus.Suspended)
            await CreateSessionAsync(ct);
    }

    public override async Task OnDeactivateAsync(
        DeactivationReason reason, CancellationToken ct)
    {
        _state.State.Status = AgentStatus.Suspended;
        await _state.WriteStateAsync();
        _session?.Dispose();
        _session = null;
    }

    // --- IAgentGrain ---

    public async Task WakeAsync()
    {
        if (_session is not null) return;

        await CreateSessionAsync(CancellationToken.None);
        _state.State.Status = AgentStatus.Idle;
        await _state.WriteStateAsync();

        await _outputStream!.OnNextAsync(
            new AgentStreamEvent(AgentStreamEventType.StatusChanged, Status: AgentStatus.Idle));
    }

    public async Task SuspendAsync()
    {
        _session?.Dispose();
        _session = null;
        _state.State.Status = AgentStatus.Suspended;
        await _state.WriteStateAsync();

        await _outputStream!.OnNextAsync(
            new AgentStreamEvent(AgentStreamEventType.StatusChanged, Status: AgentStatus.Suspended));

        DeactivateOnIdle();
    }

    public async Task SendAsync(string prompt)
    {
        if (_session is null)
            await CreateSessionAsync(CancellationToken.None);

        _state.State.Status = AgentStatus.Processing;
        _state.State.MessageHistory.Add(new ChatMessage
        {
            Role = "user", Content = prompt, Timestamp = DateTime.UtcNow
        });

        await _outputStream!.OnNextAsync(
            new AgentStreamEvent(AgentStreamEventType.StatusChanged, Status: AgentStatus.Processing));

        try
        {
            var fullResponse = new StringBuilder();

            await _session!.SendStreamingAsync(prompt, delta =>
            {
                fullResponse.Append(delta);
                _outputStream!.OnNextAsync(
                    new AgentStreamEvent(AgentStreamEventType.Delta, Text: delta));
                return Task.CompletedTask;
            });

            _state.State.MessageHistory.Add(new ChatMessage
            {
                Role = "assistant",
                Content = fullResponse.ToString(),
                Timestamp = DateTime.UtcNow
            });

            _state.State.Status = AgentStatus.Idle;
            await _state.WriteStateAsync();

            await _outputStream!.OnNextAsync(
                new AgentStreamEvent(AgentStreamEventType.Completed));
        }
        catch (Exception ex)
        {
            _state.State.Status = AgentStatus.Error;
            await _state.WriteStateAsync();

            await _outputStream!.OnNextAsync(
                new AgentStreamEvent(AgentStreamEventType.Error, Text: ex.Message));
        }
    }

    public Task<AgentStatus> GetStatusAsync() =>
        Task.FromResult(_state.State.Status);

    public Task<IReadOnlyList<ChatMessage>> GetHistoryAsync() =>
        Task.FromResult<IReadOnlyList<ChatMessage>>(_state.State.MessageHistory);

    // --- Helpers ---

    protected IAsyncStream<AgentStreamEvent> OutputStream => _outputStream!;
    protected AgentGrainState State => _state.State;

    protected async Task WriteStateAsync() => await _state.WriteStateAsync();

    private async Task CreateSessionAsync(CancellationToken ct)
    {
        var client = _clientFactory.Create();
        _session = _state.State.SessionId is not null
            ? await client.ResumeSessionAsync(_state.State.SessionId, ct)
            : await client.CreateSessionAsync(_state.State.CharterPath, ct);

        _state.State.SessionId = _session.SessionId;
        await OnSessionCreatedAsync(_session);
    }
}
```

### RalphAgentGrain — Persistent Collaborator

```csharp
public sealed class RalphAgentGrain : AgentGrain
{
    public RalphAgentGrain(
        [PersistentState("agent", "agentStore")]
        IPersistentState<AgentGrainState> state,
        ISquadClientFactory clientFactory,
        ILogger<RalphAgentGrain> logger) : base(state, clientFactory, logger) { }

    protected override string GetCharterPath() => "templates/ralph/charter.md";

    protected override IReadOnlyList<AgentTool> GetTools() =>
    [
        // Ralph's pre-set tools: code analysis, knowledge recall, etc.
        // Defined in implementation phase
    ];
}
```

### ScribeAgentGrain — Silent Logger / Decision Merger

```csharp
public sealed class ScribeAgentGrain : AgentGrain
{
    public ScribeAgentGrain(
        [PersistentState("agent", "agentStore")]
        IPersistentState<AgentGrainState> state,
        ISquadClientFactory clientFactory,
        ILogger<ScribeAgentGrain> logger) : base(state, clientFactory, logger) { }

    protected override string GetCharterPath() => "templates/scribe/charter.md";

    protected override IReadOnlyList<AgentTool> GetTools() =>
    [
        // Scribe's pre-set tools: log decisions, merge learnings, etc.
        // Defined in implementation phase
    ];

    // Scribe observes all agent streams silently
    // See "Inter-Agent Communication" section for observation pattern
}
```

### SquadLeaderAgentGrain — Lead / Orchestrator

SquadLeader has agent management tools that resolve other grains via `IGrainFactory`.

```csharp
public sealed class SquadLeaderAgentGrain : AgentGrain
{
    private readonly IGrainFactory _grainFactory;

    public SquadLeaderAgentGrain(
        [PersistentState("agent", "agentStore")]
        IPersistentState<AgentGrainState> state,
        ISquadClientFactory clientFactory,
        IGrainFactory grainFactory,
        ILogger<SquadLeaderAgentGrain> logger) : base(state, clientFactory, logger)
    {
        _grainFactory = grainFactory;
    }

    protected override string GetCharterPath() => "templates/squadleader/charter.md";

    protected override IReadOnlyList<AgentTool> GetTools() =>
    [
        new AgentTool("WakeAgent", "Activate an agent", WakeAgentAsync),
        new AgentTool("SuspendAgent", "Suspend an agent", SuspendAgentAsync),
        new AgentTool("SendTo", "Send message to specific agent", SendToAgentAsync),
        new AgentTool("GetAgentStatus", "List all agents and states", GetAllStatusAsync),
    ];

    private async Task<string> WakeAgentAsync(string args)
    {
        var grain = AgentGrainResolver.Resolve(_grainFactory, args);
        await grain.WakeAsync();
        return $"Agent '{args}' is now awake.";
    }

    private async Task<string> SuspendAgentAsync(string args)
    {
        var grain = AgentGrainResolver.Resolve(_grainFactory, args);
        await grain.SuspendAsync();
        return $"Agent '{args}' suspended.";
    }

    private async Task<string> SendToAgentAsync(string args)
    {
        // args format: "agentName|prompt"
        var parts = args.Split('|', 2);
        var grain = AgentGrainResolver.Resolve(_grainFactory, parts[0]);
        await grain.SendAsync(parts[1]);
        return $"Message sent to '{parts[0]}'.";
    }

    private async Task<string> GetAllStatusAsync(string args)
    {
        // Query all configured agents — implementation resolves from SquadConfig
        return "{ ... }"; // JSON status map
    }
}
```

### SquadMemberGrain — Configurable Generic Agent

For any agent defined in `squad.config.json` that isn't one of the core three. Receives its configuration at activation from a `SquadMemberConfiguration` resolved via DI or grain state.

```csharp
[GenerateSerializer, Alias(nameof(SquadMemberConfiguration))]
public sealed class SquadMemberConfiguration
{
    [Id(0)] public string CharterPath { get; set; } = "";
    [Id(1)] public string Role { get; set; } = "";
    [Id(2)] public string Description { get; set; } = "";
    [Id(3)] public List<string> ToolNames { get; set; } = [];
    [Id(4)] public Dictionary<string, string> Parameters { get; set; } = [];
}

public sealed class SquadMemberGrain : AgentGrain
{
    private readonly ISquadConfigProvider _configProvider;
    private SquadMemberConfiguration? _config;

    public SquadMemberGrain(
        [PersistentState("agent", "agentStore")]
        IPersistentState<AgentGrainState> state,
        ISquadClientFactory clientFactory,
        ISquadConfigProvider configProvider,
        ILogger<SquadMemberGrain> logger) : base(state, clientFactory, logger)
    {
        _configProvider = configProvider;
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        // Load configuration for this agent from squad.config.json
        _config = _configProvider.GetMemberConfig(this.GetPrimaryKeyString());
        await base.OnActivateAsync(ct);
    }

    protected override string GetCharterPath() =>
        _config?.CharterPath ?? $"templates/{this.GetPrimaryKeyString()}/charter.md";

    protected override IReadOnlyList<AgentTool> GetTools()
    {
        if (_config is null) return [];

        // Resolve tools by name from a tool registry
        // e.g., _config.ToolNames = ["code-analysis", "file-search"]
        // → look up AgentTool instances from IToolRegistry
        return []; // resolved at implementation time
    }
}
```

### Grain Resolution

The SignalR hub and startup code need to resolve the correct grain type by agent name:

```csharp
public static class AgentGrainResolver
{
    private static readonly HashSet<string> CoreAgents = new(StringComparer.OrdinalIgnoreCase)
    {
        "ralph", "scribe", "squadleader"
    };

    /// <summary>
    /// Resolves the correct IAgentGrain reference for the given agent name.
    /// Core agents use their dedicated grain types; others use SquadMemberGrain.
    /// Orleans routes to the correct grain class via the grain key.
    /// </summary>
    public static IAgentGrain Resolve(IGrainFactory factory, string agentName) =>
        agentName.ToLowerInvariant() switch
        {
            "ralph" => factory.GetGrain<IAgentGrain>(agentName, grainClassNamePrefix: nameof(RalphAgentGrain)),
            "scribe" => factory.GetGrain<IAgentGrain>(agentName, grainClassNamePrefix: nameof(ScribeAgentGrain)),
            "squadleader" => factory.GetGrain<IAgentGrain>(agentName, grainClassNamePrefix: nameof(SquadLeaderAgentGrain)),
            _ => factory.GetGrain<IAgentGrain>(agentName, grainClassNamePrefix: nameof(SquadMemberGrain)),
        };
}
```

---

## SquadSession Lifecycle

**Key constraint:** `SquadSession` wraps a live network connection to `gh copilot`. It is **NOT serializable** and cannot be stored in grain state.

**Lifecycle:**
1. **Grain activates** → `OnActivateAsync` checks if session was previously active
2. **CreateSessionAsync** → creates fresh `SquadSession` from `ISquadClientFactory`
3. **If session ID exists** → `ResumeSessionAsync` to reconnect with prior context
4. **If no session ID** → `CreateSessionAsync` with charter path for new session
5. **Message history replay** → feed stored messages back into session for context continuity
6. **Grain deactivates** → `OnDeactivateAsync` disposes `SquadSession`, saves state
7. **Next activation** → repeat from step 1, recreating session from stored state

**For 0.4.0:** Message history stored in grain state as `List<ChatMessage>`. This provides catch-up for clients that connect after messages were sent. Upgrade to `JournaledGrain` with event sourcing planned for 0.4.1.

---

## Inter-Agent Communication

Orleans Streams with **BroadcastChannel** enable agents to communicate without direct grain-to-grain calls:

```csharp
// SquadLeader publishes a directive to all agents
var channelId = ChannelId.Create("SquadBroadcast", "all");
var writer = provider.GetChannelWriter<SquadDirective>(channelId);
await writer.Publish(new SquadDirective("booster", "wake"));

// AgentGrain base class implements IOnBroadcastChannelSubscribed
// Concrete grains inherit this behavior automatically
[ImplicitChannelSubscription]
public abstract class AgentGrain : Grain, IAgentGrain, IOnBroadcastChannelSubscribed
{
    public Task OnSubscribed(IBroadcastChannelSubscription subscription)
    {
        subscription.Attach<SquadDirective>(
            item => HandleDirective(item),
            ex => HandleError(ex));
        return Task.CompletedTask;
    }
}
```

**Use cases:**
- SquadLeader broadcasts "wake booster" → all grains see it, Booster grain activates
- Scribe listens to all agent output streams for silent logging
- Future: agent-to-agent task delegation without client involvement

---

## SignalR Hub (Client Communication)

The SignalR hub is the **only client-facing interface**. It bridges clients to Orleans grains.

```csharp
public sealed class SquadHub : Hub
{
    private readonly IGrainFactory _grainFactory;
    private readonly IClusterClient _clusterClient;

    public SquadHub(IGrainFactory grainFactory, IClusterClient clusterClient)
    {
        _grainFactory = grainFactory;
        _clusterClient = clusterClient;
    }

    // Send message to an agent; stream deltas back via OnDelta
    public async Task SendMessage(string agentName, string prompt)
    {
        var grain = AgentGrainResolver.Resolve(_grainFactory, agentName);

        // Subscribe to this agent's output stream for this connection
        var streamProvider = _clusterClient.GetStreamProvider("AgentStreams");
        var streamId = StreamId.Create("AgentOutput", agentName);
        var stream = streamProvider.GetStream<AgentStreamEvent>(streamId);

        await stream.SubscribeAsync(async (evt, token) =>
        {
            switch (evt.Type)
            {
                case AgentStreamEventType.Delta:
                    await Clients.Caller.SendAsync("OnDelta", agentName, evt.Text);
                    break;
                case AgentStreamEventType.Completed:
                    await Clients.Caller.SendAsync("OnComplete", agentName);
                    break;
                case AgentStreamEventType.StatusChanged:
                    await Clients.All.SendAsync(
                        "OnAgentStatusChanged", agentName, evt.Status.ToString());
                    break;
                case AgentStreamEventType.Error:
                    await Clients.Caller.SendAsync("OnError", agentName, evt.Text);
                    break;
            }
        });

        await grain.SendAsync(prompt);
    }

    // Wake an agent (create session)
    public async Task WakeAgent(string agentName)
    {
        var grain = AgentGrainResolver.Resolve(_grainFactory, agentName);
        await grain.WakeAsync();
    }

    // Suspend an agent (close session, preserve state)
    public async Task SuspendAgent(string agentName)
    {
        var grain = AgentGrainResolver.Resolve(_grainFactory, agentName);
        await grain.SuspendAsync();
    }

    // Get all agent statuses
    public async Task<Dictionary<string, string>> GetAgentStatus()
    {
        // Read agent names from loaded SquadConfig
        // Query each grain's status
        var statuses = new Dictionary<string, string>();
        // ... iterate configured agents, call GetStatusAsync()
        return statuses;
    }

    // Get message history for catch-up
    public async Task<IReadOnlyList<ChatMessage>> GetHistory(string agentName)
    {
        var grain = AgentGrainResolver.Resolve(_grainFactory, agentName);
        return await grain.GetHistoryAsync();
    }
}
```

---

## SquadLeader Tools

SquadLeader is an agent grain with additional **tools** in its system prompt that map to grain operations:

| Tool | Description | Implementation |
|------|-------------|----------------|
| `WakeAgent(name)` | Activate an agent grain | `AgentGrainResolver.Resolve(factory, name).WakeAsync()` |
| `SuspendAgent(name)` | Deactivate an agent grain | `AgentGrainResolver.Resolve(factory, name).SuspendAsync()` |
| `SendTo(name, prompt)` | Send message to specific agent | `AgentGrainResolver.Resolve(factory, name).SendAsync(prompt)` |
| `GetAgentStatus()` | List all agents and states | Query all configured agent grains |

These tools are defined in SquadLeader's charter/system prompt and executed by the grain when the LLM invokes them during a conversation. The grain resolves other agent grains via `IGrainFactory` (injected via DI).

---

## Server Startup

```csharp
var builder = WebApplication.CreateBuilder(args);

// Load squad config
var config = SquadConfig.Load("squad.config.json");

// Configure Orleans Silo (single silo for 0.4.0)
builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddMemoryGrainStorage("agentStore");
    siloBuilder.AddMemoryStreams("AgentStreams");
    siloBuilder.AddBroadcastChannel(
        "SquadBroadcast",
        options => options.FireAndForgetDelivery = false);
});

// Register Squad SDK services
builder.Services.AddSquadSdk(config);
builder.Services.AddSingleton<ISquadClientFactory, SquadClientFactory>();
builder.Services.AddSignalR();

var app = builder.Build();

// Map SignalR hub
app.MapHub<SquadHub>("/hub");

// Serve minimal web frontend
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

// Wake core agents on startup
var grainFactory = app.Services.GetRequiredService<IGrainFactory>();
foreach (var coreName in new[] { "ralph", "scribe", "squadleader" })
{
    var grain = AgentGrainResolver.Resolve(grainFactory, coreName);
    await grain.WakeAsync();
}

await app.RunAsync();
```

**Key decisions:**
- `UseLocalhostClustering()` — single silo, local development. Multi-silo clustering deferred to 0.7.0.
- `AddMemoryGrainStorage("agentStore")` — in-memory only for 0.4.0. Durable storage (Azure Table, etc.) added in 0.4.1.
- `AddMemoryStreams("AgentStreams")` — in-memory stream provider. Suitable for single-silo.
- Core agents woken immediately on startup. Other agents stay suspended until SquadLeader wakes them.

---

## Components to Build / Modify

### New Projects / Files

1. **`src/Squad.Server/` (new ASP.NET Core + Orleans project)**
   - `Squad.Server.csproj` — references Squad.Sdk, Microsoft.Orleans.Server 10.x, Microsoft.Orleans.Streaming 10.x
   - `Program.cs` — startup, Orleans silo config, SignalR, core agent wake
   - `Hubs/SquadHub.cs` — SignalR hub bridging clients to grains
   - `Grains/IAgentGrain.cs` — grain interface
   - `Grains/AgentGrain.cs` — abstract base grain with shared lifecycle/streaming logic
   - `Grains/RalphAgentGrain.cs` — persistent collaborator grain
   - `Grains/ScribeAgentGrain.cs` — silent logger/decision merger grain
   - `Grains/SquadLeaderAgentGrain.cs` — lead/orchestrator grain with agent management tools
   - `Grains/SquadMemberGrain.cs` — configurable generic agent grain
   - `Grains/AgentGrainResolver.cs` — resolves agent name → correct grain type
   - `Models/AgentGrainState.cs` — serializable grain state
   - `Models/AgentStreamEvent.cs` — stream event types
   - `Models/ChatMessage.cs` — serializable message record
   - `Models/SquadMemberConfiguration.cs` — configuration for generic squad members
   - `Services/ISquadClientFactory.cs` — factory for creating SquadClient instances
   - `Services/SquadClientFactory.cs` — implementation
   - `Services/ISquadConfigProvider.cs` — provides SquadMemberConfiguration by agent name

2. **`src/Squad.Server/wwwroot/` (minimal web frontend)**
   - `index.html` — layout with agent selector, message stream, input panel, status bar
   - `app.js` — SignalR connection, UI updates, event handling
   - `style.css` — basic styling

3. **`templates/` (updated agent charters)**
   - Refresh all charters to Military Squad theme
   - Consistent naming and role descriptions

### Modified Projects / Files

1. **`NSquad.slnx`** — add Squad.Server project
2. **`src/Squad.Sdk/Client/`** — add `ISquadClientFactory` interface if needed

---

## Minimal Web Frontend

**Components:**
1. **Agent Selector** — list of all agents from config, highlights active agents, click to switch
2. **Message Stream** — scrollable div, auto-scrolls on new deltas, color-codes agent names
3. **Status Bar** — all agents with current status, auto-updates via SignalR `OnAgentStatusChanged`
4. **Input Panel** — text area + send button, calls `hub.SendMessage(selectedAgent, text)`

**Tech:** Plain HTML, vanilla JS, SignalR JS client (`@microsoft/signalr`). No framework.

---

## Error Handling

**Server-side:**
- Agent grain not found → `OnError` with message (grain activates on demand, so this is rare)
- SquadSession creation fails (gh copilot unavailable) → grain enters `Error` status, publishes error event
- Streaming fails mid-message → grain publishes partial response + error event
- Grain deactivation timeout → Orleans handles gracefully, session disposed in `OnDeactivateAsync`

**Client-side:**
- SignalR connection lost → show "disconnected", attempt reconnect with backoff
- `OnError` → display in message stream with error styling
- Timeout on message (configurable, e.g., 5 min) → cancel and show timeout error

---

## No Durable Persistence (0.4.0)

**Explicitly out of scope:**
- Durable conversation storage (added in 0.4.1 with JournaledGrain event sourcing)
- Cost tracking persistence (added in 0.4.1)
- Session resumption after server restart (added in 0.4.1)
- User authentication (out of scope)
- Multi-silo clustering (deferred to 0.7.0)

**In-memory only:**
- `AddMemoryGrainStorage` — grain state lives in memory, lost on server restart
- `AddMemoryStreams` — stream events not persisted
- Server restart = all conversations and agent state lost

---

## Testing Strategy

**Manual:**
1. Start Squad Server (`dotnet run --project src/Squad.Server`)
2. Open minimal web frontend at `http://localhost:5000`
3. Verify core agents (Ralph, Scribe, SquadLeader) show as "idle"
4. Send message to Ralph → verify response streams in real-time
5. SquadLeader wakes Booster → verify status changes broadcast to all clients
6. Send message to Booster → verify response
7. SquadLeader suspends Booster → verify status changes

**Unit:**
- Agent grain state transitions (suspended → idle → processing → idle)
- SquadHub routing to correct grain
- Stream event serialization/deserialization
- Error handling paths

**Integration:**
- Full flow: SignalR connect → send message → grain processes → stream → client receives
- Multi-client: two clients connected, both see status changes
- Grain activation/deactivation lifecycle

---

## Deployment (0.4.0)

**Local testing only:**
- `dotnet run --project src/Squad.Server`
- Open browser to `http://localhost:5000`
- Requires `squad.config.json` in current directory
- Requires `gh copilot` binary on PATH
- Single Orleans silo, in-memory everything

**Dockerization deferred to 0.6.0**

---

## Constraints and Decisions

| Decision | Rationale |
|----------|-----------|
| Orleans 10.x | Latest stable release; required for current grain lifecycle APIs and streaming |
| Single silo | `gh copilot` is a local process; multi-silo requires 0.7.0 |
| Abstract AgentGrain + 4 concrete grains | Core agents (Ralph, Scribe, SquadLeader) have pre-set tools/charters; SquadMemberGrain is config-driven for all others |
| Memory grain storage | No durable persistence needed for 0.4.0; swap to Azure Table/etc in 0.4.1 |
| Memory streams | Single silo doesn't need distributed streaming |
| SquadSession recreated on activation | Wraps live network connection, not serializable |
| Message history in grain state | Provides catch-up for new clients; JournaledGrain in 0.4.1 |
| SignalR hub delegates to grains | Hub is thin — all logic lives in grains |
| BroadcastChannel for inter-agent | Decouples SquadLeader from direct grain references |
| AgentGrainResolver for grain dispatch | Centralizes name → grain-type mapping; hub and startup both use it |

---

## Success Criteria

- [ ] Squad Server starts as an Orleans Silo and loads `squad.config.json`
- [ ] Core agent grains (Ralph, Scribe, SquadLeader) activate on startup
- [ ] Client connects via SignalR to SquadHub
- [ ] `SendMessage` routes to agent grain and streams response back via Orleans Stream
- [ ] `WakeAgent` activates an agent grain and broadcasts status change
- [ ] `SuspendAgent` deactivates a grain and broadcasts status change
- [ ] `GetAgentStatus` returns accurate state from all configured agent grains
- [ ] SquadLeader can invoke WakeAgent/SuspendAgent/SendTo as tools
- [ ] Inter-agent communication works via BroadcastChannel
- [ ] Minimal web frontend displays messages and agent status in real-time
- [ ] Deltas stream with no chunking delays
- [ ] Server handles grain activation/deactivation lifecycle correctly
- [ ] SquadSession is properly recreated on grain activation and disposed on deactivation

---

## Open Questions

- **SquadLeader tool execution:** How does the grain detect tool calls in the LLM response and dispatch them? Options: parse tool-call JSON from streaming response, or use Copilot SDK's tool-call callback if available.
- **Stream subscription cleanup:** When a SignalR client disconnects, how do we unsubscribe from Orleans Streams? Use `Hub.OnDisconnectedAsync` to track and dispose subscription handles.
- **Scribe observation pattern:** Scribe needs to observe all agent output without being explicitly messaged. Use implicit subscriptions on all `AgentOutput` streams, or have Scribe subscribe to the BroadcastChannel.
- **Charter loading:** Agent charters loaded from templates directory at grain activation. How to hot-reload if charters change while server is running?
