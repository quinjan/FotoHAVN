using System.Text.Json;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Hooks;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core.Tests.Support;

/// <summary>A brain on a <see cref="ManualBrainExecutor"/> with fake displays, host, camera, and clock; deterministic.</summary>
internal sealed class BrainHarness : IDisposable
{
    public BrainHarness(bool hooks = false, FakeHostHookPort? hostHooks = null, FakeDisplayAdapter? displays = null, IEventStore? store = null, FakeStorageHookPort? storageHooks = null, FakeCameraAdapter? camera = null, ICameraAdapter? cameraOverride = null, ICameraFaultControl? cameraFaults = null, IPreviewStats? previewStats = null, FakeClock? clock = null)
    {
        Clock = clock ?? new FakeClock();
        Displays = displays ?? FakeDisplayAdapter.CreateDefault();
        Camera = camera ?? new FakeCameraAdapter(Clock);
        CameraPort = cameraOverride ?? Camera;
        Host = new RecordingHostServices(Displays.PrimaryDisplayId);
        HostHooks = hostHooks;
        Store = store ?? new InMemoryEventStore();
        StorageHooks = storageHooks;
        Dependencies = TestDependencies.Create(Executor, Clock, Displays, CameraPort, Host, Store) with { Logger = Logger, PreviewStats = previewStats };
        Brain = new Brain(Dependencies, hooks, hostHooks, storageHooks, cameraFaults);
        Run();
    }

    public ManualBrainExecutor Executor { get; } = new();

    public FakeClock Clock { get; }

    public FakeDisplayAdapter Displays { get; }

    public FakeCameraAdapter Camera { get; }

    /// <summary>What the brain is given as its camera adapter: the fake, or a decorator or double around it.</summary>
    public ICameraAdapter CameraPort { get; }

    public RecordingHostServices Host { get; }

    public FakeHostHookPort? HostHooks { get; }

    public IEventStore Store { get; }

    public FakeStorageHookPort? StorageHooks { get; }

    public RecordingLogger Logger { get; } = new();

    public BrainDependencies Dependencies { get; }

    public Brain Brain { get; }

    /// <summary>A brain with the hook dispatcher on and a fake host hook port.</summary>
    public static BrainHarness WithHooks(FakeDisplayAdapter? displays = null) => new(hooks: true, new FakeHostHookPort(), displays);

    public TestClient Connect(Role role)
    {
        var client = new TestClient(role);
        client.Connection = Brain.Connect(role, client.Receive);
        Run();
        return client;
    }

    /// <summary>Connects and completes the handshake, then forgets the welcome.</summary>
    public TestClient Handshake(Role role)
    {
        var client = Connect(role);
        client.Hello();
        Run();
        Assert.IsAssignableFrom<IRoleMessage>(client.Single());
        client.Clear();
        return client;
    }

    public void Run() => Executor.RunPending();

    public void Dispose() => Brain.Dispose();
}

/// <summary>One UI as the brain sees it: what it sent and every message it received, parsed.</summary>
internal sealed class TestClient(Role role)
{
    private readonly List<string> _received = [];

    public Role Role => role;

    public IBrainConnection Connection { get; set; } = null!;

    public IReadOnlyList<string> Raw => _received;

    public IReadOnlyList<ServerMessage> Messages => [.. _received.Select(ServerMessageReader.Read)];

    public void Receive(string json) => _received.Add(json);

    public void Clear() => _received.Clear();

    public ServerMessage Single() => Assert.Single(Messages);

    public T Single<T>()
        where T : ServerMessage => Assert.IsType<T>(Single());

    /// <summary>Compares the wire JSON, because <see cref="TestResultMessage.Result"/> is a <see cref="JsonElement"/> without value equality.</summary>
    public void AssertReceived(params ServerMessage[] expected) => Assert.Equal(expected.Select(Wire.Json), _received);

    public void Send(string json) => Connection.Receive(json);

    public void Hello(int version = ProtocolVersion.Current) => Send($$"""{"type":"hello","protocolVersion":{{version}}}""");

    /// <summary>Sends a command with a fresh id and returns the id.</summary>
    public string Command(string name, string payload = "{}")
    {
        var id = Guid.NewGuid().ToString();
        Send($$"""{"type":"command","id":"{{id}}","name":{{JsonSerializer.Serialize(name)}},"payload":{{payload}}}""");
        return id;
    }

