using FotoHavn.Catalog;
using FotoHavn.Core.Events;
using FotoHavn.Core.Events.Setup;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Storage;
using FotoHavn.Protocol;

namespace FotoHavn.Core.Tests.Events.Setup;

public sealed class EventSetupDraftTests
{
    private static readonly CatalogData Catalog = new();
    private static readonly DateOnly Today = new(2026, 10, 13);

    [Fact]
    public void A_new_draft_has_the_plan_defaults()
    {
        var draft = EventSetupDraft.CreateNew(Catalog, Today);

        Assert.True(draft.IsNew);
        Assert.Null(draft.EventId);
        Assert.Equal(("", "2026-10-13", "", "", ""), (draft.Name, draft.Date, draft.Venue, draft.Customer, draft.Notes));
        Assert.True(draft.Mirror);
        Assert.Equal(5, draft.CountdownSeconds);
        Assert.Equal(Catalog.Templates.Select(template => template.Id), draft.OfferedTemplateIds);
        Assert.Equal("signature-strip", draft.DefaultTemplateId);
        Assert.Equal(Catalog.Looks.Select(look => look.Id), draft.OfferedLookIds);
        Assert.Equal("naturale-v1", draft.DefaultLookId);
        Assert.False(draft.GuestGalleryEnabled);
        Assert.Null(draft.CameraDeviceId);
        Assert.Null(draft.GuestDisplayId);
        Assert.False(draft.HasPin);
        Assert.All(PhaseTimerRules.All, phase => Assert.Equal(PhaseTimerRules.Default(phase), draft.TimerSeconds(phase)));
        Assert.False(draft.IsDirty);
    }

    [Fact]
    public void Typing_makes_the_draft_dirty_and_typing_it_back_does_not()
    {
        var draft = EventSetupDraft.CreateNew(Catalog, Today);

        draft.SetField(EventField.Name, "Gala");
        Assert.True(draft.IsDirty);
        draft.SetField(EventField.Name, string.Empty);
        Assert.False(draft.IsDirty);
    }

    [Fact]
    public void Every_kind_of_change_makes_the_draft_dirty()
    {
        Action<EventSetupDraft>[] changes =
        [
            draft => draft.SetField(EventField.Date, "2026-11-01"),
            draft => draft.SetField(EventField.Venue, "Hall"),
            draft => draft.SetField(EventField.Customer, "Ana"),
            draft => draft.SetField(EventField.Notes, "Props"),
            draft => draft.BindCamera("c", "C"),
            draft => draft.BindGuestDisplay("d", "D"),
            draft => draft.SetMirror(false),
            draft => draft.SetCountdown(10),
            draft => draft.SetOfferedTemplates(["panorama-four"]),
            draft => draft.SetDefaultTemplate("polka-keepsake"),
            draft => draft.SetOfferedLooks(["naturale-v1"]),
            draft => draft.SetDefaultLook("sepia-v1"),
            draft => draft.SetGuestGallery(true),
            draft => draft.SetTimer(PhaseTimerPhase.Filters, 0),
            draft => draft.SetPin("1234", "1234"),
            draft => draft.SetPin("1234", string.Empty),
        ];

        foreach (var change in changes)
        {
            var draft = EventSetupDraft.CreateNew(Catalog, Today);
            change(draft);
            Assert.True(draft.IsDirty);
        }
    }

    [Fact]
    public void Clearing_a_pending_PIN_makes_the_draft_clean_again()
    {
        var draft = EventSetupDraft.CreateNew(Catalog, Today);
        draft.SetPin(SetupSamples.Pin, SetupSamples.Pin);
        Assert.True(draft.HasPendingPin);
        Assert.Equal(SetupSamples.Pin, draft.PendingPin);

        draft.SetPin(string.Empty, string.Empty);

        Assert.False(draft.HasPendingPin);
        Assert.Null(draft.PendingPin);
        Assert.False(draft.IsDirty);
    }

