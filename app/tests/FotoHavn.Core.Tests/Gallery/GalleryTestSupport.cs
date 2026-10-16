using System.Text;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Core.Tests.GuestCycles.Orchestration;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.Gallery;

/// <summary>
/// An <see cref="IEventStore"/> that passes every call to another and, for the gallery's reads, can wait on a gate, fail, or report a source as gone,
/// and records what it was asked. It counts the calls in flight so a test can settle: the brain's I/O completes on the thread pool.
/// </summary>
internal sealed class GalleryStore(IEventStore inner) : IEventStore
{
    private readonly object _gate = new();
    private readonly Queue<TaskCompletionSource> _listGates = new();
    private readonly Queue<TaskCompletionSource> _getGates = new();
    private readonly Queue<TaskCompletionSource> _markGates = new();
    private readonly Queue<TaskCompletionSource> _listResultGates = new();
    private int _inFlight;
    private int _waiting;
    private int _activeThumbnails;
    private int _peakThumbnails;

    public string DataRoot => inner.DataRoot;

    public int InFlight => Volatile.Read(ref _inFlight);

    /// <summary>How many calls wait on a gate now; while any does, the brain can't settle.</summary>
    public int Waiting => Volatile.Read(ref _waiting);

    /// <summary>How many <see cref="ListGuestCyclesAsync"/> calls fail next (with a locked-database error), then it works again.</summary>
    public int FailLists { get; set; }

    /// <summary>How many <see cref="GetGuestCycleAsync"/> calls fail next.</summary>
    public int FailGets { get; set; }

    /// <summary>When set, <see cref="MarkGuestCycleDeletingAsync"/> fails with this kind.</summary>
    public StorageErrorKind? FailMark { get; set; }

    /// <summary>Thumbnail sources whose file is gone: the store answers <see cref="StorageErrorKind.NotFound"/>.</summary>
    public HashSet<string> MissingThumbnails { get; } = new(StringComparer.Ordinal);

    /// <summary>Thumbnail sources the store fails on with a locked-database error.</summary>
    public HashSet<string> FailingThumbnails { get; } = new(StringComparer.Ordinal);

    /// <summary>Thumbnail sources whose request waits for a gate: a cold page filling in item by item.</summary>
    public Dictionary<string, TaskCompletionSource> GatedThumbnails { get; } = new(StringComparer.Ordinal);

    /// <summary>How long each thumbnail request takes, so a test can measure how many run at once.</summary>
    public TimeSpan ThumbnailDelay { get; set; }

    public int PeakThumbnailConcurrency => Volatile.Read(ref _peakThumbnails);

    public List<(string EventId, GuestCycleFilter Filter, string? Cursor)> ListCalls { get; } = [];

    public List<(string SourceId, int Width)> ThumbnailRequests { get; } = [];

    public List<string> GetCalls { get; } = [];

    public List<string> Marked { get; } = [];

    /// <summary>The next list call waits until the returned gate is completed.</summary>
    public TaskCompletionSource GateNextList() => Enqueue(_listGates);

    /// <summary>The next <see cref="GetGuestCycleAsync"/> call waits until the returned gate is completed.</summary>
    public TaskCompletionSource GateNextGet() => Enqueue(_getGates);

    /// <summary>The next list call reads the store at once but hands its page back only when the returned gate is completed: a page read before something changed.</summary>
    public TaskCompletionSource GateNextListResult() => Enqueue(_listResultGates);

    /// <summary>The next <see cref="MarkGuestCycleDeletingAsync"/> call waits until the returned gate is completed.</summary>
    public TaskCompletionSource GateNextMark() => Enqueue(_markGates);

