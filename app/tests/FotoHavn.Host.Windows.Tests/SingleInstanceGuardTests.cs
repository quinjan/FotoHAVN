using FotoHavn.Core.Hardening;
using FotoHavn.Host.Windows.Hardening;

namespace FotoHavn.Host.Windows.Tests;

/// <summary>One FOTOHAVN per Data folder (M10): real named mutexes and events, with a unique name for every test so nothing collides.</summary>
public sealed class SingleInstanceGuardTests
{
    private static SingleInstanceNames UniqueNames()
    {
        var name = $@"Local\FOTOHAVN-test-{Guid.NewGuid():N}";
        return new SingleInstanceNames("test", name, name + SingleInstancePolicy.ActivationSuffix);
    }

    // A second launch is another process; a mutex is recursive on its own thread, so the second guard is made on another thread.
    private static SingleInstanceDecision DecideOnAnotherThread(SingleInstanceNames names)
    {
        var decision = SingleInstanceDecision.Proceed;
        var thread = new Thread(() =>
        {
            using var guard = SingleInstanceGuard.Acquire(names);
            decision = guard.Decision;
        });
        thread.Start();
        thread.Join();
        return decision;
    }

    [Fact]
    public void The_first_launch_proceeds_and_a_second_on_the_same_Data_folder_activates_the_first()
    {
        var names = UniqueNames();

        using var first = SingleInstanceGuard.Acquire(names);

        Assert.Equal(SingleInstanceDecision.Proceed, first.Decision);
        Assert.Equal(names, first.Names);
        Assert.Equal(SingleInstanceDecision.ActivateExisting, DecideOnAnotherThread(names));
    }

    [Fact]
    public void Launches_on_different_Data_folders_never_collide()
    {
        var folderA = Path.Combine(Path.GetTempPath(), "fotohavn-a-" + Guid.NewGuid().ToString("N"));
        var folderB = Path.Combine(Path.GetTempPath(), "fotohavn-b-" + Guid.NewGuid().ToString("N"));
        var namesA = SingleInstancePolicy.NamesFor(folderA);
        var namesB = SingleInstancePolicy.NamesFor(folderB);

        using var first = SingleInstanceGuard.Acquire(namesA);

        Assert.NotEqual(namesA.MutexName, namesB.MutexName);
        Assert.Equal(SingleInstanceDecision.Proceed, first.Decision);
        Assert.Equal(SingleInstanceDecision.Proceed, DecideOnAnotherThread(namesB));
        Assert.Equal(SingleInstanceDecision.ActivateExisting, DecideOnAnotherThread(namesA));
    }

    [Fact]
    public void Two_spellings_of_one_Data_folder_collide()
    {
        var folder = Path.Combine(Path.GetTempPath(), "fotohavn-" + Guid.NewGuid().ToString("N"));
        var names = SingleInstancePolicy.NamesFor(folder);
        var spelledOtherwise = SingleInstancePolicy.NamesFor(folder.ToUpperInvariant().Replace('\\', '/') + "/");

        using var first = SingleInstanceGuard.Acquire(names);

        Assert.Equal(SingleInstanceDecision.ActivateExisting, DecideOnAnotherThread(spelledOtherwise));
    }

    [Fact]
    public void A_second_launch_signals_the_first_to_bring_its_window_forward()
    {
        var names = UniqueNames();
        using var first = SingleInstanceGuard.Acquire(names);
        using var activated = new SemaphoreSlim(0);
        first.ListenForActivation(() => activated.Release());

        Assert.True(SingleInstanceGuard.SignalExisting(names, TimeSpan.FromSeconds(2)));
        Assert.True(activated.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.True(SingleInstanceGuard.SignalExisting(names, TimeSpan.FromSeconds(2)));
        Assert.True(activated.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_signal_sent_before_the_first_launch_listens_is_not_lost()
    {
        var names = UniqueNames();
        using var first = SingleInstanceGuard.Acquire(names);
        Assert.True(SingleInstanceGuard.SignalExisting(names, TimeSpan.FromSeconds(2)));
        using var activated = new SemaphoreSlim(0);

        first.ListenForActivation(() => activated.Release());

        Assert.True(activated.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Signalling_nobody_gives_up_after_its_patience()
    {
        var started = DateTime.UtcNow;

        Assert.False(SingleInstanceGuard.SignalExisting(UniqueNames(), TimeSpan.FromMilliseconds(150)));

        Assert.True(DateTime.UtcNow - started >= TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void Only_the_owner_listens_and_listening_twice_does_nothing_more()
    {
        var names = UniqueNames();
        using var first = SingleInstanceGuard.Acquire(names);
        var calls = 0;
        first.ListenForActivation(() => Interlocked.Increment(ref calls));
        first.ListenForActivation(() => Interlocked.Add(ref calls, 100));
        Assert.Throws<ArgumentNullException>(() => first.ListenForActivation(null!));

        SingleInstanceGuard? contender = null;
        var thread = new Thread(() =>
        {
            contender = SingleInstanceGuard.Acquire(names);
            contender.ListenForActivation(() => Interlocked.Add(ref calls, 1000));
        });
        thread.Start();
        thread.Join();
        Assert.True(SingleInstanceGuard.SignalExisting(names, TimeSpan.FromSeconds(2)));

        SpinWait.SpinUntil(() => Volatile.Read(ref calls) >= 1, TimeSpan.FromSeconds(5));
        Thread.Sleep(100);
        Assert.Equal(1, Volatile.Read(ref calls));
        contender!.Dispose();
    }

    [Fact]
    public void When_the_owner_closes_the_next_launch_proceeds()
    {
        var names = UniqueNames();
        var first = SingleInstanceGuard.Acquire(names);
        Assert.Equal(SingleInstanceDecision.ActivateExisting, DecideOnAnotherThread(names));

        first.Dispose();
        first.Dispose();

        Assert.Equal(SingleInstanceDecision.Proceed, DecideOnAnotherThread(names));
    }

    [Fact]
    public void A_mutex_abandoned_by_a_crashed_owner_counts_as_owned()
    {
        var names = UniqueNames();
        SingleInstanceGuard? crashed = null;
        var thread = new Thread(() => crashed = SingleInstanceGuard.Acquire(names));
        thread.Start();
        thread.Join();

        using var next = SingleInstanceGuard.Acquire(names);

        Assert.Equal(SingleInstanceDecision.Proceed, next.Decision);
        GC.KeepAlive(crashed);
    }

    [Fact]
    public void A_missing_name_set_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => SingleInstanceGuard.Acquire(null!));
        Assert.Throws<ArgumentNullException>(() => SingleInstanceGuard.SignalExisting(null!, TimeSpan.Zero));
    }
}
