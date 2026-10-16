using FotoHavn.Core.Events.Active;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Core.Tests.GuestCycles.Orchestration;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core.Tests.Gallery;

/// <summary>The operator's Event Gallery (M9 contract section 4.1): opening, paging, detail, folder, delete, and every reason a command is refused.</summary>
public sealed class OperatorGalleryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Opening_the_gallery_lists_the_Events_Guest_Cycles_newest_first_with_the_first_page_loaded()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec(), new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 2), new CycleSpec());

        rig.OpenGallery().ShouldBeAccepted();

        Assert.Equal(OperatorSurface.EventGallery, rig.OperatorView.Surface);
        Assert.Equal("Studio open day", rig.Gallery.EventName);
        Assert.Equal(GalleryRig.EventId, rig.Gallery.EventId);
        Assert.Equal(GalleryAssert.NewestFirst(seeded), rig.ItemIds);
        Assert.False(rig.Gallery.Running);
        Assert.False(rig.Gallery.LoadingMore);
        Assert.False(rig.Gallery.LoadFailed);
        Assert.False(rig.Gallery.Empty);
        Assert.Null(rig.Gallery.NextCursor);
        Assert.Null(rig.OperatorView.EventList);
        Assert.Null(rig.OperatorView.RunningEvent);
        var call = Assert.Single(rig.Store.ListCalls);
        Assert.Equal((GalleryRig.EventId, GuestCycleFilter.All, (string?)null), call);
    }

    [Fact]
    public async Task A_cold_page_fills_in_item_by_item_and_never_shows_an_item_before_its_thumbnails()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedManyAsync(6);
        var slow = new[] { seeded[0], seeded[1] }.Select(cycle => (cycle.Id, Gate: new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))).ToList();
        foreach (var (id, gate) in slow)
        {
            rig.Store.GatedThumbnails[id] = gate;
        }

        rig.OpenGallery().ShouldBeAccepted();

        var partial = rig.Gallery;
        Assert.Equal(GalleryAssert.NewestFirst(seeded.Skip(2)), partial.Items.Select(item => item.GuestCycleId));
        Assert.True(partial.LoadingMore);
        Assert.False(partial.Empty);
        Assert.Null(partial.NextCursor);

        foreach (var (_, gate) in slow)
        {
            gate.SetResult();
        }

        rig.Settle();

        Assert.Equal(GalleryAssert.NewestFirst(seeded), rig.ItemIds);
        Assert.False(rig.Gallery.LoadingMore);
    }

    [Fact]
    public async Task An_item_that_finishes_after_the_gallery_closed_or_reloaded_is_ignored()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec(), new CycleSpec());
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Store.GatedThumbnails[seeded[0].Id] = gate;
        rig.OpenGallery();
        rig.OpenGallery();
        rig.Operator("CloseGallery");

        gate.SetResult();
        rig.Settle();

        Assert.Equal(OperatorSurface.EventList, rig.OperatorView.Surface);
        Assert.Null(rig.Diagnostics().Gallery);
    }

    [Fact]
    public async Task A_cycle_deleted_while_its_item_is_still_filling_in_never_shows()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec(), new CycleSpec(), new CycleSpec());
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Store.GatedThumbnails[seeded[0].Id] = gate;
        rig.OpenGallery();
        rig.Operator("DeleteGuestCycle", GalleryRig.Json("guestCycleId", seeded[2].Id)).ShouldBeAccepted();
        rig.Operator("ConfirmDeleteGuestCycle");

        gate.SetResult();
        rig.Settle();

        Assert.Equal(GalleryAssert.NewestFirst(seeded.Take(2)), rig.ItemIds);
    }

    [Fact]
    public async Task An_Event_without_Guest_Cycles_says_so()
    {
        using var rig = await GalleryRig.CreateAsync();

        rig.OpenGallery().ShouldBeAccepted();

        Assert.Empty(rig.Gallery.Items);
        Assert.True(rig.Gallery.Empty);
        Assert.False(rig.Gallery.LoadingMore);
        Assert.Null(rig.Gallery.NextCursor);
    }

    [Fact]
    public async Task While_the_first_page_loads_the_gallery_shows_loading_and_not_empty()
    {
        using var rig = await GalleryRig.CreateAsync();
        await rig.SeedAsync(new CycleSpec());
        var gate = rig.Store.GateNextList();

        rig.OpenGallery().ShouldBeAccepted();
        var loading = rig.Gallery;
        gate.SetResult();
        rig.Settle();

        Assert.Empty(loading.Items);
        Assert.True(loading.LoadingMore);
        Assert.False(loading.Empty);
        Assert.Null(loading.NextCursor);
        Assert.Single(rig.Gallery.Items);
        Assert.False(rig.Gallery.LoadingMore);
    }

    [Fact]
    public async Task Pages_are_forty_at_a_time_and_each_load_more_carries_the_cursor_it_was_given()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedManyAsync(85);
        var expected = GalleryAssert.NewestFirst(seeded);

        rig.OpenGallery();
        var firstCursor = rig.Gallery.NextCursor!;
        Assert.Equal(40, rig.Gallery.Items.Count);
        rig.LoadMore(firstCursor).ShouldBeAccepted();
        var secondCursor = rig.Gallery.NextCursor!;
        Assert.Equal(80, rig.Gallery.Items.Count);
        rig.LoadMore(secondCursor).ShouldBeAccepted();

        Assert.Equal(expected, rig.ItemIds);
        Assert.Null(rig.Gallery.NextCursor);
        Assert.Equal<string?>([null, firstCursor, secondCursor], rig.Store.ListCalls.Select(call => call.Cursor));
        Assert.All(rig.Store.ListCalls, call => Assert.Equal(GuestCycleFilter.All, call.Filter));
    }

    [Fact]
    public async Task A_stale_repeated_wrong_or_missing_cursor_is_not_available_and_so_is_a_load_while_one_runs()
    {
        using var rig = await GalleryRig.CreateAsync();
        await rig.SeedManyAsync(85);
        rig.OpenGallery();
        var cursor = rig.Gallery.NextCursor!;

        rig.LoadMore("not-the-cursor").ShouldBeRejected();
        var gate = rig.Store.GateNextList();
        rig.LoadMore(cursor).ShouldBeAccepted();
        rig.LoadMore(cursor).ShouldBeRejected();
        Assert.True(rig.Gallery.LoadingMore);
        gate.SetResult();
        rig.Settle();
        rig.LoadMore(cursor).ShouldBeRejected();
        rig.LoadMore(rig.Gallery.NextCursor!).ShouldBeAccepted();
        rig.LoadMore(string.Empty).ShouldBeRejected();

        Assert.Null(rig.Gallery.NextCursor);
        rig.LoadMore(cursor).ShouldBeRejected();
        Assert.Equal(85, rig.Gallery.Items.Count);
    }

    [Fact]
    public async Task A_first_page_that_fails_says_so_and_opening_the_gallery_again_reloads_it()
    {
        using var rig = await GalleryRig.CreateAsync();
        await rig.SeedAsync(new CycleSpec());
        rig.Store.FailLists = 1;

        rig.OpenGallery().ShouldBeAccepted();
        var failed = rig.Gallery;
        rig.OpenGallery().ShouldBeAccepted();

        Assert.True(failed.LoadFailed);
        Assert.False(failed.LoadingMore);
        Assert.Empty(failed.Items);
        Assert.False(failed.Empty);
        Assert.Null(failed.NextCursor);
        Assert.False(rig.Gallery.LoadFailed);
        Assert.Single(rig.Gallery.Items);
        Assert.Contains(rig.Active.Logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("StorageException", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_later_page_that_fails_keeps_what_is_loaded_and_loading_more_with_the_same_cursor_tries_again()
    {
        using var rig = await GalleryRig.CreateAsync();
        await rig.SeedManyAsync(45);
        rig.OpenGallery();
        var cursor = rig.Gallery.NextCursor!;
        rig.Store.FailLists = 1;

        rig.LoadMore(cursor).ShouldBeAccepted();
        var failed = rig.Gallery;
        rig.LoadMore(cursor).ShouldBeAccepted();

        Assert.True(failed.LoadFailed);
        Assert.False(failed.LoadingMore);
        Assert.Equal(40, failed.Items.Count);
        Assert.Equal(cursor, failed.NextCursor);
        Assert.False(rig.Gallery.LoadFailed);
        Assert.Equal(45, rig.Gallery.Items.Count);
        Assert.Null(rig.Gallery.NextCursor);
    }

    [Fact]
    public async Task A_page_that_arrives_after_the_gallery_was_closed_is_ignored()
    {
        using var rig = await GalleryRig.CreateAsync();
        await rig.SeedAsync(new CycleSpec());
        var gate = rig.Store.GateNextList();
        rig.OpenGallery();

        rig.Operator("CloseGallery").ShouldBeAccepted();
        gate.SetResult();
        rig.Settle();

        Assert.Equal(OperatorSurface.EventList, rig.OperatorView.Surface);
        Assert.Null(rig.OperatorView.EventGallery);
        Assert.Null(rig.Diagnostics().Gallery);
    }

    [Fact]
    public async Task A_page_that_arrives_after_the_gallery_reloaded_is_ignored_and_never_duplicates()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedManyAsync(3);
        var gate = rig.Store.GateNextList();
        rig.OpenGallery();

        rig.OpenGallery().ShouldBeAccepted();
        var reloaded = rig.ItemIds;
        gate.SetResult();
        rig.Settle();

        Assert.Equal(GalleryAssert.NewestFirst(seeded), reloaded);
        Assert.Equal(reloaded, rig.ItemIds);
        Assert.False(rig.Gallery.LoadingMore);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    public async Task An_Event_that_is_not_saved_has_no_gallery(string eventId)
    {
        using var rig = await GalleryRig.CreateAsync();

        rig.OpenGallery(eventId).ShouldBeRejected();

        Assert.Equal(OperatorSurface.EventList, rig.OperatorView.Surface);
    }

    [Fact]
    public async Task The_gallery_does_not_open_behind_an_Event_setup_draft_or_from_another_gallery()
    {
        using var rig = await GalleryRig.CreateAsync(new GalleryRigOptions { OtherEvent = true });
        rig.Operator("OpenNewEvent").ShouldBeAccepted();
        rig.OpenGallery().ShouldBeRejected();
        rig.Operator("LeaveEventSetup").ShouldBeAccepted();
        if (rig.OperatorView.Overlays.Confirmation is not null)
        {
            rig.Operator("ConfirmLeave", """{"discard":true}""");
        }

        rig.OpenGallery().ShouldBeAccepted();
        rig.OpenGallery(ActiveRig.OtherEventId).ShouldBeRejected();

        Assert.Equal(GalleryRig.EventId, rig.Gallery.EventId);
    }

    [Fact]
    public async Task The_gallery_is_not_available_while_storage_is_down()
    {
        var store = new InMemoryEventStore();
        store.FailOpen(StorageErrorKind.DiskFull);
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { Store = store, Seed = false });
        rig.Run();

        Assert.Equal(RejectReason.NotAvailable, rig.OperatorCommand("OpenGallery", GalleryRig.Json("eventId", ActiveRig.EventId)).Reason);
    }

    [Fact]
    public async Task While_the_Guest_Screen_covers_the_Operator_Screen_there_is_no_gallery()
    {
        using var rig = await GalleryRig.CreateAsync();
        var record = (await rig.Store.GetEventAsync(ActiveRig.EventId, Ct))!;
        rig.Brain.ShowActiveEvent(new ActiveEventState(record, ActiveRig.FixedStart, FakeDisplayAdapter.DefaultGuest, singleScreen: true));
        rig.Settle();

        rig.OpenGallery().ShouldBeRejected();

        Assert.Equal(OperatorSurface.GuestScreenActive, rig.OperatorView.Surface);
    }

    [Fact]
    public async Task Every_gallery_command_is_not_available_when_no_gallery_is_open()
    {
        using var rig = await GalleryRig.CreateAsync();

        foreach (var (name, payload) in new[]
        {
            ("LoadMoreGuestCycles", """{"cursor":"c"}"""),
            ("OpenGuestCycle", """{"guestCycleId":"00000000-0000-0000-0000-000000000001"}"""),
            ("CloseGuestCycle", "{}"),
            ("OpenGuestCycleFolder", """{"guestCycleId":"00000000-0000-0000-0000-000000000001"}"""),
            ("DeleteGuestCycle", """{"guestCycleId":"00000000-0000-0000-0000-000000000001"}"""),
            ("ConfirmDeleteGuestCycle", "{}"),
            ("CancelDeleteGuestCycle", "{}"),
            ("CloseGallery", "{}"),
        })
        {
            Assert.Equal(RejectReason.NotAvailable, rig.Operator(name, payload).Reason);
        }

        Assert.Empty(rig.Active.Recorded.OpenedFolders);
    }

    [Fact]
    public async Task A_Guest_Cycle_that_is_not_in_the_loaded_pages_can_not_be_opened_deleted_or_shown_in_its_folder()
    {
        using var rig = await GalleryRig.CreateAsync();
        await rig.SeedAsync(new CycleSpec());
        rig.OpenGallery();
        var unknown = """{"guestCycleId":"00000000-0000-0000-0000-0000000000ff"}""";

        Assert.Equal(RejectReason.NotAvailable, rig.Operator("OpenGuestCycle", unknown).Reason);
        Assert.Equal(RejectReason.NotAvailable, rig.Operator("OpenGuestCycleFolder", unknown).Reason);
        Assert.Equal(RejectReason.NotAvailable, rig.Operator("DeleteGuestCycle", unknown).Reason);
        Assert.Equal(RejectReason.NotAvailable, rig.Operator("CloseGuestCycle").Reason);
        Assert.Equal(RejectReason.NotAvailable, rig.Operator("ConfirmDeleteGuestCycle").Reason);
        Assert.Equal(RejectReason.NotAvailable, rig.Operator("CancelDeleteGuestCycle").Reason);
        Assert.Empty(rig.Active.Recorded.OpenedFolders);
        Assert.Null(rig.OperatorView.Overlays.Confirmation);
    }

    [Fact]
    public async Task A_completed_Guest_Cycle_opens_with_its_strip_and_its_arranged_Captures_and_closing_it_keeps_the_pages()
    {
        using var rig = await GalleryRig.CreateAsync();
        var cycle = (await rig.SeedAsync(new CycleSpec()))[0];
        rig.OpenGallery();
        var listCalls = rig.Store.ListCalls.Count;

        rig.OpenCycle(cycle.Id).ShouldBeAccepted();

        var detail = rig.Detail;
        Assert.Equal(OperatorSurface.GuestCycleDetail, rig.OperatorView.Surface);
        Assert.Null(rig.OperatorView.EventGallery);
        Assert.Equal((GalleryRig.EventId, "Studio open day", cycle.Id, cycle.Cycle.StartedAt), (detail.EventId, detail.EventName, detail.GuestCycleId, detail.StartedAt));
        Assert.Equal((GalleryOutcome.Completed, "Signature Strip", "Naturale"), (detail.Outcome, detail.TemplateName, detail.LookName));
        Assert.Equal($"https://media.fotohavn/events/{GalleryRig.EventId}/guest-cycles/{cycle.Id}/photo-strip.png?v={cycle.Strip!.Sha256[..8]}", detail.Strip!.ImageUrl);
        Assert.Equal((600, 1800, false), (detail.Strip.Width, detail.Strip.Height, detail.Strip.Landscape));
        Assert.Equal(cycle.Kept.AsEnumerable().Reverse().Select(capture => $"capture-{capture.Sequence}.jpg?v={capture.Sha256[..8]}"), detail.Captures.Select(capture => capture.ImageUrl[(capture.ImageUrl.LastIndexOf('/') + 1)..]));
        Assert.True(detail.CanOpenFolder);
        Assert.True(detail.CanDelete);
        Assert.False(detail.Unavailable);

        rig.Operator("CloseGuestCycle").ShouldBeAccepted();

        Assert.Equal(OperatorSurface.EventGallery, rig.OperatorView.Surface);
        Assert.Equal([cycle.Id], rig.ItemIds);
        Assert.Equal(listCalls, rig.Store.ListCalls.Count);
    }

    [Fact]
    public async Task A_Guest_Cycle_without_a_strip_opens_with_its_kept_Captures_in_sequence_order_and_no_names()
    {
        using var rig = await GalleryRig.CreateAsync();
        var cycle = (await rig.SeedAsync(new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 3, Discarded: 1)))[0];
        rig.OpenGallery();

        rig.OpenCycle(cycle.Id);

        Assert.Null(rig.Detail.Strip);
        Assert.Null(rig.Detail.TemplateName);
        Assert.Null(rig.Detail.LookName);
        Assert.Equal(GalleryOutcome.StartedOver, rig.Detail.Outcome);
        Assert.Equal(["capture-1.jpg", "capture-2.jpg", "capture-3.jpg"], rig.Detail.Captures.Select(capture => capture.ImageUrl.Split('/')[^1].Split('?')[0]));
    }

    [Fact]
    public async Task A_Guest_Cycle_whose_files_are_gone_opens_as_unavailable_with_no_images()
    {
        using var rig = await GalleryRig.CreateAsync();
        var cycle = (await rig.SeedAsync(new CycleSpec()))[0];
        rig.Store.MissingThumbnails.Add(cycle.Id);
        rig.OpenGallery();
        Assert.True(rig.Gallery.Items.Single().Unavailable);

        rig.OpenCycle(cycle.Id);

        Assert.True(rig.Detail.Unavailable);
        Assert.Null(rig.Detail.Strip);
        Assert.Empty(rig.Detail.Captures);
        Assert.Equal(("Signature Strip", "Naturale"), (rig.Detail.TemplateName, rig.Detail.LookName));
    }

    [Fact]
    public async Task A_Guest_Cycle_that_is_gone_from_the_store_shows_a_notice_and_leaves_the_list()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec(), new CycleSpec());
        rig.OpenGallery();
        await rig.Memory.MarkGuestCycleDeletingAsync(seeded[0].Id, Ct);

        rig.OpenCycle(seeded[0].Id).ShouldBeAccepted();

        Assert.Equal(OperatorSurface.EventGallery, rig.OperatorView.Surface);
        Assert.Equal([seeded[1].Id], rig.ItemIds);
        Assert.Equal(OperatorNoticeCode.GuestCycleNotFound, rig.OperatorView.Overlays.Notice!.Code);
    }

    [Fact]
    public async Task A_detail_that_can_not_be_read_changes_nothing()
    {
        using var rig = await GalleryRig.CreateAsync();
        var cycle = (await rig.SeedAsync(new CycleSpec()))[0];
        rig.OpenGallery();
        rig.Store.FailGets = 1;

        rig.OpenCycle(cycle.Id).ShouldBeAccepted();

        Assert.Equal(OperatorSurface.EventGallery, rig.OperatorView.Surface);
        Assert.Equal([cycle.Id], rig.ItemIds);
        Assert.Null(rig.OperatorView.Overlays.Notice);
        Assert.Contains(rig.Active.Logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task A_detail_that_arrives_after_the_operator_left_is_ignored_and_a_second_open_is_refused_while_one_shows()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec(), new CycleSpec());
        rig.OpenGallery();
        var gate = rig.Store.GateNextGet();
        rig.OpenCycle(seeded[0].Id).ShouldBeAccepted();

        rig.Operator("CloseGallery");
        gate.SetResult();
        rig.Settle();
        Assert.Equal(OperatorSurface.EventList, rig.OperatorView.Surface);

        rig.OpenGallery();
        rig.OpenCycle(seeded[0].Id).ShouldBeAccepted();
        rig.OpenCycle(seeded[1].Id).ShouldBeRejected();
        Assert.Equal(seeded[0].Id, rig.Detail.GuestCycleId);
    }

    [Fact]
    public async Task Closing_the_detail_while_it_loads_leaves_the_list_without_it()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec(), new CycleSpec());
        rig.OpenGallery();
        rig.OpenCycle(seeded[0].Id);
        rig.OpenGallery().ShouldBeAccepted();
        var gate = rig.Store.GateNextGet();
        rig.OpenCycle(seeded[1].Id);

        rig.Operator("CloseGallery");
        rig.OpenGallery();
        gate.SetResult();
        rig.Settle();

        Assert.Equal(OperatorSurface.EventGallery, rig.OperatorView.Surface);
        Assert.Null(rig.OperatorView.GuestCycleDetail);
    }

    [Fact]
    public async Task Open_folder_asks_the_host_for_the_Guest_Cycles_folder_and_the_path_never_reaches_a_presentation()
    {
        using var rig = await GalleryRig.CreateAsync();
        var cycle = (await rig.SeedAsync(new CycleSpec()))[0];
        rig.OpenGallery();

        rig.Operator("OpenGuestCycleFolder", GalleryRig.Json("guestCycleId", cycle.Id)).ShouldBeAccepted();

        var expected = Path.Combine(@"C:\FOTOHAVN-test\Data", "Events", GalleryRig.EventId, "GuestCycles", cycle.Id);
        Assert.Equal([expected], rig.Active.Recorded.OpenedFolders);
        Assert.Equal([expected], rig.Diagnostics().OpenFolderRequests.Select(request => request.Path));
        Assert.DoesNotContain("FOTOHAVN-test", rig.PresentationsSent(), StringComparison.Ordinal);
        Assert.DoesNotContain(rig.Active.Logger.Entries, entry => entry.Message.Contains("FOTOHAVN-test", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Delete_asks_first_with_the_time_and_the_number_of_files_and_cancel_removes_nothing()
    {
        using var rig = await GalleryRig.CreateAsync();
        var cycle = (await rig.SeedAsync(new CycleSpec(Discarded: 1)))[0];
        rig.OpenGallery();

        rig.DeleteCycle(cycle.Id).ShouldBeAccepted();

        var confirmation = rig.OperatorView.Overlays.Confirmation!;
        Assert.Equal(ConfirmationKind.DeleteGuestCycle, confirmation.Kind);
        Assert.Null(confirmation.EventName);
        Assert.Null(confirmation.Counts);
        Assert.Equal(new DeleteGuestCycleConfirmation(cycle.Id, cycle.Cycle.StartedAt, 5), confirmation.GuestCycle);
        Assert.Equal(OperatorSurface.EventGallery, rig.OperatorView.Surface);

        rig.Operator("CancelDeleteGuestCycle").ShouldBeAccepted();

        Assert.Null(rig.OperatorView.Overlays.Confirmation);
        Assert.Equal([cycle.Id], rig.ItemIds);
        Assert.Empty(rig.Store.Marked);
        rig.Operator("ConfirmDeleteGuestCycle").ShouldBeRejected();
    }

    [Fact]
    public async Task Confirming_removes_the_cycle_at_once_before_the_store_has_marked_it_and_the_store_then_hides_it()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec(), new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 2));
        rig.OpenGallery();
        rig.DeleteCycle(seeded[0].Id);
        var mark = rig.Store.GateNextMark();

        rig.Active.OperatorCommand("ConfirmDeleteGuestCycle").ShouldBeAccepted();

        Assert.Equal([seeded[1].Id], rig.ItemIds);
        Assert.Null(rig.OperatorView.Overlays.Confirmation);
        rig.Settle();
        Assert.Equal([seeded[0].Id], rig.Store.Marked);
        Assert.NotNull(await rig.Memory.GetGuestCycleAsync(seeded[0].Id, Ct));

        mark.SetResult();
        rig.Settle();

        Assert.Null(await rig.Memory.GetGuestCycleAsync(seeded[0].Id, Ct));
        Assert.Equal([seeded[1].Id], rig.ItemIds);
        Assert.Null(rig.OperatorView.Overlays.Notice);
    }

    [Fact]
    public async Task The_Event_list_shows_the_new_count_after_a_delete()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec(), new CycleSpec());
        rig.Operator("CloseGallery");
        rig.OpenGallery();
        rig.DeleteCycle(seeded[0].Id);
        rig.Operator("ConfirmDeleteGuestCycle");

        rig.Operator("CloseGallery");

        Assert.Equal(1, rig.OperatorView.EventList!.Events.Single(row => row.Id == GalleryRig.EventId).CompletedGuestCycles);
    }

    [Fact]
    public async Task A_delete_from_the_detail_asks_over_the_detail_and_confirming_returns_to_the_list_without_the_cycle()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec(), new CycleSpec());
        rig.OpenGallery();
        rig.OpenCycle(seeded[0].Id);
        var gate = rig.Store.GateNextGet();

        rig.DeleteCycle(seeded[0].Id).ShouldBeAccepted();
        var preparing = rig.Detail;
        gate.SetResult();
        rig.Settle();

        Assert.False(preparing.CanDelete);
        Assert.True(rig.Detail.CanDelete);
        Assert.Equal(OperatorSurface.GuestCycleDetail, rig.OperatorView.Surface);
        Assert.Equal(seeded[0].Id, rig.OperatorView.Overlays.Confirmation!.GuestCycle!.GuestCycleId);

        rig.Operator("ConfirmDeleteGuestCycle").ShouldBeAccepted();

        Assert.Equal(OperatorSurface.EventGallery, rig.OperatorView.Surface);
        Assert.Equal([seeded[1].Id], rig.ItemIds);
    }

    [Fact]
    public async Task A_delete_that_fails_in_the_store_shows_a_notice_and_the_cycle_comes_back()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec(), new CycleSpec());
        rig.OpenGallery();
        rig.DeleteCycle(seeded[0].Id);
        rig.Store.FailMark = StorageErrorKind.Locked;

        rig.Operator("ConfirmDeleteGuestCycle").ShouldBeAccepted();

        Assert.Equal(OperatorNoticeCode.GuestCycleDeleteFailed, rig.OperatorView.Overlays.Notice!.Code);
        Assert.Equal(GalleryAssert.NewestFirst(seeded), rig.ItemIds);
        Assert.Contains(rig.Active.Logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Locked", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_delete_of_a_cycle_the_store_no_longer_has_shows_that_it_is_not_there()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec(), new CycleSpec());
        rig.OpenGallery();
        rig.DeleteCycle(seeded[0].Id);
        await rig.Memory.MarkGuestCycleDeletingAsync(seeded[0].Id, Ct);

        rig.Operator("ConfirmDeleteGuestCycle").ShouldBeAccepted();

        Assert.Equal(OperatorNoticeCode.GuestCycleNotFound, rig.OperatorView.Overlays.Notice!.Code);
        Assert.Equal([seeded[1].Id], rig.ItemIds);
    }

    [Fact]
    public async Task A_cycle_that_is_gone_when_its_files_are_counted_shows_that_it_is_not_there_and_leaves_the_list()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec(), new CycleSpec());
        rig.OpenGallery();
        await rig.Memory.MarkGuestCycleDeletingAsync(seeded[0].Id, Ct);

        rig.DeleteCycle(seeded[0].Id).ShouldBeAccepted();

        Assert.Null(rig.OperatorView.Overlays.Confirmation);
        Assert.Equal(OperatorNoticeCode.GuestCycleNotFound, rig.OperatorView.Overlays.Notice!.Code);
        Assert.Equal([seeded[1].Id], rig.ItemIds);
    }

    [Fact]
    public async Task Counting_the_files_can_fail_and_the_operator_is_told_the_delete_did_not_happen()
    {
        using var rig = await GalleryRig.CreateAsync();
        var cycle = (await rig.SeedAsync(new CycleSpec()))[0];
        rig.OpenGallery();
        rig.Store.FailGets = 1;

        rig.DeleteCycle(cycle.Id).ShouldBeAccepted();

        Assert.Null(rig.OperatorView.Overlays.Confirmation);
        Assert.Equal(OperatorNoticeCode.GuestCycleDeleteFailed, rig.OperatorView.Overlays.Notice!.Code);
        Assert.Equal([cycle.Id], rig.ItemIds);
    }

    [Fact]
    public async Task A_newer_delete_request_replaces_one_still_counting_and_one_that_arrives_after_the_gallery_closed_is_ignored()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec(), new CycleSpec());
        rig.OpenGallery();
        var slow = rig.Store.GateNextGet();
        rig.DeleteCycle(seeded[0].Id);
        rig.DeleteCycle(seeded[1].Id);
        Assert.Equal(seeded[1].Id, rig.OperatorView.Overlays.Confirmation!.GuestCycle!.GuestCycleId);

        slow.SetResult();
        rig.Settle();
        Assert.Equal(seeded[1].Id, rig.OperatorView.Overlays.Confirmation!.GuestCycle!.GuestCycleId);

        var closing = rig.Store.GateNextGet();
        rig.DeleteCycle(seeded[0].Id);
        rig.Operator("CloseGallery");
        closing.SetResult();
        rig.Settle();

        Assert.Null(rig.OperatorView.Overlays.Confirmation);
    }

    [Fact]
    public async Task Leaving_the_gallery_closes_the_delete_confirmation_and_another_confirmation_replaces_it()
    {
        using var rig = await GalleryRig.CreateAsync();
        var cycle = (await rig.SeedAsync(new CycleSpec()))[0];
        rig.OpenGallery();
        rig.DeleteCycle(cycle.Id);

        rig.Operator("CloseGallery");

        Assert.Null(rig.OperatorView.Overlays.Confirmation);

        rig.OpenGallery();
        rig.DeleteCycle(cycle.Id);
        rig.OpenGallery().ShouldBeAccepted();
        Assert.Null(rig.OperatorView.Overlays.Confirmation);
        rig.Operator("ConfirmDeleteGuestCycle").ShouldBeRejected();
    }

    [Fact]
    public async Task A_confirmation_of_another_kind_replaces_the_delete_confirmation_and_confirming_does_nothing()
    {
        using var rig = await GalleryRig.CreateAsync(new GalleryRigOptions { Start = true });
        var cycle = (await rig.SeedAsync(new CycleSpec()))[0];
        rig.OpenGallery();
        rig.DeleteCycle(cycle.Id);

        rig.Operator("EndEvent").ShouldBeAccepted();

        Assert.Equal(ConfirmationKind.EndEvent, rig.OperatorView.Overlays.Confirmation!.Kind);
        rig.Operator("ConfirmDeleteGuestCycle").ShouldBeRejected();
        rig.Operator("CancelDeleteGuestCycle").ShouldBeRejected();
        Assert.Contains(cycle.Id, rig.ItemIds);
    }

    [Fact]
    public async Task The_running_Events_gallery_is_live_with_the_panel_and_deleting_a_completed_cycle_lowers_its_count()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec(), new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 1), new CycleSpec());
        rig.StartEvent();

        rig.OpenGallery().ShouldBeAccepted();

        Assert.True(rig.Gallery.Running);
        Assert.NotNull(rig.OperatorView.RunningEvent);
        Assert.Equal(2, rig.OperatorView.RunningEvent!.CompletedGuestCycles);
        rig.DeleteCycle(seeded[1].Id);
        rig.Operator("ConfirmDeleteGuestCycle");
        Assert.Equal(2, rig.OperatorView.RunningEvent.CompletedGuestCycles);
        rig.DeleteCycle(seeded[0].Id);
        rig.Operator("ConfirmDeleteGuestCycle");

        Assert.Equal(1, rig.OperatorView.RunningEvent!.CompletedGuestCycles);
        Assert.Equal([seeded[2].Id], rig.ItemIds);
    }

    [Fact]
    public async Task A_failed_delete_puts_the_completed_count_back()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec());
        rig.StartEvent();
        rig.OpenGallery();
        rig.DeleteCycle(seeded[0].Id);
        rig.Store.FailMark = StorageErrorKind.Locked;

        rig.Operator("ConfirmDeleteGuestCycle");

        Assert.Equal(1, rig.OperatorView.RunningEvent!.CompletedGuestCycles);
    }

    [Fact]
    public async Task A_delete_in_an_Event_that_is_not_running_does_not_change_the_running_Events_count()
    {
        using var rig = await GalleryRig.CreateAsync(new GalleryRigOptions { OtherEvent = true });
        await rig.SeedAsync(new CycleSpec());
        var other = (await rig.SeedAsync(ActiveRig.OtherEventId, new CycleSpec()))[0];
        rig.StartEvent();

        rig.OpenGallery(ActiveRig.OtherEventId).ShouldBeAccepted();
        rig.DeleteCycle(other.Id);
        rig.Operator("ConfirmDeleteGuestCycle");

        Assert.False(rig.Gallery.Running);
        Assert.Equal(1, rig.OperatorView.RunningEvent!.CompletedGuestCycles);
        Assert.Empty(rig.Gallery.Items);
    }

    [Fact]
    public async Task Another_Events_gallery_opens_while_an_Event_runs_and_is_not_live()
    {
        using var rig = await GalleryRig.CreateAsync(new GalleryRigOptions { OtherEvent = true });
        var other = await rig.SeedAsync(ActiveRig.OtherEventId, new CycleSpec(), new CycleSpec());
        rig.StartEvent();

        rig.OpenGallery(ActiveRig.OtherEventId).ShouldBeAccepted();

        Assert.False(rig.Gallery.Running);
        Assert.Equal("Harbour gala", rig.Gallery.EventName);
        Assert.Equal(GalleryAssert.NewestFirst(other), rig.ItemIds);
        Assert.Equal(GalleryRig.EventId, rig.OperatorView.RunningEvent!.EventId);
    }

    [Fact]
    public async Task When_the_Event_ends_its_open_gallery_stays_and_stops_being_live()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedAsync(new CycleSpec());
        rig.StartEvent();
        rig.OpenGallery();
        Assert.True(rig.Gallery.Running);

        rig.Operator("EndEvent");
        rig.Operator("ConfirmEndEvent").ShouldBeAccepted();

        Assert.Equal(OperatorSurface.EventGallery, rig.OperatorView.Surface);
        Assert.False(rig.Gallery.Running);
        Assert.Null(rig.OperatorView.RunningEvent);
        Assert.Equal([seeded[0].Id], rig.ItemIds);
    }

    [Fact]
    public async Task Closing_the_gallery_returns_to_the_Event_list_and_the_diagnostics_follow_the_gallery()
    {
        using var rig = await GalleryRig.CreateAsync();
        var seeded = await rig.SeedManyAsync(45);
        Assert.Null(rig.Diagnostics().Gallery);

        rig.OpenGallery();
        var cursor = rig.Gallery.NextCursor;
        var open = rig.Diagnostics().Gallery!;
        rig.OpenCycle(GalleryAssert.NewestFirst(seeded)[0]);
        var detail = rig.Diagnostics().Gallery!;
        rig.Operator("CloseGuestCycle");
        rig.Operator("CloseGallery").ShouldBeAccepted();

        Assert.Equal(new OperatorGalleryDiagnostics(GalleryRig.EventId, 40, cursor, null), open.Operator);
        Assert.NotNull(cursor);
        Assert.Null(open.Guest);
        Assert.Equal(GalleryAssert.NewestFirst(seeded)[0], detail.Operator!.DetailId);
        Assert.Equal(OperatorSurface.EventList, rig.OperatorView.Surface);
        Assert.NotNull(rig.OperatorView.EventList);
        Assert.Null(rig.Diagnostics().Gallery);
    }

    [Fact]
    public async Task The_gallery_state_does_not_outlive_leaving_by_another_route()
    {
        using var rig = await GalleryRig.CreateAsync();
        await rig.SeedAsync(new CycleSpec());
        rig.OpenGallery();

        rig.Brain.ShowOperatorSurface(OperatorSurface.EventList);
        rig.Settle();

        Assert.Equal(OperatorSurface.EventList, rig.OperatorView.Surface);
        Assert.Null(rig.Diagnostics().Gallery);
        rig.Operator("CloseGallery").ShouldBeRejected();
        rig.OpenGallery().ShouldBeAccepted();
        Assert.Single(rig.Gallery.Items);
    }
}
