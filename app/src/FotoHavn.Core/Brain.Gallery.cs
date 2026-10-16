using FotoHavn.Core.Gallery;
using FotoHavn.Core.Gallery.Orchestration;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core;

// The operator's Event Gallery (plan section 8, Event Gallery, Operator view; M9 contract section 4.1). The pure rules are in
// Gallery/GalleryRules.cs, the page state in Gallery/GalleryPage.cs, and the store work that builds a page with its thumbnails in
// Gallery/Orchestration/GalleryPageBuilder.cs; this partial only orchestrates them. Every handler runs on the executor, answers at once, and does
// store work off it (the Off helper in Brain.GuestCycle.cs); the outcome is posted back carrying the request number it was started with, and is
// ignored when the gallery moved on (closed, reopened, a newer request, the brain disposed).
public sealed partial class Brain
{
    private OperatorGalleryState? _operatorGallery;
    private GalleryPageBuilder? _galleryBuilder;

    private GalleryPageBuilder GalleryBuilder => _galleryBuilder ??= new GalleryPageBuilder(_dependencies.Store, _dependencies.Catalog);

    /// <summary>The open operator gallery, or null: its state only counts while the operator is on the gallery surface.</summary>
    private OperatorGalleryState? ActiveOperatorGallery => _operatorGallery is { } state && _operatorSurface == OperatorSurface.EventGallery ? state : null;

    /// <summary>
    /// <c>OpenGallery(eventId)</c>: opens the Event's gallery and loads its first page. Allowed from the Event list for any saved Event (the running
    /// one stays live) and on the open gallery itself, which reloads it; never behind Event setup or while the Guest Screen covers the Operator
    /// Screen (D38).
    /// </summary>
    private CommandResultMessage OpenGallery(string id, OpenGalleryPayload payload)
    {
        var reload = ActiveOperatorGallery is { } open && string.Equals(open.EventId, payload.EventId, StringComparison.Ordinal);
        if (!StorageReady
            || FindSummary(payload.EventId) is not { } summary
            || _activeEvent is { SingleScreen: true }
            || (!reload && (SetupInUse() || _operatorSurface != OperatorSurface.EventList)))
        {
            return NotAvailable(id);
        }

        CloseGuestCycleConfirmation();
        var state = new OperatorGalleryState(summary.Id, summary.Name);
        _operatorGallery = state;
        _operatorSurface = OperatorSurface.EventGallery;
        LoadOperatorPage(state);
        return Accepted(id);
    }

    /// <summary><c>LoadMoreGuestCycles(cursor)</c>: loads the next page when the cursor is the gallery's current one and nothing is loading (a retry after a failed page included).</summary>
    private CommandResultMessage LoadMoreGuestCycles(string id, LoadMoreGuestCyclesPayload payload)
    {
        if (ActiveOperatorGallery is not { Detail: null, LoadingMore: false } state
            || state.Page.NextCursor is not { } next
            || !string.Equals(next, payload.Cursor, StringComparison.Ordinal))
        {
            return NotAvailable(id);
        }

        LoadOperatorPage(state);
        return Accepted(id);
    }

    private void LoadOperatorPage(OperatorGalleryState state)
    {
        var cursor = state.Page.RequestCursor;
        var request = ++state.PageRequest;
        state.LoadingMore = true;
        state.LoadFailed = false;
        var builder = GalleryBuilder;
        var token = _storageCancellation.Token;
        Off(
            () => builder.OperatorPageAsync(state.EventId, cursor, token, entry => Post(() => PresentOperatorEntry(state, request, entry))),
            completed => CompleteOperatorPage(state, request, cursor, completed));
    }

    /// <summary>An item of the page being read has its thumbnails: it shows now, before the rest of the page (a cold page takes a moment). The page's own answer merges it in and drops the repeat.</summary>
    private void PresentOperatorEntry(OperatorGalleryState state, int request, GalleryEntry<GalleryItemView> entry)
    {
        if (ReferenceEquals(ActiveOperatorGallery, state) && request == state.PageRequest)
        {
            state.Page = state.Page.Insert(entry);
            PublishChanges();
        }
    }

    private void CompleteOperatorPage(OperatorGalleryState state, int request, string? cursor, Task<GalleryPageResult<GalleryItemView>> completed)
    {
        if (!ReferenceEquals(ActiveOperatorGallery, state) || request != state.PageRequest)
        {
            _ = completed.Exception;
            return;
        }

        state.LoadingMore = false;
        if (completed.IsCompletedSuccessfully)
        {
            state.Page = state.Page.ApplyPage(cursor, completed.Result.Entries, completed.Result.NextCursor);
        }
        else
        {
            state.LoadFailed = true;
            BrainLog.GalleryLoadFailed(_logger, ErrorTypeOf(completed));
        }
    }

