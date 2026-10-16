using FotoHavn.Core.Camera;
using FotoHavn.Core.Catalog;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Events;
using FotoHavn.Core.Events.Active;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.GuestCycles.Orchestration;
using FotoHavn.Core.Media;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.Events.Active;

/// <summary>What an <see cref="ActiveRig"/> is built with; everything defaults to a ready-to-start booth.</summary>
internal sealed class ActiveRigOptions
{
    public IEventStore? Store { get; init; }

    public FakeDisplayAdapter? Displays { get; init; }

    public ICameraAdapter? CameraPort { get; init; }

    /// <summary>Wraps the rig's own fake camera adapter with a double the brain is given instead.</summary>
    public Func<FakeCameraAdapter, ICameraAdapter>? CameraDecorator { get; init; }

    public IHostServices? Host { get; init; }

    public IWakeLock? WakeLock { get; init; }

    /// <summary>How the brain closes the app (a no-op by default).</summary>
    public IAppLifetime? AppLifetime { get; init; }

    /// <summary>The single-instance names the diagnostics report (none by default, as in the headless host).</summary>
    public Core.Hardening.SingleInstanceNames? SingleInstance { get; init; }

    public ICompositor? Compositor { get; init; }

    /// <summary>Replaces the brain's Capture encoder (the fake by default).</summary>
    public ICaptureEncoder? CaptureEncoder { get; init; }

    /// <summary>Gives the brain no Capture encoder at all, as a host that forgot one would.</summary>
    public bool NoCaptureEncoder { get; init; }

    /// <summary>The fake camera's frame rate; each frame rendered for a preview target costs real time, so long scenarios use fewer.</summary>
    public double CameraFramesPerSecond { get; init; } = 8;
    /// <summary>Wraps the fake clock the brain runs on (a <see cref="ScaledClock"/> in the E2E hosts).</summary>
    public Func<FakeClock, IClock>? BrainClock { get; init; }

    public bool Hooks { get; init; } = true;

    /// <summary>Changes the sample Event before it is saved.</summary>
    public Func<EventRecord, EventRecord>? Tweak { get; init; }

    /// <summary>Whether the sample Event gets an Operator PIN.</summary>
    public bool WithPin { get; init; } = true;

    /// <summary>Whether a second saved Event exists (for the list and the locked-row checks).</summary>
    public bool WithOtherEvent { get; init; }

    /// <summary>Whether the Guest Screen's UI is already connected (the headless host's client), so Start Event needn't wait for it.</summary>
    public bool GuestConnected { get; init; } = true;

    /// <summary>Whether the sample Event is saved before the brain starts (false when the store is made to fail opening).</summary>
    public bool Seed { get; init; } = true;
}

/// <summary>
/// A brain on a manual executor and a fake clock with a saved, startable Event, an Operator connection, and (by default) a Guest
/// connection. The brain's I/O completes on the thread pool, so <see cref="Pump"/> alternates running the executor with moving
/// the fake clock until a condition holds.
/// </summary>
internal sealed class ActiveRig : IDisposable
{
    public const string EventId = "11111111-1111-4111-8111-111111111111";
    public const string OtherEventId = "22222222-2222-4222-8222-222222222222";

    /// <summary>The Operator PIN of the sample Event. It must never appear in anything the brain sends.</summary>
    public const string Pin = "9137";

    public static readonly DateTimeOffset FixedStart = new(2026, 10, 13, 1, 30, 0, TimeSpan.Zero);

    private static readonly TimeSpan PumpStep = TimeSpan.FromMilliseconds(100);

    private ActiveRig(ActiveRigOptions options)
    {
        Clock = new FakeClock();
        Displays = options.Displays ?? FakeDisplayAdapter.CreateDefault();
        Camera = new FakeCameraAdapter(Clock, framesPerSecond: options.CameraFramesPerSecond);
        Store = options.Store ?? new InMemoryEventStore(Clock);
        Host = options.Host ?? new RecordingHostServices(Displays.PrimaryDisplayId);
        WakeLock = options.WakeLock ?? new NoOpWakeLock();
        BrainClock = options.BrainClock?.Invoke(Clock) ?? Clock;
        var dependencies = TestDependencies.Create(Executor, BrainClock, Displays, options.CameraPort ?? options.CameraDecorator?.Invoke(Camera) ?? Camera, Host, Store);
        Dependencies = dependencies with
        {
            WakeLock = WakeLock,
            AppLifetime = options.AppLifetime ?? new NoOpAppLifetime(),
            SingleInstance = options.SingleInstance,
            Compositor = options.Compositor ?? dependencies.Compositor,
            Logger = Logger,
            CaptureEncoder = options.NoCaptureEncoder ? null : options.CaptureEncoder ?? dependencies.CaptureEncoder,
        };
        Brain = new Brain(Dependencies, options.Hooks, null);
    }

