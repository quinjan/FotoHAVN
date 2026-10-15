using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Fakes;

/// <summary>
/// A fast, non-production <see cref="IPinHasher"/> for tests that don't exercise hashing: one SHA-256 pass over the salt
/// and the PIN, with the credential's iteration count fixed at 1. <see cref="Pbkdf2PinHasher"/> is the real one.
/// </summary>
public sealed class FastPinHasher : IPinHasher
{
    public PinCredential Create(string pin)
    {
        ArgumentException.ThrowIfNullOrEmpty(pin);
        var salt = RandomNumberGenerator.GetBytes(Pbkdf2PinHasher.SaltLength);
        return new PinCredential(Hash(pin, salt), salt, 1);
    }

    public bool Verify(string pin, PinCredential credential)
    {
        ArgumentNullException.ThrowIfNull(pin);
        ArgumentNullException.ThrowIfNull(credential);
        return CryptographicOperations.FixedTimeEquals(Hash(pin, credential.Salt), credential.Hash);
    }

    private static byte[] Hash(string pin, byte[] salt) => SHA256.HashData([.. salt, .. Encoding.UTF8.GetBytes(pin)]);
}

/// <summary>
/// A faithful in-memory <see cref="IEventStore"/> for brain tests: the same sorting, filtering, paging cursor, argument
/// rules, error kinds, and discard and deletion semantics as the real store, with no files. Ids are sequential GUIDs
/// (<c>00000000-0000-0000-0000-000000000001</c> and up) unless a generator is supplied; times come from the injected
/// <see cref="IClock"/>. Background deletion runs only when <see cref="DrainAsync"/> is called, so tests see the
/// "deleting" state deterministically. Members are thread-safe.
/// </summary>
public sealed class InMemoryEventStore : IEventStore
{
    private readonly object _gate = new();
    private readonly IClock _clock;
    private readonly IPinHasher _hasher;
    private readonly Func<string> _newId;
    private readonly Dictionary<string, EventEntry> _events = [];
    private readonly Dictionary<string, CycleEntry> _cycles = [];
    private readonly Dictionary<string, CaptureEntry> _captures = [];
    private long _order;
    private long _idCounter;
    private bool _opened;
    private StorageErrorKind? _failNext;
    private StorageErrorKind? _failOpen;

    /// <param name="clock">Time source. Defaults to a <see cref="FakeClock"/> that never moves.</param>
    /// <param name="hasher">PIN hasher. Defaults to <see cref="FastPinHasher"/>.</param>
    /// <param name="newId">Id generator for Guest Cycles and Captures. Defaults to sequential GUIDs.</param>
    public InMemoryEventStore(IClock? clock = null, IPinHasher? hasher = null, Func<string>? newId = null)
    {
        _clock = clock ?? new FakeClock();
        _hasher = hasher ?? new FastPinHasher();
        _newId = newId ?? NextSequentialId;
    }

    public string DataRoot { get; init; } = @"C:\FOTOHAVN\Data";

    /// <summary>What <see cref="OpenAsync"/> reports as <see cref="StorageOpenResult.SchemaVersion"/>.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>What <see cref="OpenAsync"/> reports as the reconciler's work. Set it to simulate repairs.</summary>
    public ReconcileReport ReconcileOnOpen { get; set; } = ReconcileReport.Empty;

    /// <summary>How many times <see cref="OpenAsync"/> was called, failed or not.</summary>
    public int OpenCalls { get; private set; }

    /// <summary>Events and Guest Cycles marked deleting that <see cref="DrainAsync"/> hasn't removed yet.</summary>
    public int PendingDeletionCount
    {
        get
        {
            lock (_gate)
            {
                return _events.Values.Count(entry => entry.Deleting) + _cycles.Values.Count(entry => entry.Deleting && !EventIsDeleting(entry));
            }
        }
    }

    /// <summary>Makes the next call to any member except <see cref="OpenAsync"/>, <see cref="DrainAsync"/>, and disposal fail once with <paramref name="kind"/>.</summary>
    public void FailNext(StorageErrorKind kind)
    {
        lock (_gate)
        {
            _failNext = kind;
        }
    }

    /// <summary>Makes every <see cref="OpenAsync"/> fail with <paramref name="kind"/> until <see cref="ClearFailures"/>.</summary>
    public void FailOpen(StorageErrorKind kind)
    {
        lock (_gate)
        {
            _failOpen = kind;
        }
    }

