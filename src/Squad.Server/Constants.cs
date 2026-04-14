
namespace Squad.Server;

public static class Constants
{
    // Orleans storage / streaming (camelCase values — can't match nameof)
    public const string Agent = "agent";
    public const string AgentStateStore = "agentStore";

    // Orleans names — identifier matches value, use nameof
    public const string PubSubStore = nameof(PubSubStore);
    public const string AgentStreams = nameof(AgentStreams);
    public const string AgentOutput = nameof(AgentOutput);

    // Core agent / grain type names
    public const string SquadLeader = nameof(SquadLeader);
    public const string Ralph = nameof(Ralph);
    public const string Scribe = nameof(Scribe);
    public const string SquadMember = nameof(SquadMember);

    // Chat message roles (lowercase API contract — can't match nameof)
    public const string RoleUser = "user";
    public const string RoleAssistant = "assistant";

    // SquadLeader tool names
    public const string WakeAgent = nameof(WakeAgent);
    public const string SuspendAgent = nameof(SuspendAgent);
    public const string SendTo = nameof(SendTo);
    public const string GetAgentStatus = nameof(GetAgentStatus);

    // SignalR hub callback method names
    public const string OnDelta = nameof(OnDelta);
    public const string OnComplete = nameof(OnComplete);
    public const string OnAgentStatusChanged = nameof(OnAgentStatusChanged);
    public const string OnError = nameof(OnError);
}