    private static string ErrorTypeOf(Task completed) => completed.Exception?.GetBaseException().GetType().Name ?? "canceled";

    /// <summary><c>OpenGuestCycle(guestCycleId)</c>: reads the Guest Cycle off the executor, then shows its detail; one that is gone shows a notice and leaves the list.</summary>
    private CommandResultMessage OpenGuestCycle(string id, OpenGuestCyclePayload payload)
    {
        if (ActiveOperatorGallery is not { Detail: null } state || state.Page.Find(payload.GuestCycleId) is not { } entry)
        {
            return NotAvailable(id);
        }

        var cycleId = entry.GuestCycleId;
        var unavailable = entry.View.Unavailable;
        var request = ++state.DetailRequest;
        var store = _dependencies.Store;
        var token = _storageCancellation.Token;
        Off(() => store.GetGuestCycleAsync(cycleId, token), completed => CompleteOpenGuestCycle(state, request, cycleId, unavailable, completed));
        return Accepted(id);
    }

    private void CompleteOpenGuestCycle(OperatorGalleryState state, int request, string cycleId, bool unavailable, Task<GuestCycleDetail?> completed)
    {
        if (!ReferenceEquals(ActiveOperatorGallery, state) || request != state.DetailRequest || state.Detail is not null)
        {
            _ = completed.Exception;
            return;
        }

        if (!completed.IsCompletedSuccessfully)
        {
            BrainLog.GalleryLoadFailed(_logger, ErrorTypeOf(completed));
            return;
        }

        if (completed.Result is not { } detail)
        {
            ShowNotice(OperatorNoticeCode.GuestCycleNotFound);
            RemoveGuestCycle(state.EventId, cycleId, state.Page.Find(cycleId)?.View.Outcome == GalleryOutcome.Completed);
            return;
        }

        state.Detail = OperatorDetailOf(state, detail, unavailable);
    }

    private GuestCycleDetailView OperatorDetailOf(OperatorGalleryState state, GuestCycleDetail detail, bool unavailable)
    {
        var strip = detail.PhotoStrip;
        var templateName = strip is null ? null : _dependencies.Catalog.FindTemplate(strip.TemplateId)?.Name ?? strip.TemplateId;
        var lookName = strip is null ? null : _dependencies.Catalog.FindLook(strip.LookId)?.Name ?? strip.LookId;
        var view = GalleryRules.OperatorDetail(state.EventId, state.EventName, detail, templateName, lookName, unavailable, canDelete: true);
        return unavailable ? view with { Strip = null, Captures = [] } : view;
    }

    /// <summary><c>CloseGuestCycle</c>: back from the detail to the list, which keeps its loaded pages.</summary>
    private CommandResultMessage CloseGuestCycle(string id)
    {
        if (ActiveOperatorGallery is not { Detail: not null } state)
        {
            return NotAvailable(id);
        }

        state.Detail = null;
        state.DetailRequest++;
        return Accepted(id);
    }

    /// <summary><c>OpenGuestCycleFolder(guestCycleId)</c>: asks the host to open the Guest Cycle's folder. The path goes to the host and the diagnostics, never into a presentation.</summary>
    private CommandResultMessage OpenGuestCycleFolder(string id, OpenGuestCycleFolderPayload payload)
    {
        if (ActiveOperatorGallery is not { } state || state.Page.Find(payload.GuestCycleId) is not { } entry)
        {
            return NotAvailable(id);
        }

        RequestOpenFolder(GalleryRules.GuestCycleFolder(_dependencies.Store.DataRoot, state.EventId, entry.GuestCycleId));
        return Accepted(id);
    }

    /// <summary><c>DeleteGuestCycle(guestCycleId)</c>: counts the Guest Cycle's files off the executor, then asks "Delete this Guest Cycle?" with its time and that count.</summary>
    private CommandResultMessage DeleteGuestCycle(string id, DeleteGuestCyclePayload payload)
    {
        if (ActiveOperatorGallery is not { } state || state.Page.Find(payload.GuestCycleId) is not { } entry)
        {
            return NotAvailable(id);
        }

        var cycleId = entry.GuestCycleId;
        var request = ++state.DeleteRequest;
        state.DeletePreparing = true;
        var store = _dependencies.Store;
        var token = _storageCancellation.Token;
        Off(() => store.GetGuestCycleAsync(cycleId, token), completed => CompleteDeletePreparation(state, request, cycleId, completed));
        return Accepted(id);
    }

