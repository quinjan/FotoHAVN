using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;
using FotoHavn.Protocol.Cameras;
using Microsoft.Extensions.Logging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using CapturedFrame = FotoHavn.Core.Camera.CapturedFrame;

namespace FotoHavn.Camera.Windows;

/// <summary>
/// One open camera: a <see cref="MediaCapture"/> in exclusive control with a single <see cref="MediaFrameReader"/>. The frame
/// thread (the reader's callback) feeds the <see cref="FramePipeline"/>; a failure or disposal tears everything down exactly once.
/// </summary>
internal sealed class WindowsCameraSession : ICameraSession
{
    private const int Running = 0;
    private const int Failing = 1;
    private const int Disposed = 2;
    private const int MaximumConsecutiveFrameErrors = 10;

    private static readonly TimeSpan TeardownStepTimeout = TimeSpan.FromSeconds(3);

    private readonly MediaCapture _capture;
    private readonly MediaFrameReader _reader;
    private readonly FramePipeline _pipeline;
    private readonly FrameClock _clock;
    private readonly ICameraPolicySet _policies;
    private readonly ILogger _logger;
    private readonly Action _released;
    private readonly SoftwareBitmapCopier _copier = new();
    private readonly object _frameLock = new();
    private readonly Lock _teardownGate = new();
    private Task? _teardown;
    private int _state = Running;
    private int _frameErrors;
    private int _failedRaised;

    private WindowsCameraSession(
        string deviceId,
        CameraFormat format,
        MediaCapture capture,
        MediaFrameReader reader,
        FramePipeline pipeline,
        FrameClock clock,
        ICameraPolicySet policies,
        ILogger logger,
        Action released)
    {
        DeviceId = deviceId;
        Format = format;
        _capture = capture;
        _reader = reader;
        _pipeline = pipeline;
        _clock = clock;
        _policies = policies;
        _logger = logger;
        _released = released;
    }

    public event EventHandler<CameraFailure>? Failed;

    public string DeviceId { get; }

    public CameraFormat Format { get; }

    public TimeSpan Now => _clock.Now;

    public CameraFrameInfo? LatestFrame => _pipeline.LatestFrame;

    internal FramePipeline Pipeline => _pipeline;

    internal FrameClock Clock => _clock;

    internal bool IsAlive => Volatile.Read(ref _state) == Running;

