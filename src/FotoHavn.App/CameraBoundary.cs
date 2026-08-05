using FotoHavn.Core;
using System.Collections.Concurrent;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;

namespace FotoHavn.App;

public sealed class CameraBoundary : ICameraBoundary, IAsyncDisposable
{
    private static readonly string[] RequestedProperties =
    [
        "System.Devices.ContainerId",
        "System.Devices.LocationPaths",
    ];

    private readonly ConcurrentDictionary<string, DeviceInformation> devices = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim ownershipGate = new(1, 1);
    private readonly CameraSessionOwner<CameraOwnedStream> sessionOwner = new();
    private DeviceWatcher? watcher;

    public event EventHandler? AvailableCamerasChanged;

    public event EventHandler<SoftwareBitmap>? PreviewFrameAvailable;

    public IReadOnlyList<AvailableCamera> AvailableCameras => devices.Values
        .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(device => device.Id, StringComparer.Ordinal)
        .Select(device => new AvailableCamera(device.Id, device.Name, ResolveStableLabel(device)))
        .ToArray();

    public string? StreamId => sessionOwner.StreamId;

    public Task StartDiscoveryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (watcher is not null)
        {
            return Task.CompletedTask;
        }

        watcher = DeviceInformation.CreateWatcher(
            DeviceInformation.GetAqsFilterFromDeviceClass(DeviceClass.VideoCapture),
            RequestedProperties);
        watcher.Added += OnDeviceAdded;
        watcher.Updated += OnDeviceUpdated;
        watcher.Removed += OnDeviceRemoved;
        watcher.Start();
        return Task.CompletedTask;
    }

    public async Task<CameraOpenResult> OpenAsync(CameraDeviceId deviceId, CancellationToken cancellationToken)
    {
        await ownershipGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisposeCurrentAsync().ConfigureAwait(false);
            if (!devices.ContainsKey(deviceId.Value))
            {
                return CameraOpenResult.Unavailable;
            }

            var nextCapture = new MediaCapture();
            try
            {
                await nextCapture.InitializeAsync(CameraOpenPolicy.CreateSettings(deviceId))
                    .AsTask(cancellationToken)
                    .ConfigureAwait(false);

                var source = FindColorVideoSource(nextCapture);
                if (source is null)
                {
                    nextCapture.Dispose();
                    return CameraOpenResult.Unavailable;
                }

                var formats = CameraFormatSelector.SelectOnePerTier(source.SupportedFormats);
                if (formats.Count == 0)
                {
                    nextCapture.Dispose();
                    return CameraOpenResult.Unavailable;
                }

                var fallback = await CameraFormatFallback.TryEachAsync(
                    formats,
                    async format =>
                    {
                        MediaFrameReader? nextReader = null;
                        try
                        {
                            await source.SetFormatAsync(format).AsTask(cancellationToken).ConfigureAwait(false);
                            nextReader = await nextCapture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8)
                                .AsTask(cancellationToken)
                                .ConfigureAwait(false);
                            nextReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
                            var firstFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                            nextReader.FrameArrived += FirstFrameArrived;
                            nextReader.FrameArrived += OnFrameArrived;

                            var startStatus = await nextReader.StartAsync().AsTask(cancellationToken).ConfigureAwait(false);
                            if (startStatus != MediaFrameReaderStartStatus.Success)
                            {
                                throw new InvalidOperationException($"Camera reader failed to start: {startStatus}.");
                            }

                            await firstFrame.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                            nextReader.FrameArrived -= FirstFrameArrived;
                            return nextReader;

                            void FirstFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
                            {
                                using var frame = sender.TryAcquireLatestFrame();
                                var bitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
                                if (bitmap is not null && CameraFrameEligibility.IsEligible(bitmap.PixelWidth, bitmap.PixelHeight, isDecoded: true))
                                {
                                    firstFrame.TrySetResult();
                                }
                            }
                        }
                        catch
                        {
                            await DisposeCandidateReaderAsync(nextReader).ConfigureAwait(false);
                            throw;
                        }
                    },
                    cancellationToken).ConfigureAwait(false);

                if (fallback.Value is not null)
                {
                    await sessionOwner.AdoptAsync(
                        deviceId,
                        Guid.NewGuid().ToString("N"),
                        new CameraOwnedStream(nextCapture, fallback.Value, OnFrameArrived)).ConfigureAwait(false);
                    return CameraOpenResult.Ready;
                }

                nextCapture.Dispose();
                return fallback.LastFailure is null
                    ? CameraOpenResult.Unavailable
                    : CameraFailureMapper.Map(fallback.LastFailure);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                nextCapture.Dispose();
                await DisposeCurrentAsync().ConfigureAwait(false);
                return CameraFailureMapper.Map(exception);
            }
            catch (OperationCanceledException)
            {
                nextCapture.Dispose();
                await DisposeCurrentAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            ownershipGate.Release();
        }
    }

    public async Task ReleaseAsync(CancellationToken cancellationToken)
    {
        await ownershipGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisposeCurrentAsync().ConfigureAwait(false);
        }
        finally
        {
            ownershipGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (watcher is not null)
        {
            watcher.Stop();
            watcher.Added -= OnDeviceAdded;
            watcher.Updated -= OnDeviceUpdated;
            watcher.Removed -= OnDeviceRemoved;
            watcher = null;
        }

        await ReleaseAsync(CancellationToken.None).ConfigureAwait(false);
        ownershipGate.Dispose();
    }

    private static MediaFrameSource? FindColorVideoSource(MediaCapture mediaCapture) =>
        mediaCapture.FrameSources.Values
            .Where(source => source.Info.SourceKind == MediaFrameSourceKind.Color)
            .Where(source => source.Info.MediaStreamType is MediaStreamType.VideoPreview or MediaStreamType.VideoRecord)
            .OrderBy(source => source.Info.MediaStreamType == MediaStreamType.VideoPreview ? 0 : 1)
            .FirstOrDefault();

    private async Task DisposeCandidateReaderAsync(MediaFrameReader? candidate)
    {
        if (candidate is null)
        {
            return;
        }

        candidate.FrameArrived -= OnFrameArrived;
        try
        {
            await candidate.StopAsync();
        }
        catch
        {
            // A reader that never started or was disconnected may reject StopAsync.
        }
        finally
        {
            candidate.Dispose();
        }
    }

    private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation device)
    {
        devices[device.Id] = device;
        AvailableCamerasChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (devices.TryGetValue(update.Id, out var device))
        {
            device.Update(update);
            AvailableCamerasChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        devices.TryRemove(update.Id, out _);
        AvailableCamerasChanged?.Invoke(this, EventArgs.Empty);
        if (sessionOwner.IsOwnedDevice(update.Id))
        {
            _ = ReleaseAfterRemovalAsync(update.Id);
        }
    }

    private async Task ReleaseAfterRemovalAsync(CameraDeviceId deviceId)
    {
        await ownershipGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await sessionOwner.RemoveAsync(deviceId).ConfigureAwait(false);
        }
        catch
        {
            // Device removal teardown is best effort; selection state is already Disconnected.
        }
        finally
        {
            ownershipGate.Release();
        }
    }

    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        using var frame = sender.TryAcquireLatestFrame();
        var bitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
        if (bitmap is null)
        {
            return;
        }

        PreviewFrameAvailable?.Invoke(this, SoftwareBitmap.Copy(bitmap));
    }

    private async Task DisposeCurrentAsync()
    {
        await sessionOwner.ReleaseAsync().ConfigureAwait(false);
    }

    private static string ResolveStableLabel(DeviceInformation device)
    {
        if (device.Properties.TryGetValue("System.Devices.LocationPaths", out var pathsValue) &&
            pathsValue is string[] paths && paths.Length > 0)
        {
            return paths[0].Split(['#', '\\'], StringSplitOptions.RemoveEmptyEntries).Last();
        }

        if (device.Properties.TryGetValue("System.Devices.ContainerId", out var containerValue) && containerValue is Guid containerId)
        {
            return containerId.ToString("N")[..8];
        }

        return CameraIdentityLabel.FromDeviceId(device.Id);
    }
}

internal sealed class CameraOwnedStream(
    MediaCapture capture,
    MediaFrameReader reader,
    TypedEventHandler<MediaFrameReader, MediaFrameArrivedEventArgs> frameHandler) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        reader.FrameArrived -= frameHandler;
        try
        {
            await reader.StopAsync();
        }
        finally
        {
            reader.Dispose();
            capture.Dispose();
        }
    }
}
