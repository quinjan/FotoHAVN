using System.Text.Json;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Catalog;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Events;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Storage;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.Events.Setup;

/// <summary>Sample Events for the Event list and Event setup tests. Ids are valid store ids; the camera and display are the fakes' own.</summary>
internal static class SetupSamples
{
    public const string HarbourId = "22222222-2222-4222-8222-222222222222";
    public const string WeddingId = "33333333-3333-4333-8333-333333333333";
    public const string OlderId = "11111111-1111-4111-8111-111111111111";
    public const string Pin = "739104";

    public static EventRecord Event(string id, string name = "Harbour gala", DateOnly? date = null, string? venue = "Harbour Hall") =>
        StorageSamples.Event(id, name, date ?? new DateOnly(2026, 6, 12), venue) with
        {
            CameraDeviceId = FakeCameraAdapter.DefaultCamera.DeviceId,
            CameraName = FakeCameraAdapter.DefaultCamera.Name,
            GuestDisplayId = FakeDisplayAdapter.DefaultGuest.Id,
            GuestDisplayName = FakeDisplayAdapter.DefaultGuest.Name,
            PhaseTimers = PhaseTimerRules.Defaults(),
        };

    /// <summary>A store with three Events (newest date first: wedding, harbour, older); the wedding has one finished Guest Cycle.</summary>
    public static async Task<InMemoryEventStore> SeededStoreAsync(IClock? clock = null)
    {
        var store = new InMemoryEventStore(clock);
        await store.OpenAsync(CancellationToken.None);
        await store.CreateEventAsync(Event(OlderId, "Older party", new DateOnly(2026, 3, 1), venue: null), CancellationToken.None);
        await store.CreateEventAsync(Event(WeddingId, "Lindqvist wedding", new DateOnly(2026, 10, 24)), CancellationToken.None);
        await store.CreateEventAsync(Event(HarbourId), CancellationToken.None);
        return store;
    }

    public static async Task<EventRecord> LoadAsync(IEventStore store, string id) =>
        (await store.GetEventAsync(id, CancellationToken.None))!;
}

/// <summary>An Event store that runs a hook before the calls Event setup and the Event list make, so a test can fail, delay, or gate one by name.</summary>
internal sealed class HookedStore(InMemoryEventStore inner) : IEventStore
{
    public Func<string, Task>? Before { get; set; }

    public InMemoryEventStore Inner => inner;

    public string DataRoot => inner.DataRoot;

    public Task<StorageOpenResult> OpenAsync(CancellationToken ct) => inner.OpenAsync(ct);

    public Task<IReadOnlyList<EventSummary>> ListEventsAsync(CancellationToken ct) => Run(nameof(ListEventsAsync), () => inner.ListEventsAsync(ct));

    public Task<EventRecord?> GetEventAsync(string eventId, CancellationToken ct) => Run(nameof(GetEventAsync), () => inner.GetEventAsync(eventId, ct));

    public Task CreateEventAsync(EventRecord record, CancellationToken ct) => Run(nameof(CreateEventAsync), () => inner.CreateEventAsync(record, ct));

    public Task UpdateEventAsync(EventRecord record, CancellationToken ct) => Run(nameof(UpdateEventAsync), () => inner.UpdateEventAsync(record, ct));

    public Task SetPinAsync(string eventId, string pin, CancellationToken ct) => Run(nameof(SetPinAsync), () => inner.SetPinAsync(eventId, pin, ct));

    public Task<bool> VerifyPinAsync(string eventId, string pin, CancellationToken ct) => inner.VerifyPinAsync(eventId, pin, ct);

    public Task MarkEventDeletingAsync(string eventId, CancellationToken ct) => Run(nameof(MarkEventDeletingAsync), () => inner.MarkEventDeletingAsync(eventId, ct));

    public Task<GuestCycleRecord> StartGuestCycleAsync(string eventId, CancellationToken ct) => inner.StartGuestCycleAsync(eventId, ct);

    public Task EndGuestCycleAsync(string guestCycleId, GuestCycleEndReason reason, CancellationToken ct) => inner.EndGuestCycleAsync(guestCycleId, reason, ct);

    public Task<CaptureRecord> CommitCaptureAsync(CaptureCommit commit, CancellationToken ct) => inner.CommitCaptureAsync(commit, ct);