    private void CompleteDeletePreparation(OperatorGalleryState state, int request, string cycleId, Task<GuestCycleDetail?> completed)
    {
        if (!ReferenceEquals(ActiveOperatorGallery, state) || request != state.DeleteRequest)
        {
            _ = completed.Exception;
            return;
        }

        state.DeletePreparing = false;
        if (!completed.IsCompletedSuccessfully)
        {
            BrainLog.GalleryLoadFailed(_logger, ErrorTypeOf(completed));
            ShowNotice(OperatorNoticeCode.GuestCycleDeleteFailed);
        }
        else if (completed.Result is not { } detail)
        {
            ShowNotice(OperatorNoticeCode.GuestCycleNotFound);
            RemoveGuestCycle(state.EventId, cycleId, state.Page.Find(cycleId)?.View.Outcome == GalleryOutcome.Completed);
        }
        else
        {
            state.DeleteCycleId = cycleId;
            state.DeleteCycleCompleted = detail.Cycle.EndReason == GuestCycleEndReason.Completed;
            SetConfirmation(GalleryRules.DeleteConfirmation(detail));
        }
    }

    /// <summary>
    /// <c>ConfirmDeleteGuestCycle</c>: the Guest Cycle leaves both galleries in this turn, and only then does the store mark it deleting (off the
    /// executor; its files go in the background).
    /// </summary>
    private CommandResultMessage ConfirmDeleteGuestCycle(string id)
    {
        if (_confirmation is not { Kind: ConfirmationKind.DeleteGuestCycle } || ActiveOperatorGallery is not { DeleteCycleId: { } cycleId } state)
        {
            return NotAvailable(id);
        }

        var completed = state.DeleteCycleCompleted;
        _confirmation = null;
        state.DeleteCycleId = null;
        RemoveGuestCycle(state.EventId, cycleId, completed);
        var store = _dependencies.Store;
        OffVoid(() => store.MarkGuestCycleDeletingAsync(cycleId, CancellationToken.None), marked => CompleteDelete(state.EventId, completed, marked));
        return Accepted(id);
    }

    private void CompleteDelete(string eventId, bool completedCycle, Task marked)
    {
        if (marked.IsCompletedSuccessfully)
        {
            RefreshEventList();
            return;
        }

        var kind = (FailureOf(marked) as StorageException)?.Kind;
        BrainLog.GuestCycleDeleteFailed(_logger, ErrorTypeOf(marked), kind);
        ShowNotice(kind == StorageErrorKind.NotFound ? OperatorNoticeCode.GuestCycleNotFound : OperatorNoticeCode.GuestCycleDeleteFailed);
        if (kind != StorageErrorKind.NotFound && completedCycle && _activeEvent is { } active && string.Equals(active.Event.Id, eventId, StringComparison.Ordinal))
        {
            active.CompletedGuestCycles++;
        }

        ReloadGalleries();
        RefreshEventList();
    }

    /// <summary>Reads the first page of each open gallery again, so a Guest Cycle whose delete failed is back when it still exists.</summary>
    private void ReloadGalleries()
    {
        if (ActiveOperatorGallery is { } state)
        {
            state.Page = GalleryPage.Empty<GalleryEntry<GalleryItemView>>();
            LoadOperatorPage(state);
        }

        ReloadGuestGallery();
    }

    /// <summary><c>CancelDeleteGuestCycle</c>: closes the confirmation.</summary>
    private CommandResultMessage CancelDeleteGuestCycle(string id)
    {
        if (_confirmation is not { Kind: ConfirmationKind.DeleteGuestCycle } || ActiveOperatorGallery is not { } state)
        {
            return NotAvailable(id);
        }

        _confirmation = null;
        state.DeleteCycleId = null;
        return Accepted(id);
    }

    /// <summary><c>CloseGallery</c>: back to the Event list. The gallery's state is dropped, so answers still in flight are ignored.</summary>
    private CommandResultMessage CloseGallery(string id)
    {
        if (ActiveOperatorGallery is null)
        {
            return NotAvailable(id);
        }

        _operatorGallery = null;
        _operatorSurface = OperatorSurface.EventList;
        CloseGuestCycleConfirmation();
        return Accepted(id);
    }

    private void CloseGuestCycleConfirmation()
    {
        if (_confirmation?.Kind == ConfirmationKind.DeleteGuestCycle)
        {
            _confirmation = null;
        }
    }

    /// <summary>
    /// The operator presentation while a gallery is open (<c>eventGallery</c> or, with a Guest Cycle open, <c>guestCycleDetail</c>, with
    /// <paramref name="panel"/> and <paramref name="overlays"/> as on every operator surface), or null when the operator isn't in one.
    /// </summary>
    private OperatorPresentation? RenderEventGallery(RunningEventPanel? panel, OperatorOverlays overlays)
    {
        if (ActiveOperatorGallery is not { } state)
        {
            return null;
        }

        if (state.Detail is { } detail)
        {
            return new OperatorPresentation(
                OperatorSurface.GuestCycleDetail, null, null, panel, overlays, null, null, null, null, detail with { CanDelete = !state.DeletePreparing });
        }

        var view = new EventGalleryView(
            state.EventId,
            state.EventName,
            string.Equals(ActiveEventId, state.EventId, StringComparison.Ordinal),
            [.. state.Page.Items.Select(entry => entry.View)],
            state.Page.NextCursor,
            state.LoadingMore,
            state.LoadFailed,
            state.Page.IsEmpty);
        return new OperatorPresentation(OperatorSurface.EventGallery, null, null, panel, overlays, null, null, null, view, null);
    }

