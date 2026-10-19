using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Events.Setup;
using FotoHavn.Core.Tests.Storage;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Events.EventList;

public sealed class EventListTests
{
    private static async Task<SetupRig> RigAsync(bool hooks = false)
    {
        var store = new HookedStore(await SetupSamples.SeededStoreAsync());
        return new SetupRig(store, hooks: hooks);
    }

    private static HookedStore StoreOf(SetupRig rig) => (HookedStore)rig.Store;

    [Fact]
    public async Task The_list_is_newest_date_first_and_every_action_works_while_nothing_runs()
    {
        using var rig = await RigAsync();

        Assert.Equal([SetupSamples.WeddingId, SetupSamples.HarbourId, SetupSamples.OlderId], rig.List.Events.Select(row => row.Id));
        Assert.True(rig.List.NewEventAvailable);
        Assert.Equal([new DateOnly(2026, 10, 24), new DateOnly(2026, 6, 12), new DateOnly(2026, 3, 1)], rig.List.Events.Select(row => row.Date));
        Assert.All(rig.List.Events, row =>
        {
            Assert.False(row.Running);
            Assert.Equal(new EventRowActions(true, true, true, true, true), row.Actions);
            Assert.Null(row.StartDisabledReason);
        });
    }

    [Fact]
    public void An_empty_store_shows_an_empty_list_with_New_Event_available()
    {
        using var rig = new SetupRig();

        Assert.Empty(rig.List.Events);
        Assert.True(rig.List.NewEventAvailable);
    }

    [Fact]
    public async Task While_an_Event_runs_its_row_keeps_only_Gallery_and_Open_folder_and_every_Start_is_off()
    {
        using var rig = await RigAsync();
        rig.StartRunning(await SetupSamples.LoadAsync(rig.Store, SetupSamples.HarbourId));

        var rows = rig.List.Events.ToDictionary(row => row.Id);
        var running = rows[SetupSamples.HarbourId];
        Assert.True(running.Running);
        Assert.Equal(new EventRowActions(false, false, false, true, true), running.Actions);
        Assert.Equal(StartDisabledReason.EventRunning, running.StartDisabledReason);
        foreach (var other in new[] { SetupSamples.WeddingId, SetupSamples.OlderId })
        {
            Assert.False(rows[other].Running);
            Assert.Equal(new EventRowActions(false, true, true, true, true), rows[other].Actions);
            Assert.Equal(StartDisabledReason.EventRunning, rows[other].StartDisabledReason);
        }
    }