    /// <summary>Opens the device exclusively, applies the policy's format and starts the frame stream. Leaves nothing open on failure.</summary>
    public static async Task<WindowsCameraSession> OpenAsync(
        string deviceId,
        IPreviewChannel preview,
        ICameraPolicySet policies,
        ILogger logger,
        Action released,
        CancellationToken cancellationToken)
    {
        MediaCapture? capture = null;
        MediaFrameReader? reader = null;
        WindowsCameraSession? session = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            capture = new MediaCapture();
            await capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                VideoDeviceId = deviceId,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
            }).AsTask(cancellationToken).ConfigureAwait(false);

            var source = SelectColorSource(capture)
                ?? throw new CameraException(CameraFailureKind.NoSupportedFormat, "The camera has no colour video stream.");
            var supported = source.SupportedFormats.ToArray();
            var mapped = supported.Select(CameraFormatMapper.Create).ToArray();
            var chosen = policies.ChooseFormat(mapped)
                ?? throw new CameraException(CameraFailureKind.NoSupportedFormat, $"None of the camera's {mapped.Length} formats is usable.");
            var index = CameraFormatMapper.IndexOf(mapped, chosen);
            if (index < 0)
            {
                throw new CameraException(CameraFailureKind.NoSupportedFormat, "The chosen format is not one the camera offers.");
            }

            await source.SetFormatAsync(supported[index]).AsTask(cancellationToken).ConfigureAwait(false);
            CameraLog.FormatChosen(logger, deviceId, chosen.Width, chosen.Height, chosen.FramesPerSecond, chosen.Subtype);

            reader = await CreateReaderAsync(capture, source, logger).ConfigureAwait(false);
            reader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;

            var clock = new FrameClock();
            var pipeline = new FramePipeline(preview, policies, logger);
            session = new WindowsCameraSession(deviceId, chosen, capture, reader, pipeline, clock, policies, logger, released);
            capture = null;
            reader = null;

            session._capture.Failed += session.OnCaptureFailed;
            session._capture.CaptureDeviceExclusiveControlStatusChanged += session.OnExclusiveControlChanged;
            session._reader.FrameArrived += session.OnFrameArrived;

            var status = await session._reader.StartAsync().AsTask(cancellationToken).ConfigureAwait(false);
            if (CameraFormatMapper.ForStartStatus(status) is { } startFailure)
            {
                throw startFailure;
            }

            return session;
        }
        catch (OperationCanceledException)
        {
            await ReleaseAsync(session, reader, capture, logger).ConfigureAwait(false);
            throw;
        }
        catch (CameraException)
        {
            await ReleaseAsync(session, reader, capture, logger).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await ReleaseAsync(session, reader, capture, logger).ConfigureAwait(false);
            var kind = policies.Classify(exception, CameraOperation.Open);
            CameraLog.OpenFailed(logger, exception, deviceId, $"0x{exception.HResult:X8}", kind);
            throw new CameraException(kind, $"Opening the camera failed (0x{exception.HResult:X8}): {exception.Message}", exception);
        }
    }

    public void SetPreviewTarget(PreviewTarget target) => _pipeline.SetPreviewTarget(target);

    public Task<CapturedFrame> CaptureAsync(TimeSpan notBefore, CancellationToken cancellationToken) =>
        _pipeline.CaptureAsync(notBefore, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        var previous = Interlocked.Exchange(ref _state, Disposed);
        if (previous == Running)
        {
            _pipeline.Terminate(new ObjectDisposedException(nameof(WindowsCameraSession), "The camera session was disposed."));
        }

        await StartTeardown().ConfigureAwait(false);
    }

    private static MediaFrameSource? SelectColorSource(MediaCapture capture)
    {
        var colour = capture.FrameSources.Values
            .Where(source => source.Info.SourceKind == MediaFrameSourceKind.Color && source.SupportedFormats.Count > 0)
            .ToArray();
        return colour.FirstOrDefault(source => source.Info.MediaStreamType == MediaStreamType.VideoRecord)
            ?? colour.FirstOrDefault(source => source.Info.MediaStreamType == MediaStreamType.VideoPreview)
            ?? colour.FirstOrDefault();
    }

    private static async Task<MediaFrameReader> CreateReaderAsync(MediaCapture capture, MediaFrameSource source, ILogger logger)
    {
        try
        {
            return await capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8).AsTask().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or InvalidOperationException)
        {
            CameraLog.ReaderConversionFallback(logger, exception);
            return await capture.CreateFrameReaderAsync(source).AsTask().ConfigureAwait(false);
        }
    }

    private static async Task ReleaseAsync(WindowsCameraSession? session, MediaFrameReader? reader, MediaCapture? capture, ILogger logger)
    {
        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(false);
            return;
        }

        try
        {
            if (reader is not null)
            {
                try
                {
                    await reader.StopAsync().AsTask().WaitAsync(TeardownStepTimeout).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    CameraLog.CleanupStopFailed(logger, exception);
                }

                reader.Dispose();
            }
        }
        finally
        {
            capture?.Dispose();
        }
    }

    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (!Monitor.TryEnter(_frameLock))
        {
            return;
        }

        try
        {
            if (Volatile.Read(ref _state) != Running)
            {
                return;
            }

            using var frame = sender.TryAcquireLatestFrame();
            using var bitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
            if (frame is null || bitmap is null)
            {
                return;
            }

            var timestamp = _clock.Stamp(frame.SystemRelativeTime);
            _pipeline.Process(timestamp, new SoftwareBitmapFrame(bitmap, _copier));
            _frameErrors = 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            CameraLog.FrameFailed(_logger, exception, DeviceId);
            if (++_frameErrors >= MaximumConsecutiveFrameErrors)
            {
                Fail(CameraFailureKind.Failed, $"{MaximumConsecutiveFrameErrors} frames in a row could not be processed: {exception.Message}");
            }
        }
        finally
        {
            Monitor.Exit(_frameLock);
        }
    }

    private void OnCaptureFailed(MediaCapture sender, MediaCaptureFailedEventArgs args)
    {
        var hresult = unchecked((int)args.Code);
        var kind = _policies.Classify(hresult, CameraOperation.Streaming);
        CameraLog.CaptureFailed(_logger, DeviceId, $"0x{hresult:X8}", kind, args.Message);
        Fail(kind, $"MediaCapture.Failed 0x{hresult:X8}: {args.Message}");
    }

    private void OnExclusiveControlChanged(MediaCapture sender, MediaCaptureDeviceExclusiveControlStatusChangedEventArgs args)
    {
        if (args.Status == MediaCaptureDeviceExclusiveControlStatus.SharedReadOnlyAvailable)
        {
            CameraLog.ExclusiveControlLost(_logger, DeviceId);
            Fail(CameraFailureKind.ExclusiveLost, "Another app took exclusive control of the camera.");
        }
    }

    /// <summary>Marks the session dead, faults pending captures, releases the device, then raises <see cref="Failed"/> once.</summary>
    private void Fail(CameraFailureKind kind, string detail)
    {
        if (Interlocked.CompareExchange(ref _state, Failing, Running) != Running)
        {
            return;
        }

        _pipeline.Terminate(new CameraException(kind, detail));
        _ = Task.Run(async () =>
        {
            try
            {
                await StartTeardown().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                CameraLog.TeardownThrew(_logger, exception, DeviceId);
            }

            if (Interlocked.Exchange(ref _failedRaised, 1) == 0)
            {
                try
                {
                    Failed?.Invoke(this, new CameraFailure(kind, detail));
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    CameraLog.FailedHandlerThrew(_logger, exception, DeviceId);
                }
            }
        });
    }

    private Task StartTeardown()
    {
        lock (_teardownGate)
        {
            return _teardown ??= TeardownAsync();
        }
    }

    private async Task TeardownAsync()
    {
        await Task.Yield();
        try
        {
            _reader.FrameArrived -= OnFrameArrived;
            _capture.Failed -= OnCaptureFailed;
            _capture.CaptureDeviceExclusiveControlStatusChanged -= OnExclusiveControlChanged;

            try
            {
                await _reader.StopAsync().AsTask().WaitAsync(TeardownStepTimeout).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                CameraLog.ReaderStopFailed(_logger, exception, DeviceId);
            }

            // Let a frame callback that is already running finish before its objects go away.
            if (Monitor.TryEnter(_frameLock, TeardownStepTimeout))
            {
                Monitor.Exit(_frameLock);
            }

            _reader.Dispose();
        }
        finally
        {
            try
            {
                _capture.Dispose();
            }
            finally
            {
                _released();
            }
        }
    }
}
