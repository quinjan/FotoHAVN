using FotoHavn.Core.Catalog;
using FotoHavn.Core.GuestCycles;
using FotoHavn.Core.GuestCycles.Orchestration;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core;

// Filters and the Keepsake (plan section 7, Steps): Look thumbnails from the first arranged Capture, the strip preview of the selected Look,
// the final Photo Strip, and the commit that completes the cycle. Every composition runs off the executor; a result for an older request,
// or for a cycle that ended, is dropped. A failed composition keeps the previous preview and shows the inline error (Q25).
public sealed partial class Brain
{
    /// <summary>The width of a Look thumbnail in the option list (the compositor scales a centre-cropped Capture to it).</summary>
    private const int LookThumbnailWidth = 320;

    /// <summary>
    /// Guest previews take cache keys from their own counter, far above the diagnostics surface's small revisions, so a key never answers
    /// for a different image (the WebView would keep the old one under the same address).
    /// </summary>
    private const long GuestPreviewRevisionBase = 1_000_000;

    private long _previewRevision = GuestPreviewRevisionBase;

    /// <summary>Filters appears: the Look thumbnails and the strip preview of the selected Look are composed.</summary>
    private void EnterFilters(GuestCycleRun run)
    {
        run.StripPreviewKey = null;
        StartThumbnails(run);
        StartStripPreview(run);
    }

    private void StartThumbnails(GuestCycleRun run)
    {
        var model = run.Model;
        var firstCaptureId = model.Windows[0]!;
        var firstWindow = TemplateOf(model.TemplateId).Windows[0];
        var aspectRatio = (double)firstWindow.Width / firstWindow.Height;
        var photo = new CompositionPhoto.Encoded(run.Jpegs[firstCaptureId]);
        var compositor = _dependencies.Compositor;
        foreach (var lookId in run.Active.Event.OfferedLookIds)
        {
            var cacheKey = (model.TemplateId, firstCaptureId, lookId);
            if (run.ThumbnailKeys.ContainsKey(cacheKey) || !run.ThumbnailsInFlight.Add(cacheKey))
            {
                continue;
            }

            // The key's revision is taken now, in request order, so the same cycle always gets the same keys however the compositions finish.
            var revision = ++_previewRevision;
            Off(() => compositor.ComposeLookThumbnailAsync(photo, aspectRatio, lookId, LookThumbnailWidth, CancellationToken.None), composed => CompleteThumbnail(run, cacheKey, revision, composed));
        }
    }

    private void CompleteThumbnail(GuestCycleRun run, (string TemplateId, string CaptureId, string LookId) cacheKey, long revision, Task<CompositionResult> composed)
    {
        run.ThumbnailsInFlight.Remove(cacheKey);
        if (run.Ended)
        {
            return;
        }

        if (!composed.IsCompletedSuccessfully)
        {
            // A missing thumbnail just shows the Look without one.
            BrainLog.GuestCycleCompositionFailed(_logger, FailureOf(composed), CompositionStage.Preview);
            return;
        }

        var key = PreviewKey.For(cacheKey.TemplateId, cacheKey.LookId, revision);
        if (TryCache(key, composed.Result.Png))
        {
            run.ThumbnailKeys[cacheKey] = key;
            run.OwnedKeys.Add(key);
        }
    }

    /// <summary>The strip with the selected Look: from the cache when this arrangement was composed with it, otherwise composed now.</summary>
    private void StartStripPreview(GuestCycleRun run)
    {
        var model = run.Model;
        var cacheKey = (model.TemplateId, model.LookId, model.Revision);
        var request = ++run.CompositionRequest;
        run.CompositionFailed = false;
        if (run.StripKeys.TryGetValue(cacheKey, out var cached))
        {
            run.StripPreviewKey = cached;
            run.Composing = false;
            return;
        }

        run.Composing = true;
        var composition = CompositionOf(run);
        var compositor = _dependencies.Compositor;
        var revision = ++_previewRevision;
        Off(() => compositor.ComposeAsync(composition, CancellationToken.None), composed => CompleteStripPreview(run, request, cacheKey, revision, composed));
    }

    private void CompleteStripPreview(GuestCycleRun run, int request, (string TemplateId, string LookId, long Revision) cacheKey, long revision, Task<CompositionResult> composed)
    {
        if (run.Ended || request != run.CompositionRequest)
        {
            return;
        }

        var key = PreviewKey.For(cacheKey.TemplateId, cacheKey.LookId, revision);
        if (!composed.IsCompletedSuccessfully || !TryCache(key, composed.Result.Png))
        {
            FailComposition(run, CompositionStage.Preview, FailureOf(composed));
            return;
        }

        run.OwnedKeys.Add(key);
        run.StripKeys[cacheKey] = key;
        run.StripPreviewKey = key;
        run.Composing = false;
    }

    private static CompositionRequest CompositionOf(GuestCycleRun run) => new(
        run.Model.TemplateId,
        run.Model.LookId,
        [.. run.Model.Windows.Select(captureId => (CompositionPhoto?)new CompositionPhoto.Encoded(run.Jpegs[captureId!]))]);

    private void FailComposition(GuestCycleRun run, CompositionStage stage, Exception? exception)
    {
        BrainLog.GuestCycleCompositionFailed(_logger, exception, stage);
        run.Composing = false;
        run.CompositionFailed = true;
        run.FailedStage = stage;
    }

    private CommandResultMessage SelectLook(string id, SelectLookPayload payload)
    {
        if (ActionableRun is not { Model.Step: GuestCycleStep.Filters } run)
        {
            return NotAvailable(id);
        }

        if (!run.Active.Event.OfferedLookIds.Contains(payload.LookId, StringComparer.Ordinal))
        {
            return Invalid(id);
        }

        // Tapping the Look already selected is a harmless no-op.
        if (ApplyModel(run, new SelectLookAction(payload.LookId)).Applied)
        {
            StartStripPreview(run);
            AfterChange(run);
        }

        return Accepted(id);
    }

