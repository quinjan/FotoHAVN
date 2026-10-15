using FotoHavn.Catalog;
using FotoHavn.Core.GuestCycles;

namespace FotoHavn.Core.Tests.GuestCycles;

/// <summary>
/// The website's <c>online-photobooth.test.mjs</c> cases that apply to the booth's reducer (plan section 7, Retake and arrangement
/// rules), each named after the website test it ports. The cases about the camera, the compositor, and Looks' pixels belong to the
/// camera and catalog tests (M5, M7).
/// </summary>
public sealed class WebsiteSessionPortTests
{
    private const string Pair = "pair-v1";
    private const string LongStrip = "long-strip-v1";

    [Fact]
    public void Layout_changes_preserve_all_source_identities_order_and_retained_photographs()
    {
        var first = GuestCycleKit.Loaded();
        var state = first.Do(new SelectTemplateAction(Pair, 2));
        Assert.Equal(["A", "B"], state.Windows);
        Assert.Equal(["C", "D"], state.RetainedPool);

        state = state.Move(0, 1);
        state = state.Do(new SelectTemplateAction(LongStrip, 4));
        Assert.Equal(["B", "A", "C", "D"], state.Windows);
        Assert.Empty(state.RetainedPool);
        foreach (var id in new[] { "A", "B", "C", "D" })
        {
            Assert.Same(first.KeptCaptures[id], state.KeptCaptures[id]);
        }

        Assert.Equal(["A", "B", "C", "D"], first.Windows);
    }

    [Fact]
    public void A_larger_layout_cannot_continue_until_its_newly_empty_positions_are_filled()
    {
        var state = GuestCycleKit.Loaded("A", "B").Do(new SelectTemplateAction(Pair, 2));
        Assert.True(state.CanContinue);

        state = state.Do(new SelectTemplateAction(LongStrip, 4));
        Assert.Equal(["A", "B", null, null], state.Windows);
        Assert.False(state.CanContinue);
        Assert.Same(state, state.Do(new GoToStepAction(GuestCycleStep.Filters)));
        Assert.Same(state, state.Do(new GoToStepAction(GuestCycleStep.Keepsake)));
    }

    [Fact]
    public void A_retake_is_transactional_cancel_retry_and_accept_affect_only_its_selected_position()
    {
        var first = GuestCycleKit.Loaded();
        var retake = first.Do(new BeginRetakeAction(2, 1));
        Assert.Equal(1, retake.Selected);

        var candidate = retake.Do(new CaptureCommittedAction(2, 1, GuestCycleKit.Capture("E", 5)));
        Assert.Equal(["A", "B", "C", "D"], candidate.Windows);
        Assert.False(candidate.CanContinue);

        var cancelled = candidate.Try(new CancelAction());
        Assert.Equal(["A", "B", "C", "D"], cancelled.Model.Windows);
        Assert.False(cancelled.Model.KeptCaptures.ContainsKey("E"));
        Assert.Same(first.KeptCaptures["B"], cancelled.Model.KeptCaptures["B"]);
        Assert.Equal(["E"], cancelled.DiscardedCaptureIds);

        var retry = candidate.Try(new TryAgainAction());
        Assert.Null(retry.Model.Operation!.Candidate);
        Assert.False(retry.Model.KeptCaptures.ContainsKey("E"));
        Assert.Same(first.KeptCaptures["B"], retry.Model.KeptCaptures["B"]);
        Assert.Equal(["E"], retry.DiscardedCaptureIds);

        var accepted = candidate.Try(new AcceptRetakeAction());
        Assert.Equal(["A", "E", "C", "D"], accepted.Model.Windows);
        Assert.False(accepted.Model.KeptCaptures.ContainsKey("B"));
        Assert.Equal(1, accepted.Model.Selected);
        Assert.Equal(["B"], accepted.DiscardedCaptureIds);
        foreach (var id in new[] { "A", "C", "D" })
        {
            Assert.Same(first.KeptCaptures[id], accepted.Model.KeptCaptures[id]);
        }
    }

    [Fact]
    public void Cancelled_and_stale_operations_cannot_commit_a_late_photograph_or_finish_a_newer_operation()
    {
        var state = GuestCycleKit.OnPhotographs().Do(new BeginSequenceAction(8));
        state = state.Do(new CaptureCommittedAction(8, 0, GuestCycleKit.Capture("A")));
        state = state.Do(new CancelAction());
        var cancelled = state;

        Assert.Same(state, state.Do(new CaptureCommittedAction(8, 1, GuestCycleKit.Capture("B", 2))));
        state = state.Do(new BeginSequenceAction(9));
        Assert.Same(state, state.Do(new CaptureCommittedAction(8, 2, GuestCycleKit.Capture("C", 3))));
        Assert.Same(state, state.Do(new FinishSequenceAction(8)));
        Assert.Equal(["A", null, null, null], cancelled.Windows);

        var departed = state.Do(new GoToStepAction(GuestCycleStep.Template));
        Assert.Same(departed, departed.Do(new CaptureCommittedAction(9, 3, GuestCycleKit.Capture("D", 4))));
    }

