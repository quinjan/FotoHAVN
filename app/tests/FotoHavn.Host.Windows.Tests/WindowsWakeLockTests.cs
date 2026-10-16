using FotoHavn.Host.Windows.Hardening;

namespace FotoHavn.Host.Windows.Tests;

/// <summary>The Windows wake lock (M10): its logic over a fake of the power-request calls, and one real round trip of the calls themselves.</summary>
public sealed class WindowsWakeLockTests
{
    [Fact]
    public void Acquiring_makes_one_request_with_the_reason_and_both_kinds()
    {
        var api = new FakePowerRequests();
        var wakeLock = new WindowsWakeLock(api, new RecordingLogger());

        wakeLock.Acquire("FOTOHAVN Event event-1 is running");

        Assert.True(wakeLock.IsHeld);
        Assert.Equal("FOTOHAVN Event event-1 is running", wakeLock.Reason);
        Assert.Equal(["create:FOTOHAVN Event event-1 is running", "set:DisplayRequired", "set:SystemRequired"], api.Calls);
    }

    [Fact]
    public void Acquiring_while_held_keeps_the_first_request_and_reason()
    {
        var api = new FakePowerRequests();
        var wakeLock = new WindowsWakeLock(api, new RecordingLogger());
        wakeLock.Acquire("first");

        wakeLock.Acquire("second");

        Assert.Equal("first", wakeLock.Reason);
        Assert.Single(api.Calls, call => call.StartsWith("create", StringComparison.Ordinal));
    }

    [Fact]
    public void Releasing_clears_both_kinds_and_closes_the_request()
    {
        var api = new FakePowerRequests();
        var wakeLock = new WindowsWakeLock(api, new RecordingLogger());
        wakeLock.Acquire("reason");

        wakeLock.Release();

        Assert.False(wakeLock.IsHeld);
        Assert.Null(wakeLock.Reason);
        Assert.Equal(["create:reason", "set:DisplayRequired", "set:SystemRequired", "clear:DisplayRequired", "clear:SystemRequired", "close"], api.Calls);
        Assert.Equal(0, api.Open);
    }

    [Fact]
    public void Releasing_nothing_is_harmless_and_the_lock_can_be_taken_again()
    {
        var api = new FakePowerRequests();
        var wakeLock = new WindowsWakeLock(api, new RecordingLogger());

        wakeLock.Release();
        Assert.Empty(api.Calls);

        wakeLock.Acquire("one");
        wakeLock.Release();
        wakeLock.Acquire("two");

        Assert.True(wakeLock.IsHeld);
        Assert.Equal("two", wakeLock.Reason);
        Assert.Equal(1, api.Open);
    }

