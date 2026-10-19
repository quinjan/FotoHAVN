using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

/// <summary>Starting a Guest Cycle, its steps and their progress, skipped steps, Template choice, forward-only steps, the removed Start over, and diagnostics (plan section 7, Steps).</summary>
public sealed class GuestCycleStepTests
{
    private const string FirstCycle = "00000000-0000-0000-0000-000000000001";

    private static GuestRigOptions OneTemplate => new() { Tweak = record => record with { OfferedTemplateIds = ["signature-strip"] } };

    private static GuestRigOptions ThreeTemplates => new()
    {
        Tweak = record => record with { OfferedTemplateIds = ["signature-strip", "panorama-four", "butter-gingham"] },
    };

    [Fact]
    public async Task STEP_INSIDE_creates_a_cycle_in_the_store_and_shows_the_Template_step()
    {
        using var rig = await GuestCycleRig.CreateAsync();

        Assert.Equal(GuestSurface.Start, rig.Surface);
        rig.Guest("BeginGuestCycle").ShouldBeAccepted();

        var view = rig.View;
        Assert.Equal(GuestSurface.Template, view.Surface);
        Assert.Equal(FirstCycle, view.GuestCycle!.Id);
        Assert.Equal(GuestCycleStepName.Template, view.GuestCycle.Step);
        Assert.Equal([GuestStepState.Current, GuestStepState.Upcoming, GuestStepState.Upcoming, GuestStepState.Upcoming], view.GuestCycle.Steps.Select(step => step.State));
        Assert.Null(view.Photographs);
        Assert.Null(view.Filters);
        Assert.Null(view.Keepsake);
        Assert.Null(view.OperatorAssistance);

        var detail = await rig.OnlyCycleAsync();
        Assert.Equal(FirstCycle, detail.Cycle.Id);
        Assert.Null(detail.Cycle.EndReason);
    }

    [Fact]
    public async Task The_Template_step_offers_the_Events_Templates_with_their_geometry_and_blank_previews()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();

        var template = rig.Template;
        Assert.Equal("signature-strip", template.SelectedId);
        Assert.True(template.CanContinue);
        Assert.Equal(["signature-strip", "panorama-four"], template.Templates.Select(option => option.Id));

        var signature = template.Templates[0];
        Assert.Equal((600, 1800, false), (signature.Width, signature.Height, signature.Landscape));
        Assert.Equal(4, signature.Windows.Count);
        Assert.Equal(new TemplateWindowView(0, 450, 600, 450), signature.Windows[1]);
        Assert.False(string.IsNullOrEmpty(signature.Name));
        Assert.False(string.IsNullOrEmpty(signature.Description));
        Assert.True(template.Templates[1].Landscape);