    public Task SetCaptureStateAsync(string captureId, CaptureState state, CancellationToken ct) => inner.SetCaptureStateAsync(captureId, state, ct);

    public Task<PhotoStripRecord> CommitPhotoStripAsync(PhotoStripCommit commit, CancellationToken ct) => inner.CommitPhotoStripAsync(commit, ct);

    public Task MarkGuestCycleDeletingAsync(string guestCycleId, CancellationToken ct) => inner.MarkGuestCycleDeletingAsync(guestCycleId, ct);

    public Task<GuestCyclePage> ListGuestCyclesAsync(string eventId, GuestCycleFilter filter, string? cursor, int pageSize, CancellationToken ct) =>
        Run(nameof(ListGuestCyclesAsync), () => inner.ListGuestCyclesAsync(eventId, filter, cursor, pageSize, ct));

    public Task<GuestCycleDetail?> GetGuestCycleAsync(string guestCycleId, CancellationToken ct) => inner.GetGuestCycleAsync(guestCycleId, ct);

    public Task<string> GetThumbnailPathAsync(string sourceId, int width, CancellationToken ct) => inner.GetThumbnailPathAsync(sourceId, width, ct);

    public Task DrainAsync(CancellationToken ct) => inner.DrainAsync(ct);

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    private async Task<T> Run<T>(string method, Func<Task<T>> call)
    {
        if (Before is { } before)
        {
            await before(method).ConfigureAwait(false);
        }

        return await call().ConfigureAwait(false);
    }

    private async Task Run(string method, Func<Task> call)
    {
        if (Before is { } before)
        {
            await before(method).ConfigureAwait(false);
        }

        await call().ConfigureAwait(false);
    }
}

/// <summary>A compositor that waits for <see cref="Gate"/> before it composes, so a test can close setup first.</summary>
internal sealed class GatedCompositor : ICompositor
{
    private readonly FotoHavn.Core.Fakes.FakeCompositor _inner = new(new FotoHavn.Catalog.CatalogData());

    public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<CompositionResult> ComposeAsync(CompositionRequest request, CancellationToken ct)
    {
        await Gate.Task.ConfigureAwait(false);
        return await _inner.ComposeAsync(request, ct).ConfigureAwait(false);
    }

    public Task<CompositionResult> ComposeLookThumbnailAsync(CompositionPhoto photo, double aspectRatio, string lookId, int width, CancellationToken ct) =>
        _inner.ComposeLookThumbnailAsync(photo, aspectRatio, lookId, width, ct);
}

