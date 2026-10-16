using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.GuestCycles.Orchestration;

/// <summary>The pure decisions the Guest Cycle's orchestration makes from the model and the clock (plan section 7), apart from any I/O.</summary>
internal static class GuestCyclePlan
{
    /// <summary>The Phase Timer's phase for a cycle that stands as <paramref name="model"/> shows, or null while no phase is timed (the final Photo Strip is being made).</summary>
    public static PhaseTimerPhase? PhaseFor(GuestCycleModel model, bool finishing)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (finishing)
        {
            return null;
        }

        return model.Step switch
        {
            GuestCycleStep.Template => PhaseTimerPhase.Template,
            GuestCycleStep.Photographs => model.IsComplete ? PhaseTimerPhase.PhotographsReview : PhaseTimerPhase.PhotographsCapture,
            GuestCycleStep.Filters => PhaseTimerPhase.Filters,
            _ => PhaseTimerPhase.Keepsake,
        };
    }

    /// <summary>The tab the guest sees: a Retake whose candidate waits shows Arrange (the model's tab stays Camera during an operation).</summary>
    public static PhotographsTab EffectiveTab(GuestCycleModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.Operation is { Mode: OperationMode.Retake, Candidate: not null } ? PhotographsTab.Arrange : model.Tab;
    }

    /// <summary>The step progress: all four steps in order with their state. A skipped step stays skipped; a done step can be gone back to until the cycle completes.</summary>
    public static IReadOnlyList<GuestCycleStepEntry> Steps(GuestCycleStep current, bool templateSkipped, bool filtersSkipped)
    {
        GuestCycleStep[] order = [GuestCycleStep.Template, GuestCycleStep.Photographs, GuestCycleStep.Filters, GuestCycleStep.Keepsake];
        return [.. order.Select(step =>
        {
            var skipped = (step == GuestCycleStep.Template && templateSkipped) || (step == GuestCycleStep.Filters && filtersSkipped);
            var state = skipped
                ? GuestStepState.Skipped
                : step < current ? GuestStepState.Done : step == current ? GuestStepState.Current : GuestStepState.Upcoming;
            return new GuestCycleStepEntry(ToName(step), state, state == GuestStepState.Done && current != GuestCycleStep.Keepsake);
        })];
    }

    public static GuestCycleStepName ToName(GuestCycleStep step) => step switch
    {
        GuestCycleStep.Template => GuestCycleStepName.Template,
        GuestCycleStep.Photographs => GuestCycleStepName.Photographs,
        GuestCycleStep.Filters => GuestCycleStepName.Filters,
        _ => GuestCycleStepName.Keepsake,
    };

    public static RunningEventStep ToRunning(GuestCycleStep step) => step switch
    {
        GuestCycleStep.Template => RunningEventStep.Template,
        GuestCycleStep.Photographs => RunningEventStep.Photographs,
        GuestCycleStep.Filters => RunningEventStep.Filters,
        _ => RunningEventStep.Keepsake,
    };

    /// <summary>The whole seconds a warning shows: the time left rounded up, never below 1 while any is left.</summary>
    public static int WholeSecondsLeft(TimeSpan remaining) => (int)Math.Ceiling(remaining.TotalSeconds);

    /// <summary>
    /// How long to wait before the timer next matters: until the deadline, or while the warning shows, until the displayed
    /// whole second changes (so the push lands as the number does).
    /// </summary>
    public static TimeSpan WakeDelay(TimeSpan untilDeadline, TimeSpan remaining, bool warning)
    {
        var delay = untilDeadline < TimeSpan.Zero ? TimeSpan.Zero : untilDeadline;
        if (!warning)
        {
            return delay;
        }

        var fraction = remaining.TotalSeconds - Math.Floor(remaining.TotalSeconds);
        var untilNumberChanges = fraction > 0 ? TimeSpan.FromSeconds(fraction) : GuestCycleTiming.SecondTick;
        return untilNumberChanges < delay ? untilNumberChanges : delay;
    }

    /// <summary>The media URL of a Capture (<c>?v=</c> is the file's hash, a cache buster).</summary>
    public static string CaptureUrl(string eventId, string cycleId, int sequence, string sha256) =>
        $"https://media.fotohavn/events/{eventId}/guest-cycles/{cycleId}/capture-{sequence}.jpg?v={Version(sha256)}";

    /// <summary>The media URL of the committed Photo Strip.</summary>
    public static string StripUrl(string eventId, string cycleId, string sha256) =>
        $"https://media.fotohavn/events/{eventId}/guest-cycles/{cycleId}/photo-strip.png?v={Version(sha256)}";

    private static string Version(string sha256) => sha256.Length > 8 ? sha256[..8] : sha256;
}
