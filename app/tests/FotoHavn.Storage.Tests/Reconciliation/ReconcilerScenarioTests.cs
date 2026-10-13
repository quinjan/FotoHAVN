using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Storage.FileSystem;
using FotoHavn.Storage.Reconciliation;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage.Tests.Reconciliation;

/// <summary>The reconciler on a fully damaged Data folder, after crashes, cancellation, and failures.</summary>
public sealed class ReconcilerScenarioTests
{
    [Fact]
    public async Task It_repairs_every_kind_of_damage_in_one_run()
    {
        using var folder = new SeededDataFolder();
        var scenario = folder.SeedReconcileScenario();
        var protectedBefore = scenario.ProtectedFiles.Where(file => !file.EndsWith("fotohavn.db", StringComparison.Ordinal)).Select(file => (file, File.ReadAllBytes(file))).ToList();

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.StagingFilesDeleted);
        Assert.Equal(2, report.OrphanFilesDeleted);
        Assert.Equal(0, report.ArtifactProblems);
        Assert.Equal(0, report.CyclesMarkedUnrecoverable);
        Assert.Equal(1, report.CyclesClosedAsAppClosed);
        Assert.Equal(2, report.GuestCyclesDeleted);
        Assert.Equal(1, report.EventsDeleted);
        Assert.Equal(1, report.DiscardedFilesDeleted);
        Assert.Equal(4, report.OrphanThumbnailsDeleted);
        Assert.Empty(report.Problems);
        Assert.True(report.HasRepairs);

        Assert.False(File.Exists(scenario.StagingFile));
        Assert.False(File.Exists(scenario.OrphanFile));
        Assert.False(File.Exists(scenario.OrphanFolderFile));
        Assert.False(Directory.Exists(scenario.OrphanEventFolder));
        Assert.False(File.Exists(scenario.DiscardedCaptureFile));
        Assert.False(Directory.Exists(folder.CycleFolder(scenario.DeletingCycleId)));
        Assert.False(Directory.Exists(folder.EventFolder(scenario.DeletingEventId)));
        Assert.All(scenario.StaleThumbnails, path => Assert.False(File.Exists(path)));
        Assert.All(scenario.LiveThumbnails, path => Assert.True(File.Exists(path)));
        Assert.Equal(scenario.LiveThumbnails.Count, Directory.GetFiles(folder.ThumbnailsDir).Length);

        Assert.Equal(1, folder.Count("events"));
        Assert.Equal(2, folder.Count("guest_cycles"));
        Assert.Equal(4, folder.Count("captures"));
        Assert.Equal(1, folder.Count("photo_strips"));
        Assert.Equal("app_closed", folder.Text("SELECT end_reason FROM guest_cycles WHERE id = $id", ("$id", scenario.OpenCycleId)));
        Assert.Equal("completed", folder.Text("SELECT end_reason FROM guest_cycles WHERE id = $id", ("$id", scenario.CompletedCycleId)));