    private TaskCompletionSource Enqueue(Queue<TaskCompletionSource> queue)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            queue.Enqueue(gate);
        }

        return gate;
    }

    private async Task<T> Tracked<T>(Queue<TaskCompletionSource>? gates, Func<Task<T>> call)
    {
        Interlocked.Increment(ref _inFlight);
        try
        {
            TaskCompletionSource? gate = null;
            if (gates is not null)
            {
                lock (_gate)
                {
                    gates.TryDequeue(out gate);
                }
            }

            if (gate is not null)
            {
                Interlocked.Increment(ref _waiting);
                try
                {
                    await gate.Task.ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _waiting);
                }
            }

            return await call().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    public Task<GuestCyclePage> ListGuestCyclesAsync(string eventId, GuestCycleFilter filter, string? cursor, int pageSize, CancellationToken ct)
    {
        lock (_gate)
        {
            ListCalls.Add((eventId, filter, cursor));
        }

        return Tracked(_listGates, async () =>
        {
            if (FailLists > 0)
            {
                FailLists--;
                throw new StorageException(StorageErrorKind.Locked, "Locked.");
            }

            var page = await inner.ListGuestCyclesAsync(eventId, filter, cursor, pageSize, ct).ConfigureAwait(false);
            TaskCompletionSource? after;
            lock (_gate)
            {
                _listResultGates.TryDequeue(out after);
            }

            if (after is not null)
            {
                Interlocked.Increment(ref _waiting);
                try
                {
                    await after.Task.ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _waiting);
                }
            }

            return page;
        });
    }

    public Task<GuestCycleDetail?> GetGuestCycleAsync(string guestCycleId, CancellationToken ct)
    {
        lock (_gate)
        {
            GetCalls.Add(guestCycleId);
        }

        return Tracked(_getGates, () =>
        {
            if (FailGets > 0)
            {
                FailGets--;
                return Task.FromException<GuestCycleDetail?>(new StorageException(StorageErrorKind.Locked, "Locked."));
            }

            return inner.GetGuestCycleAsync(guestCycleId, ct);
        });
    }

    public Task<string> GetThumbnailPathAsync(string sourceId, int width, CancellationToken ct)
    {
        lock (_gate)
        {
            ThumbnailRequests.Add((sourceId, width));
        }

        return Tracked(null, async () =>
        {
            var active = Interlocked.Increment(ref _activeThumbnails);
            lock (_gate)
            {
                _peakThumbnails = Math.Max(_peakThumbnails, active);
            }

            try
            {
                if (GatedThumbnails.TryGetValue(sourceId, out var gate))
                {
                    Interlocked.Increment(ref _waiting);
                    try
                    {
                        await gate.Task.ConfigureAwait(false);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _waiting);
                    }
                }

                if (ThumbnailDelay > TimeSpan.Zero)
                {
                    await Task.Delay(ThumbnailDelay, ct).ConfigureAwait(false);
                }

                if (MissingThumbnails.Contains(sourceId))
                {
                    throw new StorageException(StorageErrorKind.NotFound, "The file is gone.");
                }

                if (FailingThumbnails.Contains(sourceId))
                {
                    throw new StorageException(StorageErrorKind.Locked, "Locked.");
                }

                return await inner.GetThumbnailPathAsync(sourceId, width, ct).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _activeThumbnails);
            }
        });
    }

    public Task MarkGuestCycleDeletingAsync(string guestCycleId, CancellationToken ct)
    {
        lock (_gate)
        {
            Marked.Add(guestCycleId);
        }

        return Tracked(_markGates, async () =>
        {
            if (FailMark is { } kind)
            {
                throw new StorageException(kind, "The delete failed.");
            }

            await inner.MarkGuestCycleDeletingAsync(guestCycleId, ct).ConfigureAwait(false);
            return true;
        });
    }

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
}

/// <summary>What one seeded Guest Cycle should look like: how it ended, how many Captures it kept, and (completed ones always) its Photo Strip's size.</summary>
internal sealed record CycleSpec(
    GuestCycleEndReason? Reason = GuestCycleEndReason.Completed,
    int Captures = 4,
    int Discarded = 0,
    int StripWidth = 600,
    int StripHeight = 1800,
    string Template = "signature-strip",
    string Look = "naturale-v1");

/// <summary>A Guest Cycle the seed wrote, with the records the store holds for it.</summary>
internal sealed record SeededCycle(GuestCycleRecord Cycle, IReadOnlyList<CaptureRecord> Kept, PhotoStripRecord? Strip)
{
    public string Id => Cycle.Id;
}

