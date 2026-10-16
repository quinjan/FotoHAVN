using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core;

// Wave A's stubs touch no brain state yet; B1's bodies do, and then this suppression goes.
#pragma warning disable CA1822

// The operator's Event Gallery (plan section 8, Event Gallery, Operator view; M9 contract). Owner: M9 wave B1. Wave A wired the dispatch
// (Brain.Commands.cs), the render hook (RenderOperator), the diagnostics hook, and the live-update notifications; every command is a stub that
// answers rejected notAvailable until B1 builds it. The pure rules are in Gallery/GalleryRules.cs and the page state in Gallery/GalleryPage.cs;
// this partial only orchestrates them. Every handler runs on the executor, answers at once, and does store work off it (the Off helper in
// Brain.GuestCycle.cs); the outcome is posted back carrying what it needs to be judged stale (the gallery's request number).
public sealed partial class Brain
{
    /// <summary><c>OpenGallery(eventId)</c>: opens the Event's gallery and loads its first page. B1.</summary>
    private CommandResultMessage OpenGallery(string id, OpenGalleryPayload payload) => NotAvailable(id);

    /// <summary><c>LoadMoreGuestCycles(cursor)</c>: loads the next page when the cursor is the gallery's current one. B1.</summary>
    private CommandResultMessage LoadMoreGuestCycles(string id, LoadMoreGuestCyclesPayload payload) => NotAvailable(id);

    /// <summary><c>OpenGuestCycle(guestCycleId)</c>: shows the Guest Cycle's detail. B1.</summary>
    private CommandResultMessage OpenGuestCycle(string id, OpenGuestCyclePayload payload) => NotAvailable(id);

    /// <summary><c>CloseGuestCycle</c>: back from the detail to the list. B1.</summary>
    private CommandResultMessage CloseGuestCycle(string id) => NotAvailable(id);

    /// <summary><c>OpenGuestCycleFolder(guestCycleId)</c>: asks the host to open the Guest Cycle's folder through <c>RequestOpenFolder</c>. B1.</summary>
    private CommandResultMessage OpenGuestCycleFolder(string id, OpenGuestCycleFolderPayload payload) => NotAvailable(id);

    /// <summary><c>DeleteGuestCycle(guestCycleId)</c>: opens the delete confirmation. B1.</summary>
    private CommandResultMessage DeleteGuestCycle(string id, DeleteGuestCyclePayload payload) => NotAvailable(id);

    /// <summary><c>ConfirmDeleteGuestCycle</c>: removes the Guest Cycle from both galleries at once and marks it deleting in the store. B1.</summary>
    private CommandResultMessage ConfirmDeleteGuestCycle(string id) => NotAvailable(id);

    /// <summary><c>CancelDeleteGuestCycle</c>: closes the confirmation. B1.</summary>
    private CommandResultMessage CancelDeleteGuestCycle(string id) => NotAvailable(id);

    /// <summary><c>CloseGallery</c>: back to the Event list. B1.</summary>
    private CommandResultMessage CloseGallery(string id) => NotAvailable(id);

    /// <summary>
    /// The operator presentation while a gallery is open (<c>eventGallery</c> or <c>guestCycleDetail</c>, with <paramref name="panel"/> and
    /// <paramref name="overlays"/> as on every operator surface), or null when the operator isn't in one. B1.
    /// </summary>
    private OperatorPresentation? RenderEventGallery(RunningEventPanel? panel, OperatorOverlays overlays) => null;

    /// <summary>The galleries for <c>test.getDiagnostics</c>, or null while neither is open. B1.</summary>
    private GalleryDiagnostics? GalleryDiagnosticsNow() => null;

    /// <summary>
    /// A Guest Cycle ended (the Photo Strip was committed, or the cycle was started over, ended by the operator, or closed). Both galleries update
    /// live: the operator list gets it at the top, and the guest list too when it completed. Called on the executor after the store accepted the end;
    /// the caller publishes. B1 fills it.
    /// </summary>
    private void OnGuestCycleEnded(string guestCycleId, GuestCycleEndReason reason)
    {
    }

    /// <summary>
    /// A Guest Cycle was deleted by the operator: both galleries drop it at once (and a guest browsing its strip returns to the list, or to Start
    /// when it was the last one). Called on the executor by <c>ConfirmDeleteGuestCycle</c>; the caller publishes. B1 fills it.
    /// </summary>
    private void OnGuestCycleDeleted(string guestCycleId)
    {
    }
}
