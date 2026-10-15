namespace FotoHavn.Protocol.Presentations;

// The Guest Cycle's views (plan section 7, Steps; M8 contract). Each guest screen renders from its view alone, plus the
// guestCycle record that is present for the whole cycle. Media URLs are built by the brain; the UI never makes one.

/// <summary>A Guest Cycle step as the step progress and diagnostics name it. <see cref="GuestStep"/> (the step a guest can go to) has no Keepsake.</summary>
public enum GuestCycleStepName
{
    Template,
    Photographs,
    Filters,
    Keepsake,
}

/// <summary>Where one step is in the step progress.</summary>
public enum GuestStepState
{
    /// <summary>The guest has been through it.</summary>
    Done,

    /// <summary>The guest is on it.</summary>
    Current,

    /// <summary>Ahead of the guest.</summary>
    Upcoming,

    /// <summary>The Event offers one choice, so the step is skipped (one Template, one Look).</summary>
    Skipped,
}

/// <summary>One step in the step progress.</summary>
/// <param name="Step">The step.</param>
/// <param name="State">Done, current, upcoming, or skipped.</param>
/// <param name="CanGoBack">Whether tapping it goes there (<c>GoToStep</c>): true for a done, non-skipped step while the cycle can still change. Never on Keepsake.</param>
public sealed record GuestCycleStepEntry(GuestCycleStepName Step, GuestStepState State, bool CanGoBack);

/// <summary>The running Guest Cycle, present on every cycle step and on Operator Assistance during one.</summary>
/// <param name="Id">The Guest Cycle's id (it is in the media URLs; it is not operator data).</param>
/// <param name="Step">The step the guest is on (the step the cycle returns to after Operator Assistance).</param>
/// <param name="Steps">All four steps in order with their state, for the step progress.</param>
/// <param name="StartOverAvailable">Whether the quiet <c>START OVER</c> action shows: Template, Photographs, and Filters, never Keepsake.</param>
public sealed record GuestCycleView(string Id, GuestCycleStepName Step, IReadOnlyList<GuestCycleStepEntry> Steps, bool StartOverAvailable);

/// <summary>A photograph window in a Template, in the Template's native pixels.</summary>
public sealed record TemplateWindowView(int X, int Y, int Width, int Height);

/// <summary>One Template the Event offers, with the geometry to draw its blank print.</summary>
/// <param name="Id">The Template id.</param>
/// <param name="Name">The Template's name (brain copy).</param>
/// <param name="Description">The Template's one-line description (brain copy).</param>
/// <param name="Width">The paper width in pixels.</param>
/// <param name="Height">The paper height in pixels.</param>
/// <param name="Landscape">Whether the paper is wider than tall (the UI rotates the print a quarter turn).</param>
/// <param name="Windows">The photograph windows in reading order.</param>
/// <param name="BlankPreviewKey">The preview key of the blank Template (<c>https://preview.fotohavn/&lt;key&gt;.png</c>), or null until it is composed.</param>
public sealed record TemplateOptionView(
    string Id,
    string Name,
    string Description,
    int Width,
    int Height,
    bool Landscape,
    IReadOnlyList<TemplateWindowView> Windows,
    string? BlankPreviewKey);

/// <summary>The Template step.</summary>
/// <param name="Templates">The Templates the Event offers, in the Event's order.</param>
/// <param name="SelectedId">The selected Template.</param>
/// <param name="CanContinue">Whether <c>CONTINUE TO PHOTOGRAPHS →</c> works (always true while a Template is selected).</param>
public sealed record TemplateView(IReadOnlyList<TemplateOptionView> Templates, string SelectedId, bool CanContinue);

/// <summary>The Photographs step's two tabs.</summary>
public enum PhotographsTabName
{
    Camera,
    Arrange,
}

/// <summary>The selected Template as the Photographs step draws it.</summary>
/// <param name="Id">The Template id.</param>
/// <param name="Width">The paper width in pixels.</param>
/// <param name="Height">The paper height in pixels.</param>
/// <param name="Landscape">Whether the paper is wider than tall.</param>
/// <param name="Windows">The photograph windows in reading order.</param>
/// <param name="BlankPreviewKey">The preview key of the blank Template, or null until it is composed.</param>
public sealed record PhotographsTemplateView(string Id, int Width, int Height, bool Landscape, IReadOnlyList<TemplateWindowView> Windows, string? BlankPreviewKey);

/// <summary>One photograph window on the Photographs step.</summary>
/// <param name="Index">The window's index, in reading order.</param>
/// <param name="CaptureId">The Capture in it, or null when empty.</param>
/// <param name="ImageUrl">The Capture's media URL (<c>https://media.fotohavn/events/&lt;event&gt;/guest-cycles/&lt;cycle&gt;/capture-&lt;n&gt;.jpg</c>), or null when empty.</param>
public sealed record PhotographsWindowView(int Index, string? CaptureId, string? ImageUrl);

/// <summary>The capture sequence's progress. While a Retake runs, the sequence describes it too (one window).</summary>
/// <param name="Running">Whether a sequence or Retake is running (countdown, flash, or the pause).</param>
/// <param name="TargetWindow">The window being captured now, or null when idle.</param>
/// <param name="CountdownRemaining">The whole seconds left in the countdown, pushed every second; null between countdowns.</param>
/// <param name="FlashSequence">Increments once per Capture; the UI plays one flash per increment and ignores a value it has seen.</param>
/// <param name="CapturedInSequence">Captures taken in this sequence so far.</param>
/// <param name="TotalInSequence">Captures this sequence takes in all.</param>
/// <param name="Pausing">Whether the 800 ms pause after a flash is running.</param>
public sealed record CaptureSequenceView(
    bool Running,
    int? TargetWindow,
    int? CountdownRemaining,
    long FlashSequence,
    int CapturedInSequence,
    int TotalInSequence,
    bool Pausing);

