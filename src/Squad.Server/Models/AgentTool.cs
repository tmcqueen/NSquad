namespace Squad.Server.Models;

/// <summary>
/// Describes a tool available to an agent grain — name, description, and invocation handler.
/// The handler receives a raw args string and returns a result string.
/// </summary>
public sealed class AgentTool
{
    private readonly Func<string, Task<string>> _handler;

    public AgentTool(string name, string description, Func<string, Task<string>> handler)
    {
        Name = name;
        Description = description;
        _handler = handler;
    }

    public string Name { get; }
    public string Description { get; }

    public Task<string> InvokeAsync(string args) => _handler(args);
}