    public ManualBrainExecutor Executor { get; } = new();

    public RecordingLogger Logger { get; } = new();

    public FakeClock Clock { get; }

    public IClock BrainClock { get; }

    public FakeDisplayAdapter Displays { get; }

    public FakeCameraAdapter Camera { get; }

    public IEventStore Store { get; }

    public IHostServices Host { get; }

    /// <summary>The host when it is the default recording one.</summary>
    public RecordingHostServices Recorded => (RecordingHostServices)Host;

    public IWakeLock WakeLock { get; }

    public BrainDependencies Dependencies { get; }

    public FakeCompositor FakeCompositor => (FakeCompositor)Dependencies.Compositor;

    public PreviewCache Previews => (PreviewCache)Dependencies.Previews;

    public Brain Brain { get; }

    public TestClient Operator { get; private set; } = null!;

    public TestClient? Guest { get; private set; }

    public static async Task<ActiveRig> CreateAsync(ActiveRigOptions? options = null)
    {
        options ??= new ActiveRigOptions();
        var rig = new ActiveRig(options);
        if (options.Seed)
        {
            await rig.SeedAsync(options);
        }
        rig.Run();
        rig.Operator = rig.Handshake(Role.Operator, keepWelcome: true);
        if (options.GuestConnected)
        {
            rig.Guest = rig.Handshake(Role.Guest, keepWelcome: true);
        }

        return rig;
    }

    /// <summary>The sample Event as Event setup would have saved it, bound to the fake camera and the fake guest display.</summary>
    public static EventRecord SampleEvent(string id = EventId, string name = "Studio open day") => new(
        id,
        name,
        new DateOnly(2026, 10, 13),
        "Harbour Hall",
        "Ana Lindqvist",
        null,
        "fake-camera",
        "Fake camera",
        FakeDisplayAdapter.DefaultGuest.Id,
        FakeDisplayAdapter.DefaultGuest.Name,
        EventRecord.NoPrinter,
        true,
        5,
        "signature-strip",
        "naturale-v1",
        true,
        ["signature-strip", "panorama-four"],
        ["naturale-v1", "monochrome-v1"],
        PhaseTimerRules.Defaults());

    private async Task SeedAsync(ActiveRigOptions options)
    {
        await Store.OpenAsync(CancellationToken.None);
        var record = options.Tweak?.Invoke(SampleEvent()) ?? SampleEvent();
        await Store.CreateEventAsync(record, CancellationToken.None);
        if (options.WithPin)
        {
            await Store.SetPinAsync(record.Id, Pin, CancellationToken.None);
        }

        if (options.WithOtherEvent)
        {
            await Store.CreateEventAsync(SampleEvent(OtherEventId, "Harbour gala") with { EventDate = new DateOnly(2026, 6, 12) }, CancellationToken.None);
        }
    }

    public TestClient Connect(Role role)
    {
        var client = new TestClient(role);
        client.Connection = Brain.Connect(role, client.Receive);
        Run();
        return client;
    }

    /// <summary>Connects and completes the handshake. The welcome is forgotten unless <paramref name="keepWelcome"/>.</summary>
    public TestClient Handshake(Role role, bool keepWelcome = false)
    {
        var client = Connect(role);
        client.Hello();
        Run();
        if (!keepWelcome)
        {
            client.Clear();
        }

        return client;
    }

    /// <summary>Connects a Guest UI after the Guest Screen opened, as the Windows host does.</summary>
    public TestClient ConnectGuest()
    {
        Guest = Handshake(Role.Guest, keepWelcome: true);
        return Guest;
    }