        // Start Event composed the blank previews; the keys are the ones the preview host serves.
        Assert.Equal("signature-strip.naturale-v1.r0", signature.BlankPreviewKey);
        Assert.True(rig.Active.Previews.TryGet(signature.BlankPreviewKey!, out _));
    }

    [Fact]
    public async Task A_blank_preview_that_was_not_composed_is_null()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.Active.Previews.Remove("signature-strip.naturale-v1.r0");
        rig.Guest("SelectTemplate", """{"templateId":"panorama-four"}""");

        Assert.Null(rig.Template.Templates[0].BlankPreviewKey);
        Assert.NotNull(rig.Template.Templates[1].BlankPreviewKey);
    }

    [Fact]
    public async Task STEP_INSIDE_is_refused_while_no_cycle_may_start()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Active.Camera.Stall();
        rig.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(GuestSurface.StartUnavailable, rig.Surface);

        rig.Guest("BeginGuestCycle").ShouldBeRejected();
        Assert.Equal(GuestSurface.StartUnavailable, rig.Surface);
    }

    [Fact]
    public async Task STEP_INSIDE_is_refused_while_storage_is_known_to_be_failing()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Active.Executor.Post(() => rig.Brain.SetActiveStorageFailed(true));
        rig.Settle();

        rig.Guest("BeginGuestCycle").ShouldBeRejected();
    }

    [Fact]
    public async Task STEP_INSIDE_is_refused_behind_the_PIN_dialog_and_when_no_Event_runs_and_when_a_cycle_exists()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Active.HoldToExit();
        rig.Guest("BeginGuestCycle").ShouldBeRejected();
        rig.Guest("CancelExit").ShouldBeAccepted();

        rig.Begin();
        rig.Guest("BeginGuestCycle").ShouldBeRejected();

        using var idle = await GuestCycleRig.CreateAsync(new GuestRigOptions { Start = false });
        idle.Guest("BeginGuestCycle").ShouldBeRejected();
    }

    [Fact]
    public async Task A_second_tap_while_the_store_creates_the_cycle_is_refused()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Store.StartGate = new TaskCompletionSource();

        rig.Guest("BeginGuestCycle").ShouldBeAccepted();
        rig.Guest("BeginGuestCycle").ShouldBeRejected();
        Assert.Equal(GuestSurface.Start, rig.Surface);

        rig.Store.StartGate.SetResult();
        rig.Store.StartGate = null;
        rig.Settle();
        Assert.Equal(GuestSurface.Template, rig.Surface);
    }

    [Fact]
    public async Task An_Event_that_ends_while_the_cycle_is_being_created_leaves_no_open_cycle()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Store.StartGate = new TaskCompletionSource();
        rig.Guest("BeginGuestCycle").ShouldBeAccepted();

        rig.Operator("EndEvent").ShouldBeAccepted();
        rig.Operator("ConfirmEndEvent").ShouldBeAccepted();
        rig.Store.StartGate.SetResult();
        rig.Store.StartGate = null;
        rig.Settle();

        var ended = Assert.Single(rig.Store.Ended);
        Assert.Equal((FirstCycle, GuestCycleEndReason.OperatorExit), ended);
        Assert.Equal(GuestSurface.Start, rig.Surface);
    }

    [Fact]
    public async Task One_offered_Template_skips_the_Template_step()
    {
        using var rig = await GuestCycleRig.CreateAsync(OneTemplate);

        rig.Begin();

        Assert.Equal(GuestSurface.Photographs, rig.Surface);
        var steps = rig.View.GuestCycle!.Steps;
        Assert.Equal(new GuestCycleStepEntry(GuestCycleStepName.Template, GuestStepState.Skipped), steps[0]);
        Assert.Equal(new GuestCycleStepEntry(GuestCycleStepName.Photographs, GuestStepState.Current), steps[1]);
        rig.Guest("GoToStep", """{"step":"template"}""").ShouldBeRejected();
    }

    [Fact]
    public async Task One_offered_Look_skips_the_Filters_step_in_the_progress()
    {
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions { Tweak = record => record with { OfferedLookIds = ["naturale-v1"] } });

        rig.Begin();

        Assert.Equal(new GuestCycleStepEntry(GuestCycleStepName.Filters, GuestStepState.Skipped), rig.View.GuestCycle!.Steps[2]);
    }

    [Fact]
    public async Task Choosing_a_Template_changes_the_windows_the_guest_fills()
    {
        using var rig = await GuestCycleRig.CreateAsync(ThreeTemplates);
        rig.Begin();

        rig.Guest("SelectTemplate", """{"templateId":"butter-gingham"}""").ShouldBeAccepted();
        Assert.Equal("butter-gingham", rig.Template.SelectedId);

        rig.ToPhotographs();
        Assert.Equal(3, rig.Photographs.Windows.Count);
        Assert.Equal("butter-gingham", rig.Photographs.Template.Id);
        Assert.Equal(547.0 / 522.0, rig.Photographs.CropAspect, 6);
    }

    [Fact]
    public async Task Choosing_the_Template_already_selected_is_a_harmless_tap_and_an_unoffered_one_is_invalid()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();

        rig.Guest("SelectTemplate", """{"templateId":"signature-strip"}""").ShouldBeAccepted();
        rig.Guest("SelectTemplate", """{"templateId":"polka-keepsake"}""").ShouldBeRejected(RejectReason.InvalidPayload);
        rig.Guest("SelectTemplate", """{"templateId":"nope"}""").ShouldBeRejected(RejectReason.InvalidPayload);
        Assert.Equal("signature-strip", rig.Template.SelectedId);
    }

    [Fact]
    public async Task A_Template_can_only_be_chosen_on_the_Template_step()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();

        rig.Guest("SelectTemplate", """{"templateId":"panorama-four"}""").ShouldBeRejected();
    }

    [Fact]
    public async Task CONTINUE_goes_to_Photographs_and_the_Camera_tab_with_empty_windows()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();

        rig.ToPhotographs();

        var view = rig.Photographs;
        Assert.Equal(PhotographsTabName.Camera, view.Tab);
        Assert.All(view.Windows, window => Assert.Null(window.CaptureId));
        Assert.True(view.CanTakePhotographs);
        Assert.False(view.CanReview);
        Assert.False(view.CanContinue);
        Assert.True(view.Mirrored);
        Assert.Equal(600.0 / 450.0, view.CropAspect, 6);
        Assert.Equal(new CaptureSequenceView(false, null, null, 0, 0, 0, false, true), view.Sequence);
        Assert.Equal([GuestStepState.Done, GuestStepState.Current, GuestStepState.Upcoming, GuestStepState.Upcoming], rig.View.GuestCycle!.Steps.Select(step => step.State));
    }

    [Fact]
    public async Task Guests_never_go_back_to_an_earlier_step()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();

        rig.Guest("GoToStep", """{"step":"template"}""").ShouldBeRejected();
        Assert.Equal(GuestSurface.Photographs, rig.Surface);

        rig.ToFilters();
        rig.Guest("GoToStep", """{"step":"photographs"}""").ShouldBeRejected();
        rig.Guest("GoToStep", """{"step":"template"}""").ShouldBeRejected();
        Assert.Equal(GuestSurface.Filters, rig.Surface);
        Assert.Equal(4, (await rig.OnlyCycleAsync()).Captures.Count(capture => capture.State == CaptureState.Kept));
    }

    [Fact]
    public async Task Going_back_is_refused_on_the_Keepsake_too()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        ToStep(rig, "keepsake");

        rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeRejected();
        rig.Guest("GoToStep", """{"step":"template"}""").ShouldBeRejected();
        Assert.Equal(GuestSurface.Keepsake, rig.Surface);
    }

    [Fact]
    public async Task A_step_the_guest_is_on_cannot_be_gone_to_and_Filters_waits_for_every_photograph()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();

        rig.Guest("GoToStep", """{"step":"template"}""").ShouldBeRejected();
        rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeRejected();
        rig.ToPhotographs();
        rig.Guest("GoToStep", """{"step":"photographs"}""").ShouldBeRejected();
        rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeRejected();
        Assert.Equal(GuestSurface.Photographs, rig.Surface);
    }

    [Fact]
    public async Task Step_commands_are_refused_when_no_cycle_exists()
    {
        using var rig = await GuestCycleRig.CreateAsync();

        rig.Guest("GoToStep", """{"step":"photographs"}""").ShouldBeRejected();
        rig.Guest("SelectTemplate", """{"templateId":"signature-strip"}""").ShouldBeRejected();
        rig.Guest("TakePhotographs").ShouldBeRejected();
    }

    [Fact]
    public async Task The_operators_panel_names_the_step_the_guest_is_on()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        Assert.Equal(RunningEventStep.Start, rig.OperatorView.RunningEvent!.CurrentStep);

        rig.Begin();
        Assert.Equal(RunningEventStep.Template, rig.OperatorView.RunningEvent!.CurrentStep);
        rig.ToPhotographs();
        Assert.Equal(RunningEventStep.Photographs, rig.OperatorView.RunningEvent!.CurrentStep);
        rig.TakeAll();
        rig.ToFilters();
        Assert.Equal(RunningEventStep.Filters, rig.OperatorView.RunningEvent!.CurrentStep);
        rig.Guest("FinishStrip").ShouldBeAccepted();
        rig.Settle();
        Assert.Equal(RunningEventStep.Keepsake, rig.OperatorView.RunningEvent!.CurrentStep);
    }

    [Theory]
    [InlineData("template")]
    [InlineData("photographs")]
    [InlineData("filters")]
    [InlineData("keepsake")]
    public async Task START_OVER_MORE_TIME_and_STOP_are_unknown_commands_on_every_step(string step)
    {
        using var rig = await GuestCycleRig.CreateAsync();
        ToStep(rig, step);
        var surface = rig.Surface;

        foreach (var removed in new[] { "StartOver", "ConfirmStartOver", "KeepGoing", "MoreTime", "StopCapture" })
        {
            rig.Guest(removed).ShouldBeRejected(RejectReason.UnknownCommand);
        }

        Assert.Equal(surface, rig.Surface);
        Assert.Empty(rig.Store.Ended);
    }

    [Fact]
    public async Task Diagnostics_describe_the_running_cycle_and_are_null_without_one()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        Assert.Null(rig.Diagnostics().GuestCycle);

        rig.Begin();
        var template = rig.CycleDiagnostics();
        Assert.Equal(FirstCycle, template.Id);
        Assert.Equal(GuestCycleStepName.Template, template.Step);
        Assert.Null(template.Tab);
        Assert.Equal((0, 4, false, null), (template.WindowsFilled, template.WindowCount, template.SequenceRunning, template.CountdownRemaining));
        Assert.Equal(PhaseTimer.Template, template.Timer.Phase);
        Assert.Equal(30_000, template.Timer.RemainingMs);
        Assert.False(template.Timer.Paused);

        rig.ToPhotographs();
        var photographs = rig.CycleDiagnostics();
        Assert.Equal(PhotographsTabName.Camera, photographs.Tab);
        Assert.Equal(PhaseTimer.PhotographsCapture, photographs.Timer.Phase);
    }

    private static void ToStep(GuestCycleRig rig, string step)
    {
        switch (step)
        {
            case "template":
                rig.Begin();
                break;
            case "photographs":
                rig.ToArrange();
                break;
            case "filters":
                rig.ToArrange();
                rig.ToFilters();
                break;
            default:
                rig.ToArrange();
                rig.ToFilters();
                rig.Guest("FinishStrip").ShouldBeAccepted();
                Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50)));
                break;
        }
    }
}

internal static class GuestCycleRigDiagnostics
{
    public static GuestCycleDiagnostics? CycleDiagnosticsOrNull(this GuestCycleRig rig) => rig.Diagnostics().GuestCycle;
}
