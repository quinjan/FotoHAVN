using FotoHavn.Core.Events;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.Events.Active;

/// <summary>The Guest Screen's exit: the 3-second hold, the PIN dialog, the wrong-PIN lockout on the brain's clock, and the PIN never leaking.</summary>
public sealed class ExitHoldAndPinTests
{
    private const string WrongPin = "0000";

    private static async Task<ActiveRig> RunningAsync(ActiveRigOptions? options = null)
    {
        var rig = await ActiveRig.CreateAsync(options);
        rig.StartAndExpectSuccess();
        return rig;
    }

    private static PinDialogOverlay Dialog(ActiveRig rig) => rig.LatestGuest().Overlays.PinDialog!;

    // The hold

    [Fact]
    public async Task Beginning_the_hold_shows_it_and_completing_it_after_three_seconds_opens_the_PIN_dialog()
    {
        using var rig = await RunningAsync();

        var begin = rig.GuestCommand("BeginExitHold");
        Assert.Equal(CommandResultMessage.Accepted(begin.Id), begin);
        Assert.Equal(new ExitHoldOverlay(true), rig.LatestGuest().Overlays.ExitHold);
        Assert.Null(rig.LatestGuest().Overlays.PinDialog);

        rig.Advance(ExitHoldRules.RequiredHold);
        var complete = rig.GuestCommand("CompleteExitHold");

        Assert.Equal(CommandResultMessage.Accepted(complete.Id), complete);
        Assert.Null(rig.LatestGuest().Overlays.ExitHold);
        Assert.Equal(new PinDialogOverlay(null, null, 5), Dialog(rig));
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
    }

    [Fact]
    public async Task The_hold_is_accepted_from_2_85_seconds_on_the_brains_clock()
    {
        using var rig = await RunningAsync();
        rig.GuestCommand("BeginExitHold");
        rig.Advance(TimeSpan.FromSeconds(2.5));

        var early = rig.GuestCommand("CompleteExitHold");

        Assert.Equal(CommandResultMessage.Rejected(early.Id, RejectReason.NotAvailable), early);
        Assert.Null(rig.LatestGuest().Overlays.ExitHold);
        Assert.Null(rig.LatestGuest().Overlays.PinDialog);

        rig.GuestCommand("BeginExitHold");
        rig.Advance(ExitHoldRules.MinimumAccepted - TimeSpan.FromMilliseconds(10));
        Assert.Equal(RejectReason.NotAvailable, rig.GuestCommand("CompleteExitHold").Reason);

        rig.GuestCommand("BeginExitHold");
        rig.Advance(ExitHoldRules.MinimumAccepted);
        Assert.Equal(CommandStatus.Accepted, rig.GuestCommand("CompleteExitHold").Status);
        Assert.NotNull(rig.LatestGuest().Overlays.PinDialog);
    }

    [Fact]
    public async Task A_tap_is_rejected_and_clears_the_hold_so_a_new_one_can_begin()
    {
        using var rig = await RunningAsync();
        rig.GuestCommand("BeginExitHold");

        var tap = rig.GuestCommand("CompleteExitHold");

        Assert.Equal(RejectReason.NotAvailable, tap.Reason);
        Assert.Equal(CommandStatus.Accepted, rig.GuestCommand("BeginExitHold").Status);
    }

    [Fact]
    public async Task The_hold_commands_need_the_right_state()
    {
        using var rig = await RunningAsync();

        Assert.Equal(RejectReason.NotAvailable, rig.GuestCommand("CompleteExitHold").Reason);
        Assert.Equal(RejectReason.NotAvailable, rig.GuestCommand("CancelExit").Reason);
        Assert.Equal(CommandStatus.Accepted, rig.GuestCommand("BeginExitHold").Status);
        Assert.Equal(RejectReason.NotAvailable, rig.GuestCommand("BeginExitHold").Reason);

        rig.Advance(ExitHoldRules.RequiredHold);
        rig.GuestCommand("CompleteExitHold");
        Assert.Equal(RejectReason.NotAvailable, rig.GuestCommand("BeginExitHold").Reason);
    }

    [Fact]
    public async Task Releasing_early_cancels_the_hold()
    {
        using var rig = await RunningAsync();
        rig.GuestCommand("BeginExitHold");

        var cancel = rig.GuestCommand("CancelExit");

        Assert.Equal(CommandResultMessage.Accepted(cancel.Id), cancel);
        Assert.Null(rig.LatestGuest().Overlays.ExitHold);
    }

