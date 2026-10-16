using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Core.Tests.GuestCycles.Orchestration;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core.Tests.Gallery;

/// <summary>Live updates (M9 contract section 8): a Guest Cycle that ends appears in the open galleries, and one that is deleted leaves them in the same turn.</summary>
public sealed class GalleryLiveTests
{
    private static readonly TimeSpan Fine = TimeSpan.FromMilliseconds(100);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static void ToKeepsake(GuestCycleRig rig)
    {
        rig.ToArrange();
        rig.ToFilters();
        rig.Guest("FinishStrip");
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake, TimeSpan.FromSeconds(10), Fine));
    }

    private static void OpenOperatorGallery(GuestCycleRig rig) => rig.Operator("OpenGallery", GalleryRig.Json("eventId", ActiveRig.EventId)).ShouldBeAccepted();

    private static void ReturnToStart(GuestCycleRig rig) =>
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Start, TimeSpan.FromSeconds(30)));

    [Fact]
    public async Task A_cycle_that_completes_appears_at_the_top_of_the_running_Events_open_gallery()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        OpenOperatorGallery(rig);
        Assert.True(rig.OperatorView.EventGallery!.Empty);

        ToKeepsake(rig);

        var gallery = rig.OperatorView.EventGallery!;
        var item = Assert.Single(gallery.Items);
        Assert.True(gallery.Running);
        Assert.False(gallery.Empty);
        Assert.Equal((GalleryOutcome.Completed, "Signature Strip", "Naturale"), (item.Outcome, item.TemplateName, item.LookName));
        Assert.NotNull(item.Strip);
        Assert.Empty(item.Captures);
        Assert.Equal(1, rig.OperatorView.RunningEvent!.CompletedGuestCycles);
    }

    [Fact]
    public async Task Two_completed_cycles_and_one_start_over_show_three_entries_newest_first()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        OpenOperatorGallery(rig);

        ToKeepsake(rig);
        ReturnToStart(rig);
        ToKeepsake(rig);
        ReturnToStart(rig);
        rig.ToArrange();
        rig.Guest("StartOver").ShouldBeAccepted();
        rig.Guest("ConfirmStartOver").ShouldBeAccepted();
        rig.Settle();

        var items = rig.OperatorView.EventGallery!.Items;
        Assert.Equal([GalleryOutcome.StartedOver, GalleryOutcome.Completed, GalleryOutcome.Completed], items.Select(item => item.Outcome));
        Assert.Null(items[0].Strip);
        Assert.Equal(4, items[0].Captures.Count);
        Assert.Equal(items.Select(item => item.GuestCycleId).Distinct().Count(), items.Count);
        Assert.Equal(2, rig.OperatorView.RunningEvent!.CompletedGuestCycles);
    }

    [Fact]
    public async Task An_unfinished_cycle_is_not_listed_until_it_ends()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        OpenOperatorGallery(rig);

        rig.Begin();
        rig.Operator("CloseGallery");
        OpenOperatorGallery(rig);

        Assert.Empty(rig.OperatorView.EventGallery!.Items);
        Assert.True(rig.OperatorView.EventGallery.Empty);
    }

    [Fact]
    public async Task A_cycle_ended_by_the_operator_appears_after_the_Event_has_ended_in_a_gallery_that_is_no_longer_live()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        OpenOperatorGallery(rig);
        rig.ToArrange();

        rig.Operator("EndEvent").ShouldBeAccepted();
        rig.Operator("ConfirmEndEvent").ShouldBeAccepted();
        rig.Settle();

        var gallery = rig.OperatorView.EventGallery!;
        Assert.False(gallery.Running);
        var item = Assert.Single(gallery.Items);
        Assert.Equal(GalleryOutcome.EndedByOperator, item.Outcome);
        Assert.Equal(4, item.Captures.Count);
    }

    [Fact]
    public async Task The_completed_cycle_reaches_the_guest_gallery_and_the_Start_link_when_the_guest_is_back_on_Start()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        Assert.False(rig.View.Start!.GuestGalleryLinkVisible);

        ToKeepsake(rig);
        ReturnToStart(rig);

        Assert.True(rig.View.Start!.GuestGalleryLinkVisible);
        rig.Guest("OpenGuestGallery").ShouldBeAccepted();
        var strip = Assert.Single(rig.View.GuestGallery!.Strips);
        Assert.Equal((600, 1800, false), (strip.Width, strip.Height, strip.Landscape));
        rig.Guest("OpenGuestStrip", GalleryRig.Json("guestCycleId", strip.GuestCycleId)).ShouldBeAccepted();
        Assert.Equal(4, rig.View.GuestStripDetail!.Captures.Count);
    }

    [Fact]
    public async Task A_guest_on_the_Keepsake_of_a_cycle_the_operator_deletes_returns_to_Start_in_the_same_turn()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        OpenOperatorGallery(rig);
        ToKeepsake(rig);
        var id = rig.OperatorView.EventGallery!.Items.Single().GuestCycleId;
        rig.Operator("DeleteGuestCycle", GalleryRig.Json("guestCycleId", id)).ShouldBeAccepted();

        rig.Active.OperatorCommand("ConfirmDeleteGuestCycle").ShouldBeAccepted();

        Assert.Equal(GuestSurface.Start, rig.View.Surface);
        Assert.False(rig.View.Start!.GuestGalleryLinkVisible);
        Assert.Empty(rig.OperatorView.EventGallery!.Items);
        Assert.Equal(0, rig.OperatorView.RunningEvent!.CompletedGuestCycles);
    }

    [Fact]
    public async Task A_guest_on_the_Keepsake_of_another_cycle_stays_when_a_different_cycle_is_deleted()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        await rig.Memory.StartGuestCycleAsync(ActiveRig.EventId, Ct);
        var older = await rig.Memory.StartGuestCycleAsync(ActiveRig.EventId, Ct);
        await rig.Memory.EndGuestCycleAsync(older.Id, GuestCycleEndReason.StartedOver, Ct);
        rig.Operator("OpenGallery", GalleryRig.Json("eventId", ActiveRig.EventId)).ShouldBeAccepted();
        ToKeepsake(rig);
        rig.Operator("DeleteGuestCycle", GalleryRig.Json("guestCycleId", older.Id)).ShouldBeAccepted();

        rig.Operator("ConfirmDeleteGuestCycle").ShouldBeAccepted();

        Assert.Equal(GuestSurface.Keepsake, rig.View.Surface);
        Assert.Single(rig.OperatorView.EventGallery!.Items);
    }

    [Fact]
    public async Task A_cycle_that_ends_while_another_Events_gallery_is_open_changes_nothing_there()
    {
        var rig = await GalleryRig.CreateAsync(new GalleryRigOptions { OtherEvent = true });
        using var lifetime = rig;
        await rig.SeedAsync(ActiveRig.OtherEventId, new CycleSpec());
        rig.StartEvent();
        rig.OpenGallery(ActiveRig.OtherEventId);
        var fresh = (await rig.SeedAsync(new CycleSpec()))[0];

        End(rig, fresh.Id, GuestCycleEndReason.Completed);

        Assert.Single(rig.Gallery.Items);
        Assert.DoesNotContain(fresh.Id, rig.ItemIds);
    }

    private static void End(GalleryRig rig, string id, GuestCycleEndReason reason)
    {
        rig.Active.Executor.Post(() => rig.Brain.OnGuestCycleEnded(id, reason));
        rig.Settle();
    }

    [Fact]
    public async Task A_completed_cycle_reaches_both_open_galleries_without_resetting_the_guests_timer()
    {
        var rig = await GalleryRig.CreateAsync();
        using var lifetime = rig;
        await rig.SeedAsync(new CycleSpec());
        rig.StartEvent();
        rig.OpenGuestGallery();
        rig.OpenGallery();
        rig.Advance(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        var fresh = (await rig.SeedAsync(new CycleSpec()))[0];

        End(rig, fresh.Id, GuestCycleEndReason.Completed);
        End(rig, fresh.Id, GuestCycleEndReason.Completed);

        Assert.Equal(fresh.Id, rig.ItemIds[0]);
        Assert.Equal(fresh.Id, rig.StripIds[0]);
        Assert.Equal((2, 2), (rig.ItemIds.Length, rig.StripIds.Length));
        Assert.Equal(50_000, rig.Diagnostics().Gallery!.Guest!.Timer.RemainingMs);
    }

    [Fact]
    public async Task A_cycle_that_did_not_complete_reaches_the_operator_list_only()
    {
        var rig = await GalleryRig.CreateAsync();
        using var lifetime = rig;
        await rig.SeedAsync(new CycleSpec());
        rig.StartEvent();
        rig.OpenGuestGallery();
        rig.OpenGallery();
        var abandoned = (await rig.SeedAsync(new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 2)))[0];

        End(rig, abandoned.Id, GuestCycleEndReason.StartedOver);

        Assert.Equal(abandoned.Id, rig.ItemIds[0]);
        Assert.Equal(GalleryOutcome.StartedOver, rig.Gallery.Items[0].Outcome);
        Assert.Single(rig.StripIds);
    }

    [Fact]
    public async Task A_completed_cycles_strip_thumbnail_is_made_when_it_ends_even_with_no_gallery_open()
    {
        var rig = await GalleryRig.CreateAsync();
        using var lifetime = rig;
        var seeded = await rig.SeedAsync(new CycleSpec(), new CycleSpec(GuestCycleEndReason.OperatorExit, Captures: 2));
        rig.StartEvent();

        End(rig, seeded[1].Id, GuestCycleEndReason.OperatorExit);
        Assert.Empty(rig.Store.ThumbnailRequests);
        End(rig, seeded[0].Id, GuestCycleEndReason.Completed);

        Assert.Equal([(seeded[0].Id, 400)], rig.Store.ThumbnailRequests);
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
    }

    [Fact]
    public async Task A_cycle_that_ended_before_the_first_page_arrived_is_not_listed_twice()
    {
        var rig = await GalleryRig.CreateAsync();
        using var lifetime = rig;
        await rig.SeedAsync(new CycleSpec());
        rig.StartEvent();
        var stale = rig.Store.GateNextListResult();
        rig.OpenGallery();
        var fresh = (await rig.SeedAsync(new CycleSpec()))[0];

        End(rig, fresh.Id, GuestCycleEndReason.Completed);
        Assert.Equal([fresh.Id], rig.ItemIds);
        stale.SetResult();
        rig.Settle();

        Assert.Equal(2, rig.ItemIds.Length);
        Assert.Equal(fresh.Id, rig.ItemIds[0]);
        Assert.Equal(2, rig.ItemIds.Distinct().Count());
    }

    [Fact]
    public async Task An_ended_cycle_that_can_not_be_read_or_is_gone_is_skipped_and_logged()
    {
        var rig = await GalleryRig.CreateAsync();
        using var lifetime = rig;
        var seeded = await rig.SeedAsync(new CycleSpec());
        rig.StartEvent();
        rig.OpenGallery();
        var gone = (await rig.SeedAsync(new CycleSpec()))[0];
        var failing = (await rig.SeedAsync(new CycleSpec()))[0];
        await rig.Memory.MarkGuestCycleDeletingAsync(gone.Id, Ct);

        End(rig, gone.Id, GuestCycleEndReason.Completed);
        rig.Store.FailGets = 1;
        End(rig, failing.Id, GuestCycleEndReason.Completed);

        Assert.Equal([seeded[0].Id], rig.ItemIds);
        Assert.Contains(rig.Active.Logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task A_cycle_that_ended_while_the_gallery_was_closing_is_ignored()
    {
        var rig = await GalleryRig.CreateAsync();
        using var lifetime = rig;
        await rig.SeedAsync(new CycleSpec());
        rig.StartEvent();
        rig.OpenGuestGallery();
        rig.OpenGallery();
        var fresh = (await rig.SeedAsync(new CycleSpec()))[0];
        var gate = rig.Store.GateNextGet();
        End(rig, fresh.Id, GuestCycleEndReason.Completed);

        rig.Operator("CloseGallery");
        rig.Guest("BackToStart");
        gate.SetResult();
        rig.Settle();

        Assert.Equal(OperatorSurface.EventList, rig.OperatorView.Surface);
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        rig.OpenGuestGallery();
        Assert.Equal(2, rig.StripIds.Length);
    }
}
