using FotoHavn.Core.Camera;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Fakes;

/// <summary>A photograph the fake camera returns from <see cref="ICameraSession.CaptureAsync"/> instead of the test pattern.</summary>
/// <param name="Bgra">Width × Height × 4 bytes in B, G, R, A order, rows tightly packed.</param>
/// <param name="Width">The photograph's width.</param>
/// <param name="Height">The photograph's height.</param>
public sealed record FixturePhotograph(byte[] Bgra, int Width, int Height);

/// <summary>
/// A camera adapter with configurable cameras, for brain tests and the headless host. Each open session produces
/// <see cref="FakeTestPattern"/> frames at 1280×720 on the <see cref="IClock"/> it is given: with a <see cref="FakeClock"/>
/// frames arrive only as the test advances it (deterministic); with <see cref="SystemClock"/> they arrive in real time.
/// The camera clock (<see cref="ICameraSession.Now"/>) starts at zero when the adapter is created.
/// </summary>
/// <remarks>
/// Failure controls act on every camera: <see cref="Fail"/>, <see cref="Stall"/>, <see cref="Disappear"/>,
/// <see cref="InUseByAnotherApp"/>, and <see cref="Recover"/> to undo them all. Like the real adapter, a device has at most
/// one live session, and a session that failed is dead and no longer holds the device. All members are thread-safe;
/// events are raised outside the adapter's lock.
/// </remarks>
public sealed class FakeCameraAdapter : ICameraAdapter
{
    /// <summary>The fake frame width.</summary>
    public const int FrameWidth = 1280;

    /// <summary>The fake frame height.</summary>
    public const int FrameHeight = 720;

    private readonly object _gate = new();
    private readonly IClock _clock;
    private readonly DateTimeOffset _epoch;
    private readonly List<AvailableCamera> _known;
    private readonly HashSet<string> _missing = new(StringComparer.Ordinal);
    private readonly HashSet<string> _inUse = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FakeCameraSession> _sessions = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<FixturePhotograph> _photographs;
    private CameraFailureKind? _failKind;
    private bool _stalled;

