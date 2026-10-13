using FotoHavn.Core.Storage;

namespace FotoHavn.Storage.Tests.Reconciliation;

/// <summary>Plan section 10, reconciler step 3 (rows whose file is missing or differs) and step 4 (cycles that never ended).</summary>
public sealed class ReconcilerArtifactTests
{
    private static string? EndReason(SeededDataFolder folder, string cycleId) =>
        folder.Text("SELECT end_reason FROM guest_cycles WHERE id = $id", ("$id", cycleId));

    private static string? EndedAt(SeededDataFolder folder, string cycleId) =>
        folder.Text("SELECT ended_at FROM guest_cycles WHERE id = $id", ("$id", cycleId));

    private static string? CaptureState(SeededDataFolder folder, string captureId) =>
        folder.Text("SELECT state FROM captures WHERE id = $id", ("$id", captureId));

    [Fact]
    public async Task Step_3_finds_nothing_when_every_file_matches_its_row()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var capture = folder.AddCapture(cycle, 1);
        folder.AddPhotoStrip(cycle, [capture.Id]);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(0, report.ArtifactProblems);
        Assert.Equal(0, report.CyclesMarkedUnrecoverable);
        Assert.Empty(report.Problems);
    }

    [Fact]
    public async Task Step_3_keeps_a_capture_with_a_missing_file_as_kept_in_a_finished_cycle_and_keeps_the_cycle_and_other_files()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var lost = folder.AddCapture(cycle, 1, writeFile: false);
        var other = folder.AddCapture(cycle, 2);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.ArtifactProblems);
        Assert.Equal(0, report.CyclesMarkedUnrecoverable);
        Assert.Equal("kept", CaptureState(folder, lost.Id));
        Assert.Equal("kept", CaptureState(folder, other.Id));
        Assert.Equal(2, folder.Count("captures"));
        Assert.Equal("completed", EndReason(folder, cycle));
        Assert.True(File.Exists(other.Path));
        var problem = Assert.Single(report.Problems);
        Assert.Contains(lost.Id, problem);
        Assert.Contains(cycle, problem);
    }

    [Fact]
    public async Task Step_3_changes_no_row_for_a_missing_or_corrupt_file_in_a_finished_cycle()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var missing = folder.AddCapture(cycle, 1, writeFile: false);
        var corrupt = folder.AddCapture(cycle, 2);
        File.WriteAllBytes(corrupt.Path, [1]);
        _ = folder.AddPhotoStrip(cycle, [missing.Id], writeFile: false);
        var before = Snapshot(folder);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(3, report.ArtifactProblems);
        Assert.Equal(before, Snapshot(folder));
        Assert.Equal(1, folder.Count("photo_strips"));
    }

    [Fact]
    public async Task Step_3_adopts_a_file_that_reappears_because_its_row_was_kept()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var capture = folder.AddCapture(cycle, 1, writeFile: false);
        var strip = folder.AddPhotoStrip(cycle, [capture.Id], writeFile: false);

        var missing = await ReconcilerKit.RunAsync(folder);
        Directory.CreateDirectory(Path.GetDirectoryName(capture.Path)!);
        File.WriteAllBytes(capture.Path, capture.Bytes);
        File.WriteAllBytes(strip.Path, strip.Bytes);
        var restored = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(2, missing.ArtifactProblems);
        Assert.Equal(2, missing.Problems.Count);
        Assert.False(restored.HasRepairs);
        Assert.Empty(restored.Problems);
        Assert.Equal(0, restored.OrphanFilesDeleted);
        Assert.True(File.Exists(capture.Path));
        Assert.True(File.Exists(strip.Path));
        Assert.Equal("kept", CaptureState(folder, capture.Id));
        Assert.Equal(1, folder.Count("photo_strips"));
    }

    private static List<string> Snapshot(SeededDataFolder folder)
    {
        var rows = new List<string>();
        foreach (var table in new[] { "events", "guest_cycles", "captures", "photo_strips" })
        {
            using var command = folder.Connection.CreateCommand();
            command.CommandText = $"SELECT * FROM {table} ORDER BY 1, 2";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(table + ":" + string.Join('|', Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "<null>" : reader.GetValue(i).ToString())));
            }
        }

        return rows;
    }

    [Fact]
    public async Task Step_3_ends_an_unfinished_cycle_as_unrecoverable_not_app_closed_and_keeps_its_other_files()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent());
        folder.AddCapture(cycle, 1, writeFile: false);
        var other = folder.AddCapture(cycle, 2);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.ArtifactProblems);
        Assert.Equal(1, report.CyclesMarkedUnrecoverable);
        Assert.Equal(0, report.CyclesClosedAsAppClosed);
        Assert.Equal("unrecoverable", EndReason(folder, cycle));
        Assert.Equal(ReconcilerKit.NowText, EndedAt(folder, cycle));
        Assert.True(File.Exists(other.Path));
        Assert.Equal(2, report.Problems.Count);
        Assert.Contains(report.Problems, problem => problem.Contains(cycle) && problem.Contains("unrecoverable"));
    }

    [Fact]
    public async Task Step_3_counts_an_unfinished_cycle_once_however_many_files_are_damaged()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent());
        folder.AddCapture(cycle, 1, writeFile: false);
        folder.AddCapture(cycle, 2, writeFile: false);
        var third = folder.AddCapture(cycle, 3);
        folder.AddPhotoStrip(cycle, [third.Id], writeFile: false);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(3, report.ArtifactProblems);
        Assert.Equal(1, report.CyclesMarkedUnrecoverable);
    }

    [Fact]
    public async Task Step_3_flags_a_file_with_a_different_size_and_keeps_the_row_and_file()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var capture = folder.AddCapture(cycle, 1);
        File.WriteAllBytes(capture.Path, [.. capture.Bytes, 1]);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.ArtifactProblems);
        Assert.Equal("kept", CaptureState(folder, capture.Id));
        Assert.True(File.Exists(capture.Path));
        Assert.Contains("doesn't match", Assert.Single(report.Problems));
    }

    [Fact]
    public async Task Step_3_flags_a_file_with_the_same_size_but_a_different_hash()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var capture = folder.AddCapture(cycle, 1);
        var changed = capture.Bytes.ToArray();
        changed[0] ^= 0xFF;
        File.WriteAllBytes(capture.Path, changed);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.ArtifactProblems);
        Assert.Equal("kept", CaptureState(folder, capture.Id));
    }

    [Fact]
    public async Task Step_3_accepts_an_upper_case_hash_in_the_row()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        folder.AddCapture(cycle, 1);
        using (var command = folder.Connection.CreateCommand())
        {
            command.CommandText = "UPDATE captures SET sha256 = upper(sha256)";
            command.ExecuteNonQuery();
        }

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(0, report.ArtifactProblems);
    }

    [Fact]
    public async Task Step_3_flags_a_corrupt_file_again_on_every_run_but_changes_nothing_more()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var capture = folder.AddCapture(cycle, 1);
        File.WriteAllBytes(capture.Path, [1, 2, 3]);

        var first = await ReconcilerKit.RunAsync(folder);
        var second = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, first.ArtifactProblems);
        Assert.Equal(1, second.ArtifactProblems);
        Assert.Equal("kept", CaptureState(folder, capture.Id));
        Assert.Equal("completed", EndReason(folder, cycle));
    }

    [Fact]
    public async Task Step_3_keeps_the_row_of_a_photo_strip_whose_file_is_missing_and_leaves_the_cycle_completed()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var capture = folder.AddCapture(cycle, 1);
        folder.AddPhotoStrip(cycle, [capture.Id], writeFile: false);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.ArtifactProblems);
        Assert.Equal(1, folder.Count("photo_strips"));
        Assert.Equal("completed", EndReason(folder, cycle));
        var problem = Assert.Single(report.Problems);
        Assert.Contains("Photo Strip", problem);
        Assert.Contains("kept", problem);
    }

    [Fact]
    public async Task Step_3_flags_a_corrupt_photo_strip_and_keeps_it()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var capture = folder.AddCapture(cycle, 1);
        var strip = folder.AddPhotoStrip(cycle, [capture.Id]);
        File.WriteAllBytes(strip.Path, [7]);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.ArtifactProblems);
        Assert.Equal(1, folder.Count("photo_strips"));
        Assert.True(File.Exists(strip.Path));
    }

    [Fact]
    public async Task Step_3_treats_a_row_with_an_unsafe_file_name_as_missing_and_never_touches_the_file_it_names()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var capture = folder.AddCapture(cycle, 1);
        var outside = folder.AddFile(Path.Combine(folder.DataRoot, "outside.jpg"));
        using (var command = folder.Connection.CreateCommand())
        {
            command.CommandText = "UPDATE captures SET file_name = '..\\..\\..\\..\\outside.jpg'";
            command.ExecuteNonQuery();
        }

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.ArtifactProblems);
        Assert.Equal("kept", CaptureState(folder, capture.Id));
        Assert.True(File.Exists(outside));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    public async Task Step_3_treats_a_row_with_an_empty_or_dot_file_name_as_missing(string fileName)
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent(), "completed");
        var capture = folder.AddCapture(cycle, 1);
        using (var command = folder.Connection.CreateCommand())
        {
            command.CommandText = "UPDATE captures SET file_name = $name";
            command.Parameters.AddWithValue("$name", fileName);
            command.ExecuteNonQuery();
        }

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.ArtifactProblems);
        Assert.Equal("kept", CaptureState(folder, capture.Id));
    }

    [Fact]
    public async Task Step_3_ignores_discarded_captures_and_cycles_marked_deleting()
    {
        using var folder = new SeededDataFolder();
        var eventId = folder.AddEvent();
        var cycle = folder.AddCycle(eventId, "completed");
        folder.AddCapture(cycle, 1, state: "discarded", writeFile: false);
        var deleting = folder.AddCycle(eventId, "completed", deleting: true);
        folder.AddCapture(deleting, 1, writeFile: false);
        var deletingEvent = folder.AddEvent(deleting: true);
        var inDeletingEvent = folder.AddCycle(deletingEvent, "completed");
        folder.AddCapture(inDeletingEvent, 1, writeFile: false);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(0, report.ArtifactProblems);
        Assert.Empty(report.Problems);
    }

    [Fact]
    public async Task Step_3_problems_name_ids_and_never_paths()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent());
        folder.AddCapture(cycle, 1, writeFile: false);
        var strip = folder.AddCapture(cycle, 2);
        File.WriteAllBytes(strip.Path, [0]);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.NotEmpty(report.Problems);
        Assert.All(report.Problems, problem =>
        {
            Assert.DoesNotContain(folder.DataRoot, problem, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\\", problem);
        });
    }

    [Fact]
    public async Task Step_3_reports_a_missing_file_on_every_run_and_changes_nothing_after_the_first()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent());
        folder.AddCapture(cycle, 1, writeFile: false);
        folder.AddPhotoStrip(cycle, [], writeFile: false);

        var first = await ReconcilerKit.RunAsync(folder);
        var afterFirst = Snapshot(folder);
        var second = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(2, first.ArtifactProblems);
        Assert.Equal(1, first.CyclesMarkedUnrecoverable);
        Assert.Equal(2, second.ArtifactProblems);
        Assert.Equal(0, second.CyclesMarkedUnrecoverable);
        Assert.Equal(afterFirst, Snapshot(folder));
    }

    [Fact]
    public async Task Step_4_ends_a_cycle_that_never_ended_as_app_closed_at_the_current_time()
    {
        using var folder = new SeededDataFolder();
        var cycle = folder.AddCycle(folder.AddEvent());

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, report.CyclesClosedAsAppClosed);
        Assert.Equal("app_closed", EndReason(folder, cycle));
        Assert.Equal(ReconcilerKit.NowText, EndedAt(folder, cycle));
    }

    [Fact]
    public async Task Step_4_leaves_ended_cycles_alone()
    {
        using var folder = new SeededDataFolder();
        var eventId = folder.AddEvent();
        var completed = folder.AddCycle(eventId, "completed");
        var startedOver = folder.AddCycle(eventId, "started_over");
        var endedAt = EndedAt(folder, completed);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(0, report.CyclesClosedAsAppClosed);
        Assert.Equal("completed", EndReason(folder, completed));
        Assert.Equal("started_over", EndReason(folder, startedOver));
        Assert.Equal(endedAt, EndedAt(folder, completed));
    }

    [Fact]
    public async Task Step_4_does_not_count_open_cycles_that_are_being_deleted()
    {
        using var folder = new SeededDataFolder();
        var eventId = folder.AddEvent();
        folder.AddCycle(eventId, deleting: true);
        var deletingEvent = folder.AddEvent(deleting: true);
        folder.AddCycle(deletingEvent);

        var report = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(0, report.CyclesClosedAsAppClosed);
        Assert.Equal(2, report.GuestCyclesDeleted);
    }

    [Fact]
    public async Task Step_4_is_idempotent()
    {
        using var folder = new SeededDataFolder();
        folder.AddCycle(folder.AddEvent());

        var first = await ReconcilerKit.RunAsync(folder);
        var second = await ReconcilerKit.RunAsync(folder);

        Assert.Equal(1, first.CyclesClosedAsAppClosed);
        Assert.False(second.HasRepairs);
    }
}
