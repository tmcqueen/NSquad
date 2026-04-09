using System.Text.Json;
using Squad.Cli.Commands;
using Squad.Sdk.Config;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class ImportCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private static string MakeManifestJson(string agentName = "builder") =>
        JsonSerializer.Serialize(new
        {
            version = "1.0",
            exported_at = DateTimeOffset.UtcNow.ToString("O"),
            casting = new { registry = new { universe = "mcqueen" } },
            agents = new Dictionary<string, object>
            {
                [agentName] = new { charter = "# Builder", history = "" }
            },
            skills = Array.Empty<string>()
        });

    [Test]
    public async Task ValidateManifest_accepts_valid_manifest()
    {
        var json = MakeManifestJson();
        var manifest = JsonSerializer.Deserialize<ExportManifest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        // Should not throw
        ImportCommand.ValidateManifest(manifest);
    }

    [Test]
    public async Task ImportManifest_creates_agent_directory()
    {
        var json = MakeManifestJson("edie");
        var manifestPath = Path.Combine(_tempDir, "squad-export.json");
        await File.WriteAllTextAsync(manifestPath, json);

        await ImportCommand.ImportAsync(_tempDir, manifestPath, force: true);

        var agentDir = Path.Combine(_tempDir, ".squad", "agents", "edie");
        Directory.Exists(agentDir).ShouldBeTrue();
        File.Exists(Path.Combine(agentDir, "charter.md")).ShouldBeTrue();
    }

    [Test]
    public async Task ImportManifest_fails_for_wrong_version()
    {
        var json = """{"version":"2.0","casting":{},"agents":{},"skills":[]}""";
        var manifestPath = Path.Combine(_tempDir, "bad.json");
        await File.WriteAllTextAsync(manifestPath, json);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            ImportCommand.ImportAsync(_tempDir, manifestPath, force: false));
    }
}
