using System.Text;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Streams;
using Squad.Server.Models;
using Squad.Server.Services;
using Squad.Sdk.Client;

namespace Squad.Server.Grains;

/* 
 * <summary>
 * Abstract base for all agent grains. Provides shared lifecycle, streaming,
 * and state management. Concrete grains override GetCharterPath() and GetTools().
 * </summary>
 */
public abstract class AgentGrain : Grain, IAgentGrain
{
    private readonly IPersistentState<AgentGrainState> _state;
    private readonly ISquadClientFactory _clientFactory;
    private readonly ILogger _logger;

    // NOT serializable — recreated on each activation
    private SquadClient? _client;
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

    // --- Extension points ---

    protected abstract string GetCharterPath();
    protected abstract IReadOnlyList<AgentTool> GetTools();
    protected virtual Task OnSessionCreatedAsync(SquadSession session) => Task.CompletedTask;

    // --- Lifecycle ---

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var streamProvider = this.GetStreamProvider("AgentStreams");
        var streamId = StreamId.Create("AgentOutput", this.GetPrimaryKeyString());
        _outputStream = streamProvider.GetStream<AgentStreamEvent>(streamId);

        _state.State.CharterPath = GetCharterPath();
        _state.State.AgentName = this.GetPrimaryKeyString();

        if (_state.State.Status != AgentStatus.Suspended)
        {
            try
            {
                await CreateSessionAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recreate session for {AgentName} on activation",
                    _state.State.AgentName);
                _state.State.Status = AgentStatus.Error;
                await _state.WriteStateAsync();
            }
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _state.State.Status = AgentStatus.Suspended;
        await _state.WriteStateAsync();

        if (_session is not null) { await _session.DisposeAsync(); _session = null; }
        if (_client is not null) { await _client.DisposeAsync(); _client = null; }
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
        if (_session is not null) { await _session.DisposeAsync(); _session = null; }
        if (_client is not null) { await _client.DisposeAsync(); _client = null; }

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
            Role = "user",
            Content = prompt,
            Timestamp = DateTime.UtcNow,
        });

        await _outputStream!.OnNextAsync(
            new AgentStreamEvent(AgentStreamEventType.StatusChanged, Status: AgentStatus.Processing));

        try
        {
            var fullResponse = new StringBuilder();

            await _session!.SendStreamingAsync(prompt, delta =>
            {
                fullResponse.Append(delta);
                // Fire-and-forget: Action<string> callback can't await
                _ = _outputStream!.OnNextAsync(
                    new AgentStreamEvent(AgentStreamEventType.Delta, Text: delta));
            });

            _state.State.MessageHistory.Add(new ChatMessage
            {
                Role = "assistant",
                Content = fullResponse.ToString(),
                Timestamp = DateTime.UtcNow,
            });

            _state.State.Status = AgentStatus.Idle;
            await _state.WriteStateAsync();

            await _outputStream!.OnNextAsync(new AgentStreamEvent(AgentStreamEventType.Completed));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendAsync failed for agent {AgentName}", _state.State.AgentName);
            _state.State.Status = AgentStatus.Error;
            await _state.WriteStateAsync();

            await _outputStream!.OnNextAsync(
                new AgentStreamEvent(AgentStreamEventType.Error, Text: ex.Message));
        }
    }

    public Task<AgentStatus> GetStatusAsync() => Task.FromResult(_state.State.Status);

    public Task<IReadOnlyList<ChatMessage>> GetHistoryAsync() =>
        Task.FromResult<IReadOnlyList<ChatMessage>>(_state.State.MessageHistory);

    // --- Protected helpers ---

    protected IAsyncStream<AgentStreamEvent> OutputStream => _outputStream!;
    protected AgentGrainState State => _state.State;
    protected Task WriteStateAsync() => _state.WriteStateAsync();

    // --- Private ---

    private async Task CreateSessionAsync(CancellationToken ct)
    {
        if (_session is not null) { await _session.DisposeAsync(); _session = null; }
        if (_client is not null) { await _client.DisposeAsync(); _client = null; }

        _client = await _clientFactory.CreateAsync(ct);

        string charterContent = File.Exists(_state.State.CharterPath)
            ? await File.ReadAllTextAsync(_state.State.CharterPath, ct)
            : "";

        if (_state.State.SessionId is not null)
        {
            _session = await _client.ResumeSessionAsync(
                _state.State.SessionId, _state.State.AgentName, ct);
        }
        else
        {
            _session = await _client.CreateSessionAsync(
                new SquadSessionOptions
                {
                    AgentName = _state.State.AgentName,
                    SystemMessageAppend = charterContent,
                },
                ct);
        }

        _state.State.SessionId = _session.SessionId;
        await OnSessionCreatedAsync(_session);
    }
}
