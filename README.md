![CodeRabbit Pull Request Reviews](https://img.shields.io/coderabbit/prs/github/tmcqueen/NSquad?utm_source=oss&utm_medium=github&utm_campaign=tmcqueen%2FNSquad&labelColor=171717&color=FF570A&link=https%3A%2F%2Fcoderabbit.ai&label=CodeRabbit+Reviews)

# NSquad

.NET 10 port of [Squad](https://github.com/bradygaster/squad) — a programmable multi-agent runtime for GitHub Copilot.

## Status

Early development. SDK foundation + 3 CLI commands implemented.

## Prerequisites

- .NET 10 SDK
- GitHub Copilot CLI installed and authenticated (`copilot --version`)

## Installation

```bash
dotnet tool install --global Squad.Cli
```

## CLI Commands

```bash
squad doctor           # Validate .squad/ setup
squad cast             # Show current agent roster
squad cost             # Show token usage and cost summary
```

## SDK Usage

```csharp
using Squad.Sdk;
using Squad.Sdk.Client;

// Register services
services.AddSquadSdk();

// Or use directly
await using var client = await SquadClient.CreateAsync();
await using var session = await client.CreateSessionAsync(new SquadSessionOptions
{
    AgentName = "builder",
    Model = "claude-sonnet-4.5",
    Streaming = true,
});

var response = await session.SendAsync("Implement the new feature");
Console.WriteLine(response);
```

## Configuration

Create `squad.config.json` at your repo root:

```json
{
  "version": "1.0",
  "team": { "name": "My Team" },
  "agents": [
    { "name": "builder", "role": "feature-dev", "model": "claude-sonnet-4.5" },
    { "name": "tester",  "role": "testing",     "model": "claude-haiku-4.5" }
  ],
  "routing": {
    "defaultAgent": "builder",
    "rules": [
      { "pattern": "test|spec|coverage", "agent": "tester" }
    ]
  }
}
```

## Development

```bash
dotnet build
dotnet run --project tests/Squad.Sdk.Tests   # SDK tests
dotnet run --project tests/Squad.Cli.Tests   # CLI tests
dotnet run --project src/Squad.Cli -- doctor # Run CLI locally
```