    [Fact]
    public async Task The_hold_commands_are_not_available_while_no_Event_runs()
    {
        using var rig = await ActiveRig.CreateAsync();

        Assert.Equal(RejectReason.NotAvailable, rig.GuestCommand("BeginExitHold").Reason);
        Assert.Equal(RejectReason.NotAvailable, rig.GuestCommand("CompleteExitHold").Reason);
        Assert.Equal(RejectReason.NotAvailable, rig.GuestCommand("CancelExit").Reason);
        Assert.Equal(RejectReason.NotAvailable, rig.SubmitPin(ActiveRig.Pin).Reason);
    }

    [Fact]
    public async Task The_operator_cannot_use_the_Guest_Screens_exit_commands()
    {
        using var rig = await RunningAsync();

        Assert.Equal(RejectReason.WrongRole, rig.OperatorCommand("BeginExitHold").Reason);
        Assert.Equal(RejectReason.WrongRole, rig.OperatorCommand("SubmitPin", """{"digits":"9137"}""").Reason);
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
    }

    // The PIN

    [Fact]
    public async Task The_right_PIN_ends_the_Event_exactly_as_END_EVENT_does()
    {
        using var rig = await RunningAsync(new ActiveRigOptions { Hooks = true });
        rig.HoldToExit();

        var submit = rig.SubmitPin(ActiveRig.Pin);

        Assert.Equal(CommandResultMessage.Accepted(submit.Id), submit);
        Assert.Null(rig.Brain.ActiveEventId);
        Assert.Equal(["OpenGuestScreen(fake-guest)", "CloseGuestScreen"], rig.Recorded.Calls);
        Assert.Equal(CameraState.Idle, rig.Brain.CameraSlotState);
        Assert.False(rig.Diagnostics().WakeLock.Held);
        Assert.Equal(OperatorSurface.EventList, rig.LatestOperator().Surface);
        Assert.Null(rig.LatestOperator().RunningEvent);
        Assert.Equal(new GuestPresentation(GuestSurface.Start, new StartView(false, false), null, GuestOverlays.None, null), rig.LatestGuest());
    }

    [Fact]
    public async Task A_wrong_PIN_is_accepted_and_the_presentation_counts_down_the_attempts()
    {
        using var rig = await RunningAsync();
        rig.HoldToExit();

        var submit = rig.SubmitPin(WrongPin);

        Assert.Equal(CommandResultMessage.Accepted(submit.Id), submit);
        Assert.Equal(new PinDialogOverlay(PinDialogError.WrongPin, null, 4), Dialog(rig));
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);