    [Fact]
    public void A_refused_request_is_logged_once_and_never_thrown_into_the_brain()
    {
        var api = new FakePowerRequests { FailCreate = 5 };
        var logger = new RecordingLogger();
        var wakeLock = new WindowsWakeLock(api, logger);

        wakeLock.Acquire("reason");
        wakeLock.Acquire("reason");
        wakeLock.Release();

        Assert.False(wakeLock.IsHeld);
        var warning = Assert.Single(logger.Entries);
        Assert.Contains("failed to create", warning.Message, StringComparison.Ordinal);
        Assert.Contains("5", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_kind_that_fails_still_holds_the_one_that_worked()
    {
        var api = new FakePowerRequests { FailSet = PowerRequestKind.DisplayRequired };
        var logger = new RecordingLogger();
        var wakeLock = new WindowsWakeLock(api, logger);

        wakeLock.Acquire("reason");
        wakeLock.Release();

        Assert.Single(logger.Entries);
        Assert.Equal(["create:reason", "set:DisplayRequired", "set:SystemRequired", "clear:SystemRequired", "close"], api.Calls);
    }

    [Fact]
    public void When_no_kind_could_be_set_the_request_is_closed_and_nothing_is_held()
    {
        var api = new FakePowerRequests { FailEverySet = true };
        var logger = new RecordingLogger();
        var wakeLock = new WindowsWakeLock(api, logger);

        wakeLock.Acquire("reason");

        Assert.False(wakeLock.IsHeld);
        Assert.Null(wakeLock.Reason);
        Assert.Equal(0, api.Open);
        Assert.Single(logger.Entries);
    }

    [Fact]
    public void An_empty_reason_is_refused()
    {
        var wakeLock = new WindowsWakeLock(new FakePowerRequests(), new RecordingLogger());

        Assert.Throws<ArgumentException>(() => wakeLock.Acquire(" "));
    }

    [Fact]
    public async Task Any_thread_may_acquire_and_release_and_every_request_is_closed()
    {
        var api = new FakePowerRequests();
        var wakeLock = new WindowsWakeLock(api, new RecordingLogger());

        await Task.WhenAll(Enumerable.Range(0, 8).Select(index => Task.Run(() =>
        {
            for (var round = 0; round < 200; round++)
            {
                wakeLock.Acquire($"reason {index}");
                _ = wakeLock.IsHeld;
                _ = wakeLock.Reason;
                wakeLock.Release();
            }
        })));
        wakeLock.Release();

        Assert.False(wakeLock.IsHeld);
        Assert.Equal(0, api.Open);
    }

    [Fact]
    public void The_real_calls_create_set_clear_and_close_a_request()
    {
        var api = new NativePowerRequestApi();

        var request = api.Create("FOTOHAVN wake lock test", out var createError);

        Assert.True(request.HasValue, $"PowerCreateRequest failed with Windows error {createError}");
        Assert.True(api.Set(request.Value, PowerRequestKind.DisplayRequired, out _));
        Assert.True(api.Set(request.Value, PowerRequestKind.SystemRequired, out _));
        Assert.True(api.Clear(request.Value, PowerRequestKind.DisplayRequired));
        Assert.True(api.Clear(request.Value, PowerRequestKind.SystemRequired));
        api.Close(request.Value);
        Assert.False(api.Set(request.Value, PowerRequestKind.DisplayRequired, out var closedError));
        Assert.NotEqual(0, closedError);
        Assert.Throws<ArgumentNullException>(() => api.Create(null!, out _));
    }

    [Fact]
    public void The_real_lock_holds_and_releases_through_the_wake_lock()
    {
        var wakeLock = new WindowsWakeLock(new NativePowerRequestApi(), new RecordingLogger());

        wakeLock.Acquire("FOTOHAVN wake lock test");
        Assert.True(wakeLock.IsHeld);
        wakeLock.Release();

        Assert.False(wakeLock.IsHeld);
    }

    private sealed class FakePowerRequests : IPowerRequestApi
    {
        private readonly Lock _gate = new();
        private readonly List<string> _calls = [];
        private int _next = 100;

        public int FailCreate { get; init; }

        public PowerRequestKind? FailSet { get; init; }

        public bool FailEverySet { get; init; }

        public int Open { get; private set; }

        public IReadOnlyList<string> Calls
        {
            get
            {
                lock (_gate)
                {
                    return [.. _calls];
                }
            }
        }

        public IntPtr? Create(string reason, out int error)
        {
            lock (_gate)
            {
                error = FailCreate;
                if (FailCreate != 0)
                {
                    return null;
                }

                _calls.Add("create:" + reason);
                Open++;
                return _next++;
            }
        }

        public bool Set(IntPtr request, PowerRequestKind kind, out int error)
        {
            lock (_gate)
            {
                _calls.Add("set:" + kind);
                var fail = FailEverySet || FailSet == kind;
                error = fail ? 87 : 0;
                return !fail;
            }
        }

        public bool Clear(IntPtr request, PowerRequestKind kind)
        {
            lock (_gate)
            {
                _calls.Add("clear:" + kind);
                return true;
            }
        }

        public void Close(IntPtr request)
        {
            lock (_gate)
            {
                _calls.Add("close");
                Open--;
            }
        }
    }
}