    [Fact]
    public void The_draft_never_prints_a_pending_PIN()
    {
        var draft = EventSetupDraft.CreateNew(Catalog, Today);
        draft.SetPin(SetupSamples.Pin, SetupSamples.Pin);

        Assert.DoesNotContain(SetupSamples.Pin, draft.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_Event_loads_into_a_clean_draft()
    {
        var record = SetupSamples.Event(SetupSamples.HarbourId) with
        {
            Notes = "Bring the props.",
            Mirror = false,
            CountdownSeconds = 10,
            GuestGalleryEnabled = true,
            HasPin = true,
            OfferedTemplateIds = ["panorama-four", "signature-strip"],
            DefaultTemplateId = "panorama-four",
            PhaseTimers = [new PhaseTimerSetting(PhaseTimerPhase.Template, 0)],
        };

        var draft = EventSetupDraft.FromRecord(Catalog, record);

        Assert.False(draft.IsNew);
        Assert.Equal(SetupSamples.HarbourId, draft.EventId);
        Assert.Equal(("Harbour gala", "2026-06-12", "Harbour Hall", "Ana Lindqvist", "Bring the props."), (draft.Name, draft.Date, draft.Venue, draft.Customer, draft.Notes));
        Assert.Equal(("fake-camera", "Fake camera", "fake-guest", "Fake guest"), (draft.CameraDeviceId, draft.CameraName, draft.GuestDisplayId, draft.GuestDisplayName));
        Assert.False(draft.Mirror);
        Assert.Equal(10, draft.CountdownSeconds);
        Assert.True(draft.GuestGalleryEnabled);
        Assert.True(draft.HasSavedPin);
        Assert.Equal(["signature-strip", "panorama-four"], draft.OfferedTemplateIds);
        Assert.Equal("panorama-four", draft.DefaultTemplateId);
        Assert.Equal(0, draft.TimerSeconds(PhaseTimerPhase.Template));
        Assert.Equal(PhaseTimerRules.Default(PhaseTimerPhase.Keepsake), draft.TimerSeconds(PhaseTimerPhase.Keepsake));
        Assert.False(draft.IsDirty);
    }

    [Fact]
    public void An_Event_without_optional_texts_loads_them_empty()
    {
        var record = SetupSamples.Event(SetupSamples.OlderId, venue: null) with { CustomerName = null, Notes = null };

        var draft = EventSetupDraft.FromRecord(Catalog, record);

        Assert.Equal(string.Empty, draft.Venue);
        Assert.Equal(string.Empty, draft.Customer);
        Assert.Equal(string.Empty, draft.Notes);
    }

    [Fact]
    public void The_record_is_trimmed_normalised_and_complete()
    {
        var draft = EventSetupDraft.CreateNew(Catalog, Today);
        draft.SetField(EventField.Name, "  Harbour gala  ");
        draft.SetField(EventField.Venue, "  ");
        draft.SetField(EventField.Customer, " Ana ");
        draft.SetField(EventField.Notes, string.Empty);
        draft.BindCamera("camera-1", "Camera");
        draft.BindGuestDisplay("display-1", "Display");
        draft.SetOfferedTemplates(["polka-keepsake", "signature-strip"]);
        draft.SetTimer(PhaseTimerPhase.Filters, 0);

        var record = draft.ToRecord(SetupSamples.HarbourId);

        Assert.Equal(SetupSamples.HarbourId, record.Id);
        Assert.Equal("Harbour gala", record.Name);
        Assert.Equal(Today, record.EventDate);
        Assert.Null(record.Venue);
        Assert.Equal("Ana", record.CustomerName);
        Assert.Null(record.Notes);
        Assert.Equal(("camera-1", "Camera", "display-1", "Display"), (record.CameraDeviceId, record.CameraName, record.GuestDisplayId, record.GuestDisplayName));
        Assert.Equal(EventRecord.NoPrinter, record.Printer);
        Assert.Equal(["signature-strip", "polka-keepsake"], record.OfferedTemplateIds);
        Assert.Equal(PhaseTimerRules.All, record.PhaseTimers.Select(timer => timer.Phase));
        Assert.Equal(0, record.PhaseTimers.Single(timer => timer.Phase == PhaseTimerPhase.Filters).Seconds);
    }

    [Fact]
    public void Marking_the_draft_saved_makes_it_an_update_and_clean_and_drops_the_pending_PIN()
    {
        var draft = EventSetupDraft.CreateNew(Catalog, Today);
        draft.SetField(EventField.Name, "Gala");
        draft.SetPin(SetupSamples.Pin, SetupSamples.Pin);

        draft.MarkSaved(SetupSamples.HarbourId, pinSaved: true);

        Assert.False(draft.IsNew);
        Assert.Equal(SetupSamples.HarbourId, draft.EventId);
        Assert.True(draft.HasSavedPin);
        Assert.False(draft.HasPendingPin);
        Assert.False(draft.IsDirty);
    }

    [Fact]
    public void Marking_the_row_created_keeps_the_draft_dirty_so_the_save_can_be_retried()
    {
        var draft = EventSetupDraft.CreateNew(Catalog, Today);
        draft.SetPin(SetupSamples.Pin, SetupSamples.Pin);

        draft.MarkCreated(SetupSamples.HarbourId);

        Assert.False(draft.IsNew);
        Assert.True(draft.HasPendingPin);
        Assert.True(draft.IsDirty);
    }

    [Fact]
    public void Missing_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => EventSetupDraft.CreateNew(null!, Today));
        Assert.Throws<ArgumentNullException>(() => EventSetupDraft.FromRecord(null!, SetupSamples.Event(SetupSamples.HarbourId)));
        Assert.Throws<ArgumentNullException>(() => EventSetupDraft.FromRecord(Catalog, null!));
        Assert.Throws<ArgumentNullException>(() => EventSetupDraft.CreateNew(Catalog, Today).SetField(EventField.Name, null!));
    }
}