    /// <summary>The galleries for <c>test.getDiagnostics</c>, or null while neither is open.</summary>
    private GalleryDiagnostics? GalleryDiagnosticsNow()
    {
        var operatorGallery = ActiveOperatorGallery is { } state
            ? new OperatorGalleryDiagnostics(state.EventId, state.Page.Items.Length, state.Page.NextCursor, state.Detail?.GuestCycleId)
            : null;
        var guestGallery = GuestGalleryDiagnosticsNow();
        return operatorGallery is null && guestGallery is null ? null : new GalleryDiagnostics(operatorGallery, guestGallery);
    }

    /// <summary>
    /// A Guest Cycle ended (the Photo Strip was committed, or the cycle was started over, ended by the operator, or closed). The operator list of
    /// that Event gets it at the top, and the guest list too when it completed; a completed cycle's strip thumbnail is made now, so browsing finds it
    /// ready. Called on the executor after the store accepted the end; the caller publishes.
    /// </summary>
    internal void OnGuestCycleEnded(string guestCycleId, GuestCycleEndReason reason)
    {
        var operatorGallery = ActiveOperatorGallery;
        var guestGallery = reason == GuestCycleEndReason.Completed ? ActiveGuestGallery : null;
        if (operatorGallery is null && reason != GuestCycleEndReason.Completed)
        {
            return;
        }

        var builder = GalleryBuilder;
        var token = _storageCancellation.Token;
        Off(
            () => builder.EndedCycleAsync(guestCycleId, operatorGallery is not null, guestGallery is not null, token),
            completed => CompleteGuestCycleEnded(operatorGallery, guestGallery, completed));
    }

    private void CompleteGuestCycleEnded(OperatorGalleryState? operatorGallery, GuestGalleryState? guestGallery, Task<EndedCycleEntries?> completed)
    {
        if (!completed.IsCompletedSuccessfully)
        {
            BrainLog.GalleryLoadFailed(_logger, ErrorTypeOf(completed));
            return;
        }

        if (completed.Result is not { } ended)
        {
            return;
        }

        if (ended.Operator is { } item
            && operatorGallery is not null
            && ReferenceEquals(ActiveOperatorGallery, operatorGallery)
            && string.Equals(operatorGallery.EventId, ended.EventId, StringComparison.Ordinal))
        {
            operatorGallery.Page = operatorGallery.Page.Insert(item);
        }

        if (ended.Guest is { } strip && guestGallery is not null && ReferenceEquals(ActiveGuestGallery, guestGallery))
        {
            guestGallery.Page = guestGallery.Page.Insert(strip);
        }
    }

    /// <summary>
    /// A Guest Cycle was deleted by the operator: both galleries drop it at once (a guest browsing its strip returns to the list, or to Start when it
    /// was the last one, and a guest looking at its Keepsake returns to Start). Called on the executor; the caller publishes.
    /// </summary>
    internal void OnGuestCycleDeleted(string guestCycleId)
    {
        if (_operatorGallery is { } state)
        {
            state.Page = state.Page.Remove(guestCycleId);
            if (state.Detail is { } detail && string.Equals(detail.GuestCycleId, guestCycleId, StringComparison.Ordinal))
            {
                state.Detail = null;
                state.DetailRequest++;
            }
        }

        GuestGalleryCycleDeleted(guestCycleId);
        if (CurrentRun is { Completed: true } run && string.Equals(run.Id, guestCycleId, StringComparison.Ordinal))
        {
            ReturnToStart(run, null);
        }
    }

    /// <summary>A Guest Cycle is gone from the Event (deleted by the operator, or found missing): the Active Event's completed count falls when it completed, then both galleries drop it.</summary>
    private void RemoveGuestCycle(string eventId, string guestCycleId, bool completed)
    {
        if (completed && _activeEvent is { CompletedGuestCycles: > 0 } active && string.Equals(active.Event.Id, eventId, StringComparison.Ordinal))
        {
            active.CompletedGuestCycles--;
        }

        OnGuestCycleDeleted(guestCycleId);
    }
}

internal static partial class BrainLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading the Event Gallery from the store failed ({ErrorType}).")]
    public static partial void GalleryLoadFailed(ILogger logger, string errorType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Deleting a Guest Cycle failed ({ErrorType}, {Kind}).")]
    public static partial void GuestCycleDeleteFailed(ILogger logger, string errorType, StorageErrorKind? kind);
}
