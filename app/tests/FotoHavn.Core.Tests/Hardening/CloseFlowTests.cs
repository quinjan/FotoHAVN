using FotoHavn.Core.Events.Active;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Hardening;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Core.Tests.GuestCycles.Orchestration;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core.Tests.Hardening;

/// <summary>Closing FOTOHAVN (M10, contract section 7): every row of the close-flow table, against the brain with a fake wake lock and a recording app lifetime.</summary>
public sealed class CloseFlowTests
{
    private static async Task<(ActiveRig Rig, FakeWakeLock WakeLock, RecordingLifetime Lifetime)> RunningAsync()
    {
        var wakeLock = new FakeWakeLock();
        var lifetime = new RecordingLifetime();
        var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WakeLock = wakeLock, AppLifetime = lifetime });
        rig.StartAndExpectSuccess();
        lifetime.Brain = rig.Brain;
        return (rig, wakeLock, lifetime);
    }

    private static async Task<CloseRequestOutcome> CloseAsync(ActiveRig rig, CloseSource source)
    {
        var request = rig.Brain.RequestClose(source);
        rig.Pump(() => request.IsCompleted);
        return await request;
    }

    private static void WaitForShutdown(ActiveRig rig, RecordingLifetime lifetime) => rig.Pump(() => lifetime.ShutdownRequests > 0);

    [Fact]
    public async Task The_Operator_window_closing_with_no_Event_closes_the_app()
    {
        var lifetime = new RecordingLifetime();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { AppLifetime = lifetime });

        Assert.Equal(CloseRequestOutcome.CloseNow, await CloseAsync(rig, CloseSource.OperatorWindow));

        Assert.Equal(1, lifetime.ShutdownRequests);
        Assert.Null(rig.LatestOperator().Overlays.Confirmation);
    }

    [Fact]
    public async Task The_Operator_window_closing_during_an_Event_asks_first_and_changes_nothing()
    {
        var (rig, wakeLock, lifetime) = await RunningAsync();
        using (rig)
        {
            Assert.Equal(CloseRequestOutcome.AskToEndEvent, await CloseAsync(rig, CloseSource.OperatorWindow));

            Assert.Equal(ConfirmationKind.CloseApp, rig.LatestOperator().Overlays.Confirmation?.Kind);
            Assert.Equal(0, lifetime.ShutdownRequests);
            Assert.True(rig.Brain.IsEventActive);
            Assert.True(wakeLock.IsHeld);
            Assert.NotNull(rig.LatestOperator().RunningEvent);
        }
    }

    [Fact]
    public async Task Asking_twice_keeps_the_one_confirmation()
    {
        var (rig, _, lifetime) = await RunningAsync();
        using (rig)
        {
            await CloseAsync(rig, CloseSource.OperatorWindow);
            var revision = rig.Operator.Raw.Count;

            Assert.Equal(CloseRequestOutcome.AskToEndEvent, await CloseAsync(rig, CloseSource.OperatorWindow));

            Assert.Equal(revision, rig.Operator.Raw.Count);
            Assert.Equal(ConfirmationKind.CloseApp, rig.LatestOperator().Overlays.Confirmation?.Kind);
            Assert.Equal(0, lifetime.ShutdownRequests);
        }
    }

    [Fact]
    public async Task The_close_dialog_replaces_the_END_EVENT_dialog()
    {
        var (rig, _, _) = await RunningAsync();
        using (rig)
        {
            rig.OperatorCommand("EndEvent");
            Assert.Equal(ConfirmationKind.EndEvent, rig.LatestOperator().Overlays.Confirmation?.Kind);

            await CloseAsync(rig, CloseSource.OperatorWindow);

            Assert.Equal(ConfirmationKind.CloseApp, rig.LatestOperator().Overlays.Confirmation?.Kind);
            Assert.Equal(RejectReason.NotAvailable, rig.OperatorCommand("ConfirmEndEvent").Reason);
        }
    }

    [Fact]
    public async Task Confirming_ends_the_Event_cleanly_and_then_closes_the_app()
    {
        var (rig, wakeLock, lifetime) = await RunningAsync();
        using (rig)
        {
            await CloseAsync(rig, CloseSource.OperatorWindow);

            var answer = rig.OperatorCommand("ConfirmCloseApp");

            Assert.Equal(CommandStatus.Accepted, answer.Status);
            Assert.Equal(1, lifetime.ShutdownRequests);
            Assert.False(lifetime.EventActiveAtShutdown);
            Assert.False(rig.Brain.IsEventActive);
            Assert.Null(rig.LatestOperator().RunningEvent);
            Assert.Null(rig.LatestOperator().Overlays.Confirmation);
            Assert.Equal(wakeLock.Acquisitions, wakeLock.Releases);
            Assert.False(wakeLock.IsHeld);
            Assert.Equal("CloseGuestScreen", rig.Recorded.Calls[^1]);
        }
    }

    [Fact]
    public async Task Cancelling_closes_the_dialog_and_the_Event_keeps_running()
    {
        var (rig, wakeLock, lifetime) = await RunningAsync();
        using (rig)
        {
            await CloseAsync(rig, CloseSource.OperatorWindow);

            Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("CancelCloseApp").Status);

            Assert.Null(rig.LatestOperator().Overlays.Confirmation);
            Assert.True(rig.Brain.IsEventActive);
            Assert.True(wakeLock.IsHeld);
            Assert.Equal(0, lifetime.ShutdownRequests);
            Assert.Equal(RejectReason.NotAvailable, rig.OperatorCommand("CancelCloseApp").Reason);
            Assert.Equal(RejectReason.NotAvailable, rig.OperatorCommand("ConfirmCloseApp").Reason);
        }
    }

    [Fact]
    public async Task Confirming_is_not_available_when_another_dialog_is_open_or_no_Event_runs()
    {
        var (rig, wakeLock, lifetime) = await RunningAsync();
        using (rig)
        {
            rig.OperatorCommand("EndEvent");

            Assert.Equal(RejectReason.NotAvailable, rig.OperatorCommand("ConfirmCloseApp").Reason);
            Assert.True(wakeLock.IsHeld);
            Assert.Equal(0, lifetime.ShutdownRequests);
        }

        using var idle = await ActiveRig.CreateAsync();
        idle.Brain.SetConfirmation(new ConfirmationOverlay(ConfirmationKind.CloseApp, null, null, null));
        Assert.Equal(RejectReason.NotAvailable, idle.OperatorCommand("ConfirmEndEvent").Reason);
    }

    [Fact]
    public async Task Confirming_the_close_with_no_Event_held_closes_the_app_once()
    {
        var lifetime = new RecordingLifetime();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { AppLifetime = lifetime });
        rig.Brain.SetConfirmation(new ConfirmationOverlay(ConfirmationKind.CloseApp, null, null, null));

        var answer = rig.OperatorCommand("ConfirmCloseApp");
        WaitForShutdown(rig, lifetime);
        rig.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(CommandStatus.Accepted, answer.Status);
        Assert.Equal(1, lifetime.ShutdownRequests);
        Assert.Null(rig.LatestOperator().Overlays.Confirmation);
    }

    [Fact]
    public async Task A_close_confirmation_does_not_survive_a_start_that_fails_after_the_Event_was_held()
    {
        var wakeLock = new FakeWakeLock();
        var lifetime = new RecordingLifetime();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WakeLock = wakeLock, AppLifetime = lifetime, GuestConnected = false });
        var start = rig.BeginStart();
        rig.Pump(() => rig.Recorded.OpenedGuestScreens.Count == 1);
        Assert.Equal(CloseRequestOutcome.AskToEndEvent, await CloseAsync(rig, CloseSource.OperatorWindow));
        Assert.Equal(ConfirmationKind.CloseApp, rig.LatestOperator().Overlays.Confirmation?.Kind);

        rig.Advance(Brain.GuestScreenReadyTimeout + TimeSpan.FromSeconds(1));

        Assert.Equal(StartFailureCode.GuestScreenNotReady, (await start).Failure);
        Assert.False(wakeLock.IsHeld);
        Assert.Null(rig.LatestOperator().Overlays.Confirmation);
        Assert.Equal(RejectReason.NotAvailable, rig.OperatorCommand("ConfirmCloseApp").Reason);
        Assert.Equal(0, lifetime.ShutdownRequests);
    }

    [Fact]
    public async Task Confirming_the_close_waits_for_the_store_to_end_the_Guest_Cycle_before_shutting_down()
    {
        var lifetime = new RecordingLifetime();
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions { AppLifetime = lifetime });
        rig.Begin();
        rig.ToPhotographs();
        rig.TakeAll();
        rig.Store.EndGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = rig.Brain.RequestClose(CloseSource.OperatorWindow);
        Assert.True(rig.SettleUntil(() => request.IsCompleted, TimeSpan.FromSeconds(5)));

        rig.Operator("ConfirmCloseApp").ShouldBeAccepted();
        Assert.True(rig.SettleUntil(() => rig.Store.Ended.Count == 1, TimeSpan.FromSeconds(5)));
        Thread.Sleep(100);
        rig.Settle();

        Assert.Equal(0, lifetime.ShutdownRequests);
        rig.Store.EndGate.SetResult();
        Assert.True(rig.SettleUntil(() => lifetime.ShutdownRequests == 1, TimeSpan.FromSeconds(10)));

        var detail = await rig.OnlyCycleAsync();
        Assert.Equal(GuestCycleEndReason.OperatorExit, detail.Cycle.EndReason);
        Assert.Equal(1, lifetime.ShutdownRequests);
    }

    [Fact]
    public async Task Windows_ending_the_session_completes_only_after_the_store_ended_the_Guest_Cycle()
    {
        var lifetime = new RecordingLifetime();
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions { AppLifetime = lifetime });
        rig.Begin();
        rig.ToPhotographs();
        rig.TakeAll();
        rig.Store.EndGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        var request = rig.Brain.RequestClose(CloseSource.SessionEnd);
        Assert.True(rig.SettleUntil(() => rig.Store.Ended.Count == 1, TimeSpan.FromSeconds(5)));
        Thread.Sleep(100);
        rig.Settle();

        Assert.False(request.IsCompleted);
        Assert.Equal(0, lifetime.ShutdownRequests);
        rig.Store.EndGate.SetResult();
        Assert.True(rig.SettleUntil(() => request.IsCompleted, TimeSpan.FromSeconds(10)));

        Assert.Equal(CloseRequestOutcome.EndEventAndClose, await request);
        Assert.Equal(1, lifetime.ShutdownRequests);
        Assert.Equal(GuestCycleEndReason.OperatorExit, (await rig.OnlyCycleAsync()).Cycle.EndReason);
    }

    [Fact]
    public async Task A_store_write_that_never_finishes_does_not_hold_the_shutdown_beyond_the_bound()
    {
        var lifetime = new RecordingLifetime();
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions { AppLifetime = lifetime });
        rig.Begin();
        rig.ToPhotographs();
        rig.TakeAll();
        rig.Store.EndGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Operator("EndEvent").ShouldBeAccepted();
        rig.Operator("ConfirmEndEvent").ShouldBeAccepted();
        Assert.True(rig.SettleUntil(() => rig.Store.Ended.Count == 1, TimeSpan.FromSeconds(5)));

        var settled = rig.Brain.WhenStoreWritesSettledAsync(TimeSpan.FromMilliseconds(50));
        Assert.True(rig.SettleUntil(() => settled.IsCompleted, TimeSpan.FromSeconds(10)));

        Assert.True(settled.IsCompletedSuccessfully);
        Assert.Contains(rig.Active.Logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.StartsWith("Closing without waiting further", StringComparison.Ordinal));
        Assert.True(rig.Brain.OffExecutorWork > 0);
        rig.Store.EndGate.SetResult();
        Assert.True(rig.SettleUntil(() => rig.Brain.OffExecutorWork == 0, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task Waiting_for_store_writes_completes_at_once_when_none_are_pending_or_the_brain_is_disposed()
    {
        var rig = await ActiveRig.CreateAsync();

        var idle = rig.Brain.WhenStoreWritesSettledAsync(TimeSpan.FromSeconds(10));
        rig.Run();
        await idle.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(idle.IsCompletedSuccessfully);

        rig.Dispose();
        Assert.True(rig.Brain.WhenStoreWritesSettledAsync(TimeSpan.FromSeconds(10)).IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Waiting_for_store_writes_refuses_a_bound_that_is_not_positive()
    {
        using var rig = await ActiveRig.CreateAsync();

        var thrown = Record.Exception((Action)(() => _ = rig.Brain.WhenStoreWritesSettledAsync(TimeSpan.Zero)));

        Assert.IsType<ArgumentOutOfRangeException>(thrown);
    }

    [Fact]
    public async Task Windows_ending_the_session_ends_the_Event_and_closes_without_asking()
    {
        var (rig, wakeLock, lifetime) = await RunningAsync();
        using (rig)
        {
            var outcome = await CloseAsync(rig, CloseSource.SessionEnd);

            Assert.Equal(CloseRequestOutcome.EndEventAndClose, outcome);
            Assert.Equal(1, lifetime.ShutdownRequests);
            Assert.False(lifetime.EventActiveAtShutdown);
            Assert.False(wakeLock.IsHeld);
            Assert.Equal(1, wakeLock.Releases);
            Assert.Null(rig.LatestOperator().Overlays.Confirmation);
            Assert.Null(rig.LatestOperator().RunningEvent);
        }
    }

    [Fact]
    public async Task Windows_ending_the_session_with_no_Event_just_closes()
    {
        var lifetime = new RecordingLifetime();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { AppLifetime = lifetime });

        Assert.Equal(CloseRequestOutcome.CloseNow, await CloseAsync(rig, CloseSource.SessionEnd));

        Assert.Equal(1, lifetime.ShutdownRequests);
    }

    [Fact]
    public async Task Windows_ending_the_session_while_the_dialog_is_open_ends_the_Event_and_closes()
    {
        var (rig, wakeLock, lifetime) = await RunningAsync();
        using (rig)
        {
            await CloseAsync(rig, CloseSource.OperatorWindow);

            Assert.Equal(CloseRequestOutcome.EndEventAndClose, await CloseAsync(rig, CloseSource.SessionEnd));

            Assert.Equal(1, lifetime.ShutdownRequests);
            Assert.Null(rig.LatestOperator().Overlays.Confirmation);
            Assert.Equal(1, wakeLock.Releases);
        }
    }

    [Fact]
    public async Task The_Guest_window_is_refused_during_an_Event_and_the_brain_does_nothing_else()
    {
        var (rig, wakeLock, lifetime) = await RunningAsync();
        using (rig)
        {
            var request = rig.Brain.RequestClose(CloseSource.GuestWindow);

            Assert.True(request.IsCompleted);
            Assert.Equal(CloseRequestOutcome.Refuse, await request);
            rig.Run();
            Assert.Equal(0, lifetime.ShutdownRequests);
            Assert.Null(rig.LatestOperator().Overlays.Confirmation);
            Assert.True(wakeLock.IsHeld);
        }
    }

    [Fact]
    public async Task The_Guest_window_may_close_with_no_Event_without_closing_the_app()
    {
        var lifetime = new RecordingLifetime();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { AppLifetime = lifetime });

        Assert.Equal(CloseRequestOutcome.CloseNow, await rig.Brain.RequestClose(CloseSource.GuestWindow));
        rig.Run();

        Assert.Equal(0, lifetime.ShutdownRequests);
    }

    [Fact]
    public async Task A_close_during_the_start_waiting_for_the_Guest_Screen_ends_the_start_and_releases_everything()
    {
        var wakeLock = new FakeWakeLock();
        var lifetime = new RecordingLifetime();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WakeLock = wakeLock, AppLifetime = lifetime, GuestConnected = false });
        var start = rig.BeginStart();
        rig.Pump(() => rig.Recorded.OpenedGuestScreens.Count == 1);
        Assert.True(wakeLock.IsHeld);

        Assert.Equal(CloseRequestOutcome.AskToEndEvent, await CloseAsync(rig, CloseSource.OperatorWindow));
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("ConfirmCloseApp").Status);

        Assert.Equal(StartEventResult.Ended, await start);
        Assert.False(wakeLock.IsHeld);
        Assert.Equal(wakeLock.Acquisitions, wakeLock.Releases);
        Assert.Equal(1, lifetime.ShutdownRequests);
    }

    [Fact]
    public async Task Ending_through_a_close_ends_an_unfinished_Guest_Cycle_as_operator_exit()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();
        rig.TakeAll();

        var request = rig.Brain.RequestClose(CloseSource.SessionEnd);
        Assert.True(rig.SettleUntil(() => request.IsCompleted, TimeSpan.FromSeconds(10)));

        Assert.Equal(CloseRequestOutcome.EndEventAndClose, await request);
        var ended = Assert.Single(rig.Store.Ended);
        Assert.Equal(GuestCycleEndReason.OperatorExit, ended.Reason);
        var detail = await rig.OnlyCycleAsync();
        Assert.Equal(GuestCycleEndReason.OperatorExit, detail.Cycle.EndReason);
        Assert.Equal(4, detail.Captures.Count);
    }

    [Fact]
    public async Task Confirming_the_close_keeps_the_Captures_of_the_unfinished_Guest_Cycle()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();
        rig.TakeAll();

        var request = rig.Brain.RequestClose(CloseSource.OperatorWindow);
        rig.Settle();
        Assert.Equal(CloseRequestOutcome.AskToEndEvent, await request);
        rig.Operator("ConfirmCloseApp").ShouldBeAccepted();
        rig.Settle();

        var detail = await rig.OnlyCycleAsync();
        Assert.Equal(GuestCycleEndReason.OperatorExit, detail.Cycle.EndReason);
        Assert.Equal(4, detail.Captures.Count);
        Assert.False(rig.Active.WakeLock.IsHeld);
    }

    [Fact]
    public async Task A_shutdown_that_throws_faults_the_request_and_is_reported_to_the_executor()
    {
        var lifetime = new ThrowingLifetime();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { AppLifetime = lifetime });

        var request = rig.Brain.RequestClose(CloseSource.OperatorWindow);
        var thrown = Record.Exception(rig.Run);

        Assert.True(request.IsFaulted);
        Assert.IsType<InvalidOperationException>(await Assert.ThrowsAsync<InvalidOperationException>(() => request));
        Assert.NotNull(thrown);
    }

    [Fact]
    public async Task The_wake_lock_is_held_for_exactly_the_Event_on_every_close_path()
    {
        foreach (var source in new[] { CloseSource.SessionEnd, CloseSource.OperatorWindow })
        {
            var (rig, wakeLock, _) = await RunningAsync();
            using (rig)
            {
                Assert.True(wakeLock.IsHeld);
                await CloseAsync(rig, source);
                if (source == CloseSource.OperatorWindow)
                {
                    Assert.True(wakeLock.IsHeld);
                    rig.OperatorCommand("ConfirmCloseApp");
                }

                Assert.False(wakeLock.IsHeld);
                Assert.Equal(1, wakeLock.Acquisitions);
                Assert.Equal(1, wakeLock.Releases);
            }
        }
    }

    [Fact]
    public async Task Exiting_with_the_Event_still_held_releases_the_wake_lock_when_the_brain_is_disposed()
    {
        var (rig, wakeLock, _) = await RunningAsync();

        rig.Dispose();

        Assert.False(wakeLock.IsHeld);
        Assert.Equal(wakeLock.Acquisitions, wakeLock.Releases);
    }

    private sealed class RecordingLifetime : IAppLifetime
    {
        private int _shutdownRequests;

        public Brain? Brain { get; set; }

        public int ShutdownRequests => Volatile.Read(ref _shutdownRequests);

        public bool EventActiveAtShutdown { get; private set; } = true;

        public void Shutdown()
        {
            EventActiveAtShutdown = Brain?.IsEventActive ?? false;
            Interlocked.Increment(ref _shutdownRequests);
        }
    }

    private sealed class ThrowingLifetime : IAppLifetime
    {
        public void Shutdown() => throw new InvalidOperationException("The host could not close.");
    }
}