    /// <summary>Clears <see cref="FailNext"/> and <see cref="FailOpen"/>.</summary>
    public void ClearFailures()
    {
        lock (_gate)
        {
            _failNext = null;
            _failOpen = null;
        }
    }

    /// <summary>The Capture's file bytes, or null when the file is gone (its Capture was discarded and the cycle ended) or the Capture is unknown.</summary>
    public ReadOnlyMemory<byte>? GetCaptureFile(string captureId)
    {
        lock (_gate)
        {
            return _captures.TryGetValue(captureId, out var entry) && entry.File is { } file ? new ReadOnlyMemory<byte>(file) : (ReadOnlyMemory<byte>?)null;
        }
    }

    /// <summary>The Photo Strip's file bytes, or null when the cycle has none.</summary>
    public ReadOnlyMemory<byte>? GetPhotoStripFile(string guestCycleId)
    {
        lock (_gate)
        {
            return _cycles.TryGetValue(guestCycleId, out var entry) && entry.StripFile is { } file ? new ReadOnlyMemory<byte>(file) : (ReadOnlyMemory<byte>?)null;
        }
    }

    public Task<StorageOpenResult> OpenAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<StorageOpenResult>(ct);
        }

        lock (_gate)
        {
            OpenCalls++;
            if (_failOpen is { } kind)
            {
                return Task.FromException<StorageOpenResult>(Failure(kind));
            }

            _opened = true;
            return Task.FromResult(new StorageOpenResult(SchemaVersion, ReconcileOnOpen));
        }
    }

    /// <summary>How many times <see cref="ProbeWritableAsync"/> was called, failed or not.</summary>
    public int ProbeCalls { get; private set; }

    /// <summary>Succeeds like a writable Data folder; <see cref="FailNext"/> makes it fail like one that isn't.</summary>
    public Task ProbeWritableAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            ProbeCalls++;
        }

        return Run(ct, () => { });
    }

    public Task<IReadOnlyList<EventSummary>> ListEventsAsync(CancellationToken ct) =>
        Run<IReadOnlyList<EventSummary>>(ct, () => [.. _events.Values
            .Where(entry => !entry.Deleting)
            .OrderByDescending(entry => entry.Record.EventDate)
            .ThenByDescending(entry => entry.Record.CreatedAt)
            .ThenByDescending(entry => entry.Order)
            .Select(Summarize)]);

    public Task<EventRecord?> GetEventAsync(string eventId, CancellationToken ct) =>
        Run<EventRecord?>(ct, () =>
        {
            StorageIds.ThrowIfInvalid(eventId, nameof(eventId));
            return _events.TryGetValue(eventId, out var entry) && !entry.Deleting ? Present(entry) : null;
        });

    public Task CreateEventAsync(EventRecord record, CancellationToken ct) =>
        Run(ct, () =>
        {
            StorageValidation.ValidateEvent(record);
            if (_events.ContainsKey(record.Id))
            {
                throw new StorageException(StorageErrorKind.Conflict, "An Event with this id exists.");
            }

            var now = _clock.UtcNow;
            _events[record.Id] = new EventEntry(Copy(record, now, now), null, _order++);
        });

    public Task UpdateEventAsync(EventRecord record, CancellationToken ct) =>
        Run(ct, () =>
        {
            StorageValidation.ValidateEvent(record);
            var entry = FindEvent(record.Id);
            entry.Record = Copy(record, entry.Record.CreatedAt, _clock.UtcNow);
        });

    public Task SetPinAsync(string eventId, string pin, CancellationToken ct) =>
        Run(ct, () =>
        {
            StorageValidation.ValidatePin(eventId, pin);
            FindEvent(eventId).Pin = _hasher.Create(pin);
        });

    public Task<bool> VerifyPinAsync(string eventId, string pin, CancellationToken ct) =>
        Run(ct, () =>
        {
            StorageValidation.ValidatePin(eventId, pin);
            return FindEvent(eventId).Pin is { } credential && _hasher.Verify(pin, credential);
        });

    public Task MarkEventDeletingAsync(string eventId, CancellationToken ct) =>
        Run(ct, () =>
        {
            StorageIds.ThrowIfInvalid(eventId, nameof(eventId));
            FindEvent(eventId).Deleting = true;
        });

    public Task<GuestCycleRecord> StartGuestCycleAsync(string eventId, CancellationToken ct) =>
        Run(ct, () =>
        {
            StorageIds.ThrowIfInvalid(eventId, nameof(eventId));
            _ = FindEvent(eventId);
            var cycle = new GuestCycleRecord(_newId(), eventId, _clock.UtcNow, null, null);
            _cycles[cycle.Id] = new CycleEntry(cycle);
            return cycle;
        });

    public Task EndGuestCycleAsync(string guestCycleId, GuestCycleEndReason reason, CancellationToken ct) =>
        Run(ct, () =>
        {
            StorageIds.ThrowIfInvalid(guestCycleId, nameof(guestCycleId));
            StorageValidation.ValidateEndReason(reason);
            var entry = FindCycle(guestCycleId);
            EnsureOpen(entry);
            End(entry, reason, _clock.UtcNow);
        });

    public Task<CaptureRecord> CommitCaptureAsync(CaptureCommit commit, CancellationToken ct) =>
        Run(ct, () =>
        {
            StorageValidation.ValidateCaptureCommit(commit);
            var cycle = FindCycle(commit.GuestCycleId);
            EnsureOpen(cycle);
            if (cycle.Captures.Any(existing => existing.Record.Sequence == commit.Sequence))
            {
                throw new StorageException(StorageErrorKind.Conflict, "The Capture sequence is already used.");
            }

            var bytes = commit.Jpeg.ToArray();
            var record = new CaptureRecord(
                _newId(),
                commit.GuestCycleId,
                commit.Sequence,
                $"capture-{commit.Sequence}.jpg",
                bytes.Length,
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                commit.Width,
                commit.Height,
                commit.Mirrored,
                commit.TakenAt,
                CaptureState.Kept);
            var entry = new CaptureEntry(record, bytes);
            cycle.Captures.Add(entry);
            _captures[record.Id] = entry;
            return record;
        });

    public Task SetCaptureStateAsync(string captureId, CaptureState state, CancellationToken ct) =>
        Run(ct, () =>
        {
            StorageIds.ThrowIfInvalid(captureId, nameof(captureId));
            if (!Enum.IsDefined(state))
            {
                throw new ArgumentOutOfRangeException(nameof(state), state, "Not a Capture state.");
            }
            if (!_captures.TryGetValue(captureId, out var entry) || CycleOf(entry).Deleting)
            {
                throw new StorageException(StorageErrorKind.NotFound, "The Capture doesn't exist.");
            }

            EnsureOpen(CycleOf(entry));
            entry.Record = entry.Record with { State = state };
        });

    public Task<PhotoStripRecord> CommitPhotoStripAsync(PhotoStripCommit commit, CancellationToken ct) =>
        Run(ct, () =>
        {
            StorageValidation.ValidatePhotoStripCommit(commit);
            var cycle = FindCycle(commit.GuestCycleId);
            EnsureOpen(cycle);
            if (cycle.Strip is not null)
            {
                throw new StorageException(StorageErrorKind.Conflict, "The Guest Cycle already has a Photo Strip.");
            }

            foreach (var captureId in commit.Arrangement)
            {
                if (!cycle.Captures.Any(capture => capture.Record.Id == captureId && capture.Record.State == CaptureState.Kept))
                {
                    throw new StorageException(StorageErrorKind.Conflict, "The arrangement names a Capture that isn't a kept Capture of this Guest Cycle.");
                }
            }

            var bytes = commit.Png.ToArray();
            var now = _clock.UtcNow;
            cycle.Strip = new PhotoStripRecord(
                commit.GuestCycleId,
                commit.TemplateId,
                commit.LookId,
                [.. commit.Arrangement],
                "photo-strip.png",
                bytes.Length,
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                commit.Width,
                commit.Height,
                now);
            cycle.StripFile = bytes;
            End(cycle, GuestCycleEndReason.Completed, now);
            return cycle.Strip;
        });

    public Task MarkGuestCycleDeletingAsync(string guestCycleId, CancellationToken ct) =>
        Run(ct, () =>
        {
            StorageIds.ThrowIfInvalid(guestCycleId, nameof(guestCycleId));
            FindCycle(guestCycleId).Deleting = true;
        });

    public Task<GuestCyclePage> ListGuestCyclesAsync(string eventId, GuestCycleFilter filter, string? cursor, int pageSize, CancellationToken ct) =>
        Run(ct, () =>
        {
            StorageValidation.ValidatePaging(eventId, filter, cursor, pageSize);
            _ = FindEvent(eventId);
            var rows = _cycles.Values
                .Where(entry => entry.Cycle.EventId == eventId && !entry.Deleting)
                .Where(entry => filter == GuestCycleFilter.All
                    || (entry.Cycle.EndReason == GuestCycleEndReason.Completed && entry.Strip is not null))
                .OrderByDescending(entry => entry.Cycle.StartedAt)
                .ThenByDescending(entry => entry.Cycle.Id, StringComparer.Ordinal)
                .AsEnumerable();
            if (cursor is not null)
            {
                _ = GuestCycleCursor.TryDecode(cursor, out var startedAt, out var id);
                rows = rows.Where(entry => entry.Cycle.StartedAt < startedAt
                    || (entry.Cycle.StartedAt == startedAt && string.CompareOrdinal(entry.Cycle.Id, id) < 0));
            }

            var page = rows.Take(pageSize + 1).ToList();
            var items = page.Take(pageSize).Select(entry => new GuestCycleSummary(
                entry.Cycle,
                entry.Captures.Count(capture => capture.Record.State == CaptureState.Kept),
                entry.Strip)).ToList();
            var next = page.Count > pageSize ? GuestCycleCursor.Encode(items[^1].Cycle.StartedAt, items[^1].Cycle.Id) : null;
            return new GuestCyclePage(items, next);
        });

    public Task<GuestCycleDetail?> GetGuestCycleAsync(string guestCycleId, CancellationToken ct) =>
        Run<GuestCycleDetail?>(ct, () =>
        {
            StorageIds.ThrowIfInvalid(guestCycleId, nameof(guestCycleId));
            if (!_cycles.TryGetValue(guestCycleId, out var entry) || entry.Deleting || EventIsDeleting(entry))
            {
                return null;
            }

            return new GuestCycleDetail(
                entry.Cycle,
                [.. entry.Captures.Where(capture => capture.Record.State == CaptureState.Kept).Select(capture => capture.Record).OrderBy(capture => capture.Sequence)],
                entry.Strip);
        });

    public Task<string> GetThumbnailPathAsync(string sourceId, int width, CancellationToken ct) =>
        Run(ct, () =>
        {
            StorageValidation.ValidateThumbnailRequest(sourceId, width);
            if (_captures.TryGetValue(sourceId, out var capture) && capture.File is not null && capture.Record.State == CaptureState.Kept && !CycleOf(capture).Deleting && !EventIsDeleting(CycleOf(capture)))
            {
                return ThumbnailName.RelativePath(sourceId, capture.Record.Sha256, width);
            }

            if (_cycles.TryGetValue(sourceId, out var cycle) && cycle.Strip is { } strip && !cycle.Deleting && !EventIsDeleting(cycle))
            {
                return ThumbnailName.RelativePath(sourceId, strip.Sha256, width);
            }

            throw new StorageException(StorageErrorKind.NotFound, "The thumbnail's source doesn't exist.");
        });

    /// <summary>Removes Events and Guest Cycles marked deleting, with their Captures and Photo Strips.</summary>
    public Task DrainAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled(ct);
        }

        lock (_gate)
        {
            var doomedEvents = _events.Where(pair => pair.Value.Deleting).Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);
            var doomedCycles = _cycles.Where(pair => pair.Value.Deleting || doomedEvents.Contains(pair.Value.Cycle.EventId)).ToList();
            foreach (var (id, cycle) in doomedCycles)
            {
                foreach (var capture in cycle.Captures)
                {
                    _captures.Remove(capture.Record.Id);
                }

                _cycles.Remove(id);
            }

            foreach (var id in doomedEvents)
            {
                _events.Remove(id);
            }
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static StorageException Failure(StorageErrorKind kind) =>
        new(kind, $"Injected storage failure: {kind}.");

    private static EventRecord Copy(EventRecord record, DateTimeOffset createdAt, DateTimeOffset updatedAt) => record with
    {
        OfferedTemplateIds = [.. record.OfferedTemplateIds],
        OfferedLookIds = [.. record.OfferedLookIds],
        PhaseTimers = [.. record.PhaseTimers],
        CreatedAt = createdAt,
        UpdatedAt = updatedAt,
        HasPin = false,
    };

    private static void EnsureOpen(CycleEntry entry)
    {
        if (entry.Cycle.EndReason is not null)
        {
            throw new StorageException(StorageErrorKind.Conflict, "The Guest Cycle has ended.");
        }
    }

    private EventSummary Summarize(EventEntry entry)
    {
        var cycles = _cycles.Values.Where(cycle => cycle.Cycle.EventId == entry.Record.Id && !cycle.Deleting).ToList();
        return new EventSummary(
            entry.Record.Id,
            entry.Record.Name,
            entry.Record.EventDate,
            entry.Record.Venue,
            cycles.Count(cycle => cycle.Cycle.EndReason == GuestCycleEndReason.Completed),
            cycles.Count == 0 ? null : cycles.Max(cycle => cycle.Cycle.StartedAt));
    }

    private string NextSequentialId() =>
        string.Create(CultureInfo.InvariantCulture, $"00000000-0000-0000-0000-{Interlocked.Increment(ref _idCounter):D12}");

    private static EventRecord Present(EventEntry entry) => Copy(entry.Record, entry.Record.CreatedAt, entry.Record.UpdatedAt) with { HasPin = entry.Pin is not null };

    private EventEntry FindEvent(string eventId) =>
        _events.TryGetValue(eventId, out var entry) && !entry.Deleting
            ? entry
            : throw new StorageException(StorageErrorKind.NotFound, "The Event doesn't exist.");

    private CycleEntry FindCycle(string guestCycleId) =>
        _cycles.TryGetValue(guestCycleId, out var entry) && !entry.Deleting && !EventIsDeleting(entry)
            ? entry
            : throw new StorageException(StorageErrorKind.NotFound, "The Guest Cycle doesn't exist.");

    private CycleEntry CycleOf(CaptureEntry capture) => _cycles[capture.Record.GuestCycleId];

    private bool EventIsDeleting(CycleEntry cycle) => _events.TryGetValue(cycle.Cycle.EventId, out var entry) && entry.Deleting;

    private static void End(CycleEntry entry, GuestCycleEndReason reason, DateTimeOffset at)
    {
        entry.Cycle = entry.Cycle with { EndedAt = at, EndReason = reason };
        foreach (var capture in entry.Captures.Where(capture => capture.Record.State == CaptureState.Discarded))
        {
            capture.File = null;
        }
    }

    private void ConsumeFailure()
    {
        if (_failNext is { } kind)
        {
            _failNext = null;
            throw Failure(kind);
        }
    }

