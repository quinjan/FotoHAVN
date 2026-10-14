using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core;

// Storage in the brain (plan section 10, contract sections 5 and 6): open the Event store at startup, show the Event list or
// the storage error, and let the operator retry. The brain starts the store's async calls on its executor, never waits for
// them, and posts each outcome back as a new work item.
public sealed partial class Brain
{
    private readonly CancellationTokenSource _storageCancellation = new();
    private StorageDiagnosticsState _storageState = StorageDiagnosticsState.Opening;
    private StorageErrorReason? _storageError;
    private bool _storageOpenInFlight;

    // False from construction until the first open answers: the operator shows the Starting surface, never an empty Event list.
    private bool _storageResolved;
    private IReadOnlyList<EventSummary> _eventSummaries = [];
    private StorageOpenResult? _storageOpened;

    private void StartStorageOpen()
    {
        _storageOpenInFlight = true;
        _storageState = StorageDiagnosticsState.Opening;
        var load = LoadStorageAsync(_storageCancellation.Token);
        load.ContinueWith(
            completed => Post(() => CompleteStorageOpen(completed.Result)),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void CompleteStorageOpen(StorageLoad load)
    {
        _storageOpenInFlight = false;
        _storageResolved = true;
        if (load.Error is { } error)
        {
            _storageState = StorageDiagnosticsState.Error;
            _storageError = error;
        }
        else
        {
            _storageState = StorageDiagnosticsState.Ready;
            _storageError = null;
            _storageOpened = load.Opened;
            _eventSummaries = load.Summaries;
        }

        PublishChanges();
    }

    /// <summary>Opens the store and lists its Events. It never throws: every failure is a <see cref="StorageLoad"/> with the reason to show.</summary>
    private async Task<StorageLoad> LoadStorageAsync(CancellationToken ct)
    {
        var store = _dependencies.Store;
        try
        {
            var opened = await store.OpenAsync(ct).ConfigureAwait(false);
            var summaries = await store.ListEventsAsync(ct).ConfigureAwait(false);
            BrainLog.StorageOpened(_logger, opened.SchemaVersion, summaries.Count);
            return new StorageLoad(null, opened, [.. summaries]);
        }
        catch (StorageException exception)
        {
            BrainLog.StorageOpenFailed(_logger, exception, exception.Kind);
            return new StorageLoad(ToReason(exception.Kind), null, []);
        }
#pragma warning disable CA1031 // Whatever the store throws, the operator gets the storage error surface rather than a stuck brain.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BrainLog.StorageOpenFailed(_logger, exception, StorageErrorKind.Unknown);
            return new StorageLoad(StorageErrorReason.Unknown, null, []);
        }
    }

    /// <summary><c>RetryStorage</c>: allowed only on the storage error surface and only once at a time.</summary>
    private CommandResultMessage RetryStorage(string id)
    {
        if (_storageError is null || _storageOpenInFlight || _fatalReason is not null)
        {
            return CommandResultMessage.Rejected(id, RejectReason.NotAvailable);
        }

        StartStorageOpen();
        return CommandResultMessage.Accepted(id);
    }

    private StorageDiagnostics StorageDiagnosticsNow() => new(
        _storageState,
        _storageState == StorageDiagnosticsState.Error ? _storageError : null,
        _storageOpened?.SchemaVersion,
        _storageOpened is { Reconcile: var report } ? ToDiagnostics(report) : null);

    private static ReconcileDiagnostics ToDiagnostics(ReconcileReport report) => new(
        report.StagingFilesDeleted,
        report.OrphanFilesDeleted,
        report.ArtifactProblems,
        report.CyclesMarkedUnrecoverable,
        report.CyclesClosedAsAppClosed,
        report.GuestCyclesDeleted,
        report.EventsDeleted,
        report.DiscardedFilesDeleted,
        report.OrphanThumbnailsDeleted,
        report.Problems.Count);

    /// <summary>Contract section 6: the I/O and environment kinds map one to one; the programming-error kinds become <c>unknown</c>.</summary>
    private static StorageErrorReason ToReason(StorageErrorKind kind) => kind switch
    {
        StorageErrorKind.DiskFull => StorageErrorReason.DiskFull,
        StorageErrorKind.AccessDenied => StorageErrorReason.AccessDenied,
        StorageErrorKind.Locked => StorageErrorReason.Locked,
        StorageErrorKind.NotLocalDisk => StorageErrorReason.NotLocalDisk,
        StorageErrorKind.CloudSynced => StorageErrorReason.CloudSynced,
        StorageErrorKind.NotWritable => StorageErrorReason.NotWritable,
        StorageErrorKind.IntegrityFailed => StorageErrorReason.IntegrityFailed,
        StorageErrorKind.Corrupt => StorageErrorReason.Corrupt,
        _ => StorageErrorReason.Unknown,
    };

    /// <summary>The outcome of one open: the reason to show, or the open result and the Event list rows.</summary>
    private sealed record StorageLoad(StorageErrorReason? Error, StorageOpenResult? Opened, IReadOnlyList<EventSummary> Summaries);
}

internal static partial class BrainLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Storage is open at schema version {SchemaVersion} with {EventCount} Events.")]
    public static partial void StorageOpened(ILogger logger, int schemaVersion, int eventCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Opening storage failed ({Kind}).")]
    public static partial void StorageOpenFailed(ILogger logger, Exception exception, StorageErrorKind kind);
}
