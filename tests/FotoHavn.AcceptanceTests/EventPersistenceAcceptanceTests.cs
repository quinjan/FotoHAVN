using FotoHavn.Core;
using Xunit;

namespace FotoHavn.AcceptanceTests;

public sealed class EventPersistenceAcceptanceTests
{
    [Fact]
    public void Production_Event_identity_generator_creates_UUIDv7_values()
    {
        var identity = new UuidV7EventIdentityGenerator().Create();

        Assert.True(Guid.TryParse(identity.Value, out var value));
        Assert.Equal(7, value.Version);
    }

    [Fact]
    public async Task Event_validation_requires_name_Eligible_Camera_writable_storage_and_No_Printer()
    {
        var camera = new StubCamera(new AvailableCamera("camera-1", "Booth Camera", "Port 4"));
        var fileSystem = new RecordingFileSystem();
        var orchestrator = CreateOrchestrator(
            fileSystem,
            camera,
            new StubClock(DateTimeOffset.UnixEpoch),
            new StubIdentityGenerator(new EventId("event-1")));

        var state = await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);
        Assert.False(state.Setup!.CanSave);
        state = await orchestrator.ExecuteAsync(new ChangeEventName("Summer Party"), TestContext.Current.CancellationToken);
        Assert.False(state.Setup!.CanSave);
        state = await orchestrator.ExecuteAsync(new SelectCamera("camera-1"), TestContext.Current.CancellationToken);
        Assert.False(state.Setup!.CanSave);
        state = await orchestrator.ExecuteAsync(new SelectNoPrinter(), TestContext.Current.CancellationToken);
        Assert.True(state.Setup!.CanSave);

