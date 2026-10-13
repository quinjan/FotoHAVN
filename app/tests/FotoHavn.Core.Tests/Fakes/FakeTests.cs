using System.Collections.Concurrent;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Fakes;
using static FotoHavn.Core.Tests.Support.TestDisplays;

namespace FotoHavn.Core.Tests.Fakes;

public sealed class FakeTests
{
    [Fact]
    public void Fake_display_adapter_defaults_to_the_headless_pair()
    {
        var adapter = FakeDisplayAdapter.CreateDefault();

        Assert.Equal([FakeDisplayAdapter.DefaultPrimary, FakeDisplayAdapter.DefaultGuest], adapter.GetDisplays());
        Assert.Equal("fake-primary", adapter.PrimaryDisplayId);
        Assert.Equal(new DisplayRect(1920, 0, 1920, 1080), FakeDisplayAdapter.DefaultGuest.Bounds);
        Assert.Equal(FakeDisplayAdapter.DefaultGuest.Bounds, FakeDisplayAdapter.DefaultGuest.WorkArea);
        Assert.Equal(1.0, FakeDisplayAdapter.DefaultGuest.Scale);
        Assert.False(FakeDisplayAdapter.DefaultGuest.IsPrimary);
    }

    [Fact]
    public void Fake_display_adapter_connects_disconnects_and_replaces_displays_raising_changes()
    {
        var adapter = new FakeDisplayAdapter(Display("a", primary: true));
        var changes = 0;
        adapter.DisplaysChanged += (sender, _) =>
        {
            Assert.Same(adapter, sender);
            changes++;
        };

        adapter.Connect(Display("b", x: 1920));
        adapter.Connect(Display("a", width: 2560, primary: true));
        Assert.Equal(["a", "b"], adapter.GetDisplays().Select(display => display.Id));
        Assert.Equal(2560, adapter.GetDisplays()[0].Bounds.Width);

        Assert.True(adapter.Disconnect("a"));
        Assert.False(adapter.Disconnect("a"));
        Assert.Null(adapter.PrimaryDisplayId);
        Assert.Equal(["b"], adapter.GetDisplays().Select(display => display.Id));

        adapter.SetDisplays([Display("c", primary: true)]);
        Assert.Equal("c", adapter.PrimaryDisplayId);
        Assert.Equal(4, changes);
    }

    [Fact]
    public void Fake_display_adapter_snapshots_are_immutable_and_arguments_are_checked()
    {
        var source = new[] { Display("a") };
        var adapter = new FakeDisplayAdapter(source);
        var snapshot = adapter.GetDisplays();

        source[0] = Display("changed");
        adapter.Connect(Display("b"));

        Assert.Equal(["a"], snapshot.Select(display => display.Id));
        Assert.Throws<ArgumentNullException>(() => new FakeDisplayAdapter(null!));
        Assert.Throws<ArgumentNullException>(() => adapter.SetDisplays(null!));
        Assert.Throws<ArgumentNullException>(() => adapter.Connect(null!));
        Assert.Throws<ArgumentNullException>(() => adapter.Disconnect(null!));
    }

    [Fact]
    public void Manual_executor_runs_work_in_order_on_the_caller_including_work_posted_while_running()
    {
        var executor = new ManualBrainExecutor();
        var log = new List<string>();
        executor.Post(() =>
        {
            log.Add($"first {executor.IsCurrentThread}");
            executor.Post(() => log.Add("posted inside"));
        });
        executor.Post(() => log.Add("second"));

        Assert.False(executor.IsCurrentThread);
        Assert.Equal(2, executor.PendingCount);
        Assert.Equal(3, executor.RunPending());
        Assert.Equal(["first True", "second", "posted inside"], log);
        Assert.False(executor.IsCurrentThread);
        Assert.Equal(0, executor.RunPending());
        Assert.Throws<ArgumentNullException>(() => executor.Post(null!));
    }

    [Fact]
    public void Manual_executor_surfaces_or_reports_faulty_work()
    {
        var throwing = new ManualBrainExecutor();
        throwing.Post(() => throw new InvalidOperationException("Bug."));
        Assert.Throws<InvalidOperationException>(() => throwing.RunPending());
        Assert.False(throwing.IsCurrentThread);

        var errors = new List<Exception>();
        var reporting = new ManualBrainExecutor(errors.Add);
        var ranAfter = false;
        reporting.Post(() => throw new InvalidOperationException("Bug."));
        reporting.Post(() => ranAfter = true);
        Assert.Equal(2, reporting.RunPending());
        Assert.True(ranAfter);
        Assert.IsType<InvalidOperationException>(Assert.Single(errors));
    }

