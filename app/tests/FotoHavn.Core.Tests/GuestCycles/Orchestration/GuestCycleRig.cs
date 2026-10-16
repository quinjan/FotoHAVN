using FotoHavn.Catalog;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Catalog;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.GuestCycles.Orchestration;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

/// <summary>What a <see cref="GuestCycleRig"/> is built with; everything defaults to a started Event on the fake camera and display.</summary>
internal sealed class GuestRigOptions
{
    /// <summary>Changes the sample Event before it is saved (countdown, Phase Timers, offered Templates and Looks, mirror).</summary>
    public Func<EventRecord, EventRecord>? Tweak { get; init; }

    public IHostServices? Host { get; init; }

    public ICaptureEncoder? Encoder { get; init; }

    public bool NoEncoder { get; init; }

    /// <summary>The fake camera's frame rate (a frame a quarter second by default: enough for a 5 s countdown and cheap to render).</summary>
    public double FramesPerSecond { get; init; } = 4;

    public Func<FakeClock, IClock>? BrainClock { get; init; }

    public Func<FakeCameraAdapter, ICameraAdapter>? CameraDecorator { get; init; }

    /// <summary>How the brain closes the app (a no-op by default).</summary>
    public IAppLifetime? AppLifetime { get; init; }

    /// <summary>Whether the Event is started (the default); false leaves a saved Event for a test to start itself.</summary>
    public bool Start { get; init; } = true;
}

/// <summary>
/// A running Event on the brain's manual executor and fake clock, with the Guest and Operator connected, for the Guest Cycle's tests (M8
/// contract section 9.6). The brain's I/O completes on the thread pool, so every step <see cref="Settle"/>s before it looks.
/// </summary>
internal sealed class GuestCycleRig : IDisposable
{
    private static readonly TimeSpan DefaultStep = TimeSpan.FromMilliseconds(100);

    private GuestCycleRig(ActiveRig active, CycleStore store, InMemoryEventStore memory, ScriptedCompositor compositor)
    {
        Active = active;
        Store = store;
        Memory = memory;
        Compositor = compositor;
    }

    public ActiveRig Active { get; }

    public CycleStore Store { get; }

    public InMemoryEventStore Memory { get; }

    public ScriptedCompositor Compositor { get; }

    public FakeClock Clock => Active.Clock;

    public Brain Brain => Active.Brain;

    public FakeCaptureEncoder Encoder => (FakeCaptureEncoder)Active.Dependencies.CaptureEncoder!;

    public static async Task<GuestCycleRig> CreateAsync(GuestRigOptions? options = null)
    {
        options ??= new GuestRigOptions();
        var memory = new InMemoryEventStore();
        var store = new CycleStore(memory);
        var compositor = new ScriptedCompositor(new CatalogData());
        var active = await ActiveRig.CreateAsync(new ActiveRigOptions
        {
            Store = store,
            Compositor = compositor,
            Tweak = options.Tweak,
            Host = options.Host,
            CaptureEncoder = options.Encoder,
            NoCaptureEncoder = options.NoEncoder,
            BrainClock = options.BrainClock,
            CameraFramesPerSecond = options.FramesPerSecond,
            CameraDecorator = options.CameraDecorator,
            AppLifetime = options.AppLifetime,
        });
        var rig = new GuestCycleRig(active, store, memory, compositor);
        if (options.Start)
        {
            active.StartAndExpectSuccess();
            rig.Settle();
        }

        return rig;
    }

    /// <summary>The sample Event with its Phase Timers replaced (the Template, capture, review, Filters, and Keepsake seconds), 0 meaning off.</summary>
    public static Func<EventRecord, EventRecord> Timers(int template = 30, int capture = 30, int review = 45, int filters = 30, int keepsake = 12) => record => record with
    {
        PhaseTimers =
        [
            new PhaseTimerSetting(PhaseTimerPhase.Template, template),
            new PhaseTimerSetting(PhaseTimerPhase.PhotographsCapture, capture),
            new PhaseTimerSetting(PhaseTimerPhase.PhotographsReview, review),
            new PhaseTimerSetting(PhaseTimerPhase.Filters, filters),
            new PhaseTimerSetting(PhaseTimerPhase.Keepsake, keepsake),
            new PhaseTimerSetting(PhaseTimerPhase.GuestGallery, 60),
        ],
    };

