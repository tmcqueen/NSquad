using Spectre.Console.Cli;
using Squad.Cli.Commands;
using Squad.Cli.Commands.Personal;
using Squad.Cli.Commands.Plugin;

namespace Squad.Cli;

/// <summary>
/// Builds the CommandApp. Extracted from Program.cs for testability.
/// </summary>
public static class SquadCliApp
{
    public static CommandApp Build()
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("squad");
            config.SetApplicationVersion("0.3.0");

            // init
            config.AddCommand<InitCommand>("init")
                  .WithDescription("Scaffold a new squad project (.squad/, squad.config.json, agent prompt).");

            // Plan 1 commands
            config.AddCommand<DoctorCommand>("doctor")
                  .WithDescription("Validate .squad/ setup and report health checks.");
            config.AddCommand<CastCommand>("cast")
                  .WithDescription("Show the current session cast (project + personal agents).");
            config.AddCommand<CostCommand>("cost")
                  .WithDescription("Show token usage and estimated cost per agent.");

            // Plan 2 commands
            config.AddCommand<BuildCommand>("build")
                  .WithDescription("Generate .squad/ markdown from squad.config.json.");
            config.AddCommand<ExportCommand>("export")
                  .WithDescription("Export squad state to squad-export.json.");
            config.AddCommand<ImportCommand>("import")
                  .WithDescription("Import squad from squad-export.json.");
            config.AddCommand<MigrateCommand>("migrate")
                  .WithDescription("Migrate between squad formats (--to sdk | markdown | --from ai-team).");
            config.AddCommand<CopilotCommand>("copilot")
                  .WithDescription("Add/remove @copilot coding agent from team roster.");
            config.AddCommand<LinkCommand>("link")
                  .WithDescription("Link this project to a remote team root.");
            config.AddCommand<InitRemoteCommand>("init-remote")
                  .WithDescription("Write .squad/config.json for remote squad mode.");
            config.AddCommand<EconomyCommand>("economy")
                  .WithDescription("Toggle cost-conscious model selection (on | off).");
            config.AddCommand<RolesCommand>("roles")
                  .WithDescription("List available built-in agent roles.");
            config.AddCommand<UpstreamCommand>("upstream")
                  .WithDescription("Manage upstream squad sources (add|remove|list|sync).");
            config.AddCommand<StreamsCommand>("streams")
                  .WithDescription("Manage SubSquads (list|status|activate).");
            config.AddCommand<ScheduleCommand>("schedule")
                  .WithDescription("Manage scheduled squad tasks (init|list|status|run).");
            config.AddCommand<WatchCommand>("watch")
                  .WithDescription("Run Ralph's local polling loop — triage issues and monitor PRs.");
            config.AddCommand<DiscoverCommand>("discover")
                  .WithDescription("Discover linked squads via upstream.json.");
            config.AddCommand<DelegateCommand>("delegate")
                  .WithDescription("Create a cross-squad work request.");
            config.AddCommand<ExtractCommand>("extract")
                  .WithDescription("Extract learnings from a consult session to personal squad.");

            // personal branch
            config.AddBranch("personal", personal =>
            {
                personal.SetDescription("Manage personal squad agents.");
                personal.AddCommand<PersonalInitCommand>("init").WithDescription("Initialize personal squad.");
                personal.AddCommand<PersonalListCommand>("list").WithDescription("List personal agents.");
                personal.AddCommand<PersonalAddCommand>("add").WithDescription("Add a personal agent.");
                personal.AddCommand<PersonalRemoveCommand>("remove").WithDescription("Remove a personal agent.");
            });

            // plugin branch
            config.AddBranch("plugin", plugin =>
            {
                plugin.SetDescription("Manage squad plugins and marketplaces.");
                plugin.AddBranch("marketplace", mp =>
                {
                    mp.SetDescription("Manage plugin marketplaces.");
                    mp.AddCommand<MarketplaceAddCommand>("add").WithDescription("Register a marketplace.");
                    mp.AddCommand<MarketplaceRemoveCommand>("remove").WithDescription("Remove a marketplace.");
                    mp.AddCommand<MarketplaceListCommand>("list").WithDescription("List registered marketplaces.");
                    mp.AddCommand<MarketplaceBrowseCommand>("browse").WithDescription("Browse marketplace plugins.");
                });
            });
        });
        return app;
    }
}