    [Fact]
    public void Fake_clock_fires_due_callbacks_in_due_order_at_their_due_time()
    {
        var start = new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(start);
        var log = new List<string>();
        clock.Schedule(TimeSpan.FromSeconds(3), () => log.Add($"three at {(clock.UtcNow - start).TotalSeconds}"));
        clock.Schedule(TimeSpan.FromSeconds(1), () => log.Add($"one at {(clock.UtcNow - start).TotalSeconds}"));
        clock.Schedule(TimeSpan.FromSeconds(1), () => log.Add("one again"));
        var cancelled = clock.Schedule(TimeSpan.FromSeconds(2), () => log.Add("cancelled"));
        clock.Schedule(TimeSpan.FromSeconds(10), () => log.Add("later"));

        cancelled.Dispose();
        clock.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(["one at 1", "one again", "three at 3"], log);
        Assert.Equal(start.AddSeconds(5), clock.UtcNow);
        Assert.Equal(1, clock.PendingCount);
        clock.Advance(TimeSpan.Zero);
        Assert.Equal(3, log.Count);
    }

    [Fact]
    public void Fake_clock_checks_its_arguments_and_has_a_fixed_default_start()
    {
        var clock = new FakeClock();

        Assert.Equal(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), clock.UtcNow);
        Assert.Throws<ArgumentNullException>(() => clock.Schedule(TimeSpan.Zero, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Schedule(TimeSpan.FromSeconds(-1), () => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Recording_host_services_record_calls_in_order()
    {
        var host = new RecordingHostServices("fake-primary");

        host.OpenGuestScreen(FakeDisplayAdapter.DefaultGuest);
        host.CloseGuestScreen();
        host.OpenFolder(@"C:\Data\Events\event-1");
        host.OperatorDisplayId = "fake-guest";

        Assert.Equal(["OpenGuestScreen(fake-guest)", "CloseGuestScreen", @"OpenFolder(C:\Data\Events\event-1)"], host.Calls);
        Assert.Equal([FakeDisplayAdapter.DefaultGuest], host.OpenedGuestScreens);
        Assert.Equal([@"C:\Data\Events\event-1"], host.OpenedFolders);
        Assert.Equal("fake-guest", host.GetOperatorDisplayId());
        Assert.Null(new RecordingHostServices().GetOperatorDisplayId());
        Assert.Throws<ArgumentNullException>(() => host.OpenGuestScreen(null!));
        Assert.Throws<ArgumentNullException>(() => host.OpenFolder(null!));
    }

    [Fact]
    public void Dedicated_thread_executor_runs_work_in_order_on_its_own_thread_and_survives_faulty_work()
    {
        var errors = new ConcurrentQueue<Exception>();
        var log = new ConcurrentQueue<string>();
        var executor = new DedicatedThreadBrainExecutor(errors.Enqueue, "Test brain");
        string? threadName = null;

        executor.Post(() => threadName = Thread.CurrentThread.Name);
        executor.Post(() => throw new InvalidOperationException("Bug."));
        for (var i = 0; i < 100; i++)
        {
            var value = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            executor.Post(() => log.Enqueue(value + (executor.IsCurrentThread ? string.Empty : " off-thread")));
        }

        executor.Dispose();
        executor.Dispose();
        executor.Post(() => log.Enqueue("after dispose"));

        Assert.Equal("Test brain", threadName);
        Assert.Equal(Enumerable.Range(0, 100).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)), log);
        Assert.IsType<InvalidOperationException>(Assert.Single(errors));
        Assert.False(executor.IsCurrentThread);
        Assert.Throws<ArgumentNullException>(() => executor.Post(null!));
        Assert.Throws<ArgumentNullException>(() => new DedicatedThreadBrainExecutor(null!));
    }

    [Fact]
    public void Dedicated_thread_executor_can_be_disposed_from_its_own_work()
    {
        var executor = new DedicatedThreadBrainExecutor(_ => { });
        using var disposed = new ManualResetEventSlim();

        executor.Post(() =>
        {
            executor.Dispose();
            disposed.Set();
        });

        Assert.True(disposed.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task System_clock_tells_the_time_and_fires_and_cancels_callbacks()
    {
        var clock = SystemClock.Instance;
        var before = DateTimeOffset.UtcNow;
        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelledFired = false;

        Assert.InRange(clock.UtcNow, before, DateTimeOffset.UtcNow);
        using var timer = clock.Schedule(TimeSpan.FromMilliseconds(10), fired.SetResult);
        clock.Schedule(TimeSpan.FromSeconds(30), () => cancelledFired = true).Dispose();

        await fired.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.False(cancelledFired);
        Assert.Throws<ArgumentNullException>(() => new SystemClock().Schedule(TimeSpan.Zero, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Schedule(TimeSpan.FromSeconds(-1), () => { }));
    }
}
