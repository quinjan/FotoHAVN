using FotoHavn.Core.Camera;
using FotoHavn.Core.Catalog;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

/// <summary>
/// An <see cref="IEventStore"/> that passes every call to another and can make the Guest Cycle's calls fail or wait, and counts how many are
/// in flight so a test can settle: the brain's I/O completes on the thread pool.
/// </summary>
internal sealed class CycleStore(IEventStore inner) : IEventStore
{
    private int _inFlight;

    /// <summary>Makes <see cref="StartGuestCycleAsync"/> fail (STEP INSIDE's storage failure).</summary>
    public bool FailStartCycle { get; set; }

    /// <summary>How many <see cref="CommitCaptureAsync"/> calls fail next, then they succeed again.</summary>
    public int FailCaptureCommits { get; set; }

    /// <summary>Makes <see cref="CommitPhotoStripAsync"/> fail.</summary>
    public bool FailStripCommit { get; set; }

    /// <summary>Makes <see cref="SetCaptureStateAsync"/> fail.</summary>
    public bool FailSetState { get; set; }

    /// <summary>Makes <see cref="EndGuestCycleAsync"/> fail.</summary>
    public bool FailEnd { get; set; }

    /// <summary>Makes <see cref="ProbeWritableAsync"/> fail.</summary>
    public bool FailProbe { get; set; }

    /// <summary>While set, a Capture commit completes only when the gate does.</summary>
    public TaskCompletionSource? CommitGate { get; set; }

    /// <summary>While set, <see cref="StartGuestCycleAsync"/> completes only when the gate does.</summary>
    public TaskCompletionSource? StartGate { get; set; }

    /// <summary>While set, <see cref="CommitPhotoStripAsync"/> completes only when the gate does.</summary>
    public TaskCompletionSource? StripGate { get; set; }

    public List<(string CycleId, GuestCycleEndReason Reason)> Ended { get; } = [];

    public int InFlight => Volatile.Read(ref _inFlight);

    public string DataRoot => inner.DataRoot;