/// <summary>What the guest has selected on the strip.</summary>
public enum PhotographSelectionMode
{
    /// <summary>Nothing selected.</summary>
    None,

    /// <summary>A photograph is selected: <c>RETAKE</c> and <c>MOVE</c> show.</summary>
    Selected,

    /// <summary>A Move is waiting for a destination.</summary>
    Moving,

    /// <summary>A Retake is running or its candidate is waiting for a decision.</summary>
    Retaking,
}

/// <summary>The selection on the Photographs step.</summary>
/// <param name="Index">The selected window, or null when none.</param>
/// <param name="Mode">None, selected, moving, or retaking.</param>
public sealed record PhotographSelectionView(int? Index, PhotographSelectionMode Mode);

/// <summary>A Retake's pending replacement, shown beside the original until the guest decides.</summary>
/// <param name="Index">The window it would replace.</param>
/// <param name="ImageUrl">The candidate's media URL.</param>
public sealed record RetakeCandidateView(int Index, string ImageUrl);

/// <summary>The Photographs step (Camera and Arrange tabs).</summary>
/// <param name="Tab">The tab showing.</param>
/// <param name="Template">The selected Template.</param>
/// <param name="Windows">One entry per window, in order.</param>
/// <param name="RetainedCount">Kept Captures in the retained pool (a smaller Template pushed them out).</param>
/// <param name="Sequence">The capture sequence's progress.</param>
/// <param name="Mirrored">Whether the live preview is mirrored (the Event's mirror setting, D22).</param>
/// <param name="CropAspect">The current target window's width over height, for the crop guide on the live preview.</param>
/// <param name="Selection">The selection.</param>
/// <param name="RetakeCandidate">The Retake's candidate, or null.</param>
/// <param name="Revision">The arrangement revision <c>DragPlace</c> must echo.</param>
/// <param name="CanTakePhotographs">Whether <c>TAKE PHOTOGRAPHS</c> works: an empty window exists and nothing is running.</param>
/// <param name="CanReview">Whether the Arrange tab is available: at least one photograph and nothing running.</param>
/// <param name="CanContinue">Whether <c>CONTINUE →</c> to Filters works: every window filled, nothing running, no Move.</param>
public sealed record PhotographsView(
    PhotographsTabName Tab,
    PhotographsTemplateView Template,
    IReadOnlyList<PhotographsWindowView> Windows,
    int RetainedCount,
    CaptureSequenceView Sequence,
    bool Mirrored,
    double CropAspect,
    PhotographSelectionView Selection,
    RetakeCandidateView? RetakeCandidate,
    long Revision,
    bool CanTakePhotographs,
    bool CanReview,
    bool CanContinue);

/// <summary>One Look the Event offers.</summary>
/// <param name="Id">The Look id.</param>
/// <param name="Name">The Look's name (brain copy).</param>
/// <param name="Description">The Look's one-line description (brain copy).</param>
/// <param name="ThumbnailKey">The preview key of the Look's thumbnail (<c>https://preview.fotohavn/&lt;key&gt;.png</c>), or null until it is composed.</param>
public sealed record LookOptionView(string Id, string Name, string Description, string? ThumbnailKey);

/// <summary>The Filters step.</summary>
/// <param name="Looks">The Looks the Event offers, in the Event's order.</param>
/// <param name="SelectedLookId">The selected Look.</param>
/// <param name="StripPreviewKey">The preview key of the strip with the selected Look, or null until it is composed.</param>
/// <param name="Composing">Whether the strip is being composed (a preview for a new Look, or the final Photo Strip).</param>
/// <param name="CompositionFailed">Whether the last composition failed: the UI shows "Your strip couldn't be prepared. Try again." with <c>TRY AGAIN</c> (<c>RetryComposition</c>).</param>
/// <param name="CanFinish">Whether <c>FINISH MY STRIP →</c> works: not composing, no failure showing.</param>
public sealed record FiltersView(
    IReadOnlyList<LookOptionView> Looks,
    string SelectedLookId,
    string? StripPreviewKey,
    bool Composing,
    bool CompositionFailed,
    bool CanFinish);

/// <summary>The Keepsake step: the committed Photo Strip. It displays; nothing else.</summary>
/// <param name="StripUrl">The media URL of the committed <c>photo-strip.png</c>.</param>
/// <param name="Width">The PNG's width in pixels.</param>
/// <param name="Height">The PNG's height in pixels.</param>
/// <param name="Landscape">Whether the strip is wider than tall.</param>
public sealed record KeepsakeView(string StripUrl, int Width, int Height, bool Landscape);

/// <summary>Operator Assistance on the Guest Screen (plan section 7, Failures). Guests see "A little interruption." whatever the reason; the reason is for data attributes and tests.</summary>
/// <param name="Reason">Why. The wire name for a lost guest display is <c>guestDisplay</c>, as on the running-Event panel.</param>
/// <param name="Retrying">Whether a <c>RetryAssistance</c> is being checked now; <c>RETRY</c> shows its busy state.</param>
public sealed record OperatorAssistanceView(AssistanceReason Reason, bool Retrying);
