using System.Text.Json;
using Squad.Cli.Commands;
using Squad.Sdk.Config;
using Shouldly;

namespace Squad.Cli.Tests.Commands;

public class ScheduleCommandTests
{
    private string _tempDir = string.Empty;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_tempDir, ".squad"));
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task Init_creates_schedule_json()
    {
        await ScheduleCommand.InitAsync(_tempDir);
        var path = Path.Combine(_tempDir, ".squad", "schedule.json");
        File.Exists(path).ShouldBeTrue();
    }

    [Test]
    public async Task Init_is_idempotent()
    {
        await ScheduleCommand.InitAsync(_tempDir);
        await ScheduleCommand.InitAsync(_tempDir); // second call should not throw
    }

    [Test]
    public async Task LoadSchedule_returns_template_entries()
    {
        await ScheduleCommand.InitAsync(_tempDir);
        var manifest = await ScheduleCommand.LoadScheduleAsync(_tempDir);
        manifest.Schedules.ShouldNotBeEmpty();
    }

    [Test]
    public async Task LoadSchedule_throws_when_no_file()
    {
        await Should.ThrowAsync<InvalidOperationException>(() =>
            ScheduleCommand.LoadScheduleAsync(_tempDir));
    }

    [Test]
    public void FormatTrigger_cron()
    {
        var entry = new ScheduleEntry { Trigger = new ScheduleTrigger { Type = "cron", Cron = "0 9 * * 1" } };
        ScheduleCommand.FormatTrigger(entry).ShouldContain("0 9 * * 1");
    }

    [Test]
    public void FormatTrigger_interval()
    {
        var entry = new ScheduleEntry { Trigger = new ScheduleTrigger { Type = "interval", IntervalSeconds = 3600 } };
        ScheduleCommand.FormatTrigger(entry).ShouldContain("3600");
    }
}