    private bool Gated => Store.StartGate is not null || Store.EndGate is not null || Store.CommitGate is not null || Store.StripGate is not null || Store.SetStateGate is not null || Store.ProbeGate is not null || Compositor.ThumbnailGate is not null || Compositor.StripGate is not null;

    /// <summary>
    /// Runs the executor until the brain and the I/O it started have been quiet for a couple of milliseconds. Work still queued on the thread pool counts as
    /// busy, so a loaded machine that is slow to start it doesn't look quiet.
    /// </summary>
    public void Settle()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var lastBusy = TimeSpan.Zero;
        while (clock.Elapsed < TimeSpan.FromSeconds(30))
        {
            var ran = Active.Executor.RunPending();
            if (ran > 0 || Active.Executor.PendingCount > 0 || ThreadPool.PendingWorkItemCount > 0 || (!Gated && (Brain.OffExecutorWork > 0 || Store.InFlight > 0 || Compositor.InFlight > 0)))
            {
                lastBusy = clock.Elapsed;
            }
            else if (clock.Elapsed - lastBusy >= TimeSpan.FromMilliseconds(3))
            {
                return;
            }

            Thread.Yield();
        }
    }

    /// <summary>
    /// Settles until <paramref name="done"/> holds or <paramref name="budget"/> of real time is spent, without moving the clock. A settle ends on a few
    /// milliseconds of quiet, which a loaded machine can show while work is still queued on the thread pool and a gate holds the brain's own count up;
    /// a test that releases a gate, or reads a presentation beside one, waits on what it expects instead. True when it held.
    /// </summary>
    public bool SettleUntil(Func<bool> done, TimeSpan budget)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            Settle();
            if (done())
            {
                return true;
            }

            if (clock.Elapsed >= budget)
            {
                return false;
            }

            Thread.Yield();
        }
    }

    /// <summary>Moves the fake clock forward <paramref name="duration"/> in small steps, settling after each.</summary>
    public void Advance(TimeSpan duration, TimeSpan? step = null)
    {
        var size = step ?? DefaultStep;
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += size)
        {
            Clock.Advance(elapsed + size <= duration ? size : duration - elapsed);
            Settle();
        }
    }

    /// <summary>Advances the fake clock in steps, settling after each, until <paramref name="done"/> holds or <paramref name="budget"/> is spent; true when it held.</summary>
    public bool AdvanceUntil(Func<bool> done, TimeSpan budget, TimeSpan? step = null)
    {
        var size = step ?? TimeSpan.FromSeconds(1);
        Settle();
        for (var elapsed = TimeSpan.Zero; ; elapsed += size)
        {
            if (done())
            {
                return true;
            }

            if (elapsed >= budget)
            {
                return false;
            }

            Clock.Advance(size);
            Settle();
        }
    }

    public GuestPresentation View => LatestGuest(Active.Guest!);

    public OperatorPresentation OperatorView => LatestOperator(Active.Operator);

    public GuestSurface Surface => View.Surface;

    public PhotographsView Photographs => View.Photographs!;

    public FiltersView Filters => View.Filters!;

    public TemplateView Template => View.Template!;

    public static GuestPresentation LatestGuest(TestClient client) => ServerMessageReader.Read(LastPresentation(client)) switch
    {
        GuestPresentationMessage update => update.Presentation,
        GuestWelcomeMessage welcome => welcome.Presentation,
        _ => throw new InvalidOperationException("The client has no guest presentation."),
    };

    public static OperatorPresentation LatestOperator(TestClient client) => ServerMessageReader.Read(LastPresentation(client)) switch
    {
        OperatorPresentationMessage update => update.Presentation,
        OperatorWelcomeMessage welcome => welcome.Presentation,
        _ => throw new InvalidOperationException("The client has no operator presentation."),
    };

    private static string LastPresentation(TestClient client) => client.Raw.Last(raw => raw.Contains("\"presentation\"", StringComparison.Ordinal));

    /// <summary>Sends a guest command and returns the brain's answer once the I/O it started has settled.</summary>
    public CommandResultMessage Guest(string name, string payload = "{}")
    {
        var result = Active.GuestCommand(name, payload);
        Settle();
        return result;
    }

    /// <summary>Sends an operator command and returns the brain's answer once the I/O it started has settled.</summary>
    public CommandResultMessage Operator(string name, string payload = "{}")
    {
        var result = Active.OperatorCommand(name, payload);
        Settle();
        return result;
    }

    /// <summary>STEP INSIDE: the guest begins a cycle and the brain shows its first step.</summary>
    public void Begin()
    {
        Guest("BeginGuestCycle").ShouldBeAccepted();
        Assert.NotEqual(GuestSurface.Start, Surface);
    }

    /// <summary>Continues from the Template step to Photographs with the selected Template.</summary>
    public void ToPhotographs() => Guest("GoToStep", """{"step":"photographs"}""").ShouldBeAccepted();

    /// <summary>TAKE PHOTOGRAPHS, and the sequence runs to its end (the pause after the last flash included).</summary>
    public void TakeAll()
    {
        Guest("TakePhotographs").ShouldBeAccepted();
        Assert.True(AdvanceUntil(() => !Photographs.Sequence.Running, TimeSpan.FromMinutes(2), TimeSpan.FromMilliseconds(250)), "The sequence never ended.");
    }

    /// <summary>Begins a cycle, continues past the Template step, and takes every photograph: the guest is on the Arrange tab.</summary>
    public void ToArrange()
    {
        Begin();
        if (Surface == GuestSurface.Template)
        {
            ToPhotographs();
        }

        TakeAll();
        Assert.Equal(PhotographsTabName.Arrange, Photographs.Tab);
    }

    /// <summary>Continues from the Arrange tab to Filters (the Event offers several Looks).</summary>
    public void ToFilters()
    {
        Guest("GoToStep", """{"step":"filters"}""").ShouldBeAccepted();
        Assert.Equal(GuestSurface.Filters, Surface);
        Assert.True(AdvanceUntil(() => !Filters.Composing, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50)));
    }

    public DiagnosticsResult Diagnostics() => Active.Diagnostics();

    public GuestCycleDiagnostics CycleDiagnostics() => Diagnostics().GuestCycle!;

    /// <summary>The Guest Cycle's detail in the store, with its Captures and Photo Strip.</summary>
    public async Task<GuestCycleDetail> DetailAsync(string cycleId) =>
        (await Memory.GetGuestCycleAsync(cycleId, CancellationToken.None))!;

    public async Task<GuestCycleDetail> OnlyCycleAsync()
    {
        var page = await Memory.ListGuestCyclesAsync(ActiveRig.EventId, GuestCycleFilter.All, null, 50, CancellationToken.None);
        return await DetailAsync(Assert.Single(page.Items).Cycle.Id);
    }

    public void Dispose() => Active.Dispose();
}

/// <summary>Reads a command's answer in a test.</summary>
internal static class CommandResultAssertions
{
    public static CommandResultMessage ShouldBeAccepted(this CommandResultMessage result)
    {
        Assert.Equal(CommandStatus.Accepted, result.Status);
        return result;
    }

    public static CommandResultMessage ShouldBeRejected(this CommandResultMessage result, RejectReason reason = RejectReason.NotAvailable)
    {
        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(reason, result.Reason);
        return result;
    }
}
