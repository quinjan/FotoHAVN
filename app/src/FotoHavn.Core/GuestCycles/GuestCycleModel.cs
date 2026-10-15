using System.Collections.Immutable;

namespace FotoHavn.Core.GuestCycles;

/// <summary>The Guest Cycle's steps (plan section 7, Steps). Start is not one: the model exists only inside a Guest Cycle.</summary>
public enum GuestCycleStep
{
    Template,
    Photographs,
    Filters,
    Keepsake,
}

/// <summary>The Photographs step's two tabs: the live Camera, and Arrange for the strip so far.</summary>
public enum PhotographsTab
{
    Camera,
    Arrange,
}

/// <summary>What a running operation does.</summary>
public enum OperationMode
{
    /// <summary>Fills empty windows in order, one Capture each.</summary>
    Sequence,

    /// <summary>Replaces one window's Capture; transactional until the guest accepts.</summary>
    Retake,
}

/// <summary>A Capture the Guest Cycle keeps, as the model needs to know it. The pixels live in the store; the model holds identity and geometry.</summary>
/// <param name="Id">The Capture id the store gave it.</param>
/// <param name="Sequence">The Capture's sequence number in its Guest Cycle (it names <c>capture-&lt;sequence&gt;.jpg</c>).</param>
/// <param name="ImageUrl">The media URL the guest views show for it.</param>
/// <param name="Width">The JPEG's width in pixels.</param>
/// <param name="Height">The JPEG's height in pixels.</param>
public sealed record KeptCapture(string Id, int Sequence, string ImageUrl, int Width, int Height);

/// <summary>A running capture operation. A stale or cancelled operation can never commit: its id no longer matches.</summary>
/// <param name="Id">The operation id the brain allocated; it grows with every operation.</param>
/// <param name="Mode">Sequence or Retake.</param>
/// <param name="Target">The window a Retake replaces; null for a Sequence.</param>
/// <param name="Candidate">A Retake's pending replacement, or null until one is committed.</param>
public sealed record GuestOperation(long Id, OperationMode Mode, int? Target, string? Candidate);

/// <summary>A Move in progress: which Capture moves and where it started. <see cref="SourceIndex"/> is null for a Capture in the retained pool.</summary>
/// <param name="SourceIndex">The window the Capture is in, or null when it comes from the retained pool.</param>
/// <param name="CaptureId">The Capture being moved.</param>
/// <param name="Revision">The arrangement revision the Move began at.</param>
public sealed record GuestMove(int? SourceIndex, string CaptureId, long Revision);

/// <summary>
/// The pure state of one Guest Cycle's arrangement, ported from the website's session reducer (<c>session.ts</c>) minus device
/// import, frames, and layouts (plan section 7, Retake and arrangement rules). It is immutable: <see cref="GuestCycleRules.Apply"/>
/// returns a new model, or the same instance when an action is ignored.
/// </summary>
public sealed record GuestCycleModel
{
    /// <summary>The selected Template.</summary>
    public required string TemplateId { get; init; }

    /// <summary>How many windows the selected Template has.</summary>
    public required int WindowCount { get; init; }

    /// <summary>The Capture id in each window, or null for an empty window. Always <see cref="WindowCount"/> long.</summary>
    public required IReadOnlyList<string?> Windows { get; init; }

    /// <summary>Kept Captures that are in no window: what a smaller Template pushed out. A larger Template pulls them back in.</summary>
    public IReadOnlyList<string> RetainedPool { get; init; } = [];

    /// <summary>Every Capture the Guest Cycle still holds (in a window, the retained pool, or a Retake candidate), by id.</summary>
    public IReadOnlyDictionary<string, KeptCapture> KeptCaptures { get; init; } = ImmutableDictionary<string, KeptCapture>.Empty;

    /// <summary>The running operation, or null.</summary>
    public GuestOperation? Operation { get; init; }

    /// <summary>The Move in progress, or null.</summary>
    public GuestMove? Move { get; init; }

    /// <summary>The selected window, or null.</summary>
    public int? Selected { get; init; }

    /// <summary>The selected Look.</summary>
    public required string LookId { get; init; }

    /// <summary>Counts arrangement changes (the Template, a window, or the retained pool changed), so a gesture that began on an older arrangement is refused.</summary>
    public long Revision { get; init; }

    /// <summary>The current step.</summary>
    public GuestCycleStep Step { get; init; } = GuestCycleStep.Template;

    /// <summary>The Photographs step's tab.</summary>
    public PhotographsTab Tab { get; init; } = PhotographsTab.Camera;

    /// <summary>Whether every window holds a kept Capture.</summary>
    public bool IsComplete => Windows.All(id => id is not null && KeptCaptures.ContainsKey(id));

    /// <summary>Whether the guest can leave Photographs for Filters: complete, no operation, no Move.</summary>
    public bool CanContinue => IsComplete && Operation is null && Move is null;

    /// <summary>The first empty window, or null when every window is filled.</summary>
    public int? NextEmptyWindow => EmptyWindows is [var first, ..] ? first : null;

    /// <summary>The empty windows, in order.</summary>
    public IReadOnlyList<int> EmptyWindows => [.. Windows.Select((id, index) => (id, index)).Where(slot => slot.id is null).Select(slot => slot.index)];

    /// <summary>The Captures in the windows, in window order (the Photo Strip's arrangement once every window is filled).</summary>
    public IReadOnlyList<string> ArrangedCaptureIds => [.. Windows.OfType<string>()];

    /// <summary>A new Guest Cycle on the Template step with every window empty.</summary>
    public static GuestCycleModel Create(string templateId, int windowCount, string lookId)
    {
        ArgumentException.ThrowIfNullOrEmpty(templateId);
        ArgumentException.ThrowIfNullOrEmpty(lookId);
        ArgumentOutOfRangeException.ThrowIfLessThan(windowCount, 1);
        return new GuestCycleModel
        {
            TemplateId = templateId,
            WindowCount = windowCount,
            Windows = new string?[windowCount],
            LookId = lookId,
        };
    }
}

/// <summary>What <see cref="GuestCycleRules.Apply"/> did.</summary>
/// <param name="Model">The new model, or the same instance when the action was ignored.</param>
/// <param name="Applied">False when the action was ignored (stale, wrong state, or out of range): the model is unchanged.</param>
/// <param name="DiscardedCaptureIds">Captures the action dropped from the cycle: the brain sets each <c>discarded</c> in the store. Empty for most actions.</param>
public sealed record GuestCycleOutcome(GuestCycleModel Model, bool Applied, IReadOnlyList<string> DiscardedCaptureIds);
