using FotoHavn.Core.Camera;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Hooks;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.Support;

/// <summary>A camera adapter whose sessions are scripted by the test: clock, frames, captures, and failures are all set by hand.</summary>
internal sealed class ScriptedCameraAdapter : ICameraAdapter
{
    private readonly List<ScriptedCameraSession> _sessions = [];

    public event EventHandler? CamerasChanged;

    public IReadOnlyList<AvailableCamera> Cameras { get; set; } = [new AvailableCamera("scripted", "Scripted camera")];

    public IReadOnlyList<ScriptedCameraSession> Sessions => _sessions;

    /// <summary>When set, <see cref="ListAsync"/> uses it instead of answering at once.</summary>
    public Func<Task<IReadOnlyList<AvailableCamera>>>? ListBehavior { get; set; }

    /// <summary>When set, <see cref="OpenAsync"/> uses it (it may throw) instead of creating a session at once.</summary>
    public Func<string, CancellationToken, Task<ICameraSession>>? OpenBehavior { get; set; }

    public int OpenCalls { get; private set; }

    public Task<IReadOnlyList<AvailableCamera>> ListAsync(CancellationToken cancellationToken) =>
        ListBehavior?.Invoke() ?? Task.FromResult(Cameras);

    public Task<ICameraSession> OpenAsync(string deviceId, CancellationToken cancellationToken)
    {
        OpenCalls++;
        if (OpenBehavior is { } behavior)
        {
            return behavior(deviceId, cancellationToken);
        }

        return Task.FromResult<ICameraSession>(NewSession(deviceId));
    }

    public ScriptedCameraSession NewSession(string deviceId)
    {
        var session = new ScriptedCameraSession(deviceId);
        _sessions.Add(session);
        return session;
    }

    public void RaiseCamerasChanged() => CamerasChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class ScriptedCameraSession(string deviceId) : ICameraSession
{
    public event EventHandler<CameraFailure>? Failed;

    public string DeviceId => deviceId;

    public CameraFormat Format { get; set; } = new(1920, 1080, 30, "NV12");

    public TimeSpan Now { get; set; }

    public CameraFrameInfo? LatestFrame { get; set; }

    public List<PreviewTarget> Targets { get; } = [];

    public Func<TimeSpan, CancellationToken, Task<CapturedFrame>> Capture { get; set; } =
        (_, _) => Task.FromException<CapturedFrame>(new CameraException(CameraFailureKind.Failed));

    public int Disposals { get; private set; }

    public Func<ValueTask>? DisposeBehavior { get; set; }

    public bool HasFailedSubscribers => Failed is not null;

    /// <summary>Delivers a frame stamped <paramref name="timestamp"/> and moves the session clock there.</summary>
    public void Deliver(TimeSpan timestamp)
    {
        Now = timestamp;
        LatestFrame = new CameraFrameInfo((LatestFrame?.Sequence ?? 0) + 1, timestamp, 1920, 1080);
    }

    public void Fail(CameraFailureKind kind) => Failed?.Invoke(this, new CameraFailure(kind, "scripted"));

    public void SetPreviewTarget(PreviewTarget target) => Targets.Add(target);

    public Task<CapturedFrame> CaptureAsync(TimeSpan notBefore, CancellationToken cancellationToken) => Capture(notBefore, cancellationToken);

    public ValueTask DisposeAsync()
    {
        Disposals++;
        return DisposeBehavior?.Invoke() ?? ValueTask.CompletedTask;
    }
}

/// <summary>A brain with hooks, a fake host hook port, the fake camera behind a <see cref="FaultInjectingCameraAdapter"/>, and a handshaken Operator.</summary>
internal sealed class CameraRig : IDisposable
{
    /// <summary>One frame of the fake camera at 30 fps.</summary>
    public static readonly TimeSpan Frame = TimeSpan.FromSeconds(1.0 / 30);