        var unwritable = new RecordingFileSystem { StorageReady = false };
        orchestrator = CreateOrchestrator(
            unwritable,
            camera,
            new StubClock(DateTimeOffset.UnixEpoch),
            new StubIdentityGenerator(new EventId("event-2")));
        await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);
        await CompleteValidSetupAsync(orchestrator);
        Assert.False(orchestrator.CurrentPresentation.Setup!.CanSave);
    }

    [Fact]
    public async Task Cancel_closes_a_clean_draft_but_requires_explicit_discard_for_a_dirty_draft()
    {
        var fileSystem = new RecordingFileSystem();
        var camera = new StubCamera(new AvailableCamera("camera-1", "Booth Camera", "Port 4"));
        var orchestrator = CreateOrchestrator(
            fileSystem,
            camera,
            new StubClock(DateTimeOffset.UnixEpoch),
            new StubIdentityGenerator(new EventId("unused")));

        await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);
        var cleanCancel = await orchestrator.ExecuteAsync(new CancelEventSetup(), TestContext.Current.CancellationToken);
        Assert.Null(cleanCancel.Setup);
        Assert.Empty(fileSystem.Events);

        await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new ChangeEventName("Unsaved Event"), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new SelectCamera("camera-1"), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new SelectNoPrinter(), TestContext.Current.CancellationToken);
        var confirmation = await orchestrator.ExecuteAsync(new CancelEventSetup(), TestContext.Current.CancellationToken);
        Assert.True(confirmation.Setup!.ShowsDiscardConfirmation);
        Assert.True(confirmation.Setup.IsDirty);

        var ignoredChange = await orchestrator.ExecuteAsync(
            new ChangeEventName("Bypass confirmation"),
            TestContext.Current.CancellationToken);
        var ignoredSave = await orchestrator.ExecuteAsync(
            new SaveAndCloseEventSetup(),
            TestContext.Current.CancellationToken);
        Assert.Equal("Unsaved Event", ignoredChange.Setup!.EventName);
        Assert.True(ignoredSave.Setup!.ShowsDiscardConfirmation);
        Assert.Empty(fileSystem.Events);

        var kept = await orchestrator.ExecuteAsync(new KeepEditingEventSetup(), TestContext.Current.CancellationToken);
        Assert.False(kept.Setup!.ShowsDiscardConfirmation);
        Assert.Equal("Unsaved Event", kept.Setup.EventName);

        await orchestrator.ExecuteAsync(new CancelEventSetup(), TestContext.Current.CancellationToken);
        var discarded = await orchestrator.ExecuteAsync(new DiscardEventSetupDraft(), TestContext.Current.CancellationToken);
        Assert.Null(discarded.Setup);
        Assert.Empty(fileSystem.Events);
    }

    [Fact]
    public async Task Launch_restores_only_saved_Events_with_distinct_affordances_in_last_saved_order()
    {
        var now = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        var fileSystem = new RecordingFileSystem();
        var setupCamera = new StubCamera(new AvailableCamera("camera-1", "Booth Camera", "Port 4"));
        var clock = new StubClock(now.AddHours(-1));
        var creator = CreateOrchestrator(
            fileSystem,
            setupCamera,
            clock,
            new StubIdentityGenerator(new EventId("event-1"), new EventId("event-2")));
        await creator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);
        await CompleteValidSetupAsync(creator, "Duplicate");
        await creator.ExecuteAsync(new SaveAndCloseEventSetup(), TestContext.Current.CancellationToken);
        clock.UtcNow = now;
        await creator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);
        await CompleteValidSetupAsync(creator, "Duplicate");
        await creator.ExecuteAsync(new SaveAndCloseEventSetup(), TestContext.Current.CancellationToken);

        var camera = new StubCamera();
        var orchestrator = CreateOrchestrator(
            fileSystem,
            camera,
            clock,
            new StubIdentityGenerator(new EventId("unused")));

        var launched = await orchestrator.ExecuteAsync(new LaunchApplication(), TestContext.Current.CancellationToken);

        Assert.Null(launched.ActiveEvent);
        Assert.Equal(0, camera.StartDiscoveryCount);
        Assert.Equal(0, camera.OpenCount);
        Assert.Collection(
            launched.EventTiles,
            tile => Assert.Equal(EventTileKind.NewEvent, tile.Kind),
            tile => AssertSavedTile(tile, "event-2", "Duplicate", now),
            tile => AssertSavedTile(tile, "event-1", "Duplicate", now.AddHours(-1)));

        var afterDelete = await orchestrator.ExecuteAsync(new DeleteSavedEvent(new EventId("event-2")), TestContext.Current.CancellationToken);
        Assert.DoesNotContain(afterDelete.EventTiles, tile => tile.EventId == new EventId("event-2"));
    }

    [Fact]
    public async Task Save_Close_publishes_the_saved_Event_only_after_the_atomic_commit_completes()
    {
        var fileSystem = new RecordingFileSystem
        {
            AllowSaveToComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var camera = new StubCamera(new AvailableCamera("camera-1", "Booth Camera", "Port 4"));
        var orchestrator = CreateOrchestrator(
            fileSystem,
            camera,
            new StubClock(DateTimeOffset.UnixEpoch),
            new StubIdentityGenerator(new EventId("event-1")));
        await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);
        await CompleteValidSetupAsync(orchestrator);

        var save = orchestrator.ExecuteAsync(new SaveAndCloseEventSetup(), TestContext.Current.CancellationToken);
        await fileSystem.SaveStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        Assert.False(save.IsCompleted);
        Assert.NotNull(orchestrator.CurrentPresentation.Setup);
        Assert.Empty(fileSystem.Events);

        fileSystem.AllowSaveToComplete.SetResult();
        var saved = await save;
        Assert.Null(saved.Setup);
        Assert.Contains(saved.EventTiles, tile => tile.EventId == new EventId("event-1"));
    }

    [Fact]
    public async Task Starting_a_saved_Event_opens_only_its_exact_Camera_and_does_not_resave_it()
    {
        var savedAt = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        var fileSystem = new RecordingFileSystem();
        fileSystem.Events.Add(Configuration("event-1", "Summer Party", savedAt));
        var camera = new StubCamera(
            new AvailableCamera("other-camera", "Booth Camera", "Port 7"),
            new AvailableCamera("camera-1", "Booth Camera", "Port 4"));
        var orchestrator = CreateOrchestrator(
            fileSystem,
            camera,
            new StubClock(savedAt.AddDays(1)),
            new StubIdentityGenerator(new EventId("unused")));
        await orchestrator.ExecuteAsync(new LaunchApplication(), TestContext.Current.CancellationToken);

        var confirmation = await orchestrator.ExecuteAsync(
            new StartSavedEvent(new EventId("event-1")),
            TestContext.Current.CancellationToken);
        var active = await orchestrator.ExecuteAsync(
            new ConfirmStartSavedEvent(),
            TestContext.Current.CancellationToken);

        Assert.Equal("Start “Summer Party”?", confirmation.StartEventConfirmation!.Prompt);
        Assert.Null(active.Setup);
        Assert.Equal(new EventId("event-1"), active.ActiveEvent!.Id);
        Assert.Equal("camera-1", camera.OpenedDeviceIds.Single().Value);
        Assert.Equal(savedAt, fileSystem.Events.Single().LastSavedAt);
    }

    [Fact]
    public async Task First_save_allocates_a_UUIDv7_retries_collisions_and_preserves_the_identity_on_edit()
    {
        var collision = new EventId("0198-0000-7000-8000-000000000001");
        var allocated = new EventId("0198-0000-7000-8000-000000000002");
        var identities = new StubIdentityGenerator(collision, allocated);
        var fileSystem = new RecordingFileSystem { CollidingIdentity = collision };
        var camera = new StubCamera(new AvailableCamera("camera-1", "Booth Camera", "Port 4"));
        var clock = new StubClock(new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero));
        var orchestrator = CreateOrchestrator(fileSystem, camera, clock, identities);

        var draft = await orchestrator.ExecuteAsync(new OpenNewEvent(), TestContext.Current.CancellationToken);
        Assert.Null(draft.Setup!.EventId);
        await CompleteValidSetupAsync(orchestrator);

        var saved = await orchestrator.ExecuteAsync(new SaveAndCloseEventSetup(), TestContext.Current.CancellationToken);

        Assert.Null(saved.ActiveEvent);
        Assert.Equal(1, camera.ReleaseCount);
        Assert.Equal([collision, allocated], identities.Generated);
        var created = Assert.Single(fileSystem.Events);
        Assert.Equal(allocated, created.Id);
        Assert.Equal(clock.UtcNow, created.CreatedAt);
        Assert.Equal(clock.UtcNow, created.LastSavedAt);
        Assert.Equal(PrinterChoice.NoPrinter, created.Printer);
        Assert.Equal("camera-1", created.Camera.DeviceId.Value);
        Assert.Equal("Booth Camera", created.Camera.DisplayName);

        clock.UtcNow = clock.UtcNow.AddHours(1);
        await orchestrator.ExecuteAsync(new OpenSavedEvent(allocated), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new ChangeEventName("Renamed Event"), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new SelectCamera("camera-1"), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new SaveAndCloseEventSetup(), TestContext.Current.CancellationToken);

        var updated = Assert.Single(fileSystem.Events);
        Assert.Equal(allocated, updated.Id);
        Assert.Equal(new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero), updated.CreatedAt);
        Assert.Equal(clock.UtcNow, updated.LastSavedAt);
        Assert.Equal(2, identities.Generated.Count);
        Assert.Equal(
            [EventSaveMode.CreateNew, EventSaveMode.CreateNew, EventSaveMode.UpdateExisting],
            fileSystem.SaveModes);
    }

    private static async Task CompleteValidSetupAsync(
        EventGuestCycleOrchestrator orchestrator,
        string name = "Summer Party")
    {
        await orchestrator.ExecuteAsync(new ChangeEventName(name), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new SelectCamera("camera-1"), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new SelectNoPrinter(), TestContext.Current.CancellationToken);
    }

    private static EventConfiguration Configuration(string id, string name, DateTimeOffset savedAt) =>
        new(
            new EventId(id),
            name,
            new CameraBinding("camera-1", "Booth Camera"),
            PrinterChoice.NoPrinter,
            savedAt.AddHours(-1),
            savedAt);

    private static void AssertSavedTile(
        EventTilePresentation tile,
        string id,
        string name,
        DateTimeOffset savedAt)
    {
        Assert.Equal(EventTileKind.SavedEvent, tile.Kind);
        Assert.Equal(new EventId(id), tile.EventId);
        Assert.Equal(name, tile.Label);
        Assert.Equal(savedAt, tile.LastSavedAt);
        Assert.True(tile.ShowsStart);
        Assert.True(tile.ShowsEdit);
        Assert.True(tile.ShowsDelete);
    }

    private static EventGuestCycleOrchestrator CreateOrchestrator(
        IEventFileSystem fileSystem,
        ICameraBoundary camera,
        IApplicationClock clock,
        IEventIdentityGenerator identities) =>
        new(fileSystem, camera, new StubCompositor(), clock, identities);

    private sealed class StubIdentityGenerator(params EventId[] identities) : IEventIdentityGenerator
    {
        private readonly Queue<EventId> remaining = new(identities);

        public List<EventId> Generated { get; } = [];

        public EventId Create()
        {
            var identity = remaining.Dequeue();
            Generated.Add(identity);
            return identity;
        }
    }

    private sealed class RecordingFileSystem : IEventFileSystem
    {
        public EventId? CollidingIdentity { get; init; }
        public bool StorageReady { get; init; } = true;
        public List<EventConfiguration> Events { get; } = [];
        public List<EventSaveMode> SaveModes { get; } = [];
        public TaskCompletionSource SaveStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? AllowSaveToComplete { get; init; }

        public Task<IReadOnlyList<SavedEventSummary>> LoadEventsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SavedEventSummary>>(Events
                .Select(item => new SavedEventSummary(item.Id, item.Name, item.LastSavedAt))
                .ToArray());

        public Task<EventConfiguration?> LoadEventAsync(EventId eventId, CancellationToken cancellationToken) =>
            Task.FromResult<EventConfiguration?>(Events.SingleOrDefault(item => item.Id == eventId));

        public Task<bool> ProbeStorageAsync(CancellationToken cancellationToken) => Task.FromResult(StorageReady);

        public async Task<EventSaveResult> SaveEventAtomicallyAsync(
            EventConfiguration configuration,
            EventSaveMode mode,
            CancellationToken cancellationToken)
        {
            SaveModes.Add(mode);
            SaveStarted.TrySetResult();
            if (AllowSaveToComplete is not null)
            {
                await AllowSaveToComplete.Task.WaitAsync(cancellationToken);
            }

            if (mode == EventSaveMode.CreateNew && configuration.Id == CollidingIdentity)
            {
                return EventSaveResult.IdentityCollision;
            }

            Events.RemoveAll(item => item.Id == configuration.Id);
            Events.Add(configuration);
            return EventSaveResult.Saved;
        }

        public Task DeleteEventAsync(EventId eventId, CancellationToken cancellationToken)
        {
            Events.RemoveAll(item => item.Id == eventId);
            return Task.CompletedTask;
        }
    }

    private sealed class StubCamera(params AvailableCamera[] cameras) : ICameraBoundary
    {
        public event EventHandler? AvailableCamerasChanged { add { } remove { } }
        public IReadOnlyList<AvailableCamera> AvailableCameras { get; } = cameras;
        public string? StreamId { get; private set; }
        public int StartDiscoveryCount { get; private set; }
        public int OpenCount { get; private set; }
        public List<CameraDeviceId> OpenedDeviceIds { get; } = [];
        public int ReleaseCount { get; private set; }
        public Task StartDiscoveryAsync(CancellationToken cancellationToken)
        {
            StartDiscoveryCount++;
            return Task.CompletedTask;
        }
        public Task<CameraOpenResult> OpenAsync(CameraDeviceId deviceId, CancellationToken cancellationToken)
        {
            OpenCount++;
            OpenedDeviceIds.Add(deviceId);
            StreamId = "stream-1";
            return Task.FromResult(CameraOpenResult.Ready);
        }
        public Task ReleaseAsync(CancellationToken cancellationToken)
        {
            ReleaseCount++;
            StreamId = null;
            return Task.CompletedTask;
        }
    }

    private sealed class StubClock(DateTimeOffset utcNow) : IApplicationClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubCompositor : IPhotoStripCompositor
    {
        public Task<PhotoStripCompositionResult> ComposeAsync(
            PhotoStripCompositionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PhotoStripCompositionResult(false, ReadOnlyMemory<byte>.Empty, 0, 0));
    }
}
