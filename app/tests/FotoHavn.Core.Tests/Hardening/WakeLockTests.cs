using FotoHavn.Core.Fakes;
using FotoHavn.Core.Hardening;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Protocol;

namespace FotoHavn.Core.Tests.Hardening;

/// <summary>The wake lock's rule: held exactly while an Event is held, released once on every end path (M10).</summary>
public sealed class WakeLockTests
{
    private static string ReasonFor(string eventId = ActiveRig.EventId) => WakeLockRules.ReasonFor(eventId);

    [Fact]
    public void The_reason_names_the_Event_by_its_id()
    {
        Assert.Equal("FOTOHAVN Event event-1 is running", WakeLockRules.ReasonFor("event-1"));
        Assert.Throws<ArgumentException>(() => WakeLockRules.ReasonFor(" "));
    }

    [Fact]
    public void The_no_op_lock_keeps_the_first_reason_while_held()
    {
        var wakeLock = new NoOpWakeLock();
        Assert.Null(wakeLock.Reason);

        wakeLock.Acquire("first");
        wakeLock.Acquire("second");

        Assert.Equal("first", wakeLock.Reason);
        wakeLock.Release();
        Assert.Null(wakeLock.Reason);
        Assert.Throws<ArgumentException>(() => wakeLock.Acquire(" "));
    }

    [Fact]
    public void The_fake_lock_records_every_call_in_order()
    {
        var wakeLock = new FakeWakeLock();

        wakeLock.Release();
        wakeLock.Acquire("first");
        wakeLock.Acquire("second");
        wakeLock.Release();
        wakeLock.Release();

        Assert.False(wakeLock.IsHeld);
        Assert.Null(wakeLock.Reason);
        Assert.Equal(
            [new WakeLockCall(false, null, false), new WakeLockCall(true, "first", false), new WakeLockCall(true, "second", true), new WakeLockCall(false, null, true), new WakeLockCall(false, null, false)],
            wakeLock.Calls);
        Assert.Equal(1, wakeLock.Acquisitions);
        Assert.Equal(1, wakeLock.Releases);
        Assert.Throws<ArgumentException>(() => wakeLock.Acquire(""));
    }

    [Fact]
    public void The_fake_lock_keeps_the_first_reason_while_held()
    {
        var wakeLock = new FakeWakeLock();

        wakeLock.Acquire("first");
        wakeLock.Acquire("second");

        Assert.Equal("first", wakeLock.Reason);
        Assert.True(wakeLock.IsHeld);
    }

    [Fact]
    public async Task The_lock_is_held_from_start_to_the_end_of_the_Event_and_released_once()
    {
        var wakeLock = new FakeWakeLock();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WakeLock = wakeLock });
        Assert.False(rig.Brain.IsEventActive);
        Assert.Empty(wakeLock.Calls);

        rig.StartAndExpectSuccess();

        Assert.True(rig.Brain.IsEventActive);
        Assert.Equal([new WakeLockCall(true, ReasonFor(), false)], wakeLock.Calls);
        var held = rig.Diagnostics().WakeLock;
        Assert.True(held.Held);
        Assert.Equal(ReasonFor(), held.Reason);

        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("EndEvent").Status);
        Assert.True(wakeLock.IsHeld);
        Assert.Equal(CommandStatus.Accepted, rig.OperatorCommand("ConfirmEndEvent").Status);

        Assert.False(rig.Brain.IsEventActive);
        Assert.Equal(1, wakeLock.Acquisitions);
        Assert.Equal(1, wakeLock.Releases);
        var released = rig.Diagnostics().WakeLock;
        Assert.False(released.Held);
        Assert.Null(released.Reason);
    }

    [Fact]
    public async Task The_guest_PIN_exit_releases_the_lock()
    {
        var wakeLock = new FakeWakeLock();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WakeLock = wakeLock });
        rig.StartAndExpectSuccess();

        rig.OpenAndCompleteExitWithPin();
        rig.Pump(() => !rig.Brain.IsEventActive);

        Assert.False(wakeLock.IsHeld);
        Assert.Equal(1, wakeLock.Releases);
    }

    [Fact]
    public async Task A_failed_start_never_leaves_the_lock_held()
    {
        var wakeLock = new FakeWakeLock();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WakeLock = wakeLock, GuestConnected = false });
        var task = rig.BeginStart();
        rig.Pump(() => rig.Recorded.OpenedGuestScreens.Count == 1);
        Assert.True(wakeLock.IsHeld);
        Assert.True(rig.Brain.IsEventActive);

        rig.Advance(Brain.GuestScreenReadyTimeout + TimeSpan.FromSeconds(1));

        Assert.False((await task).Succeeded);
        Assert.False(rig.Brain.IsEventActive);
        Assert.False(wakeLock.IsHeld);
        Assert.Equal(1, wakeLock.Acquisitions);
        Assert.Equal(1, wakeLock.Releases);
    }

    [Fact]
    public async Task Disposing_the_brain_with_an_Event_running_releases_the_lock()
    {
        var wakeLock = new FakeWakeLock();
        var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WakeLock = wakeLock });
        rig.StartAndExpectSuccess();
        Assert.True(wakeLock.IsHeld);

        rig.Dispose();

        Assert.False(wakeLock.IsHeld);
        Assert.False(rig.Brain.IsEventActive);
        rig.Dispose();
        Assert.Equal(1, wakeLock.Releases);
    }

    [Fact]
    public async Task Disposing_an_idle_brain_does_not_touch_the_lock()
    {
        var wakeLock = new FakeWakeLock();
        var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { WakeLock = wakeLock });

        rig.Dispose();

        Assert.Empty(wakeLock.Calls);
    }
}