/// <summary>Writes Guest Cycles into a store through its own commands, a minute apart on the store's clock, so ids and start times are deterministic.</summary>
internal static class GallerySeed
{
    public static readonly DateTimeOffset Start = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<List<SeededCycle>> AddAsync(IEventStore store, FakeClock clock, string eventId, IEnumerable<CycleSpec> specs)
    {
        var seeded = new List<SeededCycle>();
        foreach (var spec in specs)
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            var cycle = await store.StartGuestCycleAsync(eventId, Ct);
            var kept = new List<CaptureRecord>();
            for (var sequence = 1; sequence <= spec.Captures; sequence++)
            {
                kept.Add(await store.CommitCaptureAsync(Commit(cycle.Id, sequence), Ct));
            }

            for (var extra = 1; extra <= spec.Discarded; extra++)
            {
                var discarded = await store.CommitCaptureAsync(Commit(cycle.Id, spec.Captures + extra), Ct);
                await store.SetCaptureStateAsync(discarded.Id, CaptureState.Discarded, Ct);
            }

            PhotoStripRecord? strip = null;
            if (spec.Reason == GuestCycleEndReason.Completed)
            {
                strip = await store.CommitPhotoStripAsync(
                    new PhotoStripCommit(cycle.Id, spec.Template, spec.Look, [.. kept.AsEnumerable().Reverse().Select(capture => capture.Id)], Encoding.UTF8.GetBytes("strip " + cycle.Id), spec.StripWidth, spec.StripHeight),
                    Ct);
            }
            else if (spec.Reason is { } reason)
            {
                await store.EndGuestCycleAsync(cycle.Id, reason, Ct);
            }

            var ended = (await store.GetGuestCycleAsync(cycle.Id, Ct))!;
            seeded.Add(new SeededCycle(ended.Cycle, kept, strip));
        }

        return seeded;
    }

    private static CaptureCommit Commit(string cycleId, int sequence) =>
        new(cycleId, sequence, Encoding.UTF8.GetBytes($"capture {cycleId} {sequence}"), 1640, 1230, true, Start);

    /// <summary>The same spec <paramref name="count"/> times.</summary>
    public static IEnumerable<CycleSpec> Many(int count, CycleSpec? spec = null) => Enumerable.Repeat(spec ?? new CycleSpec(), count);
}

/// <summary>What a <see cref="GalleryRig"/> is built with.</summary>
internal sealed class GalleryRigOptions
{
    /// <summary>Whether the Event is started before the test begins. Seed Guest Cycles first (<see cref="GalleryRig.StartEvent"/>) when the Start link should see them.</summary>
    public bool Start { get; init; }

    public bool OtherEvent { get; init; }

    public Func<EventRecord, EventRecord>? Tweak { get; init; }
}

/// <summary>
/// An <see cref="ActiveRig"/> over a <see cref="GalleryStore"/> that holds an in-memory store the tests seed Guest Cycles into: a saved Event with
/// the Operator and Guest connected, started or not. The brain's I/O completes on the thread pool, so every command <see cref="Settle"/>s.
/// </summary>
internal sealed class GalleryRig : IDisposable
{
    private static readonly TimeSpan DefaultStep = TimeSpan.FromMilliseconds(100);

    private GalleryRig(ActiveRig active, GalleryStore store, InMemoryEventStore memory, FakeClock storeClock)
    {
        Active = active;
        Store = store;
        Memory = memory;
        StoreClock = storeClock;
    }

    public ActiveRig Active { get; }

    public GalleryStore Store { get; }

    public InMemoryEventStore Memory { get; }

    /// <summary>The clock the store stamps Guest Cycles with; <see cref="GallerySeed"/> moves it.</summary>
    public FakeClock StoreClock { get; }

    public Brain Brain => Active.Brain;

    public static string EventId => ActiveRig.EventId;

    public static async Task<GalleryRig> CreateAsync(GalleryRigOptions? options = null)
    {
        options ??= new GalleryRigOptions();
        var clock = new FakeClock(GallerySeed.Start);
        var memory = new InMemoryEventStore(clock) { DataRoot = @"C:\FOTOHAVN-test\Data" };
        var store = new GalleryStore(memory);
        var active = await ActiveRig.CreateAsync(new ActiveRigOptions { Store = store, Tweak = options.Tweak, WithOtherEvent = options.OtherEvent });
        var rig = new GalleryRig(active, store, memory, clock);
        rig.Settle();
        if (options.Start)
        {
            rig.StartEvent();
        }

        return rig;
    }

    /// <summary>Starts the saved Event (the Start link and the panel count read what the store holds now).</summary>
    public void StartEvent()
    {
        Active.StartAndExpectSuccess();
        Settle();
    }

    public Task<List<SeededCycle>> SeedAsync(params CycleSpec[] specs) => GallerySeed.AddAsync(Memory, StoreClock, EventId, specs);

    public Task<List<SeededCycle>> SeedAsync(string eventId, params CycleSpec[] specs) => GallerySeed.AddAsync(Memory, StoreClock, eventId, specs);

    public Task<List<SeededCycle>> SeedManyAsync(int count, CycleSpec? spec = null) => GallerySeed.AddAsync(Memory, StoreClock, EventId, GallerySeed.Many(count, spec));

