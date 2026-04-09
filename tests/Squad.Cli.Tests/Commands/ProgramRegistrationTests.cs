using Spectre.Console.Cli;
using Shouldly;
using Squad.Cli;

namespace Squad.Cli.Tests.Commands;

public class ProgramRegistrationTests
{
    [Test]
    public void CommandApp_builds_without_exception()
    {
        // If all command types are registered correctly, this will not throw
        var app = SquadCliApp.Build();
        app.ShouldNotBeNull();
    }
}
