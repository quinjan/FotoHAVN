using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Tests.Events.Active;

/// <summary>An <see cref="IEventStore"/> that passes every call to another and can make the calls the running Event makes fail or wait.</summary>
internal sealed class InterceptingStore(IEventStore inner) : IEventStore
{
    /// <summary>Makes <see cref="ListEventsAsync"/> fail (Start Event's storage check).</summary>
    public bool FailList { get; set; }

    /// <summary>Makes <see cref="GetEventAsync"/> fail (Start Event's load).</summary>
    public bool FailGet { get; set; }

    /// <summary>Makes <see cref="ListEventsAsync"/> throw instead of returning a faulted task.</summary>
    public bool ThrowOnList { get; set; }

    /// <summary>Makes <see cref="GetEventAsync"/> throw instead of returning a faulted task.</summary>
    public bool ThrowOnGet { get; set; }

    /// <summary>Makes <see cref="VerifyPinAsync"/> throw instead of returning a task.</summary>
    public bool ThrowOnVerify { get; set; }

    /// <summary>While set, <see cref="VerifyPinAsync"/> completes only when the gate does.</summary>
    public TaskCompletionSource? VerifyGate { get; set; }

    /// <summary>The PINs <see cref="VerifyPinAsync"/> was asked to check, in order.</summary>
    public List<string> VerifiedPins { get; } = [];

    public int ListCalls { get; private set; }

    public string DataRoot => inner.DataRoot;

    public Task DrainAsync(CancellationToken ct) => inner.DrainAsync(ct);

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    public Task<StorageOpenResult> OpenAsync(CancellationToken ct) => inner.OpenAsync(ct);

    public Task<IReadOnlyList<EventSummary>> ListEventsAsync(CancellationToken ct)
    {
        ListCalls++;
        if (ThrowOnList)
        {
            throw new InvalidOperationException("The store threw.");
        }

        return FailList ? Task.FromException<IReadOnlyList<EventSummary>>(new StorageException(StorageErrorKind.DiskFull, "Disk full.")) : inner.ListEventsAsync(ct);
    }

    public Task<EventRecord?> GetEventAsync(string eventId, CancellationToken ct)
    {
        if (ThrowOnGet)
        {
            throw new InvalidOperationException("The store threw.");
        }

        return FailGet ? Task.FromException<EventRecord?>(new StorageException(StorageErrorKind.AccessDenied, "Access denied.")) : inner.GetEventAsync(eventId, ct);
    }

    public Task CreateEventAsync(EventRecord record, CancellationToken ct) => inner.CreateEventAsync(record, ct);

    public Task UpdateEventAsync(EventRecord record, CancellationToken ct) => inner.UpdateEventAsync(record, ct);

    public Task SetPinAsync(string eventId, string pin, CancellationToken ct) => inner.SetPinAsync(eventId, pin, ct);

    public Task<bool> VerifyPinAsync(string eventId, string pin, CancellationToken ct)
    {
        VerifiedPins.Add(pin);
        if (ThrowOnVerify)
        {
            throw new InvalidOperationException("The store threw.");
        }

        return VerifyGate is { } gate
            ? gate.Task.ContinueWith(_ => inner.VerifyPinAsync(eventId, pin, ct), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap()
            : inner.VerifyPinAsync(eventId, pin, ct);
    }

    public Task MarkEventDeletingAsync(string eventId, CancellationToken ct) => inner.MarkEventDeletingAsync(eventId, ct);

    public Task<GuestCycleRecord> StartGuestCycleAsync(string eventId, CancellationToken ct) => inner.StartGuestCycleAsync(eventId, ct);

    public Task EndGuestCycleAsync(string guestCycleId, GuestCycleEndReason reason, CancellationToken ct) => inner.EndGuestCycleAsync(guestCycleId, reason, ct);

    public Task<CaptureRecord> CommitCaptureAsync(CaptureCommit commit, CancellationToken ct) => inner.CommitCaptureAsync(commit, ct);

    public Task SetCaptureStateAsync(string captureId, CaptureState state, CancellationToken ct) => inner.SetCaptureStateAsync(captureId, state, ct);

    public Task<PhotoStripRecord> CommitPhotoStripAsync(PhotoStripCommit commit, CancellationToken ct) => inner.CommitPhotoStripAsync(commit, ct);

    public Task MarkGuestCycleDeletingAsync(string guestCycleId, CancellationToken ct) => inner.MarkGuestCycleDeletingAsync(guestCycleId, ct);

    public Task<GuestCyclePage> ListGuestCyclesAsync(string eventId, GuestCycleFilter filter, string? cursor, int pageSize, CancellationToken ct) =>
        inner.ListGuestCyclesAsync(eventId, filter, cursor, pageSize, ct);

    public Task<GuestCycleDetail?> GetGuestCycleAsync(string guestCycleId, CancellationToken ct) => inner.GetGuestCycleAsync(guestCycleId, ct);

    public Task<string> GetThumbnailPathAsync(string sourceId, int width, CancellationToken ct) => inner.GetThumbnailPathAsync(sourceId, width, ct);
}
