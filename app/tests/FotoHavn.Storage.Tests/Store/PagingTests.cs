using FotoHavn.Core.Storage;
using FotoHavn.Storage.Tests.Support;

namespace FotoHavn.Storage.Tests.Store;

public sealed class PagingTests(PagingTests.Seed seed) : IClassFixture<PagingTests.Seed>
{
    private const int CycleCount = 300;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>One seeded store shared by the tests that only read.</summary>
    public sealed class Seed : IAsyncLifetime
    {
        public StoreHarness Harness { get; private set; } = null!;

        public EventRecord Event { get; private set; } = null!;

        public List<GuestCycleRecord> Cycles { get; private set; } = null!;

        public HashSet<string> Completed { get; private set; } = null!;

        public async ValueTask InitializeAsync() => (Harness, Event, Cycles, Completed) = await SeedAsync();

        public async ValueTask DisposeAsync() => await Harness.DisposeAsync();
    }

    /// <summary>The plan's 300-cycle data set: three cycles share each start time, every fifth is completed, a few started over.</summary>
    private static async Task<(StoreHarness Harness, EventRecord Event, List<GuestCycleRecord> Cycles, HashSet<string> Completed)> SeedAsync()
    {
        var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        var cycles = new List<GuestCycleRecord>();
        var completed = new HashSet<string>();
        for (var index = 0; index < CycleCount; index++)
        {
            var cycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
            cycles.Add(cycle);
            if (index % 5 == 0)
            {
                var capture = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 1, [1, 2, 3]), Ct);
                _ = await harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [capture.Id], [4, 5, 6]), Ct);
                _ = completed.Add(cycle.Id);
            }
            else if (index % 7 == 0)
            {
                await harness.Store.EndGuestCycleAsync(cycle.Id, GuestCycleEndReason.StartedOver, Ct);
            }

            if (index % 3 == 2)
            {
                harness.Clock.Advance(TimeSpan.FromSeconds(1));
            }
        }

        return (harness, record, cycles, completed);
    }

    private static async Task<List<GuestCyclePage>> AllPagesAsync(SqliteEventStore store, string eventId, GuestCycleFilter filter, int pageSize)
    {
        var pages = new List<GuestCyclePage>();
        string? cursor = null;
        do
        {
            var page = await store.ListGuestCyclesAsync(eventId, filter, cursor, pageSize, Ct);
            pages.Add(page);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        return pages;
    }

    [Theory]
    [InlineData(7)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(150)]
    [InlineData(200)]
    public async Task Paging_the_300_cycles_visits_each_once_newest_first_with_a_null_cursor_on_the_last_page(int pageSize)
    {
        var pages = await AllPagesAsync(seed.Harness.Store, seed.Event.Id, GuestCycleFilter.All, pageSize);

        var seen = pages.SelectMany(page => page.Items).Select(item => item.Cycle).ToList();
        Assert.Equal(seed.Cycles.Count, seen.Count);
        Assert.Equal(seed.Cycles.Count, seen.Select(cycle => cycle.Id).Distinct().Count());
        Assert.Equal(seen.OrderByDescending(cycle => cycle.StartedAt).ThenByDescending(cycle => cycle.Id, StringComparer.Ordinal).Select(cycle => cycle.Id), seen.Select(cycle => cycle.Id));
        Assert.Equal((int)Math.Ceiling(CycleCount / (double)pageSize), pages.Count);
        Assert.All(pages.Take(pages.Count - 1), page => Assert.Equal(pageSize, page.Items.Count));
        Assert.All(pages.Take(pages.Count - 1), page => Assert.NotNull(page.NextCursor));
        Assert.Null(pages[^1].NextCursor);
    }

    [Fact]
    public async Task A_page_that_exactly_empties_the_list_has_no_next_cursor()
    {
        var page = await seed.Harness.Store.ListGuestCyclesAsync(seed.Event.Id, GuestCycleFilter.All, null, 200, Ct);
        var second = await seed.Harness.Store.ListGuestCyclesAsync(seed.Event.Id, GuestCycleFilter.All, page.NextCursor, 100, Ct);

        Assert.Equal(200, page.Items.Count);
        Assert.NotNull(page.NextCursor);
        Assert.Equal(100, second.Items.Count);
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task CompletedOnly_lists_cycles_that_completed_with_a_Photo_Strip_and_counts_kept_Captures()
    {
        var pages = await AllPagesAsync(seed.Harness.Store, seed.Event.Id, GuestCycleFilter.CompletedOnly, 25);
        var items = pages.SelectMany(page => page.Items).ToList();

        Assert.Equal(seed.Completed.Count, items.Count);
        Assert.Equal([25, 25, 10], pages.Select(page => page.Items.Count));
        Assert.All(items, item =>
        {
            Assert.Contains(item.Cycle.Id, seed.Completed);
            Assert.Equal(GuestCycleEndReason.Completed, item.Cycle.EndReason);
            Assert.NotNull(item.PhotoStrip);
            Assert.Equal(1, item.KeptCaptureCount);
        });
        var all = await AllPagesAsync(seed.Harness.Store, seed.Event.Id, GuestCycleFilter.All, 200);
        var startedOver = all.SelectMany(page => page.Items).Where(item => item.Cycle.EndReason == GuestCycleEndReason.StartedOver).ToList();
        Assert.NotEmpty(startedOver);
        Assert.All(startedOver, item => Assert.Null(item.PhotoStrip));
    }

    [Fact]
    public async Task Another_Events_cycles_are_not_listed_and_the_arguments_are_checked()
    {
        var store = seed.Harness.Store;
        var other = Sample.Event();
        await store.CreateEventAsync(other, Ct);
        _ = await store.StartGuestCycleAsync(other.Id, Ct);

        var page = await store.ListGuestCyclesAsync(other.Id, GuestCycleFilter.All, null, 200, Ct);

        Assert.Single(page.Items);
        Assert.Null(page.NextCursor);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => store.ListGuestCyclesAsync(StorageIds.NewId(), GuestCycleFilter.All, null, 5, Ct))).Kind);
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.ListGuestCyclesAsync(seed.Event.Id, GuestCycleFilter.All, null, 0, Ct));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.ListGuestCyclesAsync(seed.Event.Id, GuestCycleFilter.All, null, 201, Ct));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.ListGuestCyclesAsync(seed.Event.Id, (GuestCycleFilter)5, null, 5, Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => store.ListGuestCyclesAsync(seed.Event.Id, GuestCycleFilter.All, "garbage!", 5, Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => store.ListGuestCyclesAsync("bad", GuestCycleFilter.All, null, 5, Ct));
    }

    [Fact]
    public async Task A_cursor_stays_stable_while_cycles_are_added_and_deleted()
    {
        var (harness, record, cycles, _) = await SeedAsync();
        await using var lifetime = harness;
        var first = await harness.Store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.All, null, 100, Ct);
        harness.Clock.Advance(TimeSpan.FromHours(1));
        var added = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        var doomed = first.Items[10].Cycle.Id;
        var oldest = cycles.OrderBy(cycle => cycle.StartedAt).ThenBy(cycle => cycle.Id, StringComparer.Ordinal).First().Id;
        await harness.Store.MarkGuestCycleDeletingAsync(doomed, Ct);
        await harness.Store.MarkGuestCycleDeletingAsync(oldest, Ct);

        var rest = new List<string>();
        var cursor = first.NextCursor;
        while (cursor is not null)
        {
            var page = await harness.Store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.All, cursor, 100, Ct);
            rest.AddRange(page.Items.Select(item => item.Cycle.Id));
            cursor = page.NextCursor;
        }

        var expected = cycles.Select(cycle => cycle.Id).Except(first.Items.Select(item => item.Cycle.Id)).Except([oldest]).ToHashSet();
        Assert.Equal(expected, rest.ToHashSet());
        Assert.Equal(expected.Count, rest.Count);
        Assert.DoesNotContain(added.Id, rest);
        await harness.Store.DrainAsync(Ct);
    }

    [Fact]
    public async Task A_deleting_cycle_is_in_neither_view()
    {
        var (harness, record, cycles, completed) = await SeedAsync();
        await using var lifetime = harness;
        var doomed = completed.First();

        await harness.Store.MarkGuestCycleDeletingAsync(doomed, Ct);
        var all = (await AllPagesAsync(harness.Store, record.Id, GuestCycleFilter.All, 200)).SelectMany(page => page.Items).ToList();
        var done = (await AllPagesAsync(harness.Store, record.Id, GuestCycleFilter.CompletedOnly, 200)).SelectMany(page => page.Items).ToList();

        Assert.Equal(cycles.Count - 1, all.Count);
        Assert.Equal(completed.Count - 1, done.Count);
        Assert.DoesNotContain(all, item => item.Cycle.Id == doomed);
        Assert.DoesNotContain(done, item => item.Cycle.Id == doomed);
        await harness.Store.DrainAsync(Ct);
    }
}
