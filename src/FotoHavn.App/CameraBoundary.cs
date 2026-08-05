using FotoHavn.Core;
using System.Collections.Concurrent;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using CoreCapturedFrame = FotoHavn.Core.CapturedFrame;

namespace FotoHavn.App;

public sealed class CameraBoundary : ICameraBoundary, IAsyncDisposable
{
    private static readonly string[] RequestedProperties =
    [
        "System.Devices.ContainerId",
        "System.Devices.LocationPaths",
    ];

    private readonly ConcurrentDictionary<string, DeviceInformation> devices = new(StringComparer.Ordinal);
    private readonly TaskCompletionSource discoveryCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object discoverySync = new();
    private readonly SemaphoreSlim ownershipGate = new(1, 1);
    private readonly CameraSessionOwner<CameraOwnedStream> sessionOwner = new();
    private readonly object frameSync = new();
    private PendingCapture? pendingCapture;
    private long latestFrameSequence;
    private long latestFrameAtUtcTicks;
    private CameraStreamFailure streamFailure = CameraStreamFailure.Unavailable;
    private int staleFramePublished = 1;
    private Timer? freshnessTimer;
    private DeviceWatcher? watcher;

    public event EventHandler? AvailableCamerasChanged;

    public event EventHandler? StreamHealthChanged;

    public event EventHandler<SoftwareBitmap>? PreviewFrameAvailable;

    public IReadOnlyList<AvailableCamera> AvailableCameras => devices.Values
        .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(device => device.Id, StringComparer.Ordinal)
        .Select(device => new AvailableCamera(device.Id, device.Name, ResolveStableLabel(device)))
        .ToArray();

    public string? StreamId => sessionOwner.StreamId;

