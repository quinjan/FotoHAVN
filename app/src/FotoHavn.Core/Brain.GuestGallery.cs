using FotoHavn.Core.Events;
using FotoHavn.Core.Events.Active;
using FotoHavn.Core.Gallery;
using FotoHavn.Core.Gallery.Orchestration;
using FotoHavn.Core.GuestCycles;
using FotoHavn.Core.GuestCycles.Orchestration;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core;

// The guest gallery and its strip detail (plan section 8, Event Gallery, Guest view; M9 contract section 4.2). The guest view carries only image
// URLs and alt text (Gallery/GalleryRules.cs builds it), and its commands can never reach an operator action: the roles are checked before dispatch
// (CommandRegistry). It exists from OpenGuestGallery until the guest leaves, the time runs out, Operator Assistance begins, or the Event ends. Its
// Phase Timer (guest_gallery, default 60 s, never off) starts when the gallery opens and is not reset by browsing; the last 20 seconds show the usual
// phaseTimerWarning overlay with phase guestGallery, and MoreTime (Brain.GuestCycleTimers.cs) restarts the full time.
public sealed partial class Brain
{
    private GuestGalleryState? _guestGallery;

    /// <summary>The open guest gallery of the running Event, or null.</summary>
    private GuestGalleryState? ActiveGuestGallery => _guestGallery is { } state && ReferenceEquals(state.Active, _activeEvent) ? state : null;

    /// <summary>Whether <c>SEE THE GOOD COMPANY →</c> shows on Start: <see cref="GalleryRules.GuestLinkVisible"/> over the Event's setting and the Active Event's completed Photo Strips.</summary>
    private static bool GuestGalleryLinkVisible(ActiveEventState active) =>
        GalleryRules.GuestLinkVisible(active.Event.GuestGalleryEnabled, active.CompletedGuestCycles);

    /// <summary>Whether the link is shown on the Guest Screen and the gallery can open: <see cref="GuestGalleryLinkVisible"/> and readable storage (a link that <c>OpenGuestGallery</c> would refuse is never shown). The camera isn't needed.</summary>
    private bool GuestGalleryLinkShown(ActiveEventState active) =>
        GuestGalleryLinkVisible(active) && StorageReady && !active.StorageFailed;

    /// <summary>
    /// <c>OpenGuestGallery</c> (<c>SEE THE GOOD COMPANY →</c>): shows the gallery with its first page loading and starts its timer. Only from Start
    /// (no cycle, no Operator Assistance, no PIN dialog) while the link shows; the camera isn't needed.
    /// </summary>
    private CommandResultMessage OpenGuestGallery(string id)
    {
        if (_activeEvent is not { } active
            || !GuestGalleryLinkShown(active)
            || ActiveGuestGallery is not null
            || _cycle is not null
            || _cycleStarting
            || _assistance is not null
            || active.PinDialogOpen)
        {
            return NotAvailable(id);
        }

        var state = new GuestGalleryState(active);
        _guestGallery = state;
        StartGuestGalleryTimer(state);
        LoadGuestPage(state);
        return Accepted(id);
    }

    /// <summary><c>LoadMoreGuestStrips(cursor)</c>: loads the next page when the list shows the current cursor and nothing is loading.</summary>
    private CommandResultMessage LoadMoreGuestStrips(string id, LoadMoreGuestStripsPayload payload)
    {
        if (ActiveGuestGallery is not { Detail: null, LoadingMore: false } state
            || state.Page.NextCursor is not { } next
            || !string.Equals(next, payload.Cursor, StringComparison.Ordinal))
        {
            return NotAvailable(id);
        }

        LoadGuestPage(state);
        return Accepted(id);
    }

    private void LoadGuestPage(GuestGalleryState state)
    {
        var cursor = state.Page.RequestCursor;
        var request = ++state.PageRequest;
        state.LoadingMore = true;
        var builder = GalleryBuilder;
        var eventId = state.Active.Event.Id;
        var token = _storageCancellation.Token;
        Off(() => builder.GuestPageAsync(eventId, cursor, token), completed => CompleteGuestPage(state, request, cursor, completed));
    }

    private void CompleteGuestPage(GuestGalleryState state, int request, string? cursor, Task<GalleryPageResult<GuestStripView>> completed)
    {
        if (!ReferenceEquals(ActiveGuestGallery, state) || request != state.PageRequest)
        {
            _ = completed.Exception;
            return;
        }

        state.LoadingMore = false;
        var first = !state.Page.Loaded;
        if (!completed.IsCompletedSuccessfully)
        {
            BrainLog.GalleryLoadFailed(_logger, ErrorTypeOf(completed));

            // A first page that can't be read sends the guest back to Start; a later one just ends the row.
            if (first)
            {
                DropGuestGallery();
            }
            else
            {
                state.Page = state.Page.ApplyPage(cursor, [], null);
            }

            return;
        }

        state.Page = state.Page.ApplyPage(cursor, completed.Result.Entries, completed.Result.NextCursor);
        if (state.Page.Items.IsEmpty)
        {
            DropGuestGallery();
        }
    }