#pragma warning disable CA1068 // The token leads so each member reads as "run this action under this token".
    private Task<T> Run<T>(CancellationToken ct, Func<T> action)
    {
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(ct);
        }

        lock (_gate)
        {
            try
            {
                if (!_opened)
                {
                    throw new InvalidOperationException("The store isn't open.");
                }

                ConsumeFailure();
                return Task.FromResult(action());
            }
#pragma warning disable CA1031 // Like an async method, the fake reports every failure through the returned task.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                return Task.FromException<T>(exception);
            }
        }
    }

    private Task<bool> Run(CancellationToken ct, Action action) => Run<bool>(ct, () =>
    {
        action();
        return true;
    });
#pragma warning restore CA1068

    private sealed class EventEntry(EventRecord record, PinCredential? pin, long order)
    {
        public EventRecord Record { get; set; } = record;

        public PinCredential? Pin { get; set; } = pin;

        public long Order { get; } = order;

        public bool Deleting { get; set; }
    }

    private sealed class CycleEntry(GuestCycleRecord cycle)
    {
        public GuestCycleRecord Cycle { get; set; } = cycle;

        public List<CaptureEntry> Captures { get; } = [];

        public PhotoStripRecord? Strip { get; set; }

        public byte[]? StripFile { get; set; }

        public bool Deleting { get; set; }
    }

    private sealed class CaptureEntry(CaptureRecord record, byte[] file)
    {
        public CaptureRecord Record { get; set; } = record;

        public byte[]? File { get; set; } = file;
    }
}
