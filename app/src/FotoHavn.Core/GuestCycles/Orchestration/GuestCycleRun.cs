using FotoHavn.Core.Events.Active;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.GuestCycles.Orchestration;

/// <summary>Where a capture sequence is, window by window.</summary>
internal enum SequenceStep
{
    /// <summary>Counting down to a frame.</summary>
    Countdown,

    /// <summary>The camera was asked for the frame; the flash plays when it arrives.</summary>
    Capturing,

    /// <summary>The frame is taken: it is being mirrored, encoded, and committed.</summary>
    Saving,

    /// <summary>The 800 ms pause after a flash.</summary>
    Pausing,
}

/// <summary>One running capture sequence or Retake attempt. Brain state, touched only on the executor.</summary>
internal sealed class CaptureSequence(long operationId, OperationMode mode, int total, int targetWindow)
{
    public long OperationId => operationId;

    public OperationMode Mode => mode;

    /// <summary>How many Captures this sequence takes in all (the empty windows when it began; 1 for a Retake).</summary>
    public int Total => total;

    public int Captured { get; set; }

    public int TargetWindow { get; set; } = targetWindow;

    public SequenceStep Step { get; set; }

    /// <summary>The whole seconds left; null between countdowns.</summary>
    public int? CountdownRemaining { get; set; }

    /// <summary>The moment the countdown reached zero on the brain's clock: the Capture's taken-at time.</summary>
    public DateTimeOffset TakenAt { get; set; }

    /// <summary>The sequence ends after the Capture in flight (a Stop, or an Operator Assistance that began meanwhile).</summary>
    public bool StopRequested { get; set; }

    /// <summary>Whether ending it restarts the capture timer (the guest stopped it) rather than leaving the timer to its next phase.</summary>
    public bool RestartsTimer { get; set; }

    /// <summary>The camera took too long to answer.</summary>
    public bool TimedOut { get; set; }

    /// <summary>The next countdown tick or the pause's end.</summary>
    public IDisposable? Wake { get; set; }

    public CancellationTokenSource? CaptureCancellation { get; set; }

    public IDisposable? CaptureTimeout { get; set; }

    /// <summary>Releases everything the sequence holds: the wake-up, the capture's timeout, and its token (a camera still waiting for its frame is told to stop).</summary>
    public void Release()
    {
        Wake?.Dispose();
        Wake = null;
        CaptureTimeout?.Dispose();
        CaptureTimeout = null;
        CaptureCancellation?.Cancel();
        CaptureCancellation?.Dispose();
        CaptureCancellation = null;
    }
}

/// <summary>What stands in the way of the Photo Strip, when composing it failed.</summary>
internal enum CompositionStage
{
    /// <summary>The strip preview of a Look.</summary>
    Preview,

    /// <summary>The final Photo Strip.</summary>
    Final,
}

/// <summary>
/// One Guest Cycle's orchestration state: the store's ids, the pure model, the Phase Timer, the capture sequence, and what Filters has
/// composed. Brain state, touched only on the executor; the brain holds one at a time.
/// </summary>
internal sealed class GuestCycleRun(ActiveEventState active, string id, GuestCycleModel model, bool templateSkipped, bool filtersSkipped)
{
    public ActiveEventState Active => active;

    /// <summary>The Guest Cycle's id in the store.</summary>
    public string Id => id;

    public GuestCycleModel Model { get; set; } = model;

    public bool TemplateSkipped => templateSkipped;

    public bool FiltersSkipped => filtersSkipped;

    /// <summary>The cycle ended in the brain (start over, exit, Event ended, or back at Start after the Keepsake): every late result is dropped.</summary>
    public bool Ended { get; set; }

    /// <summary>The Photo Strip is committed: the store recorded the cycle completed. The Keepsake step only shows it.</summary>
    public bool Completed { get; set; }

    public long NextOperationId { get; set; }

    public int NextCaptureNumber { get; set; } = 1;

    public long FlashSequence { get; set; }

    public CaptureSequence? Sequence { get; set; }

    public bool StartOverOpen { get; set; }

    /// <summary>The final Photo Strip is being composed or committed: the guest can't act.</summary>
    public bool Finishing { get; set; }

    /// <summary>The Photo Strip's commit is in flight: ending the cycle leaves its completion to the store.</summary>
    public bool CommitInFlight { get; set; }

    public PhaseTimerEngine Timer { get; set; } = PhaseTimerEngine.Stopped;

    public IDisposable? TimerWake { get; set; }

    public int TimerGeneration { get; set; }

    /// <summary>The JPEG of every Capture the cycle holds (windows, retained pool, candidate): the compositor takes its photographs from here.</summary>
    public Dictionary<string, byte[]> Jpegs { get; } = new(StringComparer.Ordinal);

    /// <summary>Strip previews composed, by Template, Look, and arrangement revision.</summary>
    public Dictionary<(string TemplateId, string LookId, long Revision), string> StripKeys { get; } = [];

    /// <summary>Look thumbnails composed, by Template, first Capture, and Look.</summary>
    public Dictionary<(string TemplateId, string CaptureId, string LookId), string> ThumbnailKeys { get; } = [];

    public HashSet<(string TemplateId, string CaptureId, string LookId)> ThumbnailsInFlight { get; } = [];

    /// <summary>Every preview key this cycle put in the preview cache, so ending it removes them all.</summary>
    public HashSet<string> OwnedKeys { get; } = new(StringComparer.Ordinal);

    public string? StripPreviewKey { get; set; }

    public bool Composing { get; set; }

    public bool CompositionFailed { get; set; }

    public CompositionStage FailedStage { get; set; }

    /// <summary>Counts composition requests, so a result for an older one is dropped.</summary>
    public int CompositionRequest { get; set; }

    public KeepsakeView? Keepsake { get; set; }
}

/// <summary>Operator Assistance: why, and whether a <c>RetryAssistance</c> is being checked.</summary>
internal sealed class AssistanceState(AssistanceReason reason)
{
    public AssistanceReason Reason => reason;

    public bool Retrying { get; set; }

    public IDisposable? Wake { get; set; }

    public Action? CameraObserver { get; set; }
}