    public CameraStreamHealth StreamHealth
    {
        get
        {
            lock (frameSync)
            {
                var ticks = latestFrameAtUtcTicks;
                return new CameraStreamHealth(
                    sessionOwner.DeviceId,
                    sessionOwner.StreamId,
                    latestFrameSequence,
                    ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero),
                    streamFailure);
            }
        }
    }

    public async Task StartDiscoveryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task discoveryTask;
        lock (discoverySync)
        {
            if (watcher is null)
            {
                watcher = DeviceInformation.CreateWatcher(
                    DeviceInformation.GetAqsFilterFromDeviceClass(DeviceClass.VideoCapture),
                    RequestedProperties);
                watcher.Added += OnDeviceAdded;
                watcher.Updated += OnDeviceUpdated;
                watcher.Removed += OnDeviceRemoved;
                watcher.EnumerationCompleted += OnEnumerationCompleted;
                watcher.Start();
            }

            discoveryTask = discoveryCompleted.Task;
        }

        await discoveryTask.WaitAsync(cancellationToken).ConfigureAwait(false);
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

            var mediaCapture = new MediaCapture();
            mediaCapture.Failed += OnCameraStreamFailed;
            mediaCapture.CaptureDeviceExclusiveControlStatusChanged += OnExclusiveControlStatusChanged;
            try
            {
                await mediaCapture.InitializeAsync(CameraOpenPolicy.CreateSettings(deviceId))
                    .AsTask(cancellationToken)
                    .ConfigureAwait(false);

                var source = FindColorVideoSource(mediaCapture);
                if (source is null)
                {
                    DisposeUnownedMediaCapture(mediaCapture);
                    return CameraOpenResult.Unavailable;
                }

                var formats = CameraFormatSelector.SelectOnePerTier(source.SupportedFormats);
                if (formats.Count == 0)
                {
                    DisposeUnownedMediaCapture(mediaCapture);
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
                            nextReader = await mediaCapture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8)
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
                                    RecordFrameReceived();
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
                        new CameraOwnedStream(
                            mediaCapture,
                            fallback.Value,
                            OnFrameArrived,
                            OnCameraStreamFailed,
                            OnExclusiveControlStatusChanged)).ConfigureAwait(false);
                    lock (frameSync)
                    {
                        streamFailure = CameraStreamFailure.None;
                    }
                    Interlocked.Exchange(ref staleFramePublished, 0);
                    StartFreshnessTimer();
                    StreamHealthChanged?.Invoke(this, EventArgs.Empty);
                    return CameraOpenResult.Ready;
                }

                DisposeUnownedMediaCapture(mediaCapture);
                return fallback.LastFailure is null
                    ? CameraOpenResult.Unavailable
                    : CameraFailureMapper.Map(fallback.LastFailure);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                DisposeUnownedMediaCapture(mediaCapture);
                await DisposeCurrentAsync().ConfigureAwait(false);
                return CameraFailureMapper.Map(exception);
            }
            catch (OperationCanceledException)
            {
                DisposeUnownedMediaCapture(mediaCapture);
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
            streamFailure = CameraStreamFailure.Unavailable;
        }
        finally
        {
            ownershipGate.Release();
        }
    }

    public async Task<CoreCapturedFrame?> CaptureFirstFreshFrameAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        if (StreamHealth is not { Failure: CameraStreamFailure.None, StreamId: not null })
        {
            return null;
        }

        PendingCapture request;
        lock (frameSync)
        {
            if (pendingCapture is not null)
            {
                throw new InvalidOperationException("A fresh Camera frame is already being requested.");
            }

            request = new PendingCapture(
                latestFrameSequence,
                new TaskCompletionSource<CoreCapturedFrame>(TaskCreationOptions.RunContinuationsAsynchronously));
            pendingCapture = request;
        }

        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            try
            {
                return await request.Completion.Task.WaitAsync(timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
        }
        finally
        {
            lock (frameSync)
            {
                if (ReferenceEquals(pendingCapture, request))
                {
                    pendingCapture = null;
                }
            }
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
            watcher.EnumerationCompleted -= OnEnumerationCompleted;
            watcher = null;
            discoveryCompleted.TrySetCanceled();
        }

        await ReleaseAsync(CancellationToken.None).ConfigureAwait(false);
        freshnessTimer?.Dispose();
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
            lock (frameSync)
            {
                streamFailure = CameraStreamFailure.Removed;
            }
            CancelPendingCapture();
            StreamHealthChanged?.Invoke(this, EventArgs.Empty);
            _ = ReleaseAfterRemovalAsync(update.Id);
        }
    }

    private void OnEnumerationCompleted(DeviceWatcher sender, object args) =>
        discoveryCompleted.TrySetResult();

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

        var receivedAt = DateTimeOffset.UtcNow;
        PendingCapture? request;
        SoftwareBitmap? capturedBitmap = null;
        long sequence;
        lock (frameSync)
        {
            sequence = ++latestFrameSequence;
            latestFrameAtUtcTicks = receivedAt.UtcTicks;
            if (streamFailure == CameraStreamFailure.Stale)
            {
                streamFailure = CameraStreamFailure.None;
            }
            request = pendingCapture;
            if (request is not null && sequence > request.AfterSequence)
            {
                pendingCapture = null;
                capturedBitmap = SoftwareBitmap.Copy(bitmap);
            }
        }

        if (Interlocked.Exchange(ref staleFramePublished, 0) == 1)
        {
            StreamHealthChanged?.Invoke(this, EventArgs.Empty);
        }

        if (request is not null && capturedBitmap is not null)
        {
            _ = EncodeCapturedFrameAsync(
                capturedBitmap,
                sequence,
                receivedAt,
                request.Completion);
        }

        PreviewFrameAvailable?.Invoke(this, SoftwareBitmap.Copy(bitmap));
    }

    private static async Task EncodeCapturedFrameAsync(
        SoftwareBitmap bitmap,
        long sequence,
        DateTimeOffset receivedAt,
        TaskCompletionSource<CoreCapturedFrame> completion)
    {
        using (bitmap)
        using (var stream = new InMemoryRandomAccessStream())
        {
            try
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
                encoder.SetSoftwareBitmap(bitmap);
                await encoder.FlushAsync();
                var bytes = new byte[checked((int)stream.Size)];
                using var reader = new DataReader(stream.GetInputStreamAt(0));
                await reader.LoadAsync((uint)bytes.Length);
                reader.ReadBytes(bytes);
                completion.TrySetResult(new CoreCapturedFrame(
                    sequence,
                    receivedAt,
                    bitmap.PixelWidth,
                    bitmap.PixelHeight,
                    bytes));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }
    }

    private void RecordFrameReceived()
    {
        lock (frameSync)
        {
            latestFrameAtUtcTicks = DateTimeOffset.UtcNow.UtcTicks;
            if (streamFailure == CameraStreamFailure.Stale)
            {
                streamFailure = CameraStreamFailure.None;
            }
        }

        if (Interlocked.Exchange(ref staleFramePublished, 0) == 1)
        {
            StreamHealthChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnCameraStreamFailed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
    {
        lock (frameSync)
        {
            streamFailure = CameraStreamFailure.StreamFailure;
        }
        CancelPendingCapture();
        StreamHealthChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnExclusiveControlStatusChanged(
        MediaCapture sender,
        MediaCaptureDeviceExclusiveControlStatusChangedEventArgs args)
    {
        if (args.Status == MediaCaptureDeviceExclusiveControlStatus.ExclusiveControlAvailable)
        {
            return;
        }

        lock (frameSync)
        {
            streamFailure = CameraStreamFailure.ExclusiveOwnershipLost;
        }
        CancelPendingCapture();
        StreamHealthChanged?.Invoke(this, EventArgs.Empty);
    }

    private void StartFreshnessTimer()
    {
        freshnessTimer?.Dispose();
        freshnessTimer = new Timer(
            static state => ((CameraBoundary)state!).PublishStaleFrameIfNeeded(),
            this,
            TimeSpan.FromMilliseconds(250),
            TimeSpan.FromMilliseconds(250));
    }

    private void PublishStaleFrameIfNeeded()
    {
        lock (frameSync)
        {
            if (latestFrameAtUtcTicks == 0 ||
                sessionOwner.StreamId is null ||
                streamFailure != CameraStreamFailure.None ||
                DateTimeOffset.UtcNow - new DateTimeOffset(latestFrameAtUtcTicks, TimeSpan.Zero) <= TimeSpan.FromSeconds(2) ||
                Interlocked.Exchange(ref staleFramePublished, 1) == 1)
            {
                return;
            }

            streamFailure = CameraStreamFailure.Stale;
        }

        StreamHealthChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task DisposeCurrentAsync()
    {
        freshnessTimer?.Dispose();
        freshnessTimer = null;
        CancelPendingCapture();
        await sessionOwner.ReleaseAsync().ConfigureAwait(false);
        lock (frameSync)
        {
            latestFrameAtUtcTicks = 0;
            streamFailure = CameraStreamFailure.Unavailable;
        }
        Interlocked.Exchange(ref staleFramePublished, 1);
        StreamHealthChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DisposeUnownedMediaCapture(MediaCapture mediaCapture)
    {
        mediaCapture.Failed -= OnCameraStreamFailed;
        mediaCapture.CaptureDeviceExclusiveControlStatusChanged -= OnExclusiveControlStatusChanged;
        mediaCapture.Dispose();
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

    private void CancelPendingCapture()
    {
        PendingCapture? request;
        lock (frameSync)
        {
            request = pendingCapture;
            pendingCapture = null;
        }

        request?.Completion.TrySetResult(null!);
    }

    private sealed record PendingCapture(
        long AfterSequence,
        TaskCompletionSource<CoreCapturedFrame> Completion);
}

internal sealed class CameraOwnedStream(
    MediaCapture mediaCapture,
    MediaFrameReader reader,
    TypedEventHandler<MediaFrameReader, MediaFrameArrivedEventArgs> frameHandler,
    MediaCaptureFailedEventHandler failureHandler,
    TypedEventHandler<MediaCapture, MediaCaptureDeviceExclusiveControlStatusChangedEventArgs> exclusiveControlHandler) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        reader.FrameArrived -= frameHandler;
        mediaCapture.Failed -= failureHandler;
        mediaCapture.CaptureDeviceExclusiveControlStatusChanged -= exclusiveControlHandler;
        try
        {
            await reader.StopAsync();
        }
        finally
        {
            reader.Dispose();
            mediaCapture.Dispose();
        }
    }
}