/// <summary>A brain with fake displays and camera, a fake clock, and a handshaken Operator, driven by wire commands.</summary>
internal sealed class SetupRig : IDisposable
{
    public SetupRig(
        IEventStore? store = null,
        IEnumerable<AvailableCamera>? cameras = null,
        FakeDisplayAdapter? displays = null,
        ICompositor? compositor = null,
        bool hooks = false)
    {
        Clock = new FakeClock(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
        Fake = new FakeCameraAdapter(Clock, cameras: cameras);
        Store = store ?? new InMemoryEventStore(Clock);
        Harness = new BrainHarness(
            hooks: hooks,
            hostHooks: hooks ? new FakeHostHookPort() : null,
            store: Store,
            camera: Fake,
            clock: Clock,
            displays: displays,
            compositor: compositor);
        Operator = Harness.Connect(Role.Operator);
        Operator.Hello();
        Harness.Run();
    }

    public FakeClock Clock { get; }

    public FakeCameraAdapter Fake { get; }

    public IEventStore Store { get; }

    public BrainHarness Harness { get; }

    public TestClient Operator { get; }

    public Brain Brain => Harness.Brain;

    public RecordingHostServices Host => Harness.Host;

    /// <summary>The latest operator presentation the page received.</summary>
    public OperatorPresentation Latest => Operator.Messages
        .Select(message => message switch
        {
            OperatorPresentationMessage update => update.Presentation,
            OperatorWelcomeMessage welcome => welcome.Presentation,
            _ => null,
        })
        .Last(presentation => presentation is not null)!;

    public EventSetupView Setup => Latest.EventSetup ?? throw new InvalidOperationException($"The surface is {Latest.Surface}.");

    public EventListView List => Latest.EventList ?? throw new InvalidOperationException($"The surface is {Latest.Surface}.");

    /// <summary>The raw JSON of the latest presentation message.</summary>
    public string LatestJson => Operator.Raw.Last(json => ServerMessageReader.Read(json) is OperatorPresentationMessage or OperatorWelcomeMessage);

    /// <summary>Sends a command and runs the brain; returns its answer.</summary>
    public CommandResultMessage Send(string name, string payload = "{}")
    {
        var id = Operator.Command(name, payload);
        Harness.Run();
        return Operator.Messages.OfType<CommandResultMessage>().Single(result => result.Id == id);
    }

    public CommandResultMessage Accepts(string name, string payload = "{}")
    {
        var result = Send(name, payload);
        Assert.Equal(CommandStatus.Accepted, result.Status);
        return result;
    }

    public void Rejects(RejectReason reason, string name, string payload = "{}")
    {
        var result = Send(name, payload);
        Assert.Equal((CommandStatus.Rejected, reason), (result.Status, result.Reason));
    }

    public void Field(string field, string value) =>
        Accepts("ChangeEventField", $$"""{"field":"{{field}}","value":{{JsonSerializer.Serialize(value)}}}""");

    public void Timer(string phase, int seconds) => Accepts("SetPhaseTimer", $$"""{"phase":"{{phase}}","seconds":{{seconds}}}""");

    public void Pin(string pin, string confirmation) =>
        Accepts("SetPin", $$"""{"pin":{{JsonSerializer.Serialize(pin)}},"confirmation":{{JsonSerializer.Serialize(confirmation)}}}""");

    public void SelectCamera(string deviceId) => Accepts("SelectCamera", $$"""{"deviceId":{{JsonSerializer.Serialize(deviceId)}}}""");

    public void SelectDisplay(string displayId) => Accepts("SelectGuestDisplay", $$"""{"displayId":{{JsonSerializer.Serialize(displayId)}}}""");

    public void WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            Harness.Run();
            if (condition())
            {
                return;
            }

            Assert.True(DateTime.UtcNow < deadline, "The condition never held.");
            Thread.Sleep(2);
        }
    }

    /// <summary>Opens a new Event and waits for the blank Template previews.</summary>
    public void OpenNew()
    {
        Accepts("OpenNewEvent");
        WaitForPreviews();
    }

    /// <summary>Opens a saved Event for editing and waits for it and its blank Template previews.</summary>
    public void OpenEdit(string eventId)
    {
        Accepts("EditEvent", $$"""{"eventId":"{{eventId}}"}""");
        WaitUntil(() => Latest.Surface == OperatorSurface.EventSetup);
        WaitForPreviews();
    }

    public void WaitForPreviews() => WaitUntil(() => Latest.EventSetup is { } setup && setup.Templates.All(template => template.BlankPreviewKey is not null));

    /// <summary>Moves the fake clock forward in camera-poll steps and lets the brain run.</summary>
    public void Advance(TimeSpan duration)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += Brain.FreshnessPollInterval)
        {
            Clock.Advance(Brain.FreshnessPollInterval);
            Harness.Run();
        }
    }

    /// <summary><c>test.getDiagnostics</c> (the rig needs <c>hooks: true</c>).</summary>
    public DiagnosticsResult Diagnostics()
    {
        var id = Operator.Test(TestHookNames.GetDiagnostics);
        WaitUntil(() => Operator.Messages.OfType<TestResultMessage>().Any(message => message.Id == id));
        var result = Operator.Messages.OfType<TestResultMessage>().Single(message => message.Id == id);
        return ProtocolJson.Deserialize<DiagnosticsResult>(result.Result!.Value);
    }

    /// <summary>Runs <paramref name="work"/> on the brain's executor, then lets it finish.</summary>
    public void OnBrain(Action work)
    {
        Harness.Executor.Post(work);
        Harness.Run();
    }

    /// <summary>Makes <paramref name="record"/> the running Event (the scaffold's stand-in for Start Event).</summary>
    public void StartRunning(EventRecord record, bool singleScreen = false)
    {
        var guest = FakeDisplayAdapter.DefaultGuest;
        Brain.ShowActiveEvent(new FotoHavn.Core.Events.Active.ActiveEventState(record, Clock.UtcNow, guest, singleScreen));
        Harness.Run();
    }

    public void Dispose() => Harness.Dispose();
}
