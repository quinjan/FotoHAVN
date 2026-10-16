using FotoHavn.Catalog;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Gallery;
using FotoHavn.Core.Gallery.Orchestration;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Gallery;

/// <summary>What the galleries show of the store: pages, thumbnails asked for before an item exists, and what a missing or failing file does to them (M9 contract section 7).</summary>
public sealed class GalleryPageBuilderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Fixture
    {
        public FakeClock Clock { get; } = new(GallerySeed.Start);

        public InMemoryEventStore Memory { get; }

        public GalleryStore Store { get; }

        public GalleryPageBuilder Builder { get; }

        public Fixture()
        {
            Memory = new InMemoryEventStore(Clock);
            Store = new GalleryStore(Memory);
            Builder = new GalleryPageBuilder(Store, new CatalogData());
        }

        public static async Task<Fixture> OpenAsync()
        {
            var fixture = new Fixture();
            await fixture.Memory.OpenAsync(Ct);
            await fixture.Memory.CreateEventAsync(ActiveRigEvent(), Ct);
            return fixture;
        }

        public Task<List<SeededCycle>> SeedAsync(params CycleSpec[] specs) => GallerySeed.AddAsync(Memory, Clock, GalleryRig.EventId, specs);

        public async Task<string> UrlAsync(string sourceId, int width) => GalleryUrls.ThumbnailUrlFromStorePath(await Memory.GetThumbnailPathAsync(sourceId, width, Ct));
    }

    private static EventRecord ActiveRigEvent() => Events.Active.ActiveRig.SampleEvent();

    [Fact]
    public async Task The_operator_page_lists_ended_cycles_newest_first_and_leaves_an_open_one_out()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = await fixture.SeedAsync(
            new CycleSpec(),
            new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 2),
            new CycleSpec(null, Captures: 1),
            new CycleSpec(GuestCycleEndReason.OperatorExit, Captures: 3));

        var page = await fixture.Builder.OperatorPageAsync(GalleryRig.EventId, null, Ct);

        Assert.Equal([seeded[3].Id, seeded[1].Id, seeded[0].Id], page.Entries.Select(entry => entry.GuestCycleId));
        Assert.Equal([GalleryOutcome.EndedByOperator, GalleryOutcome.StartedOver, GalleryOutcome.Completed], page.Entries.Select(entry => entry.View.Outcome));
        Assert.All(page.Entries, entry => Assert.Equal(entry.View.StartedAt, entry.StartedAt));
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task A_Photo_Strip_item_carries_its_thumbnail_proportion_and_the_names_of_its_Template_and_Look()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = await fixture.SeedAsync(new CycleSpec(), new CycleSpec(StripWidth: 1800, StripHeight: 600));

        var items = (await fixture.Builder.OperatorPageAsync(GalleryRig.EventId, null, Ct)).Entries.Select(entry => entry.View).ToList();

        var landscape = items[0];
        var portrait = items[1];
        Assert.Equal(await fixture.UrlAsync(seeded[0].Id, 400), portrait.Strip!.ThumbnailUrl);
        Assert.Equal((600, 1800, false), (portrait.Strip.Width, portrait.Strip.Height, portrait.Strip.Landscape));
        Assert.Equal(await fixture.UrlAsync(seeded[1].Id, 1200), landscape.Strip!.ThumbnailUrl);
        Assert.True(landscape.Strip.Landscape);
        Assert.Equal(("Signature Strip", "Naturale"), (portrait.TemplateName, portrait.LookName));
        Assert.Empty(portrait.Captures);
        Assert.False(portrait.Unavailable);
    }

    [Fact]
    public async Task A_Template_or_Look_the_catalog_no_longer_knows_shows_its_id()
    {
        var fixture = await Fixture.OpenAsync();
        var cycle = await fixture.Memory.StartGuestCycleAsync(GalleryRig.EventId, Ct);
        var capture = await fixture.Memory.CommitCaptureAsync(new CaptureCommit(cycle.Id, 1, new byte[] { 1, 2, 3 }, 1640, 1230, true, GallerySeed.Start), Ct);
        await fixture.Memory.CommitPhotoStripAsync(new PhotoStripCommit(cycle.Id, "retired-template", "retired-look", [capture.Id], new byte[] { 9 }, 600, 1800), Ct);

        var item = (await fixture.Builder.OperatorPageAsync(GalleryRig.EventId, null, Ct)).Entries.Single().View;

        Assert.Equal(("retired-template", "retired-look"), (item.TemplateName, item.LookName));
    }

    [Fact]
    public async Task A_cycle_without_a_strip_shows_its_kept_Captures_in_sequence_order_and_never_a_discarded_one()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = (await fixture.SeedAsync(new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 3, Discarded: 1)))[0];

        var item = (await fixture.Builder.OperatorPageAsync(GalleryRig.EventId, null, Ct)).Entries.Single().View;

        Assert.Null(item.Strip);
        Assert.Null(item.TemplateName);
        Assert.Null(item.LookName);
        Assert.Equal(seeded.Kept.Select(capture => GalleryUrls.ThumbnailUrlFromStorePath($"Cache/thumbnails/{capture.Id}-{capture.Sha256[..8]}-480.jpg")), item.Captures.Select(capture => capture.ThumbnailUrl));
        Assert.All(item.Captures, capture => Assert.Equal((1640, 1230), (capture.Width, capture.Height)));
        Assert.False(item.Unavailable);
    }

    [Fact]
    public async Task A_cycle_that_kept_nothing_is_listed_with_no_images_and_is_not_unavailable()
    {
        var fixture = await Fixture.OpenAsync();
        await fixture.SeedAsync(new CycleSpec(GuestCycleEndReason.AppClosed, Captures: 0));

        var item = (await fixture.Builder.OperatorPageAsync(GalleryRig.EventId, null, Ct)).Entries.Single().View;

        Assert.Equal(GalleryOutcome.AppClosed, item.Outcome);
        Assert.Null(item.Strip);
        Assert.Empty(item.Captures);
        Assert.False(item.Unavailable);
        Assert.Empty(fixture.Store.ThumbnailRequests);
    }

    [Fact]
    public async Task A_strip_whose_file_is_gone_is_unavailable_with_its_names_kept()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = (await fixture.SeedAsync(new CycleSpec()))[0];
        fixture.Store.MissingThumbnails.Add(seeded.Id);

        var item = (await fixture.Builder.OperatorPageAsync(GalleryRig.EventId, null, Ct)).Entries.Single().View;

        Assert.True(item.Unavailable);
        Assert.Null(item.Strip);
        Assert.Empty(item.Captures);
        Assert.Equal(("Signature Strip", "Naturale"), (item.TemplateName, item.LookName));
    }

    [Fact]
    public async Task A_Capture_whose_file_is_gone_makes_a_strip_less_cycle_unavailable()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = (await fixture.SeedAsync(new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 3)))[0];
        fixture.Store.MissingThumbnails.Add(seeded.Kept[1].Id);

        var item = (await fixture.Builder.OperatorPageAsync(GalleryRig.EventId, null, Ct)).Entries.Single().View;

        Assert.True(item.Unavailable);
        Assert.Empty(item.Captures);
        Assert.Null(item.TemplateName);
    }

    [Fact]
    public async Task A_store_failure_other_than_a_missing_file_fails_the_page()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = (await fixture.SeedAsync(new CycleSpec(), new CycleSpec()))[0];
        fixture.Store.FailingThumbnails.Add(seeded.Id);

        var failure = await Assert.ThrowsAsync<StorageException>(() => fixture.Builder.OperatorPageAsync(GalleryRig.EventId, null, Ct));

        Assert.Equal(StorageErrorKind.Locked, failure.Kind);
    }

    [Fact]
    public async Task A_cycle_deleted_while_the_page_is_built_is_left_out()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = await fixture.SeedAsync(new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 2), new CycleSpec());
        var page = await fixture.Memory.ListGuestCyclesAsync(GalleryRig.EventId, GuestCycleFilter.All, null, 40, Ct);
        await fixture.Memory.MarkGuestCycleDeletingAsync(seeded[0].Id, Ct);
        var builder = new GalleryPageBuilder(new ListFirstStore(fixture.Memory, page), new CatalogData());

        var built = await builder.OperatorPageAsync(GalleryRig.EventId, null, Ct);

        Assert.Equal([seeded[1].Id], built.Entries.Select(entry => entry.GuestCycleId));
    }

    [Fact]
    public async Task Each_operator_item_is_reported_the_moment_it_is_built_and_only_when_it_is()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = await fixture.SeedAsync(new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 2), new CycleSpec(), new CycleSpec());
        var page = await fixture.Memory.ListGuestCyclesAsync(GalleryRig.EventId, GuestCycleFilter.All, null, 40, Ct);
        await fixture.Memory.MarkGuestCycleDeletingAsync(seeded[0].Id, Ct);
        var builder = new GalleryPageBuilder(new ListFirstStore(fixture.Memory, page), new CatalogData());
        var reported = new System.Collections.Concurrent.ConcurrentBag<string>();

        var built = await builder.OperatorPageAsync(GalleryRig.EventId, null, Ct, entry => reported.Add(entry.GuestCycleId));

        Assert.Equal(built.Entries.Select(entry => entry.GuestCycleId).Order(StringComparer.Ordinal), reported.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(seeded[0].Id, reported);
        Assert.Equal(2, reported.Count);
    }

    [Fact]
    public async Task Pages_are_forty_cycles_with_a_cursor_until_the_last()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = await fixture.SeedAsync([.. GallerySeed.Many(85)]);
        var expected = GalleryAssert.NewestFirst(seeded);

        var first = await fixture.Builder.OperatorPageAsync(GalleryRig.EventId, null, Ct);
        var second = await fixture.Builder.OperatorPageAsync(GalleryRig.EventId, first.NextCursor, Ct);
        var third = await fixture.Builder.OperatorPageAsync(GalleryRig.EventId, second.NextCursor, Ct);

        Assert.Equal((40, 40, 5), (first.Entries.Count, second.Entries.Count, third.Entries.Count));
        Assert.NotNull(first.NextCursor);
        Assert.NotNull(second.NextCursor);
        Assert.Null(third.NextCursor);
        Assert.Equal(expected, first.Entries.Concat(second.Entries).Concat(third.Entries).Select(entry => entry.GuestCycleId));
    }

    [Fact]
    public async Task At_most_four_thumbnails_are_made_at_once_and_the_page_keeps_the_stores_order()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = await fixture.SeedAsync([.. GallerySeed.Many(12)]);
        fixture.Store.ThumbnailDelay = TimeSpan.FromMilliseconds(15);

        var page = await fixture.Builder.OperatorPageAsync(GalleryRig.EventId, null, Ct);

        Assert.Equal(GalleryAssert.NewestFirst(seeded), page.Entries.Select(entry => entry.GuestCycleId));
        Assert.InRange(fixture.Store.PeakThumbnailConcurrency, 2, GalleryPageBuilder.Parallelism);
    }

    [Fact]
    public async Task The_guest_page_asks_for_completed_cycles_only_and_carries_nothing_but_the_strip()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = await fixture.SeedAsync(new CycleSpec(), new CycleSpec(GuestCycleEndReason.StartedOver), new CycleSpec(null), new CycleSpec(StripWidth: 1800, StripHeight: 600));

        var page = await fixture.Builder.GuestPageAsync(GalleryRig.EventId, null, Ct);

        Assert.Equal(GuestCycleFilter.CompletedOnly, fixture.Store.ListCalls.Single().Filter);
        Assert.Equal([seeded[3].Id, seeded[0].Id], page.Entries.Select(entry => entry.GuestCycleId));
        var landscape = page.Entries[0].View;
        Assert.Equal((await fixture.UrlAsync(seeded[3].Id, 1200), 1800, 600, true, GalleryCopy.StripAlt), (landscape.ThumbnailUrl, landscape.Width, landscape.Height, landscape.Landscape, landscape.Alt));
    }

    [Fact]
    public async Task The_guest_page_leaves_out_a_strip_whose_file_is_gone_and_fails_on_any_other_failure()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = await fixture.SeedAsync(new CycleSpec(), new CycleSpec());
        fixture.Store.MissingThumbnails.Add(seeded[1].Id);

        var page = await fixture.Builder.GuestPageAsync(GalleryRig.EventId, null, Ct);
        fixture.Store.FailingThumbnails.Add(seeded[0].Id);
        var failure = await Assert.ThrowsAsync<StorageException>(() => fixture.Builder.GuestPageAsync(GalleryRig.EventId, null, Ct));

        Assert.Equal([seeded[0].Id], page.Entries.Select(entry => entry.GuestCycleId));
        Assert.Equal(StorageErrorKind.Locked, failure.Kind);
    }

    [Fact]
    public async Task An_ended_cycle_is_built_for_each_gallery_that_wants_it_and_its_strip_thumbnail_is_made_either_way()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = (await fixture.SeedAsync(new CycleSpec()))[0];

        var both = await fixture.Builder.EndedCycleAsync(seeded.Id, forOperator: true, forGuest: true, Ct);
        var warmOnly = await fixture.Builder.EndedCycleAsync(seeded.Id, forOperator: false, forGuest: false, Ct);

        Assert.Equal(GalleryRig.EventId, both!.EventId);
        Assert.Equal(seeded.Id, both.Operator!.GuestCycleId);
        Assert.Equal(seeded.Id, both.Guest!.GuestCycleId);
        Assert.Null(warmOnly!.Operator);
        Assert.Null(warmOnly.Guest);
        Assert.Contains((seeded.Id, 400), fixture.Store.ThumbnailRequests);
    }

    [Fact]
    public async Task An_ended_cycle_that_did_not_complete_is_never_a_guest_strip()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = (await fixture.SeedAsync(new CycleSpec(GuestCycleEndReason.OperatorExit, Captures: 2)))[0];

        var ended = await fixture.Builder.EndedCycleAsync(seeded.Id, forOperator: true, forGuest: true, Ct);

        Assert.Null(ended!.Guest);
        Assert.Equal(GalleryOutcome.EndedByOperator, ended.Operator!.View.Outcome);
        Assert.DoesNotContain(fixture.Store.ThumbnailRequests, request => request.SourceId == seeded.Id);
    }

    [Fact]
    public async Task A_cycle_that_is_open_or_gone_has_no_ended_entries()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = await fixture.SeedAsync(new CycleSpec(null), new CycleSpec());
        await fixture.Memory.MarkGuestCycleDeletingAsync(seeded[1].Id, Ct);

        Assert.Null(await fixture.Builder.EndedCycleAsync(seeded[0].Id, true, true, Ct));
        Assert.Null(await fixture.Builder.EndedCycleAsync(seeded[1].Id, true, true, Ct));
    }

    [Fact]
    public async Task The_warm_up_is_best_effort()
    {
        var fixture = await Fixture.OpenAsync();
        var seeded = (await fixture.SeedAsync(new CycleSpec()))[0];
        fixture.Store.FailingThumbnails.Add(seeded.Id);

        var ended = await fixture.Builder.EndedCycleAsync(seeded.Id, forOperator: false, forGuest: true, Ct);

        Assert.Null(ended!.Guest);
        Assert.Null(ended.Operator);
    }

    /// <summary>A store whose first list answer is a page read before something changed, so the builder meets a cycle that has since gone.</summary>
    private sealed class ListFirstStore(IEventStore inner, GuestCyclePage firstPage) : IEventStore
    {
        public string DataRoot => inner.DataRoot;

        public Task<GuestCyclePage> ListGuestCyclesAsync(string eventId, GuestCycleFilter filter, string? cursor, int pageSize, CancellationToken ct) => Task.FromResult(firstPage);

        public Task<GuestCycleDetail?> GetGuestCycleAsync(string guestCycleId, CancellationToken ct) => inner.GetGuestCycleAsync(guestCycleId, ct);

        public Task<string> GetThumbnailPathAsync(string sourceId, int width, CancellationToken ct) => inner.GetThumbnailPathAsync(sourceId, width, ct);

        public Task DrainAsync(CancellationToken ct) => inner.DrainAsync(ct);

        public ValueTask DisposeAsync() => inner.DisposeAsync();

        public Task<StorageOpenResult> OpenAsync(CancellationToken ct) => inner.OpenAsync(ct);

        public Task ProbeWritableAsync(CancellationToken ct) => inner.ProbeWritableAsync(ct);

        public Task<IReadOnlyList<EventSummary>> ListEventsAsync(CancellationToken ct) => inner.ListEventsAsync(ct);

        public Task<EventRecord?> GetEventAsync(string eventId, CancellationToken ct) => inner.GetEventAsync(eventId, ct);

        public Task CreateEventAsync(EventRecord record, CancellationToken ct) => inner.CreateEventAsync(record, ct);

        public Task UpdateEventAsync(EventRecord record, CancellationToken ct) => inner.UpdateEventAsync(record, ct);

        public Task SetPinAsync(string eventId, string pin, CancellationToken ct) => inner.SetPinAsync(eventId, pin, ct);

        public Task<bool> VerifyPinAsync(string eventId, string pin, CancellationToken ct) => inner.VerifyPinAsync(eventId, pin, ct);

        public Task MarkEventDeletingAsync(string eventId, CancellationToken ct) => inner.MarkEventDeletingAsync(eventId, ct);

        public Task<GuestCycleRecord> StartGuestCycleAsync(string eventId, CancellationToken ct) => inner.StartGuestCycleAsync(eventId, ct);

        public Task EndGuestCycleAsync(string guestCycleId, GuestCycleEndReason reason, CancellationToken ct) => inner.EndGuestCycleAsync(guestCycleId, reason, ct);

        public Task<CaptureRecord> CommitCaptureAsync(CaptureCommit commit, CancellationToken ct) => inner.CommitCaptureAsync(commit, ct);

        public Task SetCaptureStateAsync(string captureId, CaptureState state, CancellationToken ct) => inner.SetCaptureStateAsync(captureId, state, ct);

        public Task<PhotoStripRecord> CommitPhotoStripAsync(PhotoStripCommit commit, CancellationToken ct) => inner.CommitPhotoStripAsync(commit, ct);

        public Task MarkGuestCycleDeletingAsync(string guestCycleId, CancellationToken ct) => inner.MarkGuestCycleDeletingAsync(guestCycleId, ct);
    }
}
