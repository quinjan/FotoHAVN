using FotoHavn.Core.Events.Active;
using FotoHavn.Core.GuestCycles;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Gallery.Orchestration;

/// <summary>
/// The operator's open Event Gallery: the Event, the pages loaded so far, and the request numbers that let the brain ignore an answer that
/// arrives after the gallery moved on. Brain state, touched only on the executor. A new state is made for every <c>OpenGallery</c>, so an
/// answer to an older one never matches (the brain compares the reference too).
/// </summary>
internal sealed class OperatorGalleryState(string eventId, string eventName)
{
    public string EventId => eventId;

    public string EventName => eventName;

    public GalleryPage<GalleryEntry<GalleryItemView>> Page { get; set; } = GalleryPage.Empty<GalleryEntry<GalleryItemView>>();

    /// <summary>A page is being read; the first page loading is <see cref="Page"/> not yet loaded.</summary>
    public bool LoadingMore { get; set; } = true;

    public bool LoadFailed { get; set; }

    /// <summary>Counts page requests, so only the answer to the latest one is applied.</summary>
    public int PageRequest { get; set; }

    /// <summary>The Guest Cycle whose detail is open, as the operator sees it; null shows the list.</summary>
    public GuestCycleDetailView? Detail { get; set; }

    /// <summary>Counts detail requests (and closes), so a late detail never opens over a list the operator went back to.</summary>
    public int DetailRequest { get; set; }

    /// <summary>The brain is counting the files of the Guest Cycle the operator asked to delete; the detail can't delete meanwhile.</summary>
    public bool DeletePreparing { get; set; }

    /// <summary>Counts delete requests, so a newer one replaces an older one still counting.</summary>
    public int DeleteRequest { get; set; }

    /// <summary>The Guest Cycle the open delete confirmation is for, or null.</summary>
    public string? DeleteCycleId { get; set; }

    /// <summary>Whether that Guest Cycle completed (it counts toward the Event's completed Photo Strips).</summary>
    public bool DeleteCycleCompleted { get; set; }
}

/// <summary>
/// The guest's open gallery: the Active Event it belongs to, the strips loaded so far, the open strip, and its Phase Timer. Brain state, touched
/// only on the executor. It exists from <c>OpenGuestGallery</c> until the guest leaves, the time runs out, Operator Assistance begins, or the
/// Event ends; the next guest starts from Start.
/// </summary>
internal sealed class GuestGalleryState(ActiveEventState active)
{
    public ActiveEventState Active => active;

    public GalleryPage<GalleryEntry<GuestStripView>> Page { get; set; } = GalleryPage.Empty<GalleryEntry<GuestStripView>>();

    public bool LoadingMore { get; set; } = true;

    public int PageRequest { get; set; }

    /// <summary>The strip detail being shown, or null for the list.</summary>
    public GuestStripDetailView? Detail { get; set; }

    public int DetailRequest { get; set; }

    /// <summary>The Guest Cycle whose strip is open or loading; the diagnostics name it.</summary>
    public string? DetailId { get; set; }

    public PhaseTimerEngine Timer { get; set; } = PhaseTimerEngine.Stopped;

    public IDisposable? TimerWake { get; set; }

    public int TimerGeneration { get; set; }
}
