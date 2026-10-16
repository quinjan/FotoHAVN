namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

/// <summary>
/// A settle ends on a few milliseconds of quiet, which a loaded machine can show while work is still queued on the thread pool. A test that holds a
/// call at a gate keeps the brain's own count of off-executor work up, so it waits on what it expects with <see cref="GuestCycleRig.SettleUntil"/>,
/// in real time and without moving the clock.
/// </summary>
public sealed class GuestCycleRigSettleTests
{
    [Fact]
    public async Task SettleUntil_waits_for_the_Look_thumbnails_made_beside_a_composition_held_at_its_gate()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.Compositor.StripGate = new TaskCompletionSource();
        var before = rig.Clock.UtcNow;

        rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeAccepted();

        Assert.True(rig.SettleUntil(() => rig.Filters.Looks.All(look => look.ThumbnailKey is not null), TimeSpan.FromSeconds(30)));
        Assert.True(rig.Filters.Composing);
        Assert.Equal(before, rig.Clock.UtcNow);
    }

    [Fact]
    public async Task SettleUntil_waits_for_the_work_a_released_gate_lets_go()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var gate = new TaskCompletionSource();
        rig.Compositor.StripGate = gate;
        rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeAccepted();

        gate.SetResult();

        Assert.True(rig.SettleUntil(() => !rig.Filters.Composing, TimeSpan.FromSeconds(30)));
        Assert.Equal(0, rig.Brain.OffExecutorWork);
    }

    [Fact]
    public async Task SettleUntil_gives_up_when_the_budget_is_spent()
    {
        using var rig = await GuestCycleRig.CreateAsync();

        Assert.False(rig.SettleUntil(() => false, TimeSpan.FromMilliseconds(50)));
    }
}
