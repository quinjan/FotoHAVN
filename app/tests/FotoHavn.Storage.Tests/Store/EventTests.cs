using FotoHavn.Core.Storage;
using FotoHavn.Storage.Tests.Support;

namespace FotoHavn.Storage.Tests.Store;

public sealed class EventTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_created_Event_reads_back_with_its_children_in_the_order_saved()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();

        await harness.Store.CreateEventAsync(record, Ct);
        var read = await harness.Store.GetEventAsync(record.Id, Ct);

        Assert.NotNull(read);
        Assert.Equal(record with { CreatedAt = harness.Clock.UtcNow, UpdatedAt = harness.Clock.UtcNow }, read, new EventComparer());
        Assert.Equal(record.OfferedTemplateIds, read.OfferedTemplateIds);
        Assert.Equal(record.OfferedLookIds, read.OfferedLookIds);
        Assert.Equal(record.PhaseTimers, read.PhaseTimers);
        Assert.Equal(harness.Clock.UtcNow, read.CreatedAt);
        Assert.Equal(harness.Clock.UtcNow, read.UpdatedAt);
        Assert.False(read.HasPin);
    }

    [Fact]
    public async Task Optional_text_can_be_null_and_a_supplied_created_time_is_ignored()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event() with
        {
            Venue = null,
            CustomerName = null,
            Notes = null,
            CameraDeviceId = null,
            CameraName = null,
            GuestDisplayId = null,
            GuestDisplayName = null,
            Mirror = false,
            GuestGalleryEnabled = false,
            CountdownSeconds = 10,
            PhaseTimers = [],
            OfferedTemplateIds = ["a"],
            OfferedLookIds = ["b"],
            CreatedAt = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
            HasPin = true,
        };

        await harness.Store.CreateEventAsync(record, Ct);
        var read = await harness.Store.GetEventAsync(record.Id, Ct);

        Assert.NotNull(read);
        Assert.Null(read.Venue);
        Assert.Null(read.CameraName);
        Assert.False(read.Mirror);
        Assert.False(read.GuestGalleryEnabled);
        Assert.Equal(10, read.CountdownSeconds);
        Assert.Empty(read.PhaseTimers);
        Assert.Equal(harness.Clock.UtcNow, read.CreatedAt);
        Assert.False(read.HasPin);
    }

    [Fact]
    public async Task Creating_an_Event_with_an_existing_id_conflicts()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);

        var failure = await Assert.ThrowsAsync<StorageException>(() => harness.Store.CreateEventAsync(record, Ct));

        Assert.Equal(StorageErrorKind.Conflict, failure.Kind);
        Assert.Equal(2, harness.Count("SELECT COUNT(*) FROM event_templates;"));
    }

    [Fact]
    public async Task A_create_that_fails_part_way_leaves_nothing()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        harness.Execute("CREATE TRIGGER fail_looks BEFORE INSERT ON event_looks BEGIN SELECT RAISE(ABORT, 'no'); END;");

        _ = await Assert.ThrowsAsync<StorageException>(() => harness.Store.CreateEventAsync(Sample.Event(), Ct));

        Assert.Equal(0, harness.Count("SELECT COUNT(*) FROM events;"));
        Assert.Equal(0, harness.Count("SELECT COUNT(*) FROM event_templates;"));
    }

    [Fact]
    public async Task Create_refuses_what_the_contract_refuses_before_touching_the_database()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var valid = Sample.Event();

        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CreateEventAsync(valid with { Printer = "laser" }, Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CreateEventAsync(valid with { CountdownSeconds = 4 }, Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CreateEventAsync(valid with { Id = "not-a-guid" }, Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CreateEventAsync(valid with { Name = " " }, Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CreateEventAsync(valid with { OfferedLookIds = ["a", "a"] }, Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CreateEventAsync(valid with { PhaseTimers = [new(PhaseTimerPhase.Template, -1)] }, Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.CreateEventAsync(valid with { PhaseTimers = [new(PhaseTimerPhase.Template, 1), new(PhaseTimerPhase.Template, 2)] }, Ct));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => harness.Store.CreateEventAsync(null!, Ct));
        Assert.Equal(0, harness.Count("SELECT COUNT(*) FROM events;"));
    }

    [Theory]
    [InlineData("printer", "'laser'")]
    [InlineData("countdown_seconds", "4")]
    public async Task The_schema_rejects_a_printer_or_countdown_the_validation_would_also_refuse(string column, string value)
    {
        await using var harness = await StoreHarness.OpenedAsync();

        var failure = Assert.ThrowsAny<Exception>(() => harness.Execute(
            $"INSERT INTO events (id, name, event_date, default_template_id, default_look_id, created_at, updated_at, {column}) VALUES ('x', 'n', '2026-01-01', 't', 'l', 'now', 'now', {value});"));

        Assert.Contains("CHECK", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_replaces_every_column_and_child_set_keeps_created_at_and_sets_updated_at()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        await harness.Store.SetPinAsync(record.Id, "1234", Ct);
        var created = harness.Clock.UtcNow;
        harness.Clock.Advance(TimeSpan.FromHours(2));
        var changed = record with
        {
            Name = "Renamed",
            EventDate = new DateOnly(2027, 1, 2),
            Venue = null,
            Mirror = false,
            CountdownSeconds = 3,
            OfferedTemplateIds = ["only"],
            OfferedLookIds = ["classic", "sepia"],
            PhaseTimers = [new(PhaseTimerPhase.GuestGallery, 5)],
            HasPin = false,
        };

        await harness.Store.UpdateEventAsync(changed, Ct);
        var read = await harness.Store.GetEventAsync(record.Id, Ct);

        Assert.NotNull(read);
        Assert.Equal("Renamed", read.Name);
        Assert.Equal(new DateOnly(2027, 1, 2), read.EventDate);
        Assert.Null(read.Venue);
        Assert.False(read.Mirror);
        Assert.Equal(3, read.CountdownSeconds);
        Assert.Equal(["only"], read.OfferedTemplateIds);
        Assert.Equal(["classic", "sepia"], read.OfferedLookIds);
        Assert.Equal([new PhaseTimerSetting(PhaseTimerPhase.GuestGallery, 5)], read.PhaseTimers);
        Assert.Equal(created, read.CreatedAt);
        Assert.Equal(harness.Clock.UtcNow, read.UpdatedAt);
        Assert.True(read.HasPin);
        Assert.True(await harness.Store.VerifyPinAsync(record.Id, "1234", Ct));
    }

    [Fact]
    public async Task Update_of_a_missing_or_deleting_Event_is_NotFound_and_validates_first()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        await harness.Store.MarkEventDeletingAsync(record.Id, Ct);

        var missing = await Assert.ThrowsAsync<StorageException>(() => harness.Store.UpdateEventAsync(Sample.Event(), Ct));
        var deleting = await Assert.ThrowsAsync<StorageException>(() => harness.Store.UpdateEventAsync(record, Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.UpdateEventAsync(record with { Printer = "x" }, Ct));

        Assert.Equal(StorageErrorKind.NotFound, missing.Kind);
        Assert.Equal(StorageErrorKind.NotFound, deleting.Kind);
    }

    [Fact]
    public async Task Get_returns_null_for_a_missing_Event_and_refuses_a_malformed_id()
    {
        await using var harness = await StoreHarness.OpenedAsync();

        Assert.Null(await harness.Store.GetEventAsync(StorageIds.NewId(), Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.GetEventAsync("../evil", Ct));
    }

    [Fact]
    public async Task The_list_is_newest_date_first_then_newest_created_with_counts_and_last_use()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var older = Sample.Event(name: "Older", date: new DateOnly(2026, 5, 1));
        var newer = Sample.Event(name: "Newer", date: new DateOnly(2026, 9, 1));
        var sameDayFirst = Sample.Event(name: "Same day first", date: new DateOnly(2026, 7, 1));
        var sameDaySecond = Sample.Event(name: "Same day second", date: new DateOnly(2026, 7, 1));
        await harness.Store.CreateEventAsync(older, Ct);
        harness.Clock.Advance(TimeSpan.FromMinutes(1));
        await harness.Store.CreateEventAsync(newer, Ct);
        harness.Clock.Advance(TimeSpan.FromMinutes(1));
        await harness.Store.CreateEventAsync(sameDayFirst, Ct);
        harness.Clock.Advance(TimeSpan.FromMinutes(1));
        await harness.Store.CreateEventAsync(sameDaySecond, Ct);
        var open = await harness.Store.StartGuestCycleAsync(older.Id, Ct);
        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        var quit = await harness.Store.StartGuestCycleAsync(older.Id, Ct);
        await harness.Store.EndGuestCycleAsync(quit.Id, GuestCycleEndReason.OperatorExit, Ct);
        var deleted = await harness.Store.StartGuestCycleAsync(older.Id, Ct);
        await harness.Store.MarkGuestCycleDeletingAsync(deleted.Id, Ct);
        var lastUsed = harness.Clock.UtcNow;
        var capture = await harness.Store.CommitCaptureAsync(Sample.Capture(open.Id), Ct);
        _ = await harness.Store.CommitPhotoStripAsync(Sample.Strip(open.Id, [capture.Id]), Ct);

        var list = await harness.Store.ListEventsAsync(Ct);

        Assert.Equal(["Newer", "Same day second", "Same day first", "Older"], list.Select(row => row.Name));
        var olderRow = list[3];
        Assert.Equal(1, olderRow.CompletedGuestCycles);
        Assert.Equal(lastUsed, olderRow.LastUsedAt);
        Assert.Equal("Town hall", olderRow.Venue);
        Assert.Equal(new DateOnly(2026, 5, 1), olderRow.EventDate);
        Assert.Null(list[0].LastUsedAt);
        Assert.Equal(0, list[0].CompletedGuestCycles);
    }

    [Fact]
    public async Task A_deleting_Event_vanishes_from_every_read_and_refuses_every_write()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        var cycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        await harness.Store.MarkEventDeletingAsync(record.Id, Ct);

        Assert.Empty(await harness.Store.ListEventsAsync(Ct));
        Assert.Null(await harness.Store.GetEventAsync(record.Id, Ct));
        Assert.Null(await harness.Store.GetGuestCycleAsync(cycle.Id, Ct));
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.StartGuestCycleAsync(record.Id, Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.MarkEventDeletingAsync(record.Id, Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id), Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.All, null, 10, Ct))).Kind);
        await harness.Store.DrainAsync(Ct);
    }

    private sealed class EventComparer : IEqualityComparer<EventRecord>
    {
        public bool Equals(EventRecord? x, EventRecord? y) =>
            x is not null && y is not null
            && x.Id == y.Id && x.Name == y.Name && x.EventDate == y.EventDate && x.Venue == y.Venue && x.CustomerName == y.CustomerName
            && x.Notes == y.Notes && x.CameraDeviceId == y.CameraDeviceId && x.CameraName == y.CameraName && x.GuestDisplayId == y.GuestDisplayId
            && x.GuestDisplayName == y.GuestDisplayName && x.Printer == y.Printer && x.Mirror == y.Mirror && x.CountdownSeconds == y.CountdownSeconds
            && x.DefaultTemplateId == y.DefaultTemplateId && x.DefaultLookId == y.DefaultLookId && x.GuestGalleryEnabled == y.GuestGalleryEnabled
            && x.CreatedAt == y.CreatedAt && x.UpdatedAt == y.UpdatedAt && x.HasPin == y.HasPin;

        public int GetHashCode(EventRecord obj) => obj.Id.GetHashCode(StringComparison.Ordinal);
    }
}