    /// <summary><c>OpenGuestStrip(guestCycleId)</c>: reads the strip's Guest Cycle off the executor, then shows the strip large with its arranged Captures. The timer isn't restarted.</summary>
    private CommandResultMessage OpenGuestStrip(string id, OpenGuestStripPayload payload)
    {
        if (ActiveGuestGallery is not { Detail: null } state || state.Page.Find(payload.GuestCycleId) is not { } entry)
        {
            return NotAvailable(id);
        }

        var cycleId = entry.GuestCycleId;
        var request = ++state.DetailRequest;
        state.DetailId = cycleId;
        var store = _dependencies.Store;
        var token = _storageCancellation.Token;
        Off(() => store.GetGuestCycleAsync(cycleId, token), completed => CompleteOpenGuestStrip(state, request, cycleId, completed));
        return Accepted(id);
    }

    private void CompleteOpenGuestStrip(GuestGalleryState state, int request, string cycleId, Task<GuestCycleDetail?> completed)
    {
        if (!ReferenceEquals(ActiveGuestGallery, state) || request != state.DetailRequest)
        {
            _ = completed.Exception;
            return;
        }

        if (!completed.IsCompletedSuccessfully)
        {
            BrainLog.GalleryLoadFailed(_logger, ErrorTypeOf(completed));
            state.DetailId = null;
            return;
        }

        if (completed.Result is { } detail && GalleryRules.IsGuestVisible(detail))
        {
            state.Detail = GalleryRules.GuestStripDetail(state.Active.Event.Id, detail);
            return;
        }

        // A strip that is gone leaves the row; the last one leaves the gallery.
        state.DetailId = null;
        state.Page = state.Page.Remove(cycleId);
        if (state.Page.IsEmpty)
        {
            DropGuestGallery();
        }
    }

    /// <summary><c>CloseGuestStrip</c>: back from the strip detail to the gallery.</summary>
    private CommandResultMessage CloseGuestStrip(string id)
    {
        if (ActiveGuestGallery is not { Detail: not null } state)
        {
            return NotAvailable(id);
        }

        CloseGuestStripDetail(state);
        return Accepted(id);
    }

    private static void CloseGuestStripDetail(GuestGalleryState state)
    {
        state.Detail = null;
        state.DetailId = null;
        state.DetailRequest++;
    }

    /// <summary><c>BackToStart</c> (<c>BACK TO START ←</c>): leaves the gallery or the strip detail for Start.</summary>
    private CommandResultMessage BackToStart(string id)
    {
        if (ActiveGuestGallery is null)
        {
            return NotAvailable(id);
        }

        DropGuestGallery();
        return Accepted(id);
    }

    /// <summary>Forgets the guest gallery and its timer: the guest is on Start, and every late answer is dropped. Called on the executor; the caller publishes.</summary>
    private void DropGuestGallery()
    {
        if (_guestGallery is not { } state)
        {
            return;
        }

        state.TimerWake?.Dispose();
        state.TimerWake = null;
        state.Timer = PhaseTimerEngine.Stopped;
        state.TimerGeneration++;
        _guestGallery = null;
    }

    /// <summary>The operator deleted a Guest Cycle: its strip leaves the row, a guest on its detail returns to the list, and the last strip sends the guest to Start.</summary>
    private void GuestGalleryCycleDeleted(string guestCycleId)
    {
        if (ActiveGuestGallery is not { } state)
        {
            return;
        }

        var wasShown = state.Page.Contains(guestCycleId);
        state.Page = state.Page.Remove(guestCycleId);
        if (string.Equals(state.DetailId, guestCycleId, StringComparison.Ordinal))
        {
            CloseGuestStripDetail(state);
        }

        if (!GuestGalleryLinkVisible(state.Active) || (wasShown && state.Page.IsEmpty))
        {
            DropGuestGallery();
        }
    }

    /// <summary>Reads the first page again (a Guest Cycle whose delete failed may be back).</summary>
    private void ReloadGuestGallery()
    {
        if (ActiveGuestGallery is { } state)
        {
            state.Page = GalleryPage.Empty<GalleryEntry<GuestStripView>>();
            LoadGuestPage(state);
        }
    }

