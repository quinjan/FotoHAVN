using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Core.Tests.GuestCycles.Orchestration;

namespace FotoHavn.Core.Tests.Hardening;

/// <summary>The ids the brain's log lines carry (M10): the held Event, and the running Guest Cycle while there is one.</summary>
public sealed class LogScopeTests
{
    [Fact]
    public async Task With_no_Event_no_id_is_carried()
    {
        using var rig = await ActiveRig.CreateAsync();

        Assert.Equal((null, null), rig.Brain.LogScopeIds());
    }

    [Fact]
    public async Task A_running_Event_is_carried_until_it_ends()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();

        Assert.Equal((ActiveRig.EventId, null), rig.Brain.LogScopeIds());

        rig.OperatorCommand("EndEvent");
        rig.OperatorCommand("ConfirmEndEvent");

        Assert.Equal((null, null), rig.Brain.LogScopeIds());
    }

    [Fact]
    public async Task A_running_Guest_Cycle_is_carried_with_its_Event_and_dropped_when_the_cycle_ends()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();

        var (eventId, cycleId) = rig.Brain.LogScopeIds();

        Assert.Equal(ActiveRig.EventId, eventId);
        Assert.Equal(rig.CycleDiagnostics().Id, cycleId);

        rig.Operator("EndEvent").ShouldBeAccepted();
        rig.Operator("ConfirmEndEvent").ShouldBeAccepted();
        Assert.Equal((null, null), rig.Brain.LogScopeIds());
    }
}