    /// <param name="clock">Drives the frames and the camera clock.</param>
    /// <param name="preview">Where preview frames go. Null drops them (<see cref="NullPreviewChannel"/>).</param>
    /// <param name="cameras">The cameras to list. Null lists <see cref="DefaultCamera"/>.</param>
    /// <param name="framesPerSecond">The frame rate of every camera, above 0 and at most 120.</param>
    /// <param name="photographs">Photographs that captures return in turn, repeating. None makes captures the test pattern.</param>
    public FakeCameraAdapter(
        IClock clock,
        IPreviewChannel? preview = null,
        IEnumerable<AvailableCamera>? cameras = null,
        double framesPerSecond = 30,
        IEnumerable<FixturePhotograph>? photographs = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(framesPerSecond, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(framesPerSecond, 120);
        _clock = clock;
        _epoch = clock.UtcNow;
        Preview = preview ?? NullPreviewChannel.Instance;
        FramesPerSecond = framesPerSecond;
        _known = [.. cameras ?? [DefaultCamera]];
        _photographs = [.. photographs ?? []];
        foreach (var photograph in _photographs)
        {
            if (photograph.Width <= 0 || photograph.Height <= 0 || photograph.Bgra.Length != photograph.Width * photograph.Height * 4)
            {
                throw new ArgumentException("A fixture photograph holds width × height × 4 BGRA bytes.", nameof(photographs));
            }
        }
    }

    public event EventHandler? CamerasChanged;

    /// <summary>The camera the headless host lists by default.</summary>
    public static AvailableCamera DefaultCamera { get; } = new("fake-camera", "Fake camera");

    /// <summary>The frame rate of every camera.</summary>
    public double FramesPerSecond { get; }

    /// <summary>The format every fake camera streams.</summary>
    public CameraFormat Format => new(FrameWidth, FrameHeight, FramesPerSecond, "NV12");

    internal IPreviewChannel Preview { get; }

    internal IReadOnlyList<FixturePhotograph> Photographs => _photographs;

    internal IClock Clock => _clock;

    /// <summary>The camera clock: time since the adapter was created.</summary>
    internal TimeSpan Now => _clock.UtcNow - _epoch;

    /// <summary>Whether frames are stopped by <see cref="Stall"/>.</summary>
    internal bool IsStalled
    {
        get
        {
            lock (_gate)
            {
                return _stalled;
            }
        }
    }

    /// <summary>The number of sessions open (neither disposed nor failed).</summary>
    public int OpenSessionCount
    {
        get
        {
            lock (_gate)
            {
                return _sessions.Count;
            }
        }
    }

    public Task<IReadOnlyList<AvailableCamera>> ListAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<IReadOnlyList<AvailableCamera>>(cancellationToken);
        }

        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<AvailableCamera>>([.. _known.Where(camera => !_missing.Contains(camera.DeviceId))]);
        }
    }

    public Task<ICameraSession> OpenAsync(string deviceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<ICameraSession>(cancellationToken);
        }

        lock (_gate)
        {
            if (!_known.Exists(camera => camera.DeviceId == deviceId) || _missing.Contains(deviceId))
            {
                return Task.FromException<ICameraSession>(new CameraException(CameraFailureKind.NotFound));
            }

            if (_failKind is { } kind)
            {
                return Task.FromException<ICameraSession>(new CameraException(kind));
            }

            if (_inUse.Contains(deviceId) || _sessions.ContainsKey(deviceId))
            {
                return Task.FromException<ICameraSession>(new CameraException(CameraFailureKind.InUseByAnotherApp));
            }

            var session = new FakeCameraSession(this, deviceId);
            _sessions[deviceId] = session;
            session.Start();
            return Task.FromResult<ICameraSession>(session);
        }
    }

    /// <summary>Adds a camera (or renames one with the same device ID) and raises <see cref="CamerasChanged"/>.</summary>
    public void AddCamera(AvailableCamera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        lock (_gate)
        {
            var index = _known.FindIndex(existing => existing.DeviceId == camera.DeviceId);
            if (index < 0)
            {
                _known.Add(camera);
            }
            else
            {
                _known[index] = camera;
            }

            _missing.Remove(camera.DeviceId);
        }

        CamerasChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Makes every open session fail with <paramref name="kind"/> (<see cref="ICameraSession.Failed"/> is raised, pending
    /// captures throw, the device is released) and makes every open fail with it, until <see cref="Recover"/>.
    /// </summary>
    public void Fail(CameraFailureKind kind)
    {
        FakeCameraSession[] sessions;
        lock (_gate)
        {
            _failKind = kind;
            sessions = [.. _sessions.Values];
        }

        foreach (var session in sessions)
        {
            session.Break(new CameraFailure(kind, $"The fake camera was told to fail with {kind}."));
        }
    }

    /// <summary>Stops every session's frames (and preview) without any failure event, so the camera goes stale. <see cref="Recover"/> resumes them.</summary>
    public void Stall()
    {
        lock (_gate)
        {
            _stalled = true;
        }
    }

    /// <summary>
    /// Unplugs a camera: it leaves the list, <see cref="CamerasChanged"/> is raised, and its open session fails with
    /// <see cref="CameraFailureKind.Removed"/>. Returns false when no listed camera has the ID. <see cref="Recover"/> plugs it back in.
    /// </summary>
    public bool Disappear(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        FakeCameraSession? session;
        lock (_gate)
        {
            if (!_known.Exists(camera => camera.DeviceId == deviceId) || !_missing.Add(deviceId))
            {
                return false;
            }

            _sessions.TryGetValue(deviceId, out session);
        }

        session?.Break(new CameraFailure(CameraFailureKind.Removed, "The fake camera was unplugged."));
        CamerasChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Makes another app hold the camera: opening it fails with <see cref="CameraFailureKind.InUseByAnotherApp"/> until <see cref="Recover"/>. An open session is unaffected.</summary>
    public void InUseByAnotherApp(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        lock (_gate)
        {
            _inUse.Add(deviceId);
        }
    }

    /// <summary>Undoes <see cref="Fail"/>, <see cref="Stall"/>, <see cref="Disappear"/>, and <see cref="InUseByAnotherApp"/>. Sessions that already failed stay dead; frames of a stalled session resume.</summary>
    public void Recover()
    {
        bool replugged;
        lock (_gate)
        {
            replugged = _missing.Count > 0;
            _failKind = null;
            _stalled = false;
            _missing.Clear();
            _inUse.Clear();
        }

        if (replugged)
        {
            CamerasChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void Release(string deviceId, FakeCameraSession session)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(deviceId, out var held) && ReferenceEquals(held, session))
            {
                _sessions.Remove(deviceId);
            }
        }
    }
}