    /// <summary>
    /// Runs the executor until the brain and the I/O it started have been quiet for a couple of milliseconds. Gated calls aren't waited for, so while
    /// one waits the rest of the I/O (the other thumbnails of a page, say) is judged by the store's own count of calls in flight and a longer quiet.
    /// Work still queued on the thread pool counts as busy, so a loaded machine that is slow to start it doesn't look quiet.
    /// </summary>
    public void Settle()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var lastBusy = TimeSpan.Zero;
        while (clock.Elapsed < TimeSpan.FromSeconds(30))
        {
            var gated = Store.Waiting > 0;
            var ran = Active.Executor.RunPending();
            var working = gated ? Store.InFlight > Store.Waiting : Brain.OffExecutorWork > 0 || Store.InFlight > 0;
            if (ran > 0 || Active.Executor.PendingCount > 0 || working || ThreadPool.PendingWorkItemCount > 0)
            {
                lastBusy = clock.Elapsed;
            }
            else if (clock.Elapsed - lastBusy >= TimeSpan.FromMilliseconds(gated ? 40 : 3))
            {
                return;
            }

            Thread.Yield();
        }
    }

    /// <summary>
    /// Settles until <paramref name="done"/> holds or <paramref name="budget"/> of real time is spent, without moving the clock. A settle ends on a few
    /// milliseconds of quiet, which a loaded machine can show while a released gate's work is still queued on the thread pool; a test that releases a gate
    /// waits on what it expects instead. True when it held.
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

    /// <summary>Moves the brain's fake clock forward <paramref name="duration"/> in small steps, settling after each.</summary>
    public void Advance(TimeSpan duration, TimeSpan? step = null)
    {
        var size = step ?? DefaultStep;
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += size)
        {
            Active.Clock.Advance(elapsed + size <= duration ? size : duration - elapsed);
            Settle();
        }
    }

    public CommandResultMessage Operator(string name, string payload = "{}")
    {
        var result = Active.OperatorCommand(name, payload);
        Settle();
        return result;
    }

    public CommandResultMessage Guest(string name, string payload = "{}")
    {
        var result = Active.GuestCommand(name, payload);
        Settle();
        return result;
    }

    public CommandResultMessage OpenGallery(string? eventId = null) => Operator("OpenGallery", Json("eventId", eventId ?? EventId));

    public CommandResultMessage LoadMore(string cursor) => Operator("LoadMoreGuestCycles", Json("cursor", cursor));

    public CommandResultMessage OpenCycle(string cycleId) => Operator("OpenGuestCycle", Json("guestCycleId", cycleId));

    public CommandResultMessage DeleteCycle(string cycleId) => Operator("DeleteGuestCycle", Json("guestCycleId", cycleId));

    public CommandResultMessage OpenGuestGallery() => Guest("OpenGuestGallery");

    public CommandResultMessage OpenStrip(string cycleId) => Guest("OpenGuestStrip", Json("guestCycleId", cycleId));

    public CommandResultMessage LoadMoreStrips(string cursor) => Guest("LoadMoreGuestStrips", Json("cursor", cursor));

    public static string Json(string name, string value) => $$"""{"{{name}}":"{{value}}"}""";

    public OperatorPresentation OperatorView => GuestCycleRig.LatestOperator(Active.Operator);

    public GuestPresentation GuestView => GuestCycleRig.LatestGuest(Active.Guest!);

    public EventGalleryView Gallery => OperatorView.EventGallery!;

    public GuestCycleDetailView Detail => OperatorView.GuestCycleDetail!;

    public GuestGalleryView Strips => GuestView.GuestGallery!;

    public string[] ItemIds => [.. Gallery.Items.Select(item => item.GuestCycleId)];

    public string[] StripIds => [.. Strips.Strips.Select(strip => strip.GuestCycleId)];

    public DiagnosticsResult Diagnostics() => Active.Diagnostics();

    /// <summary>Every presentation (and welcome) either screen received, joined: what the brain showed, without the diagnostics a hook returned.</summary>
    public string PresentationsSent() => string.Concat(Active.Operator.Raw.Concat(Active.Guest?.Raw ?? []).Where(raw => raw.Contains("\"presentation\"", StringComparison.Ordinal)));

    public void Dispose() => Active.Dispose();
}

/// <summary>Reads views in assertions.</summary>
internal static class GalleryAssert
{
    /// <summary>The ids of cycles, newest first (the order the gallery lists them in).</summary>
    public static string[] NewestFirst(IEnumerable<SeededCycle> cycles) => [.. cycles.OrderByDescending(cycle => cycle.Cycle.StartedAt).ThenByDescending(cycle => cycle.Id, StringComparer.Ordinal).Select(cycle => cycle.Id)];
}
