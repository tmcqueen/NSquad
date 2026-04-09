using Spectre.Console.Cli;
using Squad.Cli.Commands;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("squad");
    config.SetApplicationVersion("0.1.0");
    config.AddCommand<DoctorCommand>("doctor")
          .WithDescription("Validate .squad/ setup and report health checks.");
    config.AddCommand<CastCommand>("cast")
          .WithDescription("Show the current session cast (project + personal agents).");
    config.AddCommand<CostCommand>("cost")
          .WithDescription("Show token usage and estimated cost per agent.");
});

return app.Run(args);