        foreach (var (file, bytes) in protectedBefore)
        {
            Assert.Equal(bytes, File.ReadAllBytes(file));
        }
    }

    [Fact]
    public async Task A_second_run_on_the_repaired_folder_does_nothing()
    {
        using var folder = new SeededDataFolder();
        folder.SeedReconcileScenario();
        await ReconcilerKit.RunAsync(folder);
        var files = folder.FilesUnderData();
        var rows = ReconcilerKit.RowCounts(folder);

        var again = await ReconcilerKit.RunAsync(folder);

        Assert.False(again.HasRepairs);
        Assert.Equal(files, folder.FilesUnderData());
        Assert.Equal(rows, ReconcilerKit.RowCounts(folder));
    }

    [Fact]
    public async Task It_only_touches_files_under_events_and_the_thumbnail_cache()
    {
        using var folder = new SeededDataFolder();
        var scenario = folder.SeedReconcileScenario();
        var recording = new ScriptedFileSystem(folder.Fs);

        await ReconcilerKit.RunAsync(folder, recording);

        Assert.NotEmpty(recording.Operations);
        Assert.All(recording.Operations, operation =>
        {
            var allowed = operation.Path.StartsWith(folder.EventsDir, StringComparison.OrdinalIgnoreCase)
                || operation.Path.StartsWith(folder.ThumbnailsDir, StringComparison.OrdinalIgnoreCase);
            Assert.True(allowed, $"{operation.Operation} touched {operation.Path}");
        });
        Assert.DoesNotContain(recording.Operations, operation => operation.Path.Contains("fotohavn.db", StringComparison.OrdinalIgnoreCase));
        Assert.All(scenario.ProtectedFiles, file => Assert.True(File.Exists(file)));
    }

    [Fact]
    public async Task It_uses_the_injected_clock_for_ended_at()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent());
        var clock = new FakeClock(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));

        await new StartupReconciler(clock).RunAsync(folder.Connection, folder.Fs, folder.DataRoot, CancellationToken.None);

        Assert.Equal("2030-01-02T03:04:05.0000000Z", folder.Text("SELECT ended_at FROM guest_cycles WHERE id = $id", ("$id", cycle)));
    }

    [Fact]
    public async Task A_crash_at_any_write_is_repaired_by_the_next_run_with_the_same_result_as_an_uninterrupted_run()
    {
        int totalWrites;
        IReadOnlyList<long> expectedRows;
        int expectedFiles;
        using (var reference = new SeededDataFolder())
        {
            reference.SeedReconcileScenario();
            var counting = new ScriptedFileSystem(reference.Fs);
            await ReconcilerKit.RunAsync(reference, counting);
            totalWrites = counting.WriteCount;
            expectedRows = ReconcilerKit.RowCounts(reference);
            expectedFiles = reference.FilesUnderData().Count;
        }

        Assert.True(totalWrites > 8);
        for (var crashAfter = 0; crashAfter < totalWrites; crashAfter++)
        {
            using var folder = new SeededDataFolder();
            folder.SeedReconcileScenario();
            var crashing = new ScriptedFileSystem(folder.Fs) { CrashAfterWrites = crashAfter };

            var crash = await Assert.ThrowsAsync<StorageException>(() => ReconcilerKit.RunAsync(folder, crashing));
            Assert.IsType<SimulatedCrashException>(crash.InnerException);

            var recovery = await ReconcilerKit.RunAsync(folder);
            var settled = await ReconcilerKit.RunAsync(folder);

            Assert.True(recovery.HasRepairs, $"crash after {crashAfter} writes");
            Assert.False(settled.HasRepairs, $"crash after {crashAfter} writes left work for a third run");
            Assert.Equal(expectedRows, ReconcilerKit.RowCounts(folder));
            Assert.Equal(expectedFiles, folder.FilesUnderData().Count);
        }
    }

    [Fact]
    public async Task A_disk_full_failure_surfaces_as_a_storage_exception_and_the_next_run_finishes_the_job()
    {
        using var folder = new SeededDataFolder();
        folder.SeedReconcileScenario();
        var faulty = new FaultInjectingStorageFileSystem(folder.Fs);
        faulty.Inject(StorageFaultKind.DiskFull, StorageFaultScope.AllWrites);

        var error = await Assert.ThrowsAsync<StorageException>(() => ReconcilerKit.RunAsync(folder, faulty));

        Assert.Equal(StorageErrorKind.DiskFull, error.Kind);
        faulty.Clear();
        var report = await ReconcilerKit.RunAsync(folder, faulty);
        Assert.True(report.HasRepairs);
        Assert.False((await ReconcilerKit.RunAsync(folder, faulty)).HasRepairs);
    }

    [Fact]
    public async Task An_access_denied_failure_maps_to_access_denied()
    {
        using var folder = new SeededDataFolder();
        folder.SeedReconcileScenario();
        var faulty = new FaultInjectingStorageFileSystem(folder.Fs);
        faulty.Inject(StorageFaultKind.AccessDenied, StorageFaultScope.NextWrite);

        var error = await Assert.ThrowsAsync<StorageException>(() => ReconcilerKit.RunAsync(folder, faulty));

        Assert.Equal(StorageErrorKind.AccessDenied, error.Kind);
    }

    [Fact]
    public async Task A_cancelled_token_cancels_the_run_and_a_later_run_finishes()
    {
        using var folder = new SeededDataFolder();
        folder.SeedReconcileScenario();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReconcilerKit.RunAsync(folder, ct: cancelled.Token));

        Assert.True((await ReconcilerKit.RunAsync(folder)).HasRepairs);
    }

    [Fact]
    public async Task Cancelling_between_files_stops_the_run_and_the_next_run_completes_it()
    {
        using var folder = new SeededDataFolder();
        folder.SeedReconcileScenario();
        using var source = new CancellationTokenSource();
        var scripted = new ScriptedFileSystem(folder.Fs)
        {
            OnOperation = (operation, _) =>
            {
                if (operation == nameof(IStorageFileSystem.Delete))
                {
                    source.Cancel();
                }
            },
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReconcilerKit.RunAsync(folder, scripted, source.Token));

        Assert.True((await ReconcilerKit.RunAsync(folder)).HasRepairs);
        Assert.False((await ReconcilerKit.RunAsync(folder)).HasRepairs);
    }

    [Fact]
    public async Task A_database_failure_surfaces_as_a_storage_exception()
    {
        using var folder = new SeededDataFolder();
        folder.Connection.Close();

        var error = await Assert.ThrowsAsync<StorageException>(() => ReconcilerKit.RunAsync(folder));

        Assert.Equal(StorageErrorKind.Unknown, error.Kind);
    }

    [Fact]
    public async Task A_locked_database_surfaces_as_locked()
    {
        using var folder = new SeededDataFolder();
        folder.AddCycle(folder.AddEvent());
        using var other = new SqliteConnection($"Data Source={folder.DatabasePath};Pooling=False;Default Timeout=0");
        other.Open();
        using (var begin = other.CreateCommand())
        {
            begin.CommandText = "BEGIN IMMEDIATE";
            begin.ExecuteNonQuery();
        }

        var error = await Assert.ThrowsAsync<StorageException>(() => ReconcilerKit.RunAsync(folder));

        Assert.Equal(StorageErrorKind.Locked, error.Kind);
    }

    [Fact]
    public async Task Arguments_are_checked()
    {
        using var folder = new SeededDataFolder();
        var reconciler = new StartupReconciler(new FakeClock());

        Assert.Throws<ArgumentNullException>(() => new StartupReconciler(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => reconciler.RunAsync(null!, folder.Fs, folder.DataRoot, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => reconciler.RunAsync(folder.Connection, null!, folder.DataRoot, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => reconciler.RunAsync(folder.Connection, folder.Fs, " ", CancellationToken.None));
    }
}