    [Fact]
    public void Retake_targets_cannot_move_under_capture_and_duplicate_or_wrong_slot_candidates_are_ignored()
    {
        var state = GuestCycleKit.Loaded().Do(new BeginRetakeAction(3, 1));

        Assert.Same(state, state.Do(new SelectTemplateAction(Pair, 2)));
        Assert.Same(state, state.Do(new SelectWindowAction(3)));
        Assert.Same(state, state.Move(0, 3));
        Assert.Same(state, state.Do(new CaptureCommittedAction(3, 2, GuestCycleKit.Capture("E", 5))));

        state = state.Do(new CaptureCommittedAction(3, 1, GuestCycleKit.Capture("E", 5)));
        Assert.Same(state, state.Do(new CaptureCommittedAction(3, 1, GuestCycleKit.Capture("F", 6))));
        Assert.Same(state, state.Do(new CaptureCommittedAction(3, 1, GuestCycleKit.Capture("E", 5))));
    }

    [Fact]
    public void All_arrangement_methods_share_identity_preserving_swap_and_retained_pool_semantics()
    {
        var first = GuestCycleKit.Loaded();
        Assert.Equal(["C", "B", "A", "D"], first.Move(2, 0).Windows);

        var selected = first.Do(new MoveStartAction(2));
        Assert.False(selected.CanContinue);
        Assert.Equal(["A", "B", "C", "D"], selected.Do(new MoveCancelAction()).Windows);

        var state = first.Do(new SelectTemplateAction(Pair, 2));
        state = state.Do(new MoveStartFromPoolAction("C")).Do(new MoveToAction(1));
        Assert.Equal(["A", "C"], state.Windows);
        Assert.Equal(["B", "D"], state.RetainedPool);

        var withEmpty = state with { Windows = ["A", null], RetainedPool = ["C", "B", "D"] };
        state = withEmpty.Do(new MoveStartFromPoolAction("C")).Do(new MoveToAction(1));
        Assert.Equal(["A", "C"], state.Windows);
        Assert.Equal(["B", "D"], state.RetainedPool);
        Assert.Equal(state.Windows.OfType<string>().Concat(state.RetainedPool).ToHashSet().Count, state.KeptCaptures.Count);
    }

    [Fact]
    public void Looks_preserve_original_pixels_and_returning_to_Original_uses_the_same_sources()
    {
        var first = GuestCycleKit.Loaded().Move(2, 0);
        var state = first;
        foreach (var look in new CatalogData().Looks)
        {
            state = state.Do(new SelectLookAction(look.Id));
        }

        state = state.Do(new SelectLookAction(CatalogData.NaturaleId));
        Assert.Same(first.KeptCaptures, state.KeptCaptures);
        Assert.Same(first.Windows, state.Windows);
        Assert.Equal(first.Revision, state.Revision);
    }

    [Fact]
    public void Combined_templates_preserve_captured_originals_through_three_and_four_photo_selections()
    {
        var catalog = new CatalogData();
        var original = GuestCycleKit.Loaded();
        var three = original.Do(new SelectTemplateAction(CatalogData.ButterGinghamId, catalog.FindTemplate(CatalogData.ButterGinghamId)!.WindowCount));
        Assert.Equal(["A", "B", "C"], three.Windows);
        Assert.Equal(["D"], three.RetainedPool);

        foreach (var template in catalog.Templates)
        {
            var changed = three.Do(new SelectTemplateAction(template.Id, template.WindowCount));
            Assert.Equal(template.WindowCount, changed.Windows.Count);
            foreach (var id in new[] { "A", "B", "C", "D" })
            {
                Assert.Same(original.KeptCaptures[id], changed.KeptCaptures[id]);
            }

            Assert.True(changed.CanContinue);
        }

        var panorama = three.Do(new SelectTemplateAction(CatalogData.PanoramaFourId, 4));
        Assert.Equal(["A", "B", "C", "D"], panorama.Windows);

        var capturing = original.Do(new BeginRetakeAction(55, 0));
        Assert.Same(capturing, capturing.Do(new SelectTemplateAction(CatalogData.ButterGinghamId, 3)));
    }

    [Fact]
    public void Published_template_windows_fit_their_prints_and_preserve_native_orientations()
    {
        var catalog = new CatalogData();
        foreach (var template in catalog.Templates)
        {
            foreach (var window in template.Windows)
            {
                Assert.True(window.X >= 0 && window.Y >= 0);
                Assert.True(window.X + window.Width <= template.Width && window.Y + window.Height <= template.Height);
            }
        }

        var panorama = catalog.FindTemplate(CatalogData.PanoramaFourId)!;
        Assert.Equal(3, (double)panorama.Width / panorama.Height);
        Assert.Equal(3, catalog.FindTemplate(CatalogData.ButterGinghamId)!.WindowCount);
    }
}
