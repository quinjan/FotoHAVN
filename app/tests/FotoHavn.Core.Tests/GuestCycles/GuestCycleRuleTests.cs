using FotoHavn.Core.GuestCycles;

namespace FotoHavn.Core.Tests.GuestCycles;

/// <summary>The Guest Cycle model's rules beyond the website's ported cases (plan section 7, Retake and arrangement rules): every action's guards and effects.</summary>
public sealed class GuestCycleRuleTests
{
    private static GuestCycleModel Retaking(long operationId = 2, int target = 1) => GuestCycleKit.Loaded().Do(new BeginRetakeAction(operationId, target));

    private static GuestCycleModel WithCandidate(string id = "E") => Retaking().Do(new CaptureCommittedAction(2, 1, GuestCycleKit.Capture(id, 5)));

    private static void AssertIgnored(GuestCycleModel model, GuestCycleAction action)
    {
        var outcome = model.Try(action);
        Assert.False(outcome.Applied);
        Assert.Same(model, outcome.Model);
        Assert.Empty(outcome.DiscardedCaptureIds);
    }

    // Model and queries

    [Fact]
    public void A_new_cycle_starts_on_Template_with_every_window_empty()
    {
        var model = GuestCycleKit.Fresh(3);

        Assert.Equal(GuestCycleStep.Template, model.Step);
        Assert.Equal(PhotographsTab.Camera, model.Tab);
        Assert.Equal(3, model.WindowCount);
        Assert.Equal([null, null, null], model.Windows);
        Assert.Equal([0, 1, 2], model.EmptyWindows);
        Assert.Equal(0, model.NextEmptyWindow);
        Assert.Empty(model.ArrangedCaptureIds);
        Assert.Null(model.Selected);
        Assert.False(model.IsComplete);
        Assert.False(model.CanContinue);
    }

    [Theory]
    [InlineData("", "naturale-v1", 4)]
    [InlineData("signature-strip", "", 4)]
    [InlineData("signature-strip", "naturale-v1", 0)]
    public void A_cycle_needs_a_Template_a_Look_and_a_window(string template, string look, int windows)
    {
        Assert.ThrowsAny<ArgumentException>(() => GuestCycleModel.Create(template, windows, look));
    }

    [Fact]
    public void A_full_arrangement_lists_its_Captures_in_window_order_and_has_no_empty_window()
    {
        var model = GuestCycleKit.Loaded().Move(0, 3);

        Assert.True(model.IsComplete);
        Assert.True(model.CanContinue);
        Assert.Null(model.NextEmptyWindow);
        Assert.Empty(model.EmptyWindows);
        Assert.Equal(["D", "B", "C", "A"], model.ArrangedCaptureIds);
    }

    [Fact]
    public void A_window_whose_Capture_the_cycle_no_longer_holds_does_not_count_as_filled()
    {
        var model = GuestCycleKit.Loaded() with { KeptCaptures = new Dictionary<string, KeptCapture>() };

        Assert.False(model.IsComplete);
    }

    [Fact]
    public void Apply_needs_a_model_and_an_action()
    {
        Assert.Throws<ArgumentNullException>(() => GuestCycleRules.Apply(null!, new CancelAction()));
        Assert.Throws<ArgumentNullException>(() => GuestCycleRules.Apply(GuestCycleKit.Fresh(), null!));
    }

    private sealed record UnknownAction : GuestCycleAction;

