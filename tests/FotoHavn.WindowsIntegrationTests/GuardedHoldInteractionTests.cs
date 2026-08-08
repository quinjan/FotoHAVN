using FotoHavn.App.Controls;
using Xunit;

namespace FotoHavn.WindowsIntegrationTests;

public sealed class GuardedHoldInteractionTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Continuous_hold_completes_only_after_one_and_a_half_seconds()
    {
        var interaction = new GuardedHoldInteraction(TimeSpan.FromSeconds(1.5));

        interaction.Begin(Start);

        var early = interaction.Update(Start.AddMilliseconds(1499));
        var completed = interaction.Update(Start.AddMilliseconds(1500));

        Assert.Equal(GuardedHoldState.Holding, early.State);
        Assert.InRange(early.Progress, 0.999, 1);
        Assert.Equal(GuardedHoldState.Completed, completed.State);
        Assert.Equal(1, completed.Progress);
    }

    [Fact]
    public void Cancelling_a_hold_removes_progress_and_requires_a_new_continuous_hold()
    {
        var interaction = new GuardedHoldInteraction(TimeSpan.FromSeconds(1.5));
        interaction.Begin(Start);
        _ = interaction.Update(Start.AddSeconds(1));

        var cancelled = interaction.Cancel();
        var idle = interaction.Update(Start.AddSeconds(2));
        interaction.Begin(Start.AddSeconds(2));
        var restarted = interaction.Update(Start.AddSeconds(2.5));

        Assert.Equal(GuardedHoldState.Cancelled, cancelled.State);
        Assert.Equal(0, cancelled.Progress);
        Assert.Equal(GuardedHoldState.Idle, idle.State);
        Assert.Equal(GuardedHoldState.Holding, restarted.State);
        Assert.InRange(restarted.Progress, 0.333, 0.334);
    }
}