    /// <summary>Sends a test hook call with a fresh id and returns the id.</summary>
    public string Test(string name, string? args = null)
    {
        var id = Guid.NewGuid().ToString();
        var argsMember = args is null ? string.Empty : $$""","args":{{args}}""";
        Send($$"""{"type":"test","id":"{{id}}","name":{{JsonSerializer.Serialize(name)}}{{argsMember}}}""");
        return id;
    }
}

/// <summary>A storage hook port that records its calls.</summary>
internal sealed class FakeStorageHookPort : IStorageHookPort
{
    public List<string> Calls { get; } = [];

    public Exception? Failure { get; set; }

    public void InjectFault(Protocol.Testing.StorageFaultKind kind, Protocol.Testing.StorageFaultScope scope)
    {
        Calls.Add($"InjectFault({kind}, {scope})");
        if (Failure is not null)
        {
            throw Failure;
        }
    }

    public void ClearFault()
    {
        Calls.Add("ClearFault");
        if (Failure is not null)
        {
            throw Failure;
        }
    }
}

/// <summary>A host hook port whose answers each test sets.</summary>
internal sealed class FakeHostHookPort : IHostHookPort
{
    public List<string> Calls { get; } = [];

    public Func<Task<IReadOnlyList<Protocol.Testing.TestWindow>>> Windows { get; set; } = () => Task.FromResult<IReadOnlyList<Protocol.Testing.TestWindow>>([]);

    public Func<Role, int, int, Task<Protocol.Testing.TestWindow>> SetBounds { get; set; } =
        (role, width, height) => Task.FromResult(new Protocol.Testing.TestWindow(role, new Protocol.Testing.PixelRect(0, 0, width, height), "fake-primary"));

    public Func<Role, bool> CrashUi { get; set; } = _ => true;

    public Func<CapturedFrame, bool, Task<CameraCaptureView>> SaveCapture { get; set; } =
        (frame, mirrored) => Task.FromResult(new CameraCaptureView("capture.jpg", frame.Width, frame.Height, 1234, mirrored));

    public Action Exit { get; set; } = () => { };

    public Task<IReadOnlyList<Protocol.Testing.TestWindow>> GetWindowsAsync()
    {
        Calls.Add("GetWindows");
        return Windows();
    }

    public Task<Protocol.Testing.TestWindow> SetWindowBoundsAsync(Role role, int width, int height)
    {
        Calls.Add($"SetWindowBounds({role}, {width}, {height})");
        return SetBounds(role, width, height);
    }

    public bool ScheduleCrashUi(Role role)
    {
        Calls.Add($"CrashUi({role})");
        return CrashUi(role);
    }

    public Task<CameraCaptureView> SaveDiagnosticsCaptureAsync(CapturedFrame frame, bool mirrored)
    {
        Calls.Add($"SaveDiagnosticsCapture({mirrored})");
        return SaveCapture(frame, mirrored);
    }

    public void ScheduleExit()
    {
        Calls.Add("Exit");
        Exit();
    }

    public TestHookError Classify(Exception exception) =>
        exception is HookFailure failure ? failure.Error : TestHookError.HookFailed;
}

/// <summary>A host failure with a specific hook error, as <c>TestHookException</c> is in E2E builds.</summary>
#pragma warning disable CA1032, CA1064 // A test-only stand-in; the standard constructors aren't needed.
internal sealed class HookFailure(TestHookError error) : Exception($"Hook failed: {error}.")
#pragma warning restore CA1032, CA1064
{
    public TestHookError Error => error;
}

/// <summary>A logger that keeps every entry.</summary>
internal sealed class RecordingLogger : ILogger
{
    private readonly object _gate = new();
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_gate)
        {
            _entries.Add((logLevel, formatter(state, exception), exception));
        }
    }
}

internal static class Wire
{
    public static string Json(ServerMessage message) => ProtocolJson.Serialize(message);
}

/// <summary>Display helpers.</summary>
internal static class TestDisplays
{
    public static DisplayInfo Display(string id, int x = 0, int y = 0, int width = 1920, int height = 1080, bool primary = false, double scale = 1.0) =>
        FakeDisplayAdapter.Create(id, id, x, y, width, height, scale, primary);
}
