using System.Collections.Concurrent;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;

namespace FotoHavn.Core.Tests.Brains;

public sealed class ThreadingTests
{
    [Fact]
    public void Nothing_runs_until_the_executor_runs_it()
    {
        using var harness = new BrainHarness();
        var client = new TestClient(Role.Operator);
        client.Connection = harness.Brain.Connect(Role.Operator, client.Receive);

        client.Hello();
        harness.Brain.ShowFatal("Stopped.");

        Assert.Empty(client.Raw);
        Assert.Equal(3, harness.Executor.PendingCount);
        harness.Run();
        Assert.IsType<OperatorWelcomeMessage>(client.Messages[0]);
        Assert.IsType<OperatorPresentationMessage>(client.Messages[1]);
    }

    [Fact]
    public void Work_runs_in_the_order_it_was_posted()
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(Role.Operator);

        var early = client.Command("OpenNewEvent");
        client.Hello();
        var late = client.Command("OpenNewEvent");
        harness.Run();

        Assert.Equal(CommandResultMessage.Rejected(early, RejectReason.ProtocolMismatch), client.Messages[0]);
        Assert.IsType<OperatorWelcomeMessage>(client.Messages[1]);
        Assert.Equal(CommandResultMessage.Rejected(late, RejectReason.NotAvailable), client.Messages[2]);
    }

    [Fact]
    public void Messages_from_many_threads_are_handled_one_at_a_time_on_the_executor_thread()
    {
        var errors = new ConcurrentQueue<Exception>();
        using var executor = new DedicatedThreadBrainExecutor(errors.Enqueue);
        var displays = new ExecutorCheckingDisplays(executor, FakeDisplayAdapter.DefaultPrimary);
        var host = new RecordingHostServices();
        using var brain = new Brain(new BrainDependencies(executor, new FakeClock(), displays, host));

        const int Clients = 4;
        const int CommandsPerClient = 200;
        var received = Enumerable.Range(0, Clients).Select(_ => new ConcurrentQueue<string>()).ToArray();
        var offThread = 0;
        var inside = 0;
        var overlapped = 0;
        var connections = Enumerable.Range(0, Clients)
            .Select(i => brain.Connect(i % 2 == 0 ? Role.Operator : Role.Guest, json =>
            {
                if (!executor.IsCurrentThread)
                {
                    Interlocked.Increment(ref offThread);
                }

                if (Interlocked.Increment(ref inside) != 1)
                {
                    Interlocked.Increment(ref overlapped);
                }

                received[i].Enqueue(json);
                Interlocked.Decrement(ref inside);
            }))
            .ToArray();

        var ids = new string[Clients][];
        Parallel.For(0, Clients, i =>
        {
            connections[i].Receive("""{"type":"hello","protocolVersion":1}""");
            ids[i] = [.. Enumerable.Range(0, CommandsPerClient).Select(_ => Guid.NewGuid().ToString())];
            foreach (var id in ids[i])
            {
                connections[i].Receive($$$"""{"type":"command","id":"{{{id}}}","name":"OpenNewEvent","payload":{}}""");
            }
        });

        using var done = new ManualResetEventSlim();
        executor.Post(done.Set);
        Assert.True(done.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        Assert.Empty(errors);
        Assert.Equal(0, offThread);
        Assert.Equal(0, overlapped);
        Assert.Equal(0, displays.OffThreadReads);
        for (var i = 0; i < Clients; i++)
        {
            var messages = received[i].Select(ServerMessageReader.Read).ToList();
            Assert.IsAssignableFrom<IRoleMessage>(messages[0]);
            Assert.Equal(ids[i], messages.Skip(1).Cast<CommandResultMessage>().Select(result => result.Id));
        }
    }

    [Fact]
    public void A_send_that_throws_is_logged_and_the_other_connections_are_still_served()
    {
        using var harness = new BrainHarness();
        var broken = harness.Brain.Connect(Role.Operator, _ => throw new InvalidOperationException("Socket closed."));
        var healthy = harness.Handshake(Role.Operator);

        broken.Receive("""{"type":"hello","protocolVersion":1}""");
        harness.Brain.ShowFatal("Stopped.");
        harness.Run();

        Assert.IsType<OperatorPresentationMessage>(healthy.Single());
        Assert.Equal(2, harness.Logger.Entries.Count(entry => entry.Exception is InvalidOperationException));
    }

    [Fact]
    public void A_host_hook_task_completes_back_on_the_executor()
    {
        var errors = new ConcurrentQueue<Exception>();
        using var executor = new DedicatedThreadBrainExecutor(errors.Enqueue);
        var hooks = new FakeHostHookPort();
        var windows = new TaskCompletionSource<IReadOnlyList<Protocol.Testing.TestWindow>>(TaskCreationOptions.RunContinuationsAsynchronously);
        hooks.Windows = () => windows.Task;
        using var brain = new Brain(new BrainDependencies(executor, new FakeClock(), FakeDisplayAdapter.CreateDefault(), new RecordingHostServices()), hooksEnabled: true, hooks);
        var sentOnExecutor = new ConcurrentQueue<bool>();
        using var answered = new ManualResetEventSlim();
        var connection = brain.Connect(Role.Operator, _ =>
        {
            sentOnExecutor.Enqueue(executor.IsCurrentThread);
            answered.Set();
        });

        connection.Receive($$"""{"type":"test","id":"{{Guid.NewGuid()}}","name":"test.getDiagnostics"}""");
        Thread.Sleep(50);
        Assert.False(answered.IsSet);
        windows.SetResult([]);

        Assert.True(answered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal([true], sentOnExecutor);
        Assert.Empty(errors);
    }

    /// <summary>A display adapter that counts reads made off the brain's executor thread.</summary>
    private sealed class ExecutorCheckingDisplays(IBrainExecutor executor, params DisplayInfo[] displays) : IDisplayAdapter
    {
        private int _offThreadReads;

        public event EventHandler? DisplaysChanged
        {
            add { }
            remove { }
        }

        public int OffThreadReads => Volatile.Read(ref _offThreadReads);

        public IReadOnlyList<DisplayInfo> GetDisplays()
        {
            if (!executor.IsCurrentThread)
            {
                Interlocked.Increment(ref _offThreadReads);
            }

            return displays;
        }
    }
}
