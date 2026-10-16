using FotoHavn.Core.Events.Setup;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core;

// The Event list (plan section 8, Event list; M6 contract section 4). Owner: M6 wave B1a. The rows come from the store's list (kept in
// `_eventSummaries`, refreshed after every save and delete off the executor); the delete confirmation shows the Event's name and
// what deleting removes; Open folder goes through RequestOpenFolder. EditEvent and DeleteEvent of the running Event are rejected
// eventRunning, an unknown id is notAvailable.
public sealed partial class Brain
{
    // Each list refresh takes the next number; only the answer to the latest one is applied, so a slow earlier list can't overwrite a newer one.
    private int _eventListRequest;

    // The Event the open delete confirmation is for, and the counting request that opens it (a newer DeleteEvent replaces an older one).
    private string? _deleteEventId;
    private int _deleteRequest;

    private bool StorageReady => _storageResolved && _storageError is null;

    /// <summary>The Event list as the operator sees it: the saved Events, newest first, with the running one marked and every row's actions decided here.</summary>
    private EventListView RenderEventList()
    {
        var runningId = ActiveEventId;
        var heldId = HeldEventId;
        return new EventListView([.. _eventSummaries.Select(summary => ToRow(summary, runningId, heldId))], NewEventAvailable: true);
    }

    // A starting Event is held like a running one (no Start anywhere, no Edit or Delete on its row), but its row says RUNNING only once it runs.
    private static EventListRow ToRow(EventSummary summary, string? runningId, string? heldId)
    {
        var anyHeld = heldId is not null;
        var held = anyHeld && string.Equals(summary.Id, heldId, StringComparison.Ordinal);
        var running = runningId is not null && string.Equals(summary.Id, runningId, StringComparison.Ordinal);
        return new EventListRow(
            summary.Id,
            summary.Name,
            summary.EventDate,
            summary.Venue,
            summary.CompletedGuestCycles,
            summary.LastUsedAt,
            running,
            new EventRowActions(
                CanStart: !anyHeld,
                CanEdit: !held,
                CanDelete: !held,
                CanOpenFolder: true,
                CanOpenGallery: true),
            anyHeld ? StartDisabledReason.EventRunning : null);
    }