public sealed class EventDeletionCounterTests
{
    private static async Task<(InMemoryEventStore Store, string EventId)> StoreWithEventAsync()
    {
        var store = new InMemoryEventStore();
        await store.OpenAsync(CancellationToken.None);
        await store.CreateEventAsync(SetupSamples.Event(SetupSamples.HarbourId), CancellationToken.None);
        return (store, SetupSamples.HarbourId);
    }

    [Fact]
    public async Task An_Event_with_no_Guest_Cycles_counts_zero()
    {
        var (store, id) = await StoreWithEventAsync();

        var counts = await EventDeletionCounter.CountAsync(store, id, TestContext.Current.CancellationToken);

        Assert.Equal((0, 0, 0), (counts.GuestCycles, counts.PhotoStrips, counts.Captures));
    }

    [Fact]
    public async Task Counts_are_every_cycle_every_Photo_Strip_and_every_kept_Capture()
    {
        var (store, id) = await StoreWithEventAsync();
        var token = TestContext.Current.CancellationToken;
        var finished = await store.StartGuestCycleAsync(id, token);
        var first = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(finished.Id, 1), token);
        var second = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(finished.Id, 2), token);
        await store.CommitPhotoStripAsync(StorageSamples.StripCommit(finished.Id, [first.Id, second.Id]), token);
        var abandoned = await store.StartGuestCycleAsync(id, token);
        var kept = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(abandoned.Id, 1), token);
        var dropped = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(abandoned.Id, 2), token);
        await store.SetCaptureStateAsync(dropped.Id, CaptureState.Discarded, token);
        await store.EndGuestCycleAsync(abandoned.Id, GuestCycleEndReason.StartedOver, token);
        await store.StartGuestCycleAsync(id, token);

        var counts = await EventDeletionCounter.CountAsync(store, id, token);

        Assert.Equal((3, 1, 3), (counts.GuestCycles, counts.PhotoStrips, counts.Captures));
        Assert.NotNull(kept);
    }

    [Fact]
    public async Task More_than_a_page_of_cycles_are_all_counted()
    {
        var (store, id) = await StoreWithEventAsync();
        var token = TestContext.Current.CancellationToken;
        for (var index = 0; index < EventDeletionCounter.PageSize + 5; index++)
        {
            await store.StartGuestCycleAsync(id, token);
        }

        var counts = await EventDeletionCounter.CountAsync(store, id, token);

        Assert.Equal(EventDeletionCounter.PageSize + 5, counts.GuestCycles);
    }

    [Fact]
    public async Task A_missing_store_or_Event_is_refused()
    {
        var (store, _) = await StoreWithEventAsync();

        await Assert.ThrowsAsync<ArgumentNullException>(() => EventDeletionCounter.CountAsync(null!, SetupSamples.HarbourId, TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<Exception>(() => EventDeletionCounter.CountAsync(store, "11111111-0000-4000-8000-000000000000", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void The_Event_folder_is_Data_Events_id_and_needs_both_parts()
    {
        Assert.Equal(Path.Combine(@"C:\FOTOHAVN\Data", "Events", "abc"), EventFolders.For(@"C:\FOTOHAVN\Data", "abc"));
        Assert.Throws<ArgumentException>(() => EventFolders.For(string.Empty, "abc"));
        Assert.Throws<ArgumentException>(() => EventFolders.For(@"C:\FOTOHAVN\Data", string.Empty));
    }
}
