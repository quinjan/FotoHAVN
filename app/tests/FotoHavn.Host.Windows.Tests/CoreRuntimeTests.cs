namespace FotoHavn.Host.Windows.Tests;

public sealed class CoreRuntimeTests
{
    [Fact]
    public void Executor_runs_work_in_order_on_one_thread_and_survives_a_throwing_item()
    {
        var errors = new List<Exception>();
        var executor = CoreRuntime.CreateExecutor(errors.Add);
        var ran = new List<int>();
        var threads = new HashSet<int>();
        var onExecutor = new List<bool>();
        using var done = new ManualResetEventSlim();

        for (var i = 0; i < 50; i++)
        {
            var item = i;
            executor.Post(() =>
            {
                ran.Add(item);
                threads.Add(Environment.CurrentManagedThreadId);
                onExecutor.Add(executor.IsCurrentThread);
            });
        }

        executor.Post(() => throw new InvalidOperationException("brain bug"));
        executor.Post(done.Set);

        Assert.True(done.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        executor.Dispose();
        executor.Post(() => ran.Add(-1));

        Assert.Equal(Enumerable.Range(0, 50), ran);
        Assert.Single(threads);
        Assert.All(onExecutor, Assert.True);
        Assert.False(executor.IsCurrentThread);
        Assert.IsType<InvalidOperationException>(Assert.Single(errors));
    }

    [Fact]
    public void Clock_schedules_a_callback()
    {
        var clock = CoreRuntime.CreateClock();
        using var fired = new ManualResetEventSlim();

        using var timer = clock.Schedule(TimeSpan.FromMilliseconds(-5), fired.Set);

        Assert.True(fired.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.True((DateTimeOffset.UtcNow - clock.UtcNow).Duration() < TimeSpan.FromSeconds(5));
    }
}