    private Task<T> Track<T>(Func<Task<T>> call)
    {
        Interlocked.Increment(ref _inFlight);
        var task = call();
        task.ContinueWith(_ => Interlocked.Decrement(ref _inFlight), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return task;
    }

    private static async Task<T> After<T>(TaskCompletionSource gate, Func<Task<T>> call)
    {
        await gate.Task.ConfigureAwait(false);
        return await call().ConfigureAwait(false);
    }

    /// <summary>While set, <see cref="ProbeWritableAsync"/> completes only when the gate does.</summary>
    public TaskCompletionSource? ProbeGate { get; set; }

    public Task ProbeWritableAsync(CancellationToken ct)
    {
        if (FailProbe)
        {
            return Task.FromException(new StorageException(StorageErrorKind.NotWritable, "Not writable."));
        }

        return ProbeGate is { } gate ? gate.Task.ContinueWith(_ => inner.ProbeWritableAsync(ct), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap() : inner.ProbeWritableAsync(ct);
    }

    public Task DrainAsync(CancellationToken ct) => inner.DrainAsync(ct);

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    public Task<StorageOpenResult> OpenAsync(CancellationToken ct) => inner.OpenAsync(ct);

    public Task<IReadOnlyList<EventSummary>> ListEventsAsync(CancellationToken ct) => inner.ListEventsAsync(ct);

    public Task<EventRecord?> GetEventAsync(string eventId, CancellationToken ct) => inner.GetEventAsync(eventId, ct);

    public Task CreateEventAsync(EventRecord record, CancellationToken ct) => inner.CreateEventAsync(record, ct);

    public Task UpdateEventAsync(EventRecord record, CancellationToken ct) => inner.UpdateEventAsync(record, ct);

    public Task SetPinAsync(string eventId, string pin, CancellationToken ct) => inner.SetPinAsync(eventId, pin, ct);

    public Task<bool> VerifyPinAsync(string eventId, string pin, CancellationToken ct) => inner.VerifyPinAsync(eventId, pin, ct);

    public Task MarkEventDeletingAsync(string eventId, CancellationToken ct) => inner.MarkEventDeletingAsync(eventId, ct);

    public Task<GuestCycleRecord> StartGuestCycleAsync(string eventId, CancellationToken ct) => Track(() =>
    {
        if (FailStartCycle)
        {
            return Task.FromException<GuestCycleRecord>(new StorageException(StorageErrorKind.DiskFull, "Disk full."));
        }

        return StartGate is { } gate ? After(gate, () => inner.StartGuestCycleAsync(eventId, ct)) : inner.StartGuestCycleAsync(eventId, ct);
    });

    public Task EndGuestCycleAsync(string guestCycleId, GuestCycleEndReason reason, CancellationToken ct)
    {
        lock (Ended)
        {
            Ended.Add((guestCycleId, reason));
        }

        return FailEnd ? Task.FromException(new StorageException(StorageErrorKind.Locked, "Locked.")) : inner.EndGuestCycleAsync(guestCycleId, reason, ct);
    }

    public Task<CaptureRecord> CommitCaptureAsync(CaptureCommit commit, CancellationToken ct) => Track(() =>
    {
        if (FailCaptureCommits > 0)
        {
            FailCaptureCommits--;
            return Task.FromException<CaptureRecord>(new StorageException(StorageErrorKind.DiskFull, "Disk full."));
        }

        return CommitGate is { } gate ? After(gate, () => inner.CommitCaptureAsync(commit, ct)) : inner.CommitCaptureAsync(commit, ct);
    });

    public Task SetCaptureStateAsync(string captureId, CaptureState state, CancellationToken ct) =>
        FailSetState ? Task.FromException(new StorageException(StorageErrorKind.Locked, "Locked.")) : inner.SetCaptureStateAsync(captureId, state, ct);

    public Task<PhotoStripRecord> CommitPhotoStripAsync(PhotoStripCommit commit, CancellationToken ct) => Track(() =>
    {
        if (FailStripCommit)
        {
            return Task.FromException<PhotoStripRecord>(new StorageException(StorageErrorKind.DiskFull, "Disk full."));
        }

        return StripGate is { } gate ? After(gate, () => inner.CommitPhotoStripAsync(commit, ct)) : inner.CommitPhotoStripAsync(commit, ct);
    });

    public Task MarkGuestCycleDeletingAsync(string guestCycleId, CancellationToken ct) => inner.MarkGuestCycleDeletingAsync(guestCycleId, ct);

    public Task<GuestCyclePage> ListGuestCyclesAsync(string eventId, GuestCycleFilter filter, string? cursor, int pageSize, CancellationToken ct) =>
        inner.ListGuestCyclesAsync(eventId, filter, cursor, pageSize, ct);

    public Task<GuestCycleDetail?> GetGuestCycleAsync(string guestCycleId, CancellationToken ct) => inner.GetGuestCycleAsync(guestCycleId, ct);

    public Task<string> GetThumbnailPathAsync(string sourceId, int width, CancellationToken ct) => inner.GetThumbnailPathAsync(sourceId, width, ct);
}

/// <summary>A compositor that composes like <see cref="FakeCompositor"/> and can fail or wait, and counts how many calls are in flight.</summary>
internal sealed class ScriptedCompositor(ICatalog catalog) : ICompositor
{
    private readonly FakeCompositor _inner = new(catalog);
    private int _inFlight;

    /// <summary>How many strips with photographs (not blank previews) fail next, then they compose again.</summary>
    public int FailStrips { get; set; }

    /// <summary>Makes every Look thumbnail fail.</summary>
    public bool FailThumbnails { get; set; }

    /// <summary>While set, a strip with photographs completes only when the gate does.</summary>
    public TaskCompletionSource? StripGate { get; set; }

    public int InFlight => Volatile.Read(ref _inFlight);

    public IReadOnlyList<CompositionRequest> Requests => _inner.Requests;

    public IReadOnlyList<FakeCompositor.ThumbnailRequest> ThumbnailRequests => _inner.ThumbnailRequests;

    public Task<CompositionResult> ComposeAsync(CompositionRequest request, CancellationToken ct)
    {
        if (request.Photos.All(photo => photo is null))
        {
            return _inner.ComposeAsync(request, ct);
        }

        Interlocked.Increment(ref _inFlight);
        Task<CompositionResult> task;
        if (FailStrips > 0)
        {
            FailStrips--;
            task = Task.FromException<CompositionResult>(new CompositionException(CompositionErrorKind.Unknown, "The scripted compositor failed."));
        }
        else if (StripGate is { } gate)
        {
            task = Gated(gate, request, ct);
        }
        else
        {
            task = _inner.ComposeAsync(request, ct);
        }

        task.ContinueWith(_ => Interlocked.Decrement(ref _inFlight), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return task;
    }

    private async Task<CompositionResult> Gated(TaskCompletionSource gate, CompositionRequest request, CancellationToken ct)
    {
        await gate.Task.ConfigureAwait(false);
        return await _inner.ComposeAsync(request, ct).ConfigureAwait(false);
    }

    public Task<CompositionResult> ComposeLookThumbnailAsync(CompositionPhoto photo, double aspectRatio, string lookId, int width, CancellationToken ct)
    {
        Interlocked.Increment(ref _inFlight);
        var task = FailThumbnails
            ? Task.FromException<CompositionResult>(new CompositionException(CompositionErrorKind.UndecodablePhoto, "The scripted compositor failed."))
            : _inner.ComposeLookThumbnailAsync(photo, aspectRatio, lookId, width, ct);
        task.ContinueWith(_ => Interlocked.Decrement(ref _inFlight), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return task;
    }
}

/// <summary>A host that records like <see cref="RecordingHostServices"/> and can fail to open the Guest Screen.</summary>
internal sealed class GuestScreenHost(string operatorDisplayId) : IHostServices
{
    private readonly RecordingHostServices _inner = new(operatorDisplayId);

    /// <summary>Makes <see cref="OpenGuestScreen"/> throw.</summary>
    public bool FailOpen { get; set; }

    public IReadOnlyList<string> Calls => _inner.Calls;

    public void OpenGuestScreen(DisplayInfo display)
    {
        if (FailOpen)
        {
            throw new InvalidOperationException("The host could not open the window.");
        }

        _inner.OpenGuestScreen(display);
    }

    public void CloseGuestScreen() => _inner.CloseGuestScreen();

    public void OpenFolder(string path) => _inner.OpenFolder(path);

    public void ShowIdentifyOverlays(IReadOnlyList<IdentifyOverlaySpec> overlays) => _inner.ShowIdentifyOverlays(overlays);

    public void HideIdentifyOverlays() => _inner.HideIdentifyOverlays();

    public string? GetOperatorDisplayId() => _inner.GetOperatorDisplayId();
}

/// <summary>A camera adapter that passes to the fake and records what each Capture asked for and got, before the brain mirrored it.</summary>
internal sealed class RecordingCameraAdapter(FakeCameraAdapter inner) : ICameraAdapter
{
    public event EventHandler? CamerasChanged
    {
        add => inner.CamerasChanged += value;
        remove => inner.CamerasChanged -= value;
    }

    /// <summary>Every Capture: the <c>notBefore</c> it asked for, the frame's timestamp, and the frame's first and last pixel of row zero as they came from the camera.</summary>
    public List<RecordedCapture> Captures { get; } = [];

    public Task<IReadOnlyList<AvailableCamera>> ListAsync(CancellationToken cancellationToken) => inner.ListAsync(cancellationToken);

    public async Task<ICameraSession> OpenAsync(string deviceId, CancellationToken cancellationToken) =>
        new RecordingSession(await inner.OpenAsync(deviceId, cancellationToken).ConfigureAwait(false), Captures);

    public sealed record RecordedCapture(TimeSpan NotBefore, TimeSpan Timestamp, byte[] FirstPixel, byte[] LastPixelOfRow);

    private sealed class RecordingSession(ICameraSession inner, List<RecordedCapture> captures) : ICameraSession
    {
        public event EventHandler<CameraFailure>? Failed
        {
            add => inner.Failed += value;
            remove => inner.Failed -= value;
        }

        public string DeviceId => inner.DeviceId;

        public CameraFormat Format => inner.Format;

        public TimeSpan Now => inner.Now;

        public CameraFrameInfo? LatestFrame => inner.LatestFrame;

        public void SetPreviewTarget(PreviewTarget target) => inner.SetPreviewTarget(target);

        public async Task<CapturedFrame> CaptureAsync(TimeSpan notBefore, CancellationToken cancellationToken)
        {
            var frame = await inner.CaptureAsync(notBefore, cancellationToken).ConfigureAwait(false);
            var span = frame.Bgra.Span;
            var rowEnd = (frame.Width - 1) * 4;
            lock (captures)
            {
                captures.Add(new RecordedCapture(notBefore, frame.Timestamp, span[..4].ToArray(), span.Slice(rowEnd, 4).ToArray()));
            }

            return frame;
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}