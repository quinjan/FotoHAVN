using FotoHavn.Core.Storage;
using FotoHavn.Storage.Tests.Support;

namespace FotoHavn.Storage.Tests.Crash;

/// <summary>
/// Kill-at-every-step crash tests (plan section 14, ADR 0005): the harness is a child process that kills itself right
/// after a named step of a Capture commit, a Photo Strip commit, an Event delete, or a Guest Cycle delete. A fresh store on
/// the same folder then runs the reconciler (<c>OpenAsync</c>), and the folder must satisfy every invariant.
/// </summary>
public sealed class CrashRecoveryTests
{
    public static TheoryData<string, string> EveryStep =>
        [.. CrashPoint.All.Select(step => (Scenario: ScenarioOf(step), Step: step)).Select(pair => (pair.Scenario, pair.Step))];

    public static TheoryData<string> EveryScenario => ["capture", "strip", "eventDelete", "cycleDelete"];

    private static string ScenarioOf(string step) => step.Split('.')[0] switch
    {
        "capture" => "capture",
        "strip" => "strip",
        "event" => "eventDelete",
        "cycle" => "cycleDelete",
        _ => throw new ArgumentOutOfRangeException(nameof(step), step, "Not a crash point."),
    };

    [Fact]
    public void Every_crash_point_belongs_to_a_scenario_the_harness_runs()
    {
        Assert.Equal(14, CrashPoint.All.Count);
        Assert.Equal(["capture", "strip", "eventDelete", "cycleDelete"], CrashPoint.All.Select(ScenarioOf).Distinct());
    }

    [Theory]
    [MemberData(nameof(EveryStep))]
    public async Task A_crash_at_the_step_leaves_a_folder_the_next_start_repairs(string scenario, string step)
    {
        using var scratch = new ScratchFolder();
        var data = scratch.Combine("Data");
        var seeded = await CrashDataFolder.SeedAsync(data);

        var run = await CrashHarnessProcess.RunAsync(data, scenario, step, seeded.EventId, seeded.CompletedCycleId);

        Assert.True(run.Crashed, $"The harness didn't reach {step}. Exit {run.ExitCode}. {run.Errors}");
        Assert.Equal(step, run.MarkerStep);
        Assert.NotEqual(0, run.ExitCode);

        var (store, opened) = await RealStore.OpenAsync(data);
        await using (store)
        {
            var report = opened.Reconcile;
            AssertRepairs(scenario, step, report);
            CrashDataFolder.AssertConsistent(data);
            AssertOutcome(data, scenario, step, seeded, run.MarkerCycleId);
            AssertUntouched(data, seeded);

            await store.DrainAsync(CancellationToken.None);
        }

        var (again, second) = await RealStore.OpenAsync(data);
        await using (again)
        {
            Assert.False(second.Reconcile.HasRepairs, "A second start still found something to repair.");
            CrashDataFolder.AssertConsistent(data);
        }
    }

    [Theory]
    [MemberData(nameof(EveryScenario))]
    public async Task Without_a_crash_the_harness_finishes_and_the_folder_stays_consistent(string scenario)
    {
        using var scratch = new ScratchFolder();
        var data = scratch.Combine("Data");
        var seeded = await CrashDataFolder.SeedAsync(data);

        var run = await CrashHarnessProcess.RunAsync(data, scenario, "none", seeded.EventId, seeded.CompletedCycleId);

        Assert.False(run.Crashed);
        Assert.True(run.ExitCode == 0, $"Exit {run.ExitCode}. {run.Errors}");

        var (store, opened) = await RealStore.OpenAsync(data);
        await using (store)
        {
            CrashDataFolder.AssertConsistent(data);
            AssertUntouched(data, seeded);
            switch (scenario)
            {
                case "capture":
                    // The unfinished cycle the harness started was ended app_closed by this start, with its Capture committed.
                    Assert.Equal(1, opened.Reconcile.CyclesClosedAsAppClosed);
                    Assert.Equal(1L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM captures WHERE guest_cycle_id = '{run.CycleId}'"));
                    break;
                case "strip":
                    Assert.False(opened.Reconcile.HasRepairs);
                    Assert.Equal("completed", CrashDataFolder.Text(data, $"SELECT end_reason FROM guest_cycles WHERE id = '{run.CycleId}'"));
                    Assert.Equal(1L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM photo_strips WHERE guest_cycle_id = '{run.CycleId}'"));
                    break;
                case "eventDelete":
                    AssertEventGone(data, seeded);
                    break;
                case "cycleDelete":
                    AssertCycleGone(data, seeded);
                    break;
            }
        }
    }

    [Fact]
    public async Task The_harness_rejects_bad_arguments_without_touching_the_folder()
    {
        using var scratch = new ScratchFolder();
        var data = scratch.Combine("Data");

        var unknownScenario = await CrashHarnessProcess.RunAsync(data, "nothing", "none");
        var unknownStep = await CrashHarnessProcess.RunAsync(data, "capture", "capture.nowhere");
        var missingEvent = await CrashHarnessProcess.RunAsync(data, "capture", "none");

        Assert.Equal(2, unknownScenario.ExitCode);
        Assert.Equal(2, unknownStep.ExitCode);
        Assert.Equal(2, missingEvent.ExitCode);
        Assert.False(unknownScenario.Crashed);
    }

    /// <summary>What the next start's reconciler reports for a crash at <paramref name="step"/>.</summary>
    private static void AssertRepairs(string scenario, string step, ReconcileReport report)
    {
        switch (step)
        {
            case "capture.staged" or "capture.flushed" or "strip.staged" or "strip.flushed":
                Assert.Equal(1, report.StagingFilesDeleted);
                Assert.Equal(0, report.OrphanFilesDeleted);
                break;
            case "capture.renamed" or "strip.renamed":
                Assert.Equal(0, report.StagingFilesDeleted);
                Assert.Equal(1, report.OrphanFilesDeleted);
                break;
            default:
                Assert.Equal(0, report.StagingFilesDeleted);
                Assert.Equal(0, report.OrphanFilesDeleted);
                break;
        }

        // The cycle the harness started is unfinished unless its Photo Strip committed (which completes it).
        var closed = scenario is "capture" || (scenario is "strip" && step != "strip.inserted") ? 1 : 0;
        Assert.Equal(closed, report.CyclesClosedAsAppClosed);
        Assert.Equal(0, report.ArtifactProblems);
        Assert.Empty(report.Problems);
    }

    private static void AssertOutcome(string data, string scenario, string step, SeededIds seeded, string? cycleId)
    {
        switch (scenario)
        {
            case "capture":
            {
                var committed = step == "capture.inserted";
                Assert.Equal(committed ? 1L : 0L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM captures WHERE guest_cycle_id = '{cycleId}'"));
                Assert.Equal(committed, File.Exists(Path.Combine(CrashDataFolder.CycleFolder(data, seeded.EventId, cycleId!), "capture-1.jpg")));
                Assert.Equal("app_closed", CrashDataFolder.Text(data, $"SELECT end_reason FROM guest_cycles WHERE id = '{cycleId}'"));
                break;
            }

            case "strip":
            {
                var committed = step == "strip.inserted";
                Assert.Equal(committed ? 1L : 0L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM photo_strips WHERE guest_cycle_id = '{cycleId}'"));
                Assert.Equal(committed, File.Exists(Path.Combine(CrashDataFolder.CycleFolder(data, seeded.EventId, cycleId!), "photo-strip.png")));
                Assert.Equal(committed ? "completed" : "app_closed", CrashDataFolder.Text(data, $"SELECT end_reason FROM guest_cycles WHERE id = '{cycleId}'"));
                Assert.Equal(2L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM captures WHERE guest_cycle_id = '{cycleId}' AND state = 'kept'"));
                break;
            }

            case "eventDelete":
                AssertEventGone(data, seeded);
                break;
            case "cycleDelete":
                AssertCycleGone(data, seeded);
                break;
        }
    }

    private static void AssertEventGone(string data, SeededIds seeded)
    {
        Assert.Equal(0L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM events WHERE id = '{seeded.EventId}'"));
        Assert.Equal(0L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM guest_cycles WHERE event_id = '{seeded.EventId}'"));
        Assert.Equal(0L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM captures WHERE guest_cycle_id IN ('{seeded.CompletedCycleId}', '{seeded.EndedCycleId}')"));
        Assert.Equal(0L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM photo_strips WHERE guest_cycle_id = '{seeded.CompletedCycleId}'"));
        Assert.False(Directory.Exists(CrashDataFolder.EventFolder(data, seeded.EventId)));
        Assert.Empty(CrashDataFolder.ThumbnailsOf(data, [.. seeded.CompletedCaptureIds, seeded.CompletedCycleId]));
    }

    private static void AssertCycleGone(string data, SeededIds seeded)
    {
        Assert.Equal(0L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM guest_cycles WHERE id = '{seeded.CompletedCycleId}'"));
        Assert.Equal(0L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM captures WHERE guest_cycle_id = '{seeded.CompletedCycleId}'"));
        Assert.Equal(0L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM photo_strips WHERE guest_cycle_id = '{seeded.CompletedCycleId}'"));
        Assert.False(Directory.Exists(CrashDataFolder.CycleFolder(data, seeded.EventId, seeded.CompletedCycleId)));
        Assert.Empty(CrashDataFolder.ThumbnailsOf(data, [.. seeded.CompletedCaptureIds, seeded.CompletedCycleId]));
        Assert.Equal(1L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM events WHERE id = '{seeded.EventId}'"));
        Assert.Equal(1L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM guest_cycles WHERE id = '{seeded.EndedCycleId}'"));
    }

    /// <summary>The second Event, and what the scenario doesn't delete, survive every crash.</summary>
    private static void AssertUntouched(string data, SeededIds seeded)
    {
        Assert.Equal(1L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM events WHERE id = '{seeded.OtherEventId}'"));
        Assert.Equal(1L, CrashDataFolder.Count(data, $"SELECT COUNT(*) FROM photo_strips WHERE guest_cycle_id = '{seeded.OtherCycleId}'"));
        Assert.True(File.Exists(Path.Combine(CrashDataFolder.CycleFolder(data, seeded.OtherEventId, seeded.OtherCycleId), "photo-strip.png")));
        Assert.Equal(2, CrashDataFolder.ThumbnailsOf(data, seeded.OtherCycleId, seeded.OtherCaptureId).Count);
    }
}