    /// <summary><c>OpenNewEvent</c>: a new draft with the defaults. Allowed while an Event runs; not while storage is down or a draft is open.</summary>
    private CommandResultMessage OpenNewEvent(string id)
    {
        if (!StorageReady || SetupInUse())
        {
            return NotAvailable(id);
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_dependencies.Clock.UtcNow, _dependencies.LocalTimeZone).DateTime);
        OpenSetup(EventSetupDraft.CreateNew(_dependencies.Catalog, today));
        return CommandResultMessage.Accepted(id);
    }

    /// <summary><c>EditEvent</c>: loads the saved Event off the executor and opens it in Event setup. The running Event can't be edited.</summary>
    private CommandResultMessage EditEvent(string id, EditEventPayload payload)
    {
        if (IsHeld(payload.EventId))
        {
            return CommandResultMessage.Rejected(id, RejectReason.EventRunning);
        }

        if (!StorageReady || SetupInUse() || FindSummary(payload.EventId) is null)
        {
            return NotAvailable(id);
        }

        BeginLoadSetup(payload.EventId, null);
        return CommandResultMessage.Accepted(id);
    }

    /// <summary><c>DeleteEvent</c>: counts what deleting removes off the executor, then opens the confirmation with the Event's name and the counts.</summary>
    private CommandResultMessage DeleteEvent(string id, DeleteEventPayload payload)
    {
        if (IsHeld(payload.EventId))
        {
            return CommandResultMessage.Rejected(id, RejectReason.EventRunning);
        }

        if (FindSummary(payload.EventId) is not { } summary)
        {
            return NotAvailable(id);
        }

        var request = ++_deleteRequest;
        CountDeletionAsync(summary.Id, _storageCancellation.Token).ContinueWith(
            completed => Post(() => CompleteDeleteCounts(request, summary, completed)),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return CommandResultMessage.Accepted(id);
    }

    private async Task<ConfirmationCounts> CountDeletionAsync(string eventId, CancellationToken ct) =>
        await EventDeletionCounter.CountAsync(_dependencies.Store, eventId, ct).ConfigureAwait(false);

    private void CompleteDeleteCounts(int request, EventSummary summary, Task<ConfirmationCounts> completed)
    {
        if (request != _deleteRequest || IsHeld(summary.Id))
        {
            _ = completed.Exception;
            return;
        }

        if (completed.IsCompletedSuccessfully)
        {
            _deleteEventId = summary.Id;
            SetConfirmation(new ConfirmationOverlay(ConfirmationKind.DeleteEvent, summary.Name, completed.Result, null));
        }
        else
        {
            var kind = (completed.Exception?.GetBaseException() as StorageException)?.Kind;
            BrainLog.DeleteCountsFailed(_logger, completed.Exception?.GetBaseException().GetType().Name ?? "canceled", kind);
            ShowNotice(kind == StorageErrorKind.NotFound ? OperatorNoticeCode.EventNotFound : OperatorNoticeCode.DeleteFailed);
            RefreshEventList();
        }

        PublishChanges();
    }

    /// <summary><c>ConfirmDeleteEvent</c>: marks the Event deleting off the executor, then re-lists, so the row disappears at once; the store removes the files in the background.</summary>
    private CommandResultMessage ConfirmDeleteEvent(string id)
    {
        if (_confirmation is not { Kind: ConfirmationKind.DeleteEvent } || _deleteEventId is not { } eventId)
        {
            return NotAvailable(id);
        }

        _confirmation = null;
        _deleteEventId = null;
        if (IsHeld(eventId))
        {
            return CommandResultMessage.Rejected(id, RejectReason.EventRunning);
        }

        var request = ++_eventListRequest;
        DeleteEventAsync(eventId, _storageCancellation.Token).ContinueWith(
            completed => Post(() =>
            {
                var outcome = completed.Result;
                ApplySummaries(request, outcome.Summaries);
                if (!outcome.Succeeded)
                {
                    ShowNotice(outcome.Kind == StorageErrorKind.NotFound ? OperatorNoticeCode.EventNotFound : OperatorNoticeCode.DeleteFailed);
                }

                PublishChanges();
            }),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return CommandResultMessage.Accepted(id);
    }

    private async Task<StoreOutcome> DeleteEventAsync(string eventId, CancellationToken ct)
    {
        try
        {
            await _dependencies.Store.MarkEventDeletingAsync(eventId, ct).ConfigureAwait(false);
        }
        catch (StorageException exception)
        {
            BrainLog.EventDeleteFailed(_logger, exception.GetType().Name, exception.Kind);
            return new StoreOutcome(false, exception.Kind, false, await ListQuietlyAsync(ct).ConfigureAwait(false));
        }
#pragma warning disable CA1031 // Whatever the store throws, the operator gets the delete notice and a fresh list rather than a stuck brain.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BrainLog.EventDeleteFailed(_logger, exception.GetType().Name, StorageErrorKind.Unknown);
            return new StoreOutcome(false, StorageErrorKind.Unknown, false, await ListQuietlyAsync(ct).ConfigureAwait(false));
        }

        return new StoreOutcome(true, null, false, await ListQuietlyAsync(ct).ConfigureAwait(false));
    }

    /// <summary><c>CancelDeleteEvent</c>: closes the confirmation.</summary>
    private CommandResultMessage CancelDeleteEvent(string id)
    {
        if (_confirmation is not { Kind: ConfirmationKind.DeleteEvent })
        {
            return NotAvailable(id);
        }

        _confirmation = null;
        _deleteEventId = null;
        return CommandResultMessage.Accepted(id);
    }

    /// <summary><c>OpenEventFolder</c>: asks the host to open <c>Data/Events/&lt;id&gt;</c>. Allowed for the running Event.</summary>
    private CommandResultMessage OpenEventFolder(string id, OpenEventFolderPayload payload)
    {
        if (FindSummary(payload.EventId) is null)
        {
            return NotAvailable(id);
        }

        RequestOpenFolder(EventFolders.For(_dependencies.Store.DataRoot, payload.EventId));
        return CommandResultMessage.Accepted(id);
    }

    private bool IsHeld(string eventId) => HeldEventId is { } held && string.Equals(held, eventId, StringComparison.Ordinal);

    private EventSummary? FindSummary(string eventId) =>
        _eventSummaries.FirstOrDefault(summary => string.Equals(summary.Id, eventId, StringComparison.Ordinal));

    /// <summary>Re-lists the Events off the executor and posts the list back (a failed list leaves the rows as they are).</summary>
    private void RefreshEventList()
    {
        var request = ++_eventListRequest;
        ListQuietlyAsync(_storageCancellation.Token).ContinueWith(
            completed => Post(() =>
            {
                ApplySummaries(request, completed.Result);
                PublishChanges();
            }),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>Takes <paramref name="summaries"/> as the Event list when <paramref name="request"/> is still the latest refresh.</summary>
    private void ApplySummaries(int request, IReadOnlyList<EventSummary>? summaries)
    {
        if (summaries is not null && request == _eventListRequest)
        {
            _eventSummaries = summaries;
        }
    }

    /// <summary>The store's Event list, or null when it can't be read (logged). It never throws.</summary>
    private async Task<IReadOnlyList<EventSummary>?> ListQuietlyAsync(CancellationToken ct)
    {
        try
        {
            return [.. await _dependencies.Store.ListEventsAsync(ct).ConfigureAwait(false)];
        }
#pragma warning disable CA1031 // A list that fails leaves the rows as they are; the next save, delete, or retry lists again.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BrainLog.EventListFailed(_logger, exception.GetType().Name);
            return null;
        }
    }

    /// <summary>The outcome of a store write the brain made off the executor, with the Event list read after it (null when that read failed).</summary>
    /// <param name="Succeeded">Whether every step worked.</param>
    /// <param name="Kind">The failure's kind when it didn't.</param>
    /// <param name="RecordPersisted">Whether the Event row was written (a later step may still have failed).</param>
    /// <param name="Summaries">The Event list read after the write, or null.</param>
    private sealed record StoreOutcome(bool Succeeded, StorageErrorKind? Kind, bool RecordPersisted, IReadOnlyList<EventSummary>? Summaries);
}

internal static partial class BrainLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Listing the Events failed ({ErrorType}).")]
    public static partial void EventListFailed(ILogger logger, string errorType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Counting what deleting an Event removes failed ({ErrorType}, {Kind}).")]
    public static partial void DeleteCountsFailed(ILogger logger, string errorType, StorageErrorKind? kind);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Deleting an Event failed ({ErrorType}, {Kind}).")]
    public static partial void EventDeleteFailed(ILogger logger, string errorType, StorageErrorKind kind);
}
