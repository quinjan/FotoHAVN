using FotoHavn.Core.Events.Active;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core;

// Wave A's stubs touch no brain state yet; B1's bodies do, and then this suppression goes.
#pragma warning disable CA1822

// The guest gallery and its strip detail (plan section 8, Event Gallery, Guest view; M9 contract). Owner: M9 wave B1. Wave A wired the dispatch
// (Brain.Commands.cs), the render hook (RenderGuestShell), and the Start link hook; every command is a stub that answers rejected notAvailable
// until B1 builds it. The guest view carries only image URLs and alt text (Gallery/GalleryRules.cs builds it), and its commands can never reach an
// operator action: the roles are checked before dispatch (CommandRegistry). The guest gallery's Phase Timer (guest_gallery, default 60 s, never off)
// shows the usual phaseTimerWarning overlay with phase guestGallery and takes MoreTime; B1 adds the gallery branch to MoreTime
// (Brain.GuestCycleTimers.cs).
public sealed partial class Brain
{
    /// <summary><c>OpenGuestGallery</c> (<c>SEE THE GOOD COMPANY →</c>): shows the guest gallery and loads its first page. B1.</summary>
    private CommandResultMessage OpenGuestGallery(string id) => NotAvailable(id);

    /// <summary><c>LoadMoreGuestStrips(cursor)</c>: loads the next page when the cursor is the gallery's current one. B1.</summary>
    private CommandResultMessage LoadMoreGuestStrips(string id, LoadMoreGuestStripsPayload payload) => NotAvailable(id);

    /// <summary><c>OpenGuestStrip(guestCycleId)</c>: shows the strip large with its arranged Captures. B1.</summary>
    private CommandResultMessage OpenGuestStrip(string id, OpenGuestStripPayload payload) => NotAvailable(id);

    /// <summary><c>CloseGuestStrip</c>: back from the strip detail to the gallery. B1.</summary>
    private CommandResultMessage CloseGuestStrip(string id) => NotAvailable(id);

    /// <summary><c>BackToStart</c> (<c>BACK TO START ←</c>): leaves the gallery or the strip detail for Start. B1.</summary>
    private CommandResultMessage BackToStart(string id) => NotAvailable(id);

    /// <summary>
    /// The Guest Screen's presentation while the guest gallery or a strip detail is open, or null when the guest is on Start (or in a Guest Cycle,
    /// which <c>RenderGuestCycle</c> renders first). <paramref name="overlays"/> holds the exit hold and PIN dialog; the gallery adds
    /// <c>phaseTimerWarning</c> with <c>overlays with { ... }</c>. B1.
    /// </summary>
    private GuestPresentation? RenderGuestGallery(ActiveEventState active, GuestOverlays overlays) => null;

    /// <summary>Whether <c>SEE THE GOOD COMPANY →</c> shows on Start: <see cref="Gallery.GalleryRules.GuestLinkVisible"/> over the Event's setting and the Active Event's completed Photo Strips. B1.</summary>
    private bool GuestGalleryLinkVisible(ActiveEventState active) => false;
}