    public CameraRig(IEnumerable<AvailableCamera>? cameras = null, IEnumerable<FixturePhotograph>? photographs = null, bool withFaults = true, bool withHostHooks = true)
    {
        var clock = new FakeClock();
        Epoch = clock.UtcNow;
        Preview = new FakePreviewChannel(clock);
        Fake = new FakeCameraAdapter(clock, Preview, cameras, 30, photographs);
        Faults = new FaultInjectingCameraAdapter(Fake);
        Harness = new BrainHarness(
            hooks: true,
            hostHooks: withHostHooks ? new FakeHostHookPort() : null,
            cameraOverride: Faults,
            cameraFaults: withFaults ? Faults : null,
            previewStats: Preview,
            clock: clock,
            camera: Fake);
        Operator = Harness.Connect(Role.Operator);
        Operator.Hello();
        Harness.Run();
    }

    /// <summary>The wall-clock time at which the fake camera's own clock read zero.</summary>
    public DateTimeOffset Epoch { get; }

    public FakeCameraAdapter Fake { get; }

    public FaultInjectingCameraAdapter Faults { get; }

    public FakePreviewChannel Preview { get; }

    public BrainHarness Harness { get; }

    public TestClient Operator { get; }

    public FakeClock Clock => Harness.Clock;

    public FakeHostHookPort Host => Harness.HostHooks!;

    /// <summary>The latest operator presentation the page received (the welcome or an update).</summary>
    public OperatorPresentation? Presentation => Operator.Messages
        .Select(message => message switch
        {
            OperatorPresentationMessage update => update.Presentation,
            OperatorWelcomeMessage welcome => welcome.Presentation,
            _ => null,
        })
        .LastOrDefault(presentation => presentation is not null);

    public CameraDiagnosticsView View => Presentation!.CameraDiagnostics!;

    public IReadOnlyList<long> Revisions => [.. Operator.Messages.OfType<OperatorPresentationMessage>().Select(message => message.Revision)];

    /// <summary>Moves the clock forward, then lets the brain run what that posted.</summary>
    public void Advance(TimeSpan delta)
    {
        Clock.Advance(delta);
        Harness.Run();
    }

    /// <summary>Delivers frames and freshness checks for <paramref name="duration"/>, one poll interval at a time.</summary>
    public void Run(TimeSpan duration)
    {
        var step = Brain.FreshnessPollInterval;
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += step)
        {
            Advance(elapsed + step <= duration ? step : duration - elapsed);
        }
    }

    /// <summary>Sends a hook, then runs the brain (waiting for thread-pool continuations) until its answer arrives.</summary>
    public TestResultMessage Call(string name, string? args = null) => Await(Operator.Test(name, args));

    /// <summary>Runs the brain until the hook with <paramref name="id"/> is answered.</summary>
    public TestResultMessage Await(string id)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            Harness.Run();
            if (Operator.Messages.OfType<TestResultMessage>().FirstOrDefault(message => message.Id == id) is { } result)
            {
                return result;
            }

            Assert.True(DateTime.UtcNow < deadline, "The hook was never answered.");
            Thread.Sleep(2);
        }
    }
    public TestResultMessage OpenDiagnostics() => Call(TestHookNames.OpenCameraDiagnostics);

    public TestResultMessage Select(string deviceId) =>
        Call(TestHookNames.SelectDiagnosticsCamera, $$"""{"deviceId":"{{deviceId}}"}""");

    /// <summary>Opens the surface, selects the camera, and runs the fake camera until the brain calls it ready.</summary>
    public void SelectAndWaitUntilReady(string deviceId = "fake-camera")
    {
        OpenDiagnostics();
        Assert.True(Select(deviceId).Ok);
        Run(TimeSpan.FromMilliseconds(500));
        Assert.Equal(CameraState.Ready, View.State);
    }

    public CameraDiagnostics Diagnostics()
    {
        var result = Call(TestHookNames.GetDiagnostics);
        return ProtocolJson.Deserialize<DiagnosticsResult>(result.Result!.Value).Camera;
    }

    public void Dispose() => Harness.Dispose();
}