    private CommandResultMessage FinishStrip(string id)
    {
        if (ActionableRun is not { Model.Step: GuestCycleStep.Filters, Composing: false, CompositionFailed: false } run || !run.Model.CanContinue)
        {
            return NotAvailable(id);
        }

        BeginFinalCompose(run);
        AfterChange(run);
        return Accepted(id);
    }

    /// <summary><c>RetryComposition</c>: composes again whatever failed, the Look's strip preview or the final Photo Strip.</summary>
    private CommandResultMessage RetryComposition(string id)
    {
        if (ActionableRun is not { Model.Step: GuestCycleStep.Filters, CompositionFailed: true } run)
        {
            return NotAvailable(id);
        }

        if (run.FailedStage == CompositionStage.Preview)
        {
            StartStripPreview(run);
        }
        else
        {
            BeginFinalCompose(run);
        }

        AfterChange(run);
        return Accepted(id);
    }

    // The final Photo Strip: compose with the arranged Captures, commit it (which completes the cycle), then the Keepsake.

    /// <summary>The caller syncs afterwards: the guest can't act and no Phase Timer runs until the strip is made or fails.</summary>
    private void BeginFinalCompose(GuestCycleRun run)
    {
        run.Finishing = true;
        run.Composing = true;
        run.CompositionFailed = false;
        var composition = CompositionOf(run);
        var compositor = _dependencies.Compositor;
        Off(() => compositor.ComposeAsync(composition, CancellationToken.None), composed => CompleteFinalCompose(run, composition, composed));
    }

    private void CompleteFinalCompose(GuestCycleRun run, CompositionRequest composition, Task<CompositionResult> composed)
    {
        if (run.Ended)
        {
            return;
        }

        if (!composed.IsCompletedSuccessfully)
        {
            // Filters shows the failure (also when Filters was skipped): the guest, or the timer, tries again.
            run.Finishing = false;
            FailComposition(run, CompositionStage.Final, FailureOf(composed));
            ApplyModel(run, new GoToStepAction(GuestCycleStep.Filters));
            AfterChange(run);
            return;
        }

        run.CommitInFlight = true;
        var strip = composed.Result;
        var commit = new PhotoStripCommit(run.Id, composition.TemplateId, composition.LookId, [.. run.Model.ArrangedCaptureIds], strip.Png, strip.Width, strip.Height);
        Off(() => _dependencies.Store.CommitPhotoStripAsync(commit, CancellationToken.None), committed => CompleteFinalCommit(run, committed));
    }

    private void CompleteFinalCommit(GuestCycleRun run, Task<PhotoStripRecord> committed)
    {
        run.CommitInFlight = false;
        if (run.Ended)
        {
            return;
        }

        if (!committed.IsCompletedSuccessfully)
        {
            BrainLog.GuestCycleStoreFailed(_logger, FailureOf(committed), "commit the Photo Strip");
            run.Finishing = false;
            run.Composing = false;
            EnterAssistance(AssistanceReason.Storage);
            AfterChange(run);
            return;
        }

        var strip = committed.Result;
        run.Completed = true;
        run.Finishing = false;
        run.Composing = false;
        if (run.Model.Step != GuestCycleStep.Filters)
        {
            ApplyModel(run, new GoToStepAction(GuestCycleStep.Filters));
        }

        ApplyModel(run, new GoToStepAction(GuestCycleStep.Keepsake));
        run.Keepsake = new KeepsakeView(GuestCyclePlan.StripUrl(run.Active.Event.Id, run.Id, strip.Sha256), strip.Width, strip.Height, strip.Width > strip.Height);
        run.Active.CompletedGuestCycles++;
        ReleaseFilterWork(run);
        StartSummaryRefresh();
        BrainLog.GuestCycleCompleted(_logger, run.Id);
        AfterChange(run);
    }

    /// <summary>The Keepsake shows the committed file, so the composed previews and the photographs held for composing are no longer needed.</summary>
    private void ReleaseFilterWork(GuestCycleRun run)
    {
        foreach (var key in run.OwnedKeys)
        {
            _dependencies.Previews.Remove(key);
        }

        run.OwnedKeys.Clear();
        run.StripKeys.Clear();
        run.ThumbnailKeys.Clear();
        run.StripPreviewKey = null;
        run.Jpegs.Clear();
    }

    private FiltersView RenderFilters(GuestCycleRun run)
    {
        var model = run.Model;
        var firstCaptureId = model.Windows[0]!;
        return new FiltersView(
            [.. run.Active.Event.OfferedLookIds.Select(lookId =>
            {
                var look = _dependencies.Catalog.FindLook(lookId)!;
                return new LookOptionView(look.Id, look.Name, look.Description, run.ThumbnailKeys.GetValueOrDefault((model.TemplateId, firstCaptureId, lookId)));
            })],
            model.LookId,
            run.StripPreviewKey,
            run.Composing,
            run.CompositionFailed,
            !run.Composing && !run.CompositionFailed && model.CanContinue);
    }
}

internal static partial class BrainLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Composing the Guest Cycle's {Stage} failed.")]
    public static partial void GuestCycleCompositionFailed(ILogger logger, Exception? exception, CompositionStage stage);

    [LoggerMessage(Level = LogLevel.Information, Message = "Guest Cycle {GuestCycleId} completed: its Photo Strip is committed.")]
    public static partial void GuestCycleCompleted(ILogger logger, string guestCycleId);
}
