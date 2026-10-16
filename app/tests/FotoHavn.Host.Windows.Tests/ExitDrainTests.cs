using FotoHavn.Host.Windows.Hardening;

namespace FotoHavn.Host.Windows.Tests;

/// <summary>The exit's wait for the brain's pending store writes: bounded, and a failure or a stuck write never blocks the exit.</summary>
public sealed class ExitDrainTests
{
    [Fact]
    public void Nothing_to_wait_for_finishes_at_once()
    {
        Assert.True(ExitDrain.Wait(null, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task A_wait_that_finishes_returns_true_only_after_it_finished()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = Task.Run(() => ExitDrain.Wait(() => gate.Task, TimeSpan.FromSeconds(30)), TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.False(waiting.IsCompleted);
        gate.SetResult();

        Assert.True(await waiting.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_wait_that_never_finishes_gives_up_after_the_bound_and_a_margin()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var finished = ExitDrain.Wait(() => new TaskCompletionSource().Task, TimeSpan.FromMilliseconds(100));

        Assert.False(finished);
        Assert.InRange(clock.Elapsed, TimeSpan.FromMilliseconds(90), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void A_wait_that_fails_returns_false_instead_of_throwing()
    {
        Assert.False(ExitDrain.Wait(() => Task.FromException(new InvalidOperationException("The brain failed.")), TimeSpan.FromSeconds(10)));
    }
}