    [Fact]
    public async Task The_running_Event_cannot_be_edited_or_deleted_but_the_others_can()
    {
        using var rig = await RigAsync();
        rig.StartRunning(await SetupSamples.LoadAsync(rig.Store, SetupSamples.HarbourId));

        rig.Rejects(RejectReason.EventRunning, "EditEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");
        rig.Rejects(RejectReason.EventRunning, "DeleteEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");
        Assert.Null(rig.Latest.Overlays.Confirmation);
        Assert.Equal(OperatorSurface.EventList, rig.Latest.Surface);

        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.OlderId}}"}""");
        Assert.Equal(ConfirmationKind.DeleteEvent, rig.Latest.Overlays.Confirmation!.Kind);
        rig.Accepts("CancelDeleteEvent");
        rig.Accepts("EditEvent", $$"""{"eventId":"{{SetupSamples.OlderId}}"}""");
        rig.WaitUntil(() => rig.Latest.Surface == OperatorSurface.EventSetup);
        Assert.Equal(SetupSamples.OlderId, rig.Setup.EventId);
    }

    [Fact]
    public async Task An_unknown_Event_is_not_available_for_edit_delete_and_folder()
    {
        using var rig = await RigAsync();
        const string Unknown = "99999999-9999-4999-8999-999999999999";

        rig.Rejects(RejectReason.NotAvailable, "EditEvent", $$"""{"eventId":"{{Unknown}}"}""");
        rig.Rejects(RejectReason.NotAvailable, "DeleteEvent", $$"""{"eventId":"{{Unknown}}"}""");
        rig.Rejects(RejectReason.NotAvailable, "OpenEventFolder", $$"""{"eventId":"{{Unknown}}"}""");
        Assert.Empty(rig.Host.OpenedFolders);
    }

    [Fact]
    public async Task Open_folder_asks_the_host_for_the_Event_folder_and_is_recorded_for_diagnostics_also_for_the_running_Event()
    {
        using var rig = await RigAsync(hooks: true);
        rig.Accepts("OpenEventFolder", $$"""{"eventId":"{{SetupSamples.WeddingId}}"}""");
        rig.StartRunning(await SetupSamples.LoadAsync(rig.Store, SetupSamples.HarbourId));
        rig.Accepts("OpenEventFolder", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");

        var root = rig.Store.DataRoot;
        Assert.Equal(
            [Path.Combine(root, "Events", SetupSamples.WeddingId), Path.Combine(root, "Events", SetupSamples.HarbourId)],
            rig.Host.OpenedFolders);
        var recorded = rig.Diagnostics().OpenFolderRequests;
        Assert.Equal(rig.Host.OpenedFolders, recorded.Select(request => request.Path));
        Assert.DoesNotContain(recorded, request => request.Path.Contains("Lindqvist", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Deleting_opens_a_confirmation_with_the_name_and_the_counts()
    {
        var store = await SetupSamples.SeededStoreAsync();
        var token = TestContext.Current.CancellationToken;
        var cycle = await store.StartGuestCycleAsync(SetupSamples.WeddingId, token);
        var capture = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), token);
        await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 2), token);
        await store.CommitPhotoStripAsync(StorageSamples.StripCommit(cycle.Id, [capture.Id]), token);
        await store.StartGuestCycleAsync(SetupSamples.WeddingId, token);
        using var rig = new SetupRig(store);

        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.WeddingId}}"}""");

        var confirmation = rig.Latest.Overlays.Confirmation!;
        Assert.Equal(ConfirmationKind.DeleteEvent, confirmation.Kind);
        Assert.Equal("Lindqvist wedding", confirmation.EventName);
        Assert.Equal(new ConfirmationCounts(2, 1, 2), confirmation.Counts);
        Assert.Equal(3, rig.List.Events.Count);
    }

    [Fact]
    public async Task Confirming_removes_the_row_at_once_and_the_store_cleans_up_in_the_background()
    {
        using var rig = await RigAsync();
        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");

        rig.Accepts("ConfirmDeleteEvent");

        Assert.Equal([SetupSamples.WeddingId, SetupSamples.OlderId], rig.List.Events.Select(row => row.Id));
        Assert.Null(rig.Latest.Overlays.Confirmation);
        Assert.Null(rig.Latest.Overlays.Notice);
        Assert.Null(await rig.Store.GetEventAsync(SetupSamples.HarbourId, TestContext.Current.CancellationToken));
        Assert.Equal(1, StoreOf(rig).Inner.PendingDeletionCount);
    }

    [Fact]
    public async Task Cancelling_closes_the_confirmation_and_deletes_nothing()
    {
        using var rig = await RigAsync();
        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");

        rig.Accepts("CancelDeleteEvent");

        Assert.Null(rig.Latest.Overlays.Confirmation);
        Assert.Equal(3, rig.List.Events.Count);
        rig.Rejects(RejectReason.NotAvailable, "ConfirmDeleteEvent");
        rig.Rejects(RejectReason.NotAvailable, "CancelDeleteEvent");
    }

    [Fact]
    public async Task Confirm_and_cancel_need_an_open_delete_confirmation()
    {
        using var rig = await RigAsync();

        rig.Rejects(RejectReason.NotAvailable, "ConfirmDeleteEvent");
        rig.Rejects(RejectReason.NotAvailable, "CancelDeleteEvent");

        rig.OnBrain(() => rig.Brain.SetConfirmation(new ConfirmationOverlay(ConfirmationKind.EndEvent, null, null, null)));
        rig.Rejects(RejectReason.NotAvailable, "ConfirmDeleteEvent");
        rig.Rejects(RejectReason.NotAvailable, "CancelDeleteEvent");
    }

    [Fact]
    public async Task An_Event_that_started_while_the_confirmation_was_open_is_not_deleted()
    {
        using var rig = await RigAsync();
        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");
        rig.StartRunning(await SetupSamples.LoadAsync(rig.Store, SetupSamples.HarbourId));

        rig.Rejects(RejectReason.EventRunning, "ConfirmDeleteEvent");

        Assert.Null(rig.Latest.Overlays.Confirmation);
        Assert.NotNull(await rig.Store.GetEventAsync(SetupSamples.HarbourId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_failed_delete_shows_the_delete_notice_and_keeps_the_row()
    {
        using var rig = await RigAsync();
        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");
        rig.Accepts("ConfirmDeleteEvent");

        // The row is gone; now make the next delete fail.
        StoreOf(rig).Inner.FailNext(StorageErrorKind.DiskFull);
        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.OlderId}}"}""");

        Assert.Equal(OperatorNoticeCode.DeleteFailed, rig.Latest.Overlays.Notice!.Code);
        Assert.Null(rig.Latest.Overlays.Confirmation);
    }

    [Fact]
    public async Task A_delete_the_store_refuses_shows_the_delete_notice_and_a_fresh_list()
    {
        using var rig = await RigAsync();
        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");
        StoreOf(rig).Before = method => method == nameof(IEventStore.MarkEventDeletingAsync)
            ? Task.FromException(new StorageException(StorageErrorKind.DiskFull, "Disk full."))
            : Task.CompletedTask;

        rig.Accepts("ConfirmDeleteEvent");

        Assert.Equal(OperatorNoticeCode.DeleteFailed, rig.Latest.Overlays.Notice!.Code);
        Assert.Equal(3, rig.List.Events.Count);
        Assert.Equal(1, rig.Harness.Logger.Entries.Count(entry => entry.Message.StartsWith("Deleting an Event failed", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_store_that_throws_something_unexpected_is_a_delete_notice_too()
    {
        using var rig = await RigAsync();
        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");
        StoreOf(rig).Before = method => method == nameof(IEventStore.MarkEventDeletingAsync)
            ? Task.FromException(new InvalidOperationException("boom"))
            : Task.CompletedTask;

        rig.Accepts("ConfirmDeleteEvent");

        Assert.Equal(OperatorNoticeCode.DeleteFailed, rig.Latest.Overlays.Notice!.Code);
    }

    [Fact]
    public async Task An_Event_deleted_elsewhere_is_reported_as_not_found_and_leaves_the_list()
    {
        using var rig = await RigAsync();
        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");
        await rig.Store.MarkEventDeletingAsync(SetupSamples.HarbourId, TestContext.Current.CancellationToken);

        rig.Accepts("ConfirmDeleteEvent");

        Assert.Equal(OperatorNoticeCode.EventNotFound, rig.Latest.Overlays.Notice!.Code);
        Assert.Equal([SetupSamples.WeddingId, SetupSamples.OlderId], rig.List.Events.Select(row => row.Id));
    }

    [Fact]
    public async Task When_the_counts_cannot_be_read_the_notice_replaces_the_confirmation()
    {
        using var rig = await RigAsync();
        StoreOf(rig).Before = method => method == nameof(IEventStore.ListGuestCyclesAsync)
            ? Task.FromException(new StorageException(StorageErrorKind.Locked, "Locked."))
            : Task.CompletedTask;

        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");

        Assert.Equal(OperatorNoticeCode.DeleteFailed, rig.Latest.Overlays.Notice!.Code);
        Assert.Null(rig.Latest.Overlays.Confirmation);
        rig.Rejects(RejectReason.NotAvailable, "ConfirmDeleteEvent");
    }

    [Fact]
    public async Task When_the_Event_is_gone_by_the_time_its_counts_are_read_the_notice_says_so_and_the_list_refreshes()
    {
        using var rig = await RigAsync();
        StoreOf(rig).Before = method => method == nameof(IEventStore.ListGuestCyclesAsync)
            ? Task.FromException(new StorageException(StorageErrorKind.NotFound, "Gone."))
            : Task.CompletedTask;

        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");

        Assert.Equal(OperatorNoticeCode.EventNotFound, rig.Latest.Overlays.Notice!.Code);
    }

    [Fact]
    public async Task A_second_delete_request_replaces_the_first_and_a_late_count_is_dropped()
    {
        using var rig = await RigAsync();
        var gate = new TaskCompletionSource();
        var calls = 0;
        StoreOf(rig).Before = method =>
        {
            if (method == nameof(IEventStore.ListGuestCyclesAsync) && calls++ == 0)
            {
                return gate.Task;
            }

            return Task.CompletedTask;
        };

        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");
        Assert.Null(rig.Latest.Overlays.Confirmation);
        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.OlderId}}"}""");
        Assert.Equal("Older party", rig.Latest.Overlays.Confirmation!.EventName);

        gate.SetResult();
        rig.WaitUntil(() => calls == 2);
        rig.Harness.Run();

        Assert.Equal("Older party", rig.Latest.Overlays.Confirmation!.EventName);
    }

    [Fact]
    public async Task A_count_that_arrives_after_the_Event_started_is_dropped()
    {
        using var rig = await RigAsync();
        var gate = new TaskCompletionSource();
        StoreOf(rig).Before = method => method == nameof(IEventStore.ListGuestCyclesAsync) ? gate.Task : Task.CompletedTask;
        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");
        rig.StartRunning(await SetupSamples.LoadAsync(rig.Store, SetupSamples.HarbourId));

        gate.SetResult();
        rig.Harness.Run();
        Thread.Sleep(50);
        rig.Harness.Run();

        Assert.Null(rig.Latest.Overlays.Confirmation);
    }

    [Fact]
    public async Task A_count_that_fails_after_a_newer_request_is_dropped_quietly()
    {
        using var rig = await RigAsync();
        var gate = new TaskCompletionSource();
        var calls = 0;
        StoreOf(rig).Before = method =>
        {
            if (method == nameof(IEventStore.ListGuestCyclesAsync) && calls++ == 0)
            {
                return gate.Task;
            }

            return Task.CompletedTask;
        };
        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");
        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.OlderId}}"}""");

        gate.SetException(new StorageException(StorageErrorKind.Locked, "Locked."));
        Thread.Sleep(50);
        rig.Harness.Run();

        Assert.Null(rig.Latest.Overlays.Notice);
        Assert.Equal("Older party", rig.Latest.Overlays.Confirmation!.EventName);
    }

    [Fact]
    public async Task A_list_that_cannot_be_read_after_a_delete_leaves_the_rows_as_they_were()
    {
        using var rig = await RigAsync();
        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");
        StoreOf(rig).Before = method => method == nameof(IEventStore.ListEventsAsync)
            ? Task.FromException(new StorageException(StorageErrorKind.Locked, "Locked."))
            : Task.CompletedTask;

        rig.Accepts("ConfirmDeleteEvent");

        Assert.Equal(3, rig.List.Events.Count);
        Assert.Null(rig.Latest.Overlays.Notice);
        Assert.Contains(rig.Harness.Logger.Entries, entry => entry.Message.StartsWith("Listing the Events failed", StringComparison.Ordinal));
    }

    [Fact]
    public void Opening_Event_setup_needs_storage_to_be_ready()
    {
        var store = new InMemoryEventStore();
        store.FailOpen(StorageErrorKind.DiskFull);
        using var rig = new SetupRig(store);

        rig.Rejects(RejectReason.NotAvailable, "OpenNewEvent");
        rig.Rejects(RejectReason.NotAvailable, "EditEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");
        Assert.Equal(OperatorSurface.StorageError, rig.Latest.Surface);
    }

    [Fact]
    public async Task OpenNewEvent_opens_a_draft_with_todays_local_date_and_the_defaults()
    {
        using var rig = await RigAsync();

        rig.OpenNew();

        var setup = rig.Setup;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(rig.Clock.UtcNow, TestDependencies.FixedTimeZone).DateTime);
        Assert.Equal(OperatorSurface.EventSetup, rig.Latest.Surface);
        Assert.True(setup.IsNew);
        Assert.Null(setup.EventId);
        Assert.False(setup.Dirty);
        Assert.Equal(today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), setup.Fields.Date);
        Assert.True(setup.Mirror);
        Assert.Equal(5, setup.CountdownSeconds);
        Assert.False(setup.GuestGalleryEnabled);
        Assert.True(setup.GuestSoundsEnabled);
        Assert.Equal(PrinterChoice.None, setup.Printer);
        Assert.All(setup.Templates, template => Assert.True(template.Offered));
        Assert.Equal("signature-strip", setup.Templates.Single(template => template.IsDefault).Id);
        Assert.All(setup.Looks, look => Assert.True(look.Offered));
        Assert.Equal("naturale-v1", setup.Looks.Single(look => look.IsDefault).Id);
        Assert.Equal(new EventSetupPinView(false, false), setup.Pin);
        Assert.Null(setup.Camera.SelectedDeviceId);
        Assert.Null(setup.GuestDisplay.SelectedId);
    }

    [Fact]
    public async Task The_default_date_follows_the_brains_time_zone_not_the_machines()
    {
        using var rig = await RigAsync();
        rig.Clock.Advance(TimeSpan.FromHours(11));

        rig.OpenNew();

        Assert.Equal(new DateTimeOffset(2026, 10, 13, 23, 0, 0, TimeSpan.Zero), rig.Clock.UtcNow);
        Assert.Equal("2026-10-14", rig.Setup.Fields.Date);
    }

    [Fact]
    public async Task A_second_OpenNewEvent_or_EditEvent_is_not_available_while_a_draft_is_open()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.Rejects(RejectReason.NotAvailable, "OpenNewEvent");
        rig.Rejects(RejectReason.NotAvailable, "EditEvent", $$"""{"eventId":"{{SetupSamples.OlderId}}"}""");
    }

    [Fact]
    public async Task A_draft_whose_surface_was_left_without_closing_it_is_dropped_by_the_next_open()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        rig.Field("name", "Forgotten");
        rig.Brain.ShowOperatorSurface(OperatorSurface.EventList);
        rig.Harness.Run();

        rig.OpenNew();

        Assert.Equal(string.Empty, rig.Setup.Fields.Name);
    }

    [Fact]
    public async Task A_running_Event_does_not_stop_a_new_Event_from_being_started_in_setup()
    {
        using var rig = await RigAsync();
        rig.StartRunning(await SetupSamples.LoadAsync(rig.Store, SetupSamples.HarbourId));

        rig.OpenNew();

        Assert.Equal(OperatorSurface.EventSetup, rig.Latest.Surface);
        Assert.NotNull(rig.Latest.RunningEvent);
    }
}
