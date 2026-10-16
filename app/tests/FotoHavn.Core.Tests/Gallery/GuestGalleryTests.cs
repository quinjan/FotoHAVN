using FotoHavn.Core.Events;
using FotoHavn.Core.Gallery;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Core.Tests.GuestCycles.Orchestration;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core.Tests.Gallery;

/// <summary>The guest gallery and its strip detail (M9 contract section 4.2): the Start link, what a guest sees, paging, deletions, and where the guest goes when the gallery ends.</summary>
public sealed class GuestGalleryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(GalleryRig Rig, List<SeededCycle> Seeded)> StartedAsync(int completed = 3, GalleryRigOptions? options = null, params CycleSpec[] others)
    {
        var rig = await GalleryRig.CreateAsync(options ?? new GalleryRigOptions());
        var seeded = await rig.SeedAsync([.. GallerySeed.Many(completed), .. others]);
        rig.StartEvent();
        return (rig, seeded);
    }

    private static string[] CompletedNewestFirst(IEnumerable<SeededCycle> seeded) => GalleryAssert.NewestFirst(seeded.Where(cycle => cycle.Strip is not null));

    private static void FailStorage(GalleryRig rig)
    {
        rig.Active.Executor.Post(() => rig.Brain.SetActiveStorageFailed(true));
        rig.Brain.ShowOperatorSurface(OperatorSurface.EventList);
        rig.Settle();
    }

    [Fact]
    public async Task The_Start_link_shows_when_the_Event_enables_the_gallery_and_a_completed_Photo_Strip_exists()
    {
        var (rig, _) = await StartedAsync();
        using var _ = rig;

        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        Assert.True(rig.GuestView.Start!.GuestGalleryLinkVisible);
        Assert.True(rig.GuestView.Start.StepInsideAvailable);
    }

    [Fact]
    public async Task The_Start_link_is_hidden_without_a_completed_strip_and_for_cycles_that_did_not_complete()
    {
        var (rig, _) = await StartedAsync(0, null, new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 2), new CycleSpec(GuestCycleEndReason.OperatorExit, Captures: 1));
        using var _ = rig;

        Assert.False(rig.GuestView.Start!.GuestGalleryLinkVisible);
        rig.OpenGuestGallery().ShouldBeRejected();
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        Assert.Null(rig.Diagnostics().Gallery);
    }

    [Fact]
    public async Task The_Start_link_is_hidden_and_the_gallery_refused_when_the_Event_does_not_enable_it()
    {
        var (rig, _) = await StartedAsync(2, new GalleryRigOptions { Tweak = record => record with { GuestGalleryEnabled = false } });
        using var _ = rig;

        Assert.False(rig.GuestView.Start!.GuestGalleryLinkVisible);
        rig.OpenGuestGallery().ShouldBeRejected();
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
    }

    [Fact]
    public async Task The_Start_link_counts_only_the_Active_Events_strips()
    {
        var rig = await GalleryRig.CreateAsync(new GalleryRigOptions { OtherEvent = true });
        using var _ = rig;
        await rig.SeedAsync(ActiveRig.OtherEventId, new CycleSpec());
        rig.StartEvent();

        Assert.False(rig.GuestView.Start!.GuestGalleryLinkVisible);
    }

    [Fact]
    public async Task The_gallery_does_not_need_the_camera_so_the_link_stays_on_Start_unavailable_for_the_camera()
    {
        var (rig, _) = await StartedAsync();
        using var _ = rig;
        rig.Active.Camera.Fail(CameraFailureKind.Failed);
        rig.Settle();

        Assert.Equal(GuestSurface.StartUnavailable, rig.GuestView.Surface);
        Assert.Equal(StartUnavailableReason.Camera, rig.GuestView.StartUnavailable!.Reason);
        Assert.True(rig.GuestView.StartUnavailable.GuestGalleryLinkVisible);
        rig.OpenGuestGallery().ShouldBeAccepted();
        Assert.Equal(GuestSurface.GuestGallery, rig.GuestView.Surface);
    }

    [Fact]
    public async Task The_link_is_hidden_on_Start_unavailable_for_storage_because_the_gallery_could_not_open()
    {
        var (rig, _) = await StartedAsync();
        using var _ = rig;

        FailStorage(rig);

        Assert.Equal(GuestSurface.StartUnavailable, rig.GuestView.Surface);
        Assert.Equal(StartUnavailableReason.Storage, rig.GuestView.StartUnavailable!.Reason);
        Assert.False(rig.GuestView.StartUnavailable.GuestGalleryLinkVisible);
        rig.OpenGuestGallery().ShouldBeRejected();
        Assert.Equal(GuestSurface.StartUnavailable, rig.GuestView.Surface);
    }

    [Fact]
    public async Task The_link_is_hidden_when_the_camera_and_storage_are_both_unavailable()
    {
        var (rig, _) = await StartedAsync();
        using var _ = rig;
        rig.Active.Camera.Fail(CameraFailureKind.Failed);
        FailStorage(rig);

        Assert.Equal(GuestSurface.StartUnavailable, rig.GuestView.Surface);
        Assert.False(rig.GuestView.StartUnavailable!.GuestGalleryLinkVisible);
        rig.OpenGuestGallery().ShouldBeRejected();
    }

    [Fact]
    public async Task A_stale_STEP_INSIDE_while_the_gallery_is_open_is_not_available_and_leaves_the_gallery_alone()
    {
        var (rig, _) = await StartedAsync(2);
        using var _ = rig;
        rig.OpenGuestGallery().ShouldBeAccepted();
        var before = rig.Diagnostics().Gallery;

        rig.Guest("BeginGuestCycle").ShouldBeRejected();

        Assert.Equal(GuestSurface.GuestGallery, rig.GuestView.Surface);
        Assert.Equal(RunningEventStep.GuestGallery, rig.OperatorView.RunningEvent!.CurrentStep);
        Assert.Equal(before, rig.Diagnostics().Gallery);
        Assert.Null(rig.Diagnostics().GuestCycle);

        rig.Guest("BackToStart").ShouldBeAccepted();
        rig.Guest("BeginGuestCycle").ShouldBeAccepted();
    }

    [Fact]
    public async Task Opening_shows_the_completed_strips_newest_first_with_only_an_image_its_proportion_and_alt_text()
    {
        var (rig, seeded) = await StartedAsync(3, null, new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 2), new CycleSpec(GuestCycleEndReason.OperatorExit), new CycleSpec(null), new CycleSpec(StripWidth: 1800, StripHeight: 600));
        using var _ = rig;

        rig.OpenGuestGallery().ShouldBeAccepted();

        var view = rig.GuestView;
        Assert.Equal(GuestSurface.GuestGallery, view.Surface);
        Assert.Null(view.Start);
        Assert.Null(view.StartUnavailable);
        Assert.Null(view.GuestStripDetail);
        Assert.Equal(CompletedNewestFirst(seeded), rig.StripIds);
        Assert.False(rig.Strips.LoadingMore);
        Assert.Null(rig.Strips.NextCursor);
        Assert.All(rig.Strips.Strips, strip => Assert.Equal(GalleryCopy.StripAlt, strip.Alt));
        var landscape = rig.Strips.Strips[0];
        Assert.Equal((1800, 600, true), (landscape.Width, landscape.Height, landscape.Landscape));
        Assert.StartsWith("https://media.fotohavn/thumbnails/", landscape.ThumbnailUrl, StringComparison.Ordinal);
        var call = Assert.Single(rig.Store.ListCalls, call => call.Filter == GuestCycleFilter.CompletedOnly);
        Assert.Equal(GalleryRig.EventId, call.EventId);
    }

    [Fact]
    public async Task Another_Events_strips_never_show()
    {
        var rig = await GalleryRig.CreateAsync(new GalleryRigOptions { OtherEvent = true });
        using var _ = rig;
        var mine = await rig.SeedAsync(new CycleSpec());
        await rig.SeedAsync(ActiveRig.OtherEventId, new CycleSpec(), new CycleSpec());
        rig.StartEvent();

        rig.OpenGuestGallery();

        Assert.Equal([mine[0].Id], rig.StripIds);
    }

    [Fact]
    public async Task The_first_page_waits_for_its_thumbnails_while_the_surface_shows_loading()
    {
        var (rig, seeded) = await StartedAsync(3);
        using var _ = rig;
        var gate = rig.Store.GateNextList();

        rig.OpenGuestGallery().ShouldBeAccepted();
        var loading = rig.Strips;
        var requestedBefore = rig.Store.ThumbnailRequests.Count;
        gate.SetResult();
        rig.Settle();

        Assert.Empty(loading.Strips);
        Assert.True(loading.LoadingMore);
        Assert.Null(loading.NextCursor);
        Assert.Equal(0, requestedBefore);
        Assert.Equal(CompletedNewestFirst(seeded), rig.StripIds);
        Assert.False(rig.Strips.LoadingMore);
        Assert.True(rig.Store.ThumbnailRequests.Count >= 3);
    }

    [Fact]
    public async Task A_strip_whose_file_is_gone_is_left_out_and_when_none_is_left_the_guest_goes_back_to_Start()
    {
        var (rig, seeded) = await StartedAsync(2);
        using var _ = rig;
        rig.Store.MissingThumbnails.Add(seeded[0].Id);

        rig.OpenGuestGallery();
        Assert.Equal([seeded[1].Id], rig.StripIds);
        rig.Guest("BackToStart");
        rig.Store.MissingThumbnails.Add(seeded[1].Id);
        rig.OpenGuestGallery().ShouldBeAccepted();

        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        Assert.Null(rig.Diagnostics().Gallery);
    }

    [Fact]
    public async Task A_first_page_that_can_not_be_read_sends_the_guest_back_to_Start()
    {
        var (rig, _) = await StartedAsync(2);
        using var _ = rig;
        rig.Store.FailLists = 1;

        rig.OpenGuestGallery().ShouldBeAccepted();

        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        Assert.Null(rig.Diagnostics().Gallery);
        Assert.Contains(rig.Active.Logger.Entries, entry => entry.Level == LogLevel.Warning);
        rig.OpenGuestGallery().ShouldBeAccepted();
        Assert.Equal(GuestSurface.GuestGallery, rig.GuestView.Surface);
    }

    [Fact]
    public async Task A_later_page_that_can_not_be_read_ends_the_row()
    {
        var (rig, _) = await StartedAsync(45);
        using var _ = rig;
        rig.OpenGuestGallery();
        var cursor = rig.Strips.NextCursor!;
        rig.Store.FailLists = 1;

        rig.LoadMoreStrips(cursor).ShouldBeAccepted();

        Assert.Equal(GuestSurface.GuestGallery, rig.GuestView.Surface);
        Assert.Equal(40, rig.Strips.Strips.Count);
        Assert.Null(rig.Strips.NextCursor);
        Assert.False(rig.Strips.LoadingMore);
        rig.LoadMoreStrips(cursor).ShouldBeRejected();
    }

    [Fact]
    public async Task Strips_page_forty_at_a_time_and_a_repeated_wrong_or_early_cursor_is_not_available()
    {
        var (rig, seeded) = await StartedAsync(85);
        using var _ = rig;
        rig.OpenGuestGallery();
        var first = rig.Strips.NextCursor!;
        Assert.Equal(40, rig.Strips.Strips.Count);

        rig.LoadMoreStrips("wrong").ShouldBeRejected();
        var gate = rig.Store.GateNextList();
        rig.LoadMoreStrips(first).ShouldBeAccepted();
        Assert.True(rig.Strips.LoadingMore);
        rig.LoadMoreStrips(first).ShouldBeRejected();
        gate.SetResult();
        rig.Settle();
        rig.LoadMoreStrips(first).ShouldBeRejected();
        var second = rig.Strips.NextCursor!;
        rig.LoadMoreStrips(second).ShouldBeAccepted();

        Assert.Equal(CompletedNewestFirst(seeded), rig.StripIds);
        Assert.Null(rig.Strips.NextCursor);
        Assert.Equal<string?>([null, first, second], rig.Store.ListCalls.Select(call => call.Cursor));
    }

    [Fact]
    public async Task The_gallery_opens_only_from_Start_and_only_once()
    {
        var (rig, _) = await StartedAsync();
        using var _ = rig;
        rig.OpenGuestGallery().ShouldBeAccepted();
        rig.OpenGuestGallery().ShouldBeRejected();
        rig.Guest("BackToStart");

        rig.Active.GuestCommand("BeginExitHold").ShouldBeAccepted();
        rig.Active.Advance(TimeSpan.FromSeconds(3));
        rig.Active.GuestCommand("CompleteExitHold").ShouldBeAccepted();
        rig.Settle();
        rig.OpenGuestGallery().ShouldBeRejected();
        rig.Guest("CancelExit");
        rig.Guest("BeginGuestCycle").ShouldBeAccepted();

        rig.OpenGuestGallery().ShouldBeRejected();
        Assert.Null(rig.Diagnostics().Gallery);
    }

    [Fact]
    public async Task Gallery_commands_with_no_gallery_open_are_not_available()
    {
        var (rig, _) = await StartedAsync();
        using var _ = rig;

        rig.Guest("LoadMoreGuestStrips", """{"cursor":"c"}""").ShouldBeRejected();
        rig.Guest("OpenGuestStrip", """{"guestCycleId":"00000000-0000-0000-0000-000000000001"}""").ShouldBeRejected();
        rig.Guest("CloseGuestStrip").ShouldBeRejected();
        rig.Guest("BackToStart").ShouldBeRejected();
        rig.Guest("MoreTime").ShouldBeRejected();
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
    }

    [Fact]
    public async Task A_strip_opens_large_with_its_arranged_Captures_only_and_closing_it_returns_to_the_row()
    {
        var (rig, seeded) = await StartedAsync(2, null);
        using var _ = rig;
        var cycle = seeded[1];
        rig.OpenGuestGallery();
        var listCalls = rig.Store.ListCalls.Count;

        rig.OpenStrip(cycle.Id).ShouldBeAccepted();

        var view = rig.GuestView;
        Assert.Equal(GuestSurface.GuestStripDetail, view.Surface);
        Assert.Null(view.GuestGallery);
        var detail = view.GuestStripDetail!;
        Assert.Equal($"https://media.fotohavn/events/{GalleryRig.EventId}/guest-cycles/{cycle.Id}/photo-strip.png?v={cycle.Strip!.Sha256[..8]}", detail.Strip.ImageUrl);
        Assert.Equal((600, 1800, false, GalleryCopy.StripAlt), (detail.Strip.Width, detail.Strip.Height, detail.Strip.Landscape, detail.Strip.Alt));
        Assert.Equal(cycle.Kept.AsEnumerable().Reverse().Select(capture => $"capture-{capture.Sequence}.jpg?v={capture.Sha256[..8]}"), detail.Captures.Select(capture => capture.ImageUrl.Split('/')[^1]));
        Assert.All(detail.Captures, capture => Assert.Equal(GalleryCopy.CaptureAlt, capture.Alt));
        rig.OpenStrip(seeded[0].Id).ShouldBeRejected();
        rig.LoadMoreStrips("c").ShouldBeRejected();

        rig.Guest("CloseGuestStrip").ShouldBeAccepted();

        Assert.Equal(GuestSurface.GuestGallery, rig.GuestView.Surface);
        Assert.Equal(CompletedNewestFirst(seeded), rig.StripIds);
        Assert.Equal(listCalls, rig.Store.ListCalls.Count);
        rig.Guest("CloseGuestStrip").ShouldBeRejected();
    }

    [Fact]
    public async Task A_discarded_Capture_never_reaches_the_strip_detail()
    {
        var rig = await GalleryRig.CreateAsync();
        using var _ = rig;
        var cycle = (await rig.SeedAsync(new CycleSpec(Discarded: 2)))[0];
        rig.StartEvent();
        rig.OpenGuestGallery();

        rig.OpenStrip(cycle.Id);

        Assert.Equal(4, rig.GuestView.GuestStripDetail!.Captures.Count);
        Assert.DoesNotContain(rig.GuestView.GuestStripDetail.Captures, capture => capture.ImageUrl.Contains("capture-5", StringComparison.Ordinal) || capture.ImageUrl.Contains("capture-6", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_strip_that_is_not_in_the_row_or_not_completed_can_not_be_opened()
    {
        var (rig, _) = await StartedAsync(1, null, new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 2));
        using var _ = rig;
        rig.OpenGuestGallery();
        var notCompleted = (await rig.Memory.ListGuestCyclesAsync(GalleryRig.EventId, GuestCycleFilter.All, null, 10, Ct)).Items.Single(row => row.PhotoStrip is null).Cycle.Id;

        rig.OpenStrip(notCompleted).ShouldBeRejected();
        rig.OpenStrip("00000000-0000-0000-0000-0000000000ff").ShouldBeRejected();

        Assert.Equal(GuestSurface.GuestGallery, rig.GuestView.Surface);
    }

    [Fact]
    public async Task A_strip_that_can_not_be_read_leaves_the_guest_on_the_row()
    {
        var (rig, seeded) = await StartedAsync(2);
        using var _ = rig;
        rig.OpenGuestGallery();
        rig.Store.FailGets = 1;

        rig.OpenStrip(seeded[0].Id).ShouldBeAccepted();

        Assert.Equal(GuestSurface.GuestGallery, rig.GuestView.Surface);
        Assert.Equal(2, rig.Strips.Strips.Count);
        rig.OpenStrip(seeded[0].Id).ShouldBeAccepted();
        Assert.Equal(GuestSurface.GuestStripDetail, rig.GuestView.Surface);
    }

    [Fact]
    public async Task A_strip_that_vanished_leaves_the_row_and_the_last_one_leaves_the_gallery()
    {
        var (rig, seeded) = await StartedAsync(2);
        using var _ = rig;
        rig.OpenGuestGallery();
        await rig.Memory.MarkGuestCycleDeletingAsync(seeded[0].Id, Ct);

        rig.OpenStrip(seeded[0].Id).ShouldBeAccepted();

        Assert.Equal([seeded[1].Id], rig.StripIds);
        await rig.Memory.MarkGuestCycleDeletingAsync(seeded[1].Id, Ct);
        rig.OpenStrip(seeded[1].Id).ShouldBeAccepted();
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
    }

    [Fact]
    public async Task A_strip_that_arrives_after_the_guest_went_back_is_ignored()
    {
        var (rig, seeded) = await StartedAsync(2);
        using var _ = rig;
        rig.OpenGuestGallery();
        var gate = rig.Store.GateNextGet();
        rig.OpenStrip(seeded[0].Id).ShouldBeAccepted();

        rig.Guest("BackToStart").ShouldBeAccepted();
        gate.SetResult();
        rig.Settle();

        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        Assert.Null(rig.GuestView.GuestStripDetail);
    }

    [Fact]
    public async Task Back_to_Start_leaves_the_row_or_the_detail_and_the_next_guest_starts_from_Start()
    {
        var (rig, seeded) = await StartedAsync(2);
        using var _ = rig;
        rig.OpenGuestGallery();

        rig.Guest("BackToStart").ShouldBeAccepted();
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        Assert.True(rig.GuestView.Start!.GuestGalleryLinkVisible);
        Assert.Null(rig.Diagnostics().Gallery);

        rig.OpenGuestGallery();
        rig.OpenStrip(seeded[0].Id);
        rig.Guest("BackToStart").ShouldBeAccepted();

        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        rig.OpenGuestGallery().ShouldBeAccepted();
        Assert.Equal(GuestSurface.GuestGallery, rig.GuestView.Surface);
    }

    [Fact]
    public async Task The_panel_names_the_guest_gallery_while_a_guest_browses_and_Operator_Assistance_wins()
    {
        var (rig, seeded) = await StartedAsync(2);
        using var _ = rig;
        Assert.Equal(RunningEventStep.Start, rig.OperatorView.RunningEvent!.CurrentStep);

        rig.OpenGuestGallery();
        Assert.Equal(RunningEventStep.GuestGallery, rig.OperatorView.RunningEvent!.CurrentStep);
        rig.OpenStrip(seeded[0].Id);
        Assert.Equal(RunningEventStep.GuestGallery, rig.OperatorView.RunningEvent!.CurrentStep);
        rig.Guest("CloseGuestStrip");
        rig.Guest("BackToStart");
        Assert.Equal(RunningEventStep.Start, rig.OperatorView.RunningEvent!.CurrentStep);

        rig.OpenGuestGallery();
        rig.Active.Displays.Disconnect("fake-guest");
        rig.Settle();

        Assert.Equal(RunningEventStep.OperatorAssistance, rig.OperatorView.RunningEvent!.CurrentStep);
    }

    [Fact]
    public async Task Operator_Assistance_drops_the_gallery_and_its_timer()
    {
        var (rig, _) = await StartedAsync(2);
        using var _ = rig;
        rig.OpenGuestGallery();

        rig.Active.Displays.Disconnect("fake-guest");
        rig.Settle();

        Assert.Equal(GuestSurface.OperatorAssistance, rig.GuestView.Surface);
        Assert.Null(rig.Diagnostics().Gallery);
        rig.Advance(TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(1));
        Assert.Equal(GuestSurface.OperatorAssistance, rig.GuestView.Surface);
        rig.Guest("BackToStart").ShouldBeRejected();
    }

    [Fact]
    public async Task Ending_the_Event_drops_the_gallery_and_a_new_Event_run_starts_from_Start()
    {
        var (rig, _) = await StartedAsync(2);
        using var _ = rig;
        rig.OpenGuestGallery();

        rig.Operator("EndEvent");
        rig.Operator("ConfirmEndEvent").ShouldBeAccepted();

        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        Assert.Null(rig.Diagnostics().Gallery);

        rig.StartEvent();

        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        Assert.True(rig.GuestView.Start!.GuestGalleryLinkVisible);
    }

    [Fact]
    public async Task The_exit_hold_and_the_PIN_dialog_work_over_the_gallery_and_the_PIN_ends_the_Event()
    {
        var (rig, seeded) = await StartedAsync(2);
        using var _ = rig;
        rig.OpenGuestGallery();

        rig.Active.GuestCommand("BeginExitHold").ShouldBeAccepted();
        Assert.True(rig.GuestView.Overlays.ExitHold!.Active);
        Assert.Equal(GuestSurface.GuestGallery, rig.GuestView.Surface);
        rig.Active.Advance(TimeSpan.FromSeconds(3));
        rig.Active.GuestCommand("CompleteExitHold").ShouldBeAccepted();
        rig.Settle();
        Assert.NotNull(rig.GuestView.Overlays.PinDialog);
        Assert.Equal(GuestSurface.GuestGallery, rig.GuestView.Surface);
        rig.Guest("CancelExit");

        rig.OpenStrip(seeded[0].Id);
        rig.Active.HoldToExit();
        rig.Settle();
        Assert.Equal(GuestSurface.GuestStripDetail, rig.GuestView.Surface);
        rig.Active.SubmitPin(ActiveRig.Pin).ShouldBeAccepted();
        rig.Settle();

        Assert.Null(rig.Diagnostics().Gallery);
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        Assert.Null(rig.OperatorView.RunningEvent);
    }

    [Fact]
    public async Task Deleting_a_strip_removes_it_from_the_row_in_the_same_turn_and_a_guest_on_its_detail_returns_to_the_row()
    {
        var (rig, seeded) = await StartedAsync(3);
        using var _ = rig;
        rig.OpenGuestGallery();
        rig.OpenStrip(seeded[1].Id);
        rig.OpenGallery();
        rig.DeleteCycle(seeded[0].Id);
        rig.Operator("ConfirmDeleteGuestCycle");

        Assert.Equal(GuestSurface.GuestStripDetail, rig.GuestView.Surface);
        rig.DeleteCycle(seeded[1].Id);
        rig.Store.GateNextMark();
        rig.Active.OperatorCommand("ConfirmDeleteGuestCycle").ShouldBeAccepted();

        Assert.Equal(GuestSurface.GuestGallery, rig.GuestView.Surface);
        Assert.Equal([seeded[2].Id], rig.StripIds);
        Assert.Null(rig.GuestView.GuestStripDetail);
        Assert.Equal(RunningEventStep.GuestGallery, rig.OperatorView.RunningEvent!.CurrentStep);
    }

    [Fact]
    public async Task Deleting_the_last_completed_strip_returns_the_guest_to_Start_in_the_same_turn()
    {
        var (rig, seeded) = await StartedAsync(1, null, new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 2));
        using var _ = rig;
        rig.OpenGuestGallery();
        rig.OpenGallery();
        rig.DeleteCycle(seeded[0].Id);
        rig.Store.GateNextMark();

        rig.Active.OperatorCommand("ConfirmDeleteGuestCycle").ShouldBeAccepted();

        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        Assert.False(rig.GuestView.Start!.GuestGalleryLinkVisible);
        Assert.Null(rig.Diagnostics().Gallery?.Guest);
    }

    [Fact]
    public async Task Deleting_the_last_strip_from_its_detail_returns_the_guest_to_Start()
    {
        var (rig, seeded) = await StartedAsync(1);
        using var _ = rig;
        rig.OpenGuestGallery();
        rig.OpenStrip(seeded[0].Id);
        rig.OpenGallery();
        rig.DeleteCycle(seeded[0].Id);

        rig.Operator("ConfirmDeleteGuestCycle");

        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        Assert.Null(rig.GuestView.GuestStripDetail);
    }

    [Fact]
    public async Task A_page_read_before_a_delete_can_not_bring_the_strip_back()
    {
        var (rig, seeded) = await StartedAsync(45);
        using var _ = rig;
        rig.OpenGuestGallery();
        var cursor = rig.Strips.NextCursor!;
        var oldest = GalleryAssert.NewestFirst(seeded)[^1];
        rig.OpenGallery();
        var operatorCursor = rig.Gallery.NextCursor!;
        var stale = rig.Store.GateNextListResult();
        rig.LoadMoreStrips(cursor).ShouldBeAccepted();
        rig.LoadMore(operatorCursor).ShouldBeAccepted();
        rig.DeleteCycle(oldest);
        rig.Operator("ConfirmDeleteGuestCycle");

        stale.SetResult();
        rig.Settle();

        Assert.DoesNotContain(oldest, rig.StripIds);
        Assert.Equal(44, rig.Strips.Strips.Count);
        Assert.DoesNotContain(oldest, rig.ItemIds.Concat(rig.StripIds));
    }

    [Fact]
    public async Task A_failed_delete_brings_the_strip_back_to_the_row()
    {
        var (rig, seeded) = await StartedAsync(3);
        using var _ = rig;
        rig.OpenGuestGallery();
        rig.OpenGallery();
        rig.DeleteCycle(seeded[1].Id);
        rig.Store.FailMark = StorageErrorKind.Locked;

        rig.Operator("ConfirmDeleteGuestCycle");

        Assert.Equal(CompletedNewestFirst(seeded), rig.StripIds);
        Assert.Equal(GuestSurface.GuestGallery, rig.GuestView.Surface);
        Assert.True(rig.GuestView.Overlays is { PhaseTimerWarning: null });
    }

    [Fact]
    public async Task A_delete_in_another_Events_gallery_leaves_the_guest_row_alone()
    {
        var rig = await GalleryRig.CreateAsync(new GalleryRigOptions { OtherEvent = true });
        using var _ = rig;
        var mine = await rig.SeedAsync(new CycleSpec());
        var other = await rig.SeedAsync(ActiveRig.OtherEventId, new CycleSpec());
        rig.StartEvent();
        rig.OpenGuestGallery();
        rig.OpenGallery(ActiveRig.OtherEventId);
        rig.DeleteCycle(other[0].Id);

        rig.Operator("ConfirmDeleteGuestCycle");

        Assert.Equal([mine[0].Id], rig.StripIds);
        Assert.Equal(GuestSurface.GuestGallery, rig.GuestView.Surface);
    }

    [Fact]
    public async Task The_gallery_diagnostics_name_the_row_the_cursor_and_the_open_strip()
    {
        var (rig, seeded) = await StartedAsync(45);
        using var _ = rig;

        rig.OpenGuestGallery();
        var row = rig.Diagnostics().Gallery!;
        var cursor = rig.Strips.NextCursor;
        rig.OpenStrip(seeded[44].Id);
        var detail = rig.Diagnostics().Gallery!.Guest!;

        Assert.Null(row.Operator);
        Assert.Equal((40, cursor, (string?)null), (row.Guest!.StripCount, row.Guest.NextCursor, row.Guest.DetailId));
        Assert.NotNull(cursor);
        Assert.Equal(seeded[44].Id, detail.DetailId);
        Assert.Equal(PhaseTimer.GuestGallery, detail.Timer.Phase);
    }
}
