using Squad.Sdk.Resolution;
using Shouldly;

namespace Squad.Sdk.Tests.Resolution;

public class PathResolverTests
{
    private string _root = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_root);
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void ResolveSquadDir_finds_squad_dir_in_start_dir()
    {
        var squadDir = Path.Combine(_root, ".squad");
        Directory.CreateDirectory(squadDir);
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

        var result = PathResolver.ResolveSquadDir(_root);

        result.ShouldBe(squadDir);
    }

    [Test]
    public void ResolveSquadDir_walks_up_to_find_squad_dir()
    {
        var squadDir = Path.Combine(_root, ".squad");
        Directory.CreateDirectory(squadDir);
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        var subDir = Path.Combine(_root, "src", "app");
        Directory.CreateDirectory(subDir);

        var result = PathResolver.ResolveSquadDir(subDir);

        result.ShouldBe(squadDir);
    }

    [Test]
    public void ResolveSquadDir_returns_null_when_not_found()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

        var result = PathResolver.ResolveSquadDir(_root);

        result.ShouldBeNull();
    }

    [Test]
    public void ResolveSquadDir_stops_at_git_boundary()
    {
        var outerSquad = Path.Combine(_root, ".squad");
        Directory.CreateDirectory(outerSquad);
        var innerRepo = Path.Combine(_root, "inner");
        Directory.CreateDirectory(innerRepo);
        Directory.CreateDirectory(Path.Combine(innerRepo, ".git"));

        var result = PathResolver.ResolveSquadDir(innerRepo);

        result.ShouldBeNull();
    }

    [Test]
    public void ResolveSquadDir_accepts_legacy_ai_team_dir()
    {
        var legacyDir = Path.Combine(_root, ".ai-team");
        Directory.CreateDirectory(legacyDir);
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

        var result = PathResolver.ResolveSquadDir(_root);

        result.ShouldBe(legacyDir);
    }

    [Test]
    public void DetectMode_returns_local_when_no_config_json()
    {
        var squadDir = Path.Combine(_root, ".squad");
        Directory.CreateDirectory(squadDir);

        var mode = PathResolver.DetectMode(squadDir);

        mode.ShouldBe(SquadMode.Local);
    }

    [Test]
    public void DetectMode_returns_remote_when_config_json_has_team_root()
    {
        var squadDir = Path.Combine(_root, ".squad");
        Directory.CreateDirectory(squadDir);
        File.WriteAllText(
            Path.Combine(squadDir, "config.json"),
            """{ "version": 1, "teamRoot": "../team", "projectKey": null }""");

        var mode = PathResolver.DetectMode(squadDir);

        mode.ShouldBe(SquadMode.Remote);
    }
}
