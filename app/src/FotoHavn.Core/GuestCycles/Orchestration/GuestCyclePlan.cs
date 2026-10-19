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

    /// <summary>The step progress: all four steps in order with their state. A skipped step stays skipped; it only shows where the guest is, since guests never go back.</summary>
    public static IReadOnlyList<GuestCycleStepEntry> Steps(GuestCycleStep current, bool templateSkipped, bool filtersSkipped)
    {
        GuestCycleStep[] order = [GuestCycleStep.Template, GuestCycleStep.Photographs, GuestCycleStep.Filters, GuestCycleStep.Keepsake];
        return [.. order.Select(step =>
        {
            var skipped = (step == GuestCycleStep.Template && templateSkipped) || (step == GuestCycleStep.Filters && filtersSkipped);
            var state = skipped
                ? GuestStepState.Skipped
                : step < current ? GuestStepState.Done : step == current ? GuestStepState.Current : GuestStepState.Upcoming;
            return new GuestCycleStepEntry(ToName(step), state);
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

    /// <summary>The whole seconds the Phase Timer shows: the time left rounded up, never below 1 while any is left.</summary>
    public static int WholeSecondsLeft(TimeSpan remaining) => (int)Math.Ceiling(remaining.TotalSeconds);

    /// <summary>
    /// How long a running Phase Timer waits before it next matters: until the whole second it shows changes, so the push lands as the number does,
    /// and the last of those is the expiry. Zero when the time is already up.
    /// </summary>
    public static TimeSpan WakeDelay(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var intoSecond = remaining.Ticks % TimeSpan.TicksPerSecond;
        return intoSecond > 0 ? TimeSpan.FromTicks(intoSecond) : GuestCycleTiming.SecondTick;
    }

    /// <summary>The media URL of a Capture (<c>?v=</c> is the file's hash, a cache buster).</summary>
    public static string CaptureUrl(string eventId, string cycleId, int sequence, string sha256) =>
        $"https://media.fotohavn/events/{eventId}/guest-cycles/{cycleId}/capture-{sequence}.jpg?v={Version(sha256)}";

    /// <summary>The media URL of the committed Photo Strip.</summary>
    public static string StripUrl(string eventId, string cycleId, string sha256) =>
        $"https://media.fotohavn/events/{eventId}/guest-cycles/{cycleId}/photo-strip.png?v={Version(sha256)}";

    private static string Version(string sha256) => sha256.Length > 8 ? sha256[..8] : sha256;
}
