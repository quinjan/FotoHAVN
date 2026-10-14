using System.Collections.Concurrent;
using FotoHavn.Core.Camera;
using FotoHavn.Protocol.Cameras;
using Microsoft.Extensions.Logging;
using Windows.Devices.Enumeration;

namespace FotoHavn.Camera.Windows;

/// <summary>
/// The real camera adapter (plan section 11, ADR 0004): lists video capture devices, watches them come and go, and opens a camera
/// exclusively through <c>MediaCapture</c>. It reports facts and pushes preview frames to the host's <see cref="IPreviewChannel"/>;
/// every rule lives in the brain and the Core policies. The device id is the identity. Dispose the adapter to stop the device
/// watcher; sessions belong to whoever opened them.
/// </summary>
public sealed class WindowsCameraAdapter : ICameraAdapter, IDisposable
{
    private readonly IPreviewChannel _preview;
    private readonly ILogger _logger;
    private readonly ICameraPolicySet _policies;
    private readonly ConcurrentDictionary<string, object> _held = new(StringComparer.OrdinalIgnoreCase);
    private readonly DeviceWatcher? _watcher;
    private volatile bool _watcherCompleted;
    private int _disposed;

    public WindowsCameraAdapter(IPreviewChannel preview, ILogger logger)
        : this(preview, logger, CoreCameraPolicies.Instance, watchDevices: true)
    {
    }

    internal WindowsCameraAdapter(IPreviewChannel preview, ILogger logger, ICameraPolicySet policies, bool watchDevices)
    {
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        if (watchDevices)
        {
            _watcher = DeviceInformation.CreateWatcher(DeviceClass.VideoCapture);
            _watcher.Added += OnDeviceAdded;
            _watcher.Updated += OnDeviceUpdated;
            _watcher.Removed += OnDeviceRemoved;
            _watcher.EnumerationCompleted += OnEnumerationCompleted;
            _watcher.Start();
        }
    }

    public event EventHandler? CamerasChanged;

    /// <summary>How many sessions are open or opening (a device holds at most one).</summary>
    internal int HeldDeviceCount => _held.Count;

    public async Task<IReadOnlyList<AvailableCamera>> ListAsync(CancellationToken cancellationToken)
    {
        var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture).AsTask(cancellationToken).ConfigureAwait(false);
        return [.. devices.Select(device => new AvailableCamera(device.Id, device.Name))];
    }

    public Task<ICameraSession> OpenAsync(string deviceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return OpenCoreAsync(deviceId, cancellationToken);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0 || _watcher is null)
        {
            return;
        }

        _watcher.Added -= OnDeviceAdded;
        _watcher.Updated -= OnDeviceUpdated;
        _watcher.Removed -= OnDeviceRemoved;
        _watcher.EnumerationCompleted -= OnEnumerationCompleted;
        if (_watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
        {
            _watcher.Stop();
        }
    }

    private async Task<ICameraSession> OpenCoreAsync(string deviceId, CancellationToken cancellationToken)
    {
        var token = new object();
        if (!_held.TryAdd(deviceId, token))
        {
            throw new CameraException(CameraFailureKind.InUseByAnotherApp, "This adapter already holds the camera.");
        }

        try
        {
            return await WindowsCameraSession.OpenAsync(deviceId, _preview, _policies, _logger, () => Release(deviceId, token), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Release(deviceId, token);
            throw;
        }
    }

    private void Release(string deviceId, object token) =>
        _held.TryRemove(new KeyValuePair<string, object>(deviceId, token));

    private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation args) => RaiseCamerasChanged(initialEnumeration: !_watcherCompleted);

    private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate args) => RaiseCamerasChanged(initialEnumeration: !_watcherCompleted);

    private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args) => RaiseCamerasChanged(initialEnumeration: !_watcherCompleted);

    private void OnEnumerationCompleted(DeviceWatcher sender, object args) => _watcherCompleted = true;

    // The watcher replays every existing device as Added before EnumerationCompleted; that is not a change.
    private void RaiseCamerasChanged(bool initialEnumeration)
    {
        if (initialEnumeration || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            CamerasChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            CameraLog.CamerasChangedHandlerThrew(_logger, exception);
        }
    }
}