    /// <summary>The Guest begins the exit hold, holds it for three seconds, and completes it: the PIN dialog opens.</summary>
    public void HoldToExit()
    {
        Assert.Equal(CommandStatus.Accepted, GuestCommand("BeginExitHold").Status);
        Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(CommandStatus.Accepted, GuestCommand("CompleteExitHold").Status);
    }

    public CommandResultMessage SubmitPin(string digits) => GuestCommand("SubmitPin", $$"""{"digits":"{{digits}}"}""");

    /// <summary>The Guest holds to exit and enters the Event's PIN: the Event ends.</summary>
    public void OpenAndCompleteExitWithPin()
    {
        HoldToExit();
        Assert.Equal(CommandStatus.Accepted, SubmitPin(Pin).Status);
    }

    public void Run() => Executor.RunPending();

    /// <summary>Runs the executor and moves the fake clock in 50 ms steps until <paramref name="done"/> holds.</summary>
    public void Pump(Func<bool> done, int maxIterations = 6000)
    {
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            Run();
            if (done())
            {
                return;
            }

            Clock.Advance(PumpStep);
            Run();
            Thread.Sleep(1);
        }

        Assert.Fail("The condition never held.");
    }

    /// <summary>Moves the fake clock forward <paramref name="duration"/> in 50 ms steps, running the executor in between.</summary>
    public void Advance(TimeSpan duration)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += PumpStep)
        {
            Clock.Advance(elapsed + PumpStep <= duration ? PumpStep : duration - elapsed);
            Run();
        }
    }

    /// <summary>Starts the Event on the executor and returns its task without waiting.</summary>
    public Task<StartEventResult> BeginStart(string eventId = EventId)
    {
        Task<StartEventResult>? task = null;
        Executor.Post(() => task = Brain.TryStartEventAsync(eventId));
        Run();
        return task!;
    }

    /// <summary>Starts the Event and runs the brain until the start ended.</summary>
    public StartEventResult Start(string eventId = EventId)
    {
        var task = BeginStart(eventId);
        Pump(() => task.IsCompleted);
        return task.Result;
    }

    public StartEventResult StartAndExpectSuccess()
    {
        var result = Start();
        Assert.True(result.Succeeded, $"The start failed: {result.Failure}.");
        return result;
    }

    /// <summary>Sends a command and returns the brain's answer.</summary>
    public CommandResultMessage Send(TestClient client, string name, string payload = "{}")
    {
        var id = client.Command(name, payload);
        Run();
        return client.Messages.OfType<CommandResultMessage>().Single(message => message.Id == id);
    }

    public CommandResultMessage OperatorCommand(string name, string payload = "{}") => Send(Operator, name, payload);

    public CommandResultMessage GuestCommand(string name, string payload = "{}") => Send(Guest!, name, payload);

    public OperatorPresentation LatestOperator() => Latest(Operator).Operator!;

    public GuestPresentation LatestGuest() => Latest(Guest!).Guest!;

    /// <summary>The raw JSON of the latest presentation (or welcome) message a client received.</summary>
    public static string LatestRaw(TestClient client) => client.Raw.Last(raw => raw.Contains("\"presentation\"", StringComparison.Ordinal));

    private static (OperatorPresentation? Operator, GuestPresentation? Guest) Latest(TestClient client) => client.Messages
        .Select(message => message switch
        {
            OperatorPresentationMessage update => ((OperatorPresentation?)update.Presentation, (GuestPresentation?)null),
            OperatorWelcomeMessage welcome => (welcome.Presentation, null),
            GuestPresentationMessage update => (null, update.Presentation),
            GuestWelcomeMessage welcome => (null, welcome.Presentation),
            _ => (null, null),
        })
        .Last(pair => pair.Item1 is not null || pair.Item2 is not null);

    public DiagnosticsResult Diagnostics()
    {
        var id = Operator.Test(TestHookNames.GetDiagnostics);
        Run();
        var result = Assert.Single(Operator.Messages.OfType<TestResultMessage>(), message => message.Id == id);
        Assert.True(result.Ok);
        return ProtocolJson.Deserialize<DiagnosticsResult>(result.Result!.Value);
    }

    /// <summary>Every message either client received, joined: what the brain sent over the wire.</summary>
    public string EverythingSent() => string.Concat(Operator.Raw.Concat(Guest?.Raw ?? []));

    public void Dispose() => Brain.Dispose();
}