        rig.SubmitPin(WrongPin);
        Assert.Equal(new PinDialogOverlay(PinDialogError.WrongPin, null, 3), Dialog(rig));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("1234567")]
    [InlineData("12a4")]
    [InlineData("١٢٣٤")]
    public async Task A_malformed_PIN_is_a_wrong_attempt_that_never_reaches_the_store(string digits)
    {
        var store = new InterceptingStore(new InMemoryEventStore());
        using var rig = await RunningAsync(new ActiveRigOptions { Store = store });
        rig.HoldToExit();

        var submit = rig.GuestCommand("SubmitPin", $$"""{"digits":"{{digits}}"}""");

        Assert.Equal(CommandStatus.Accepted, submit.Status);
        Assert.Equal(new PinDialogOverlay(PinDialogError.WrongPin, null, 4), Dialog(rig));
        Assert.Empty(store.VerifiedPins);
    }

    [Fact]
    public async Task A_store_that_cannot_check_the_PIN_never_ends_the_Event_and_counts_as_wrong()
    {
        var store = new InterceptingStore(new InMemoryEventStore());
        using var rig = await RunningAsync(new ActiveRigOptions { Store = store });
        rig.HoldToExit();
        store.ThrowOnVerify = true;

        Assert.Equal(CommandStatus.Accepted, rig.SubmitPin(ActiveRig.Pin).Status);

        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
        Assert.Equal(new PinDialogOverlay(PinDialogError.WrongPin, null, 4), Dialog(rig));
    }

    [Fact]
    public async Task A_PIN_for_an_Event_the_store_no_longer_has_never_ends_the_Event()
    {
        var inner = new InMemoryEventStore();
        using var rig = await RunningAsync(new ActiveRigOptions { Store = inner });
        rig.HoldToExit();
        await inner.MarkEventDeletingAsync(ActiveRig.EventId, TestContext.Current.CancellationToken);

        rig.SubmitPin(ActiveRig.Pin);

        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
        Assert.Equal(PinDialogError.WrongPin, Dialog(rig).Error);
    }

    [Fact]
    public async Task Five_wrong_PINs_lock_the_dialog_for_thirty_seconds_with_a_countdown()
    {
        using var rig = await RunningAsync();
        rig.HoldToExit();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(CommandStatus.Accepted, rig.SubmitPin(WrongPin).Status);
        }

        Assert.Equal(new PinDialogOverlay(PinDialogError.WrongPin, 30, null), Dialog(rig));

        var locked = rig.SubmitPin(ActiveRig.Pin);
        Assert.Equal(CommandResultMessage.Rejected(locked.Id, RejectReason.PinLocked), locked);
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);

        rig.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(29, Dialog(rig).LockedRemainingSeconds);
        rig.Advance(TimeSpan.FromSeconds(14));
        Assert.Equal(15, Dialog(rig).LockedRemainingSeconds);
        rig.Advance(TimeSpan.FromSeconds(14));
        Assert.Equal(1, Dialog(rig).LockedRemainingSeconds);
        Assert.Equal(RejectReason.PinLocked, rig.SubmitPin(ActiveRig.Pin).Reason);

        rig.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(new PinDialogOverlay(null, null, 5), Dialog(rig));
        Assert.Equal(CommandStatus.Accepted, rig.SubmitPin(ActiveRig.Pin).Status);
        Assert.Null(rig.Brain.ActiveEventId);
    }

    [Fact]
    public async Task The_countdown_publishes_once_a_second_while_locked_and_stops_when_the_lockout_ends()
    {
        using var rig = await RunningAsync();
        rig.HoldToExit();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            rig.SubmitPin(WrongPin);
        }

        var before = rig.Guest!.Messages.OfType<GuestPresentationMessage>().Count();
        rig.Advance(TimeSpan.FromSeconds(10));
        var ticks = rig.Guest.Messages.OfType<GuestPresentationMessage>().Count() - before;
        Assert.Equal(10, ticks);

        rig.Advance(TimeSpan.FromSeconds(20));
        var afterEnd = rig.Guest.Messages.OfType<GuestPresentationMessage>().Count();
        rig.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(afterEnd, rig.Guest.Messages.OfType<GuestPresentationMessage>().Count());
        Assert.Equal(new PinDialogOverlay(null, null, 5), Dialog(rig));
    }

    [Fact]
    public async Task Cancelling_the_dialog_keeps_a_running_lockout_running_and_reopening_shows_it()
    {
        using var rig = await RunningAsync();
        rig.HoldToExit();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            rig.SubmitPin(WrongPin);
        }

        var cancel = rig.GuestCommand("CancelExit");
        Assert.Equal(CommandResultMessage.Accepted(cancel.Id), cancel);
        Assert.Null(rig.LatestGuest().Overlays.PinDialog);

        rig.Advance(TimeSpan.FromSeconds(12));
        rig.HoldToExit();
        Assert.Equal(new PinDialogOverlay(PinDialogError.WrongPin, 15, null), Dialog(rig));

        rig.Advance(TimeSpan.FromSeconds(15));
        Assert.Equal(new PinDialogOverlay(null, null, 5), Dialog(rig));
    }

    [Fact]
    public async Task Cancelling_after_a_wrong_PIN_reopens_without_the_error_but_with_the_attempts_left()
    {
        using var rig = await RunningAsync();
        rig.HoldToExit();
        rig.SubmitPin(WrongPin);
        rig.GuestCommand("CancelExit");

        rig.HoldToExit();

        Assert.Equal(new PinDialogOverlay(null, null, 4), Dialog(rig));
    }

    [Fact]
    public async Task A_new_Event_starts_with_a_fresh_lockout()
    {
        using var rig = await RunningAsync();
        rig.HoldToExit();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            rig.SubmitPin(WrongPin);
        }

        rig.OperatorCommand("EndEvent");
        rig.OperatorCommand("ConfirmEndEvent");
        rig.StartAndExpectSuccess();
        rig.HoldToExit();

        Assert.Equal(new PinDialogOverlay(null, null, 5), Dialog(rig));
    }

    [Fact]
    public async Task The_PIN_dialog_works_only_while_it_is_open()
    {
        using var rig = await RunningAsync();

        Assert.Equal(RejectReason.NotAvailable, rig.SubmitPin(ActiveRig.Pin).Reason);
        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
    }

    [Fact]
    public async Task A_second_PIN_while_the_first_is_being_checked_is_not_available_and_a_late_result_after_cancel_is_ignored()
    {
        var store = new InterceptingStore(new InMemoryEventStore());
        using var rig = await RunningAsync(new ActiveRigOptions { Store = store });
        rig.HoldToExit();
        var gate = new TaskCompletionSource();
        store.VerifyGate = gate;

        Assert.Equal(CommandStatus.Accepted, rig.SubmitPin(ActiveRig.Pin).Status);
        Assert.Equal(RejectReason.NotAvailable, rig.SubmitPin(ActiveRig.Pin).Reason);
        Assert.Single(store.VerifiedPins);

        rig.GuestCommand("CancelExit");
        gate.SetResult();
        rig.Pump(() => rig.Executor.PendingCount == 0, maxIterations: 40);
        Thread.Sleep(50);
        rig.Run();

        Assert.Equal(ActiveRig.EventId, rig.Brain.ActiveEventId);
        Assert.Null(rig.LatestGuest().Overlays.PinDialog);
    }

    [Fact]
    public async Task A_PIN_check_that_finishes_after_the_Event_ended_changes_nothing()
    {
        var store = new InterceptingStore(new InMemoryEventStore());
        using var rig = await RunningAsync(new ActiveRigOptions { Store = store });
        rig.HoldToExit();
        var gate = new TaskCompletionSource();
        store.VerifyGate = gate;
        rig.SubmitPin(ActiveRig.Pin);

        rig.OperatorCommand("EndEvent");
        rig.OperatorCommand("ConfirmEndEvent");
        gate.SetResult();
        Thread.Sleep(50);
        rig.Run();

        Assert.Null(rig.Brain.ActiveEventId);
        Assert.Equal(["OpenGuestScreen(fake-guest)", "CloseGuestScreen"], rig.Recorded.Calls);
    }

    [Fact]
    public async Task The_lockout_countdown_stops_when_the_Event_ends()
    {
        using var rig = await RunningAsync();
        rig.HoldToExit();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            rig.SubmitPin(WrongPin);
        }

        rig.OperatorCommand("EndEvent");
        rig.OperatorCommand("ConfirmEndEvent");
        var before = rig.Guest!.Raw.Count;
        rig.Advance(TimeSpan.FromSeconds(40));

        Assert.Equal(before, rig.Guest.Raw.Count);
    }

    // Clock scale

    [Fact]
    public async Task At_a_clock_scale_of_ten_the_thirty_second_lockout_ends_in_three()
    {
        var scaled = (ScaledClock?)null;
        using var rig = await RunningAsync(new ActiveRigOptions
        {
            Hooks = true,
            BrainClock = clock => scaled = new ScaledClock(clock),
        });
        var call = rig.Operator.Test(TestHookNames.SetClockScale, """{"factor":10}""");
        rig.Run();
        Assert.True(rig.Operator.Messages.OfType<TestResultMessage>().Single(message => message.Id == call).Ok);
        Assert.Equal(10, scaled!.Factor);
        Assert.Equal(10, rig.Diagnostics().ClockScale);
        rig.HoldToExit();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            rig.SubmitPin(WrongPin);
        }

        Assert.Equal(30, Dialog(rig).LockedRemainingSeconds);
        rig.Advance(TimeSpan.FromSeconds(2.5));
        Assert.True(Dialog(rig).LockedRemainingSeconds > 0);

        rig.Advance(TimeSpan.FromSeconds(0.6));

        Assert.Equal(new PinDialogOverlay(null, null, 5), Dialog(rig));
        Assert.Equal(CommandStatus.Accepted, rig.SubmitPin(ActiveRig.Pin).Status);
        Assert.Null(rig.Brain.ActiveEventId);
    }

    [Fact]
    public async Task A_scaled_clock_lets_the_hold_complete_sooner()
    {
        using var rig = await RunningAsync(new ActiveRigOptions { Hooks = true, BrainClock = clock => new ScaledClock(clock) });
        rig.Operator.Test(TestHookNames.SetClockScale, """{"factor":10}""");
        rig.Run();
        rig.GuestCommand("BeginExitHold");
        rig.Advance(TimeSpan.FromSeconds(0.3));

        Assert.Equal(CommandStatus.Accepted, rig.GuestCommand("CompleteExitHold").Status);
    }

    // The PIN never leaves a handler

    [Fact]
    public async Task No_presentation_diagnostics_or_log_ever_holds_a_PIN()
    {
        using var rig = await RunningAsync(new ActiveRigOptions { Hooks = true });
        rig.HoldToExit();
        rig.SubmitPin(WrongPin);
        rig.SubmitPin("12a4");
        rig.GuestCommand("CancelExit");
        rig.HoldToExit();
        rig.SubmitPin(ActiveRig.Pin);
        _ = rig.Diagnostics();

        var everything = rig.EverythingSent();

        Assert.DoesNotContain(ActiveRig.Pin, everything, StringComparison.Ordinal);
        Assert.DoesNotContain(WrongPin, everything, StringComparison.Ordinal);
        Assert.DoesNotContain("12a4", everything, StringComparison.Ordinal);
        Assert.NotEmpty(rig.Logger.Entries);
        Assert.All(rig.Logger.Entries, entry =>
        {
            Assert.DoesNotContain(ActiveRig.Pin, entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(WrongPin, entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("12a4", entry.Message, StringComparison.Ordinal);
        });
    }
}