    [Fact]
    public void An_action_the_rules_do_not_know_is_a_programming_error()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GuestCycleRules.Apply(GuestCycleKit.Fresh(), new UnknownAction()));
    }

    // Template

    [Fact]
    public void Choosing_the_same_Template_or_a_template_without_windows_is_ignored()
    {
        var model = GuestCycleKit.Fresh();

        AssertIgnored(model, new SelectTemplateAction(GuestCycleKit.Signature, 4));
        AssertIgnored(model, new SelectTemplateAction("other", 0));
    }

    [Fact]
    public void A_Template_is_chosen_only_on_the_Template_step_because_guests_never_come_back_to_it()
    {
        AssertIgnored(GuestCycleKit.OnPhotographs(), new SelectTemplateAction("pair-v1", 2));
        AssertIgnored(GuestCycleKit.Loaded(), new SelectTemplateAction("pair-v1", 2));
        AssertIgnored(GuestCycleKit.Loaded().Do(new GoToStepAction(GuestCycleStep.Filters)), new SelectTemplateAction("pair-v1", 2));
    }

    [Fact]
    public void A_Template_change_gives_the_new_Templates_empty_windows_and_counts_as_an_arrangement_change()
    {
        var model = GuestCycleKit.Fresh();

        var changed = model.Do(new SelectTemplateAction("butter-gingham", 3));

        Assert.Equal("butter-gingham", changed.TemplateId);
        Assert.Equal(3, changed.WindowCount);
        Assert.Equal([null, null, null], changed.Windows);
        Assert.Equal(GuestCycleStep.Template, changed.Step);
        Assert.Equal(model.Revision + 1, changed.Revision);
    }

    // Capture sequence

    [Fact]
    public void A_sequence_fills_empty_windows_selects_each_and_counts_each_as_an_arrangement_change()
    {
        var state = GuestCycleKit.OnPhotographs().Do(new BeginSequenceAction(1));
        var revision = state.Revision;

        state = state.Do(new CaptureCommittedAction(1, 0, GuestCycleKit.Capture("A")));
        Assert.Equal(0, state.Selected);
        Assert.Equal(revision + 1, state.Revision);
        state = state.Do(new CaptureCommittedAction(1, 1, GuestCycleKit.Capture("B", 2)));

        Assert.Equal(["A", "B", null, null], state.Windows);
        Assert.Equal(revision + 2, state.Revision);
        Assert.Equal(1, state.Selected);
        Assert.Equal(2, state.NextEmptyWindow);
        Assert.Equal(OperationMode.Sequence, state.Operation!.Mode);
    }

    [Fact]
    public void A_sequence_ignores_a_duplicate_Capture_an_occupied_window_and_a_window_that_does_not_exist()
    {
        var state = GuestCycleKit.OnPhotographs().Do(new BeginSequenceAction(1)).Do(new CaptureCommittedAction(1, 0, GuestCycleKit.Capture("A")));

        AssertIgnored(state, new CaptureCommittedAction(1, 1, GuestCycleKit.Capture("A")));
        AssertIgnored(state, new CaptureCommittedAction(1, 0, GuestCycleKit.Capture("B", 2)));
        AssertIgnored(state, new CaptureCommittedAction(1, 4, GuestCycleKit.Capture("B", 2)));
        AssertIgnored(state, new CaptureCommittedAction(1, -1, GuestCycleKit.Capture("B", 2)));
        AssertIgnored(GuestCycleKit.OnPhotographs(), new CaptureCommittedAction(1, 0, GuestCycleKit.Capture("B", 2)));
    }

    [Fact]
    public void A_sequence_begins_only_on_Photographs_with_no_operation_and_an_empty_window()
    {
        AssertIgnored(GuestCycleKit.Fresh(), new BeginSequenceAction(1));
        AssertIgnored(GuestCycleKit.Loaded(), new BeginSequenceAction(1));
        AssertIgnored(GuestCycleKit.OnPhotographs().Do(new BeginSequenceAction(1)), new BeginSequenceAction(2));
    }

    [Fact]
    public void Beginning_a_sequence_shows_the_Camera_and_drops_a_pending_Move()
    {
        var model = GuestCycleKit.Loaded("A", "B").Do(new ShowTabAction(PhotographsTab.Arrange)).Do(new MoveStartAction(0));

        var begun = model.Do(new BeginSequenceAction(5));

        Assert.Equal(PhotographsTab.Camera, begun.Tab);
        Assert.Null(begun.Move);
        Assert.Equal(new GuestOperation(5, OperationMode.Sequence, null, null), begun.Operation);
    }

    [Fact]
    public void Finishing_a_sequence_that_filled_every_window_shows_Arrange_and_otherwise_stays_on_the_Camera()
    {
        var partial = GuestCycleKit.OnPhotographs().Do(new BeginSequenceAction(1)).Do(new CaptureCommittedAction(1, 0, GuestCycleKit.Capture("A")));
        var finished = partial.Do(new FinishSequenceAction(1));
        Assert.Null(finished.Operation);
        Assert.Equal(PhotographsTab.Camera, finished.Tab);

        Assert.Equal(PhotographsTab.Arrange, GuestCycleKit.Loaded().Tab);
    }

    [Fact]
    public void Finishing_ignores_a_stale_operation_no_operation_and_a_Retake()
    {
        AssertIgnored(GuestCycleKit.Loaded(), new FinishSequenceAction(1));
        AssertIgnored(Retaking(), new FinishSequenceAction(2));
        AssertIgnored(GuestCycleKit.OnPhotographs().Do(new BeginSequenceAction(4)), new FinishSequenceAction(3));
    }

    // Retake

    [Fact]
    public void A_Retake_begins_only_on_Photographs_for_an_occupied_window_with_no_operation()
    {
        var loaded = GuestCycleKit.Loaded();

        AssertIgnored(GuestCycleKit.Fresh(), new BeginRetakeAction(1, 0));
        AssertIgnored(GuestCycleKit.Loaded("A", "B"), new BeginRetakeAction(1, 3));
        AssertIgnored(loaded, new BeginRetakeAction(1, 4));
        AssertIgnored(loaded, new BeginRetakeAction(1, -1));
        AssertIgnored(Retaking(), new BeginRetakeAction(9, 0));
    }

    [Fact]
    public void A_Retake_shows_the_Camera_selects_the_target_and_leaves_the_original_in_its_window()
    {
        var model = GuestCycleKit.Loaded();
        Assert.Equal(PhotographsTab.Arrange, model.Tab);

        var retake = model.Do(new BeginRetakeAction(7, 2));

        Assert.Equal(PhotographsTab.Camera, retake.Tab);
        Assert.Equal(2, retake.Selected);
        Assert.Equal(new GuestOperation(7, OperationMode.Retake, 2, null), retake.Operation);
        Assert.Equal(model.Windows, retake.Windows);
        Assert.Equal(model.Revision, retake.Revision);
    }

    [Fact]
    public void The_original_stays_in_its_window_until_the_Retake_is_accepted()
    {
        var candidate = WithCandidate();

        Assert.Equal("B", candidate.Windows[1]);
        Assert.True(candidate.KeptCaptures.ContainsKey("B"));
        Assert.True(candidate.KeptCaptures.ContainsKey("E"));
        Assert.Equal("E", candidate.Operation!.Candidate);
    }

    [Fact]
    public void Accepting_returns_to_Arrange_counts_as_an_arrangement_change_and_ends_the_operation()
    {
        var candidate = WithCandidate();

        var accepted = candidate.Do(new AcceptRetakeAction());

        Assert.Null(accepted.Operation);
        Assert.Equal(PhotographsTab.Arrange, accepted.Tab);
        Assert.Equal(candidate.Revision + 1, accepted.Revision);
    }

    [Fact]
    public void A_discarded_Capture_id_committed_again_is_a_new_kept_Capture_because_the_model_keeps_no_record_of_discards()
    {
        var retaken = GuestCycleKit.Loaded("A", "B", "C").Do(new BeginRetakeAction(2, 1)).Do(new CaptureCommittedAction(2, 1, GuestCycleKit.Capture("E", 5)));
        var accepted = retaken.Try(new AcceptRetakeAction());
        Assert.Equal(["B"], accepted.DiscardedCaptureIds);
        Assert.DoesNotContain("B", accepted.Model.KeptCaptures.Keys);

        var again = accepted.Model.Try(new BeginSequenceAction(3)).Model.Try(new CaptureCommittedAction(3, 3, GuestCycleKit.Capture("B", 6)));

        Assert.True(again.Applied);
        Assert.Equal("B", again.Model.Windows[3]);
        Assert.Contains("B", again.Model.KeptCaptures.Keys);
    }

    [Fact]
    public void Accepting_without_a_candidate_or_a_Retake_is_ignored()
    {
        AssertIgnored(Retaking(), new AcceptRetakeAction());
        AssertIgnored(GuestCycleKit.Loaded(), new AcceptRetakeAction());
        var sequence = GuestCycleKit.OnPhotographs().Do(new BeginSequenceAction(1));
        AssertIgnored(sequence, new AcceptRetakeAction());
    }

    [Fact]
    public void Try_again_discards_only_the_candidate_and_stays_in_the_Retake()
    {
        var outcome = WithCandidate().Try(new TryAgainAction());

        Assert.True(outcome.Applied);
        Assert.Equal(["E"], outcome.DiscardedCaptureIds);
        Assert.Equal(OperationMode.Retake, outcome.Model.Operation!.Mode);
        Assert.Equal(PhotographsTab.Camera, outcome.Model.Tab);
        Assert.Equal("B", outcome.Model.Windows[1]);

        var next = outcome.Model.Do(new CaptureCommittedAction(2, 1, GuestCycleKit.Capture("F", 6)));
        Assert.Equal("F", next.Operation!.Candidate);
    }

    [Fact]
    public void Try_again_without_a_candidate_is_ignored()
    {
        AssertIgnored(Retaking(), new TryAgainAction());
        AssertIgnored(GuestCycleKit.Loaded(), new TryAgainAction());
    }

    [Fact]
    public void Keep_original_cancels_the_Retake_discards_a_pending_candidate_and_returns_to_Arrange()
    {
        var withCandidate = WithCandidate().Try(new KeepOriginalAction());
        Assert.Equal(["E"], withCandidate.DiscardedCaptureIds);
        Assert.Null(withCandidate.Model.Operation);
        Assert.Equal(PhotographsTab.Arrange, withCandidate.Model.Tab);
        Assert.Equal("B", withCandidate.Model.Windows[1]);

        var withoutCandidate = Retaking().Try(new KeepOriginalAction());
        Assert.True(withoutCandidate.Applied);
        Assert.Empty(withoutCandidate.DiscardedCaptureIds);
        Assert.Null(withoutCandidate.Model.Operation);
    }

    [Fact]
    public void Keep_original_outside_a_Retake_is_ignored()
    {
        AssertIgnored(GuestCycleKit.Loaded(), new KeepOriginalAction());
        AssertIgnored(GuestCycleKit.OnPhotographs().Do(new BeginSequenceAction(1)), new KeepOriginalAction());
    }

    // Cancel

    [Fact]
    public void Cancel_ends_a_sequence_and_keeps_every_Capture_it_saved()
    {
        var state = GuestCycleKit.OnPhotographs().Do(new BeginSequenceAction(1)).Do(new CaptureCommittedAction(1, 0, GuestCycleKit.Capture("A")));

        var cancelled = state.Try(new CancelAction());

        Assert.True(cancelled.Applied);
        Assert.Empty(cancelled.DiscardedCaptureIds);
        Assert.Null(cancelled.Model.Operation);
        Assert.Equal(["A", null, null, null], cancelled.Model.Windows);
        Assert.Equal(PhotographsTab.Camera, cancelled.Model.Tab);
    }

    [Fact]
    public void Cancel_clears_a_pending_Move_and_with_nothing_pending_is_ignored()
    {
        var moving = GuestCycleKit.Loaded().Do(new MoveStartAction(1));

        Assert.Null(moving.Do(new CancelAction()).Move);
        AssertIgnored(GuestCycleKit.Loaded(), new CancelAction());
    }

    // Selection and Move

    [Fact]
    public void Selecting_a_window_out_of_range_is_ignored()
    {
        var model = GuestCycleKit.Loaded();

        AssertIgnored(model, new SelectWindowAction(4));
        AssertIgnored(model, new SelectWindowAction(-1));
        Assert.Equal(2, model.Do(new SelectWindowAction(2)).Selected);
    }

    [Fact]
    public void A_Move_begins_only_from_an_occupied_window_and_never_during_an_operation()
    {
        var partial = GuestCycleKit.Loaded("A", "B");

        AssertIgnored(partial, new MoveStartAction(3));
        AssertIgnored(partial, new MoveStartAction(4));
        AssertIgnored(partial, new MoveStartAction(-1));
        AssertIgnored(Retaking(), new MoveStartAction(0));
    }

    [Fact]
    public void A_Move_remembers_where_it_began_and_at_which_revision()
    {
        var model = GuestCycleKit.Loaded();

        Assert.Equal(new GuestMove(2, "C", model.Revision), model.Do(new MoveStartAction(2)).Move);
    }

    [Fact]
    public void Moving_without_a_Move_or_cancelling_without_one_is_ignored()
    {
        var model = GuestCycleKit.Loaded();

        AssertIgnored(model, new MoveToAction(1));
        AssertIgnored(model, new MoveCancelAction());
    }

    [Fact]
    public void A_Move_to_a_window_that_does_not_exist_is_ignored_and_the_Move_stays()
    {
        var moving = GuestCycleKit.Loaded().Do(new MoveStartAction(1));

        AssertIgnored(moving, new MoveToAction(4));
        AssertIgnored(moving, new MoveToAction(-1));
        Assert.NotNull(moving.Do(new MoveToAction(4)).Move);
    }

    [Fact]
    public void A_Move_onto_itself_clears_the_Move_selects_the_window_and_keeps_the_revision()
    {
        var moving = GuestCycleKit.Loaded().Do(new MoveStartAction(1));

        var placed = moving.Do(new MoveToAction(1));

        Assert.Null(placed.Move);
        Assert.Equal(1, placed.Selected);
        Assert.Equal(moving.Revision, placed.Revision);
        Assert.Equal(moving.Windows, placed.Windows);
    }

    [Fact]
    public void A_Move_into_an_empty_window_moves_the_Capture_and_counts_as_an_arrangement_change()
    {
        var model = GuestCycleKit.Loaded("A", "B");

        var moved = model.Move(0, 3);

        Assert.Equal([null, "B", null, "A"], moved.Windows);
        Assert.Equal(model.Revision + 1, moved.Revision);
        Assert.Equal(3, moved.Selected);
    }

    [Fact]
    public void A_Move_during_an_operation_is_ignored()
    {
        var model = Retaking() with { Move = new GuestMove(0, "A", 0) };

        AssertIgnored(model, new MoveToAction(2));
    }

    [Fact]
    public void Cancelling_a_Move_keeps_the_arrangement()
    {
        var moving = GuestCycleKit.Loaded().Do(new MoveStartAction(1));

        var cancelled = moving.Do(new MoveCancelAction());

        Assert.Null(cancelled.Move);
        Assert.Equal(moving.Windows, cancelled.Windows);
        Assert.Equal(moving.Revision, cancelled.Revision);
    }

    // Drag

    [Fact]
    public void A_drag_release_swaps_like_any_other_arrangement_method_and_counts_as_one_change()
    {
        var model = GuestCycleKit.Loaded();

        var placed = model.Do(new DragPlaceAction(2, "C", model.Revision, 0));

        Assert.Equal(["C", "B", "A", "D"], placed.Windows);
        Assert.Equal(model.Revision + 1, placed.Revision);
        Assert.Equal(0, placed.Selected);
    }

    [Fact]
    public void A_drag_release_is_ignored_after_the_arrangement_changed_the_Capture_moved_or_an_operation_began()
    {
        var model = GuestCycleKit.Loaded();

        AssertIgnored(model, new DragPlaceAction(2, "C", model.Revision - 1, 0));
        AssertIgnored(model, new DragPlaceAction(2, "A", model.Revision, 0));
        AssertIgnored(model, new DragPlaceAction(4, "C", model.Revision, 0));
        AssertIgnored(model, new DragPlaceAction(-1, "C", model.Revision, 0));
        AssertIgnored(model.Do(new MoveStartAction(1)), new DragPlaceAction(2, "C", model.Revision, 0));
        AssertIgnored(Retaking(), new DragPlaceAction(2, "C", model.Revision, 0));
        AssertIgnored(model.Do(new GoToStepAction(GuestCycleStep.Filters)), new DragPlaceAction(2, "C", model.Revision, 0));
        AssertIgnored(model, new DragPlaceAction(2, "C", model.Revision, 4));
    }

    [Fact]
    public void A_drag_released_on_its_own_window_changes_no_arrangement()
    {
        var model = GuestCycleKit.Loaded();

        var placed = model.Do(new DragPlaceAction(2, "C", model.Revision, 2));

        Assert.Equal(model.Windows, placed.Windows);
        Assert.Equal(model.Revision, placed.Revision);
    }

    [Fact]
    public void A_late_drag_release_from_before_a_Retake_is_refused()
    {
        var model = GuestCycleKit.Loaded();
        var gesture = new DragPlaceAction(1, "B", model.Revision, 0);
        var retaken = WithCandidate().Do(new AcceptRetakeAction());

        AssertIgnored(retaken, gesture);
    }

    // Look

    [Fact]
    public void A_Look_change_ignores_the_same_Look_and_an_operation_and_keeps_the_revision()
    {
        var model = GuestCycleKit.Loaded();

        AssertIgnored(model, new SelectLookAction(GuestCycleKit.Naturale));
        AssertIgnored(Retaking(), new SelectLookAction("sepia-v1"));
        var changed = model.Do(new SelectLookAction("sepia-v1"));
        Assert.Equal("sepia-v1", changed.LookId);
        Assert.Equal(model.Revision, changed.Revision);
    }

    // Steps

    [Fact]
    public void Going_to_the_step_the_cycle_is_on_is_ignored()
    {
        AssertIgnored(GuestCycleKit.Fresh(), new GoToStepAction(GuestCycleStep.Template));
        AssertIgnored(GuestCycleKit.Loaded(), new GoToStepAction(GuestCycleStep.Photographs));
        AssertIgnored(GuestCycleKit.Loaded().Do(new GoToStepAction(GuestCycleStep.Filters)), new GoToStepAction(GuestCycleStep.Filters));
    }

    [Fact]
    public void Going_back_to_an_earlier_step_is_ignored()
    {
        var filters = GuestCycleKit.Loaded().Do(new GoToStepAction(GuestCycleStep.Filters));

        AssertIgnored(GuestCycleKit.Loaded(), new GoToStepAction(GuestCycleStep.Template));
        AssertIgnored(WithCandidate(), new GoToStepAction(GuestCycleStep.Template));
        AssertIgnored(filters, new GoToStepAction(GuestCycleStep.Photographs));
        AssertIgnored(filters, new GoToStepAction(GuestCycleStep.Template));
    }

    [Fact]
    public void Going_on_to_Photographs_opens_the_Camera_with_every_window_empty()
    {
        var model = GuestCycleKit.Fresh().Do(new SelectTemplateAction("butter-gingham", 3)).Do(new GoToStepAction(GuestCycleStep.Photographs));

        Assert.Equal(GuestCycleStep.Photographs, model.Step);
        Assert.Equal(PhotographsTab.Camera, model.Tab);
        Assert.Equal([null, null, null], model.Windows);
    }

    [Fact]
    public void Filters_needs_a_complete_arrangement_with_no_operation_and_no_Move()
    {
        AssertIgnored(GuestCycleKit.Loaded("A", "B"), new GoToStepAction(GuestCycleStep.Filters));
        AssertIgnored(GuestCycleKit.Loaded().Do(new MoveStartAction(0)), new GoToStepAction(GuestCycleStep.Filters));
        AssertIgnored(Retaking(), new GoToStepAction(GuestCycleStep.Filters));
        Assert.Equal(GuestCycleStep.Filters, GuestCycleKit.Loaded().Do(new GoToStepAction(GuestCycleStep.Filters)).Step);
    }

    [Fact]
    public void Keepsake_follows_Filters_only()
    {
        var loaded = GuestCycleKit.Loaded();

        AssertIgnored(loaded, new GoToStepAction(GuestCycleStep.Keepsake));
        AssertIgnored(GuestCycleKit.Fresh(), new GoToStepAction(GuestCycleStep.Keepsake));
        Assert.Equal(GuestCycleStep.Keepsake, loaded.Do(new GoToStepAction(GuestCycleStep.Filters)).Do(new GoToStepAction(GuestCycleStep.Keepsake)).Step);
    }

    [Fact]
    public void Going_on_to_Filters_and_the_Keepsake_keeps_every_kept_Capture()
    {
        var state = GuestCycleKit.Loaded();
        var ids = state.KeptCaptures.Keys.Order().ToArray();

        foreach (var step in new[] { GuestCycleStep.Filters, GuestCycleStep.Keepsake })
        {
            var outcome = state.Try(new GoToStepAction(step));
            Assert.True(outcome.Applied);
            Assert.Empty(outcome.DiscardedCaptureIds);
            state = outcome.Model;
            Assert.Equal(ids, state.KeptCaptures.Keys.Order().ToArray());
        }
    }

    // Tabs

    [Fact]
    public void A_tab_shows_only_on_Photographs_with_no_operation_and_Arrange_only_with_a_photograph()
    {
        AssertIgnored(GuestCycleKit.Fresh(), new ShowTabAction(PhotographsTab.Arrange));
        AssertIgnored(GuestCycleKit.OnPhotographs(), new ShowTabAction(PhotographsTab.Camera));
        AssertIgnored(GuestCycleKit.OnPhotographs(), new ShowTabAction(PhotographsTab.Arrange));
        AssertIgnored(Retaking(), new ShowTabAction(PhotographsTab.Arrange));
        AssertIgnored(GuestCycleKit.Loaded(), new ShowTabAction(PhotographsTab.Arrange));
    }

    [Fact]
    public void The_guest_can_review_a_partial_strip_and_return_to_the_Camera()
    {
        var partial = GuestCycleKit.Loaded("A", "B");
        Assert.Equal(PhotographsTab.Camera, partial.Tab);

        var arrange = partial.Do(new ShowTabAction(PhotographsTab.Arrange));
        Assert.Equal(PhotographsTab.Arrange, arrange.Tab);
        Assert.Equal(PhotographsTab.Camera, arrange.Do(new ShowTabAction(PhotographsTab.Camera)).Tab);
    }

    // Completion

    [Fact]
    public void A_completed_cycle_ignores_every_action()
    {
        var done = GuestCycleKit.Loaded().Do(new GoToStepAction(GuestCycleStep.Filters)).Do(new GoToStepAction(GuestCycleStep.Keepsake));
        GuestCycleAction[] actions =
        [
            new SelectTemplateAction("pair-v1", 2),
            new BeginSequenceAction(1),
            new BeginRetakeAction(1, 0),
            new CaptureCommittedAction(1, 0, GuestCycleKit.Capture("Z", 9)),
            new FinishSequenceAction(1),
            new CancelAction(),
            new AcceptRetakeAction(),
            new TryAgainAction(),
            new KeepOriginalAction(),
            new SelectWindowAction(0),
            new MoveStartAction(0),
            new MoveToAction(1),
            new DragPlaceAction(0, "A", done.Revision, 1),
            new MoveCancelAction(),
            new SelectLookAction("sepia-v1"),
            new GoToStepAction(GuestCycleStep.Template),
            new ShowTabAction(PhotographsTab.Arrange),
        ];

        foreach (var action in actions)
        {
            AssertIgnored(done, action);
        }
    }
}