    // The guest gallery's Phase Timer: one PhaseTimerEngine and one wake-up at its next deadline or, while the warning shows, at the next whole second.

    private void StartGuestGalleryTimer(GuestGalleryState state)
    {
        var configured = state.Active.Event.PhaseTimers.FirstOrDefault(setting => setting.Phase == PhaseTimerPhase.GuestGallery)?.Seconds ?? 0;
        var seconds = configured > 0 ? configured : PhaseTimerRules.Default(PhaseTimerPhase.GuestGallery);
        state.Timer = PhaseTimerEngine.Start(PhaseTimerPhase.GuestGallery, seconds, _dependencies.Clock.UtcNow);
        RescheduleGuestGalleryTimer(state);
    }

    private void RescheduleGuestGalleryTimer(GuestGalleryState state)
    {
        state.TimerWake?.Dispose();
        state.TimerWake = null;
        var generation = ++state.TimerGeneration;
        var now = _dependencies.Clock.UtcNow;
        if (state.Timer.NextDeadline(now) is not { } deadline)
        {
            return;
        }

        var delay = GuestCyclePlan.WakeDelay(deadline - now, state.Timer.Remaining(now)!.Value, state.Timer.IsWarning(now));
        state.TimerWake = _dependencies.Clock.Schedule(delay, () => Post(() => OnGuestGalleryTimerWake(state, generation)));
    }

    private void OnGuestGalleryTimerWake(GuestGalleryState state, int generation)
    {
        if (!ReferenceEquals(_guestGallery, state) || generation != state.TimerGeneration)
        {
            return;
        }

        if (state.Timer.IsExpired(_dependencies.Clock.UtcNow))
        {
            DropGuestGallery();
        }
        else
        {
            RescheduleGuestGalleryTimer(state);
        }

        PublishChanges();
    }

    /// <summary><c>MoreTime</c> on the gallery or a strip detail: restarts the full time while the warning shows. Called by <see cref="MoreTime"/> when no Guest Cycle takes it.</summary>
    private CommandResultMessage MoreTimeInGallery(string id)
    {
        var now = _dependencies.Clock.UtcNow;
        if (ActiveGuestGallery is not { } state || !state.Timer.IsWarning(now))
        {
            return NotAvailable(id);
        }

        state.Timer = state.Timer.Restart(now);
        RescheduleGuestGalleryTimer(state);
        return Accepted(id);
    }

    private PhaseTimerWarningOverlay? GalleryWarningOverlay(GuestGalleryState state)
    {
        var now = _dependencies.Clock.UtcNow;
        return state.Timer.IsWarning(now)
            ? new PhaseTimerWarningOverlay(PhaseTimerRules.ToProtocol(PhaseTimerPhase.GuestGallery), GuestCyclePlan.WholeSecondsLeft(state.Timer.Remaining(now)!.Value))
            : null;
    }

    /// <summary>
    /// The Guest Screen's presentation while the guest gallery or a strip detail is open, or null when the guest is on Start (or in a Guest Cycle,
    /// which <c>RenderGuestCycle</c> renders first). <paramref name="overlays"/> holds the exit hold and PIN dialog; the gallery adds
    /// <c>phaseTimerWarning</c>.
    /// </summary>
    private GuestPresentation? RenderGuestGallery(ActiveEventState active, GuestOverlays overlays)
    {
        if (ActiveGuestGallery is not { } state)
        {
            return null;
        }

        var shown = overlays with { PhaseTimerWarning = GalleryWarningOverlay(state) };
        if (state.Detail is { } detail)
        {
            return new GuestPresentation(GuestSurface.GuestStripDetail, null, null, shown, null, null, null, null, null, null, null, null, detail);
        }

        var view = new GuestGalleryView([.. state.Page.Items.Select(entry => entry.View)], state.Page.NextCursor, state.LoadingMore);
        return new GuestPresentation(GuestSurface.GuestGallery, null, null, shown, null, null, null, null, null, null, null, view, null);
    }

    private GuestGalleryDiagnostics? GuestGalleryDiagnosticsNow()
    {
        if (ActiveGuestGallery is not { } state)
        {
            return null;
        }

        var now = _dependencies.Clock.UtcNow;
        return new GuestGalleryDiagnostics(
            state.Page.Items.Length,
            state.Page.NextCursor,
            state.Detail is null ? null : state.DetailId,
            new GuestCycleTimerDiagnostics(
                PhaseTimerRules.ToProtocol(PhaseTimerPhase.GuestGallery),
                (long)state.Timer.Remaining(now)!.Value.TotalMilliseconds,
                state.Timer.IsPaused,
                state.Timer.IsWarning(now)));
    }
}
