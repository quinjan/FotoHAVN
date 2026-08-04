using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage;

namespace FotoHavn.CameraRigPrototype;

public sealed partial class MainWindow : Window
{
    private static readonly string[] IdentityProperties =
    [
        "System.Devices.DeviceInstanceId",
        "System.Devices.ContainerId"
    ];

    private readonly DispatcherQueue _dispatcher;
    private readonly List<string> _eventLines = [];
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private CameraProbeState _state = CameraProbeState.Initial;
    private IReadOnlyList<DeviceInformation> _devices = [];
    private DeviceWatcher? _watcher;
    private MediaCapture? _mediaCapture;
    private MediaFrameReader? _frameReader;
    private SoftwareBitmapSource? _previewSource;
    private ProbeReport? _report;
    private StorageFolder? _runFolder;
    private int _frameUpdatePending;
    private bool _receivedFirstFrame;

    private string PrototypeRoot
    {
        get
        {
            var projectDirectory = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            return File.Exists(Path.Combine(projectDirectory, "CameraRigPrototype.csproj"))
                ? projectDirectory
                : AppContext.BaseDirectory;
        }
    }

    private string BindingPath => Path.Combine(PrototypeRoot, "prototype-camera-binding.json");

    public MainWindow()
    {
        InitializeComponent();
        _dispatcher = DispatcherQueue;
        Closed += MainWindow_Closed;
        RenderState();
        _ = StartAsync();
    }

    private async Task StartAsync()
    {
        await RefreshDevicesAsync();
        StartWatcher();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDevicesAsync();
    }

    private async Task RefreshDevicesAsync()
    {
        try
        {
            var selector = MediaDevice.GetVideoCaptureSelector();
            var found = await DeviceInformation.FindAllAsync(selector, IdentityProperties);
            _devices = found.OrderBy(device => device.Name).ToList();
            CameraPicker.ItemsSource = _devices;

            var saved = await LoadBindingAsync();
            var exact = saved is null
                ? null
                : _devices.SingleOrDefault(device => device.Id == saved.VideoDeviceInterfaceId);

            if (exact is not null)
            {
                CameraPicker.SelectedItem = exact;
                LogEvent($"Exact saved binding resolved: {exact.Name}");
            }
            else if (saved is not null)
            {
                LogEvent("Saved interface ID is absent; re-selection required (no silent substitution)");
            }

            LogEvent($"Enumeration found {_devices.Count} Windows video-capture device(s)");
        }
        catch (Exception exception)
        {
            Fail(exception, "enumeration");
        }
    }

    private async void CameraPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CameraPicker.SelectedItem is not DeviceInformation selected)
        {
            return;
        }

        await DisposeCaptureSessionAsync();
        _state = CameraProbeReducer.Apply(_state, new DeviceChosen(selected.Id, selected.Name));
        await SaveBindingAsync(ToIdentity(selected));
        LogEvent($"Selected {selected.Name} [{selected.Id}]");
        RenderState();
    }

    private async void Initialize_Click(object sender, RoutedEventArgs e)
    {
        if (CameraPicker.SelectedItem is not DeviceInformation selected)
        {
            LogEvent("Choose a camera before initialization");
            return;
        }

        InitializeButton.IsEnabled = false;
        CaptureButton.IsEnabled = false;
        PreviewLabel.Visibility = Visibility.Visible;
        PreviewLabel.Text = "Opening camera…";
        _state = CameraProbeReducer.Apply(_state, new InitializationStarted());
        RenderState();

        try
        {
            await DisposeCaptureSessionAsync();

            _mediaCapture = new MediaCapture();
            _mediaCapture.Failed += MediaCapture_Failed;

            var settings = new MediaCaptureInitializationSettings
            {
                VideoDeviceId = selected.Id,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };

            await _mediaCapture.InitializeAsync(settings);
            _mediaCapture.CaptureDeviceExclusiveControlStatusChanged += MediaCapture_ExclusiveControlChanged;

            var previewProperties = _mediaCapture.VideoDeviceController
                .GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview);
            var photoProperties = _mediaCapture.VideoDeviceController
                .GetAvailableMediaStreamProperties(MediaStreamType.Photo);
            var verdict = CameraCapability.Evaluate(previewProperties, photoProperties);

            CapabilitiesText.Text = string.Join(Environment.NewLine,
                verdict.Observed
                    .OrderBy(value => value.Stream)
                    .ThenByDescending(value => (long)value.Width * value.Height)
                    .Select(FormatCapability));

            if (!verdict.PreviewPassed || !verdict.PhotoPassed
                || verdict.SelectedPreview is null || verdict.SelectedPhoto is null)
            {
                throw new InvalidOperationException(
                    $"Capability gate failed: preview={verdict.PreviewPassed}, photo={verdict.PhotoPassed}");
            }

            var selectedPreviewProperty = FindProperty(previewProperties, verdict.SelectedPreview);
            var selectedPhotoProperty = FindProperty(photoProperties, verdict.SelectedPhoto);
            await _mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(
                MediaStreamType.VideoPreview,
                selectedPreviewProperty);
            await _mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(
                MediaStreamType.Photo,
                selectedPhotoProperty);

            var runId = $"{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Slug(selected.Name)}";
            var evidenceRoot = Path.Combine(PrototypeRoot, "evidence");
            Directory.CreateDirectory(evidenceRoot);
            _runFolder = await StorageFolder.GetFolderFromPathAsync(
                Directory.CreateDirectory(Path.Combine(evidenceRoot, runId)).FullName);
            var storageWritable = await ProbeStorageAsync(_runFolder);

            _report = new ProbeReport
            {
                RunId = runId,
                StartedAt = DateTimeOffset.Now,
                WindowsVersion = Environment.OSVersion.VersionString,
                Camera = ToIdentity(selected),
                ObservedCapabilities = verdict.Observed,
                SelectedPreview = verdict.SelectedPreview,
                SelectedPhoto = verdict.SelectedPhoto,
                MinimumCapabilityContract = "preview >=640x480 @15fps; photo >=1280x720; exact identity; exclusive initialization; fresh color frame; writable storage",
                PreviewMirroredByUiTransform = true
            };

            _state = CameraProbeReducer.Apply(
                _state,
                new InitializationMeasured(verdict.PreviewPassed, verdict.PhotoPassed, storageWritable));
            RenderState();

            var colorSource = _mediaCapture.FrameSources.Values
                .FirstOrDefault(source => source.Info.SourceKind == MediaFrameSourceKind.Color)
                ?? throw new InvalidOperationException("No decoded color MediaFrameSource was exposed");

            _frameReader = await _mediaCapture.CreateFrameReaderAsync(colorSource);
            _frameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
            _frameReader.FrameArrived += FrameReader_FrameArrived;
            _receivedFirstFrame = false;

            var startStatus = await _frameReader.StartAsync();
            if (startStatus != MediaFrameReaderStartStatus.Success)
            {
                throw new InvalidOperationException($"Preview reader did not start: {startStatus}");
            }

            LogEvent($"Exclusive MediaCapture initialized for {selected.Name}");
            await SaveReportAsync();
        }
        catch (UnauthorizedAccessException exception)
        {
            Fail(exception, "camera privacy access; check ms-settings:privacy-webcam");
        }
        catch (Exception exception)
        {
            Fail(exception, "initialization");
        }
        finally
        {
            InitializeButton.IsEnabled = true;
        }
    }

    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaCapture is null || _runFolder is null || _report is null || !_state.Checks.AllPassed)
        {
            return;
        }

        CaptureButton.IsEnabled = false;
        InitializeButton.IsEnabled = false;
        _state = CameraProbeReducer.Apply(_state, new CaptureSequenceStarted());
        RenderState();

        try
        {
            for (var number = 1; number <= 4; number++)
            {
                var startedAt = DateTimeOffset.Now;
                var stopwatch = Stopwatch.StartNew();
                var fileName = $"capture-{number:00}.jpg";
                var file = await _runFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                var encoding = ImageEncodingProperties.CreateJpeg();
                encoding.Width = _report.SelectedPhoto.Width;
                encoding.Height = _report.SelectedPhoto.Height;

                await _mediaCapture.CapturePhotoToStorageFileAsync(encoding, file);
                stopwatch.Stop();

                var bytes = new FileInfo(file.Path).Length;
                var hash = await Sha256Async(file.Path);
                _report.Captures.Add(new CaptureEvidence(
                    number,
                    fileName,
                    hash,
                    bytes,
                    startedAt,
                    DateTimeOffset.Now,
                    stopwatch.ElapsedMilliseconds,
                    "none (camera stream saved unmirrored)"));

                _state = CameraProbeReducer.Apply(_state, new CaptureSaved(number));
                LogEvent($"Saved {fileName}: {bytes:N0} bytes, {stopwatch.ElapsedMilliseconds} ms, sha256 {hash[..12]}…");
                RenderState();
                await SaveReportAsync();
            }

            _report.Verdict = "Automated gates passed; awaiting human check of preview mirroring, Capture orientation/quality, and physical disconnect/reconnect.";
            await SaveReportAsync();
        }
        catch (Exception exception)
        {
            Fail(exception, "four-Capture sequence");
        }
        finally
        {
            InitializeButton.IsEnabled = true;
            CaptureButton.IsEnabled = _state.Checks.AllPassed;
        }
    }

    private void StartWatcher()
    {
        _watcher = DeviceInformation.CreateWatcher(MediaDevice.GetVideoCaptureSelector(), IdentityProperties);
        _watcher.Added += Watcher_Added;
        _watcher.Updated += Watcher_Updated;
        _watcher.Removed += Watcher_Removed;
        _watcher.EnumerationCompleted += Watcher_EnumerationCompleted;
        _watcher.Start();
    }

    private void Watcher_Added(DeviceWatcher sender, DeviceInformation device)
    {
        QueueWatcherEvent($"ADDED {device.Name} [{device.Id}]");
    }

    private void Watcher_Updated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        QueueWatcherEvent($"UPDATED [{update.Id}]");
    }

    private void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        QueueWatcherEvent($"REMOVED [{update.Id}]");

        if (update.Id == _state.SelectedDeviceId)
        {
            _dispatcher.TryEnqueue(async () =>
            {
                _state = CameraProbeReducer.Apply(_state, new SelectedDeviceRemoved());
                CaptureButton.IsEnabled = false;
                PreviewLabel.Text = "Camera disconnected — reconnect, then Initialize";
                PreviewLabel.Visibility = Visibility.Visible;
                await DisposeCaptureSessionAsync();
                RenderState();
                await SaveReportAsync();
            });
        }
    }

    private void Watcher_EnumerationCompleted(DeviceWatcher sender, object args)
    {
        QueueWatcherEvent("WATCHER ENUMERATION COMPLETE");
    }

    private void FrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (Interlocked.Exchange(ref _frameUpdatePending, 1) == 1)
        {
            return;
        }

        using var frame = sender.TryAcquireLatestFrame();
        var sourceBitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
        if (sourceBitmap is null)
        {
            Interlocked.Exchange(ref _frameUpdatePending, 0);
            return;
        }

        var displayBitmap = SoftwareBitmap.Convert(
            sourceBitmap,
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);

        _dispatcher.TryEnqueue(async () =>
        {
            try
            {
                _previewSource ??= new SoftwareBitmapSource();
                await _previewSource.SetBitmapAsync(displayBitmap);
                PreviewImage.Source = _previewSource;
                PreviewLabel.Visibility = Visibility.Collapsed;

                if (!_receivedFirstFrame)
                {
                    _receivedFirstFrame = true;
                    _state = CameraProbeReducer.Apply(_state, new PreviewFrameReceived());
                    if (_report is not null)
                    {
                        _report.FirstPreviewFrameAt = DateTimeOffset.Now;
                    }
                    CaptureButton.IsEnabled = _state.Checks.AllPassed;
                    LogEvent("Fresh preview frame received; UI mirror transform active");
                    RenderState();
                    await SaveReportAsync();
                }
            }
            finally
            {
                displayBitmap.Dispose();
                Interlocked.Exchange(ref _frameUpdatePending, 0);
            }
        });
    }

    private void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
    {
        _dispatcher.TryEnqueue(() =>
            Fail(new InvalidOperationException($"0x{errorEventArgs.Code:X8}: {errorEventArgs.Message}"), "MediaCapture failure event"));
    }

    private void MediaCapture_ExclusiveControlChanged(MediaCapture sender, MediaCaptureDeviceExclusiveControlStatusChangedEventArgs args)
    {
        QueueWatcherEvent($"EXCLUSIVE CONTROL {args.Status}");
        if (args.Status != MediaCaptureDeviceExclusiveControlStatus.ExclusiveControlAvailable)
        {
            _dispatcher.TryEnqueue(() =>
                Fail(new InvalidOperationException(args.Status.ToString()), "exclusive-control status"));
        }
    }

    private void QueueWatcherEvent(string message)
    {
        _dispatcher.TryEnqueue(() =>
        {
            LogEvent(message);
            _ = SaveReportAsync();
            _ = RefreshDevicesAsync();
        });
    }

    private async Task DisposeCaptureSessionAsync()
    {
        if (_frameReader is not null)
        {
            _frameReader.FrameArrived -= FrameReader_FrameArrived;
            await _frameReader.StopAsync();
            _frameReader.Dispose();
            _frameReader = null;
        }

        if (_mediaCapture is not null)
        {
            _mediaCapture.Failed -= MediaCapture_Failed;
            _mediaCapture.CaptureDeviceExclusiveControlStatusChanged -= MediaCapture_ExclusiveControlChanged;
            _mediaCapture.Dispose();
            _mediaCapture = null;
        }

        PreviewImage.Source = null;
        _previewSource = null;
        _receivedFirstFrame = false;
    }

    private async void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_watcher is not null && _watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
        {
            _watcher.Stop();
        }

        await DisposeCaptureSessionAsync();
    }

    private void RenderState()
    {
        StateText.Text = $"phase: {_state.Phase}\n"
            + $"camera: {_state.SelectedDisplayName ?? "—"}\n"
            + $"exact identity: {Mark(_state.Checks.ExactIdentityResolved)}\n"
            + $"exclusive init: {Mark(_state.Checks.ExclusiveInitialization)}\n"
            + $"preview capability: {Mark(_state.Checks.PreviewCapability)}\n"
            + $"photo capability: {Mark(_state.Checks.PhotoCapability)}\n"
            + $"fresh preview: {Mark(_state.Checks.FreshPreviewFrame)}\n"
            + $"storage writable: {Mark(_state.Checks.StorageWritable)}\n"
            + $"Captures: {_state.CompletedCaptures}/4\n"
            + $"status: {_state.Status}"
            + (_state.Error is null ? string.Empty : $"\nerror: {_state.Error}");

        EventsText.Text = string.Join(Environment.NewLine, _eventLines.TakeLast(40));
    }

    private void LogEvent(string message)
    {
        var line = $"{DateTimeOffset.Now:HH:mm:ss.fff} {message}";
        _eventLines.Add(line);
        if (_report is not null)
        {
            _report.WatcherEvents.Add(line);
        }
        RenderState();
    }

    private void Fail(Exception exception, string operation)
    {
        _state = CameraProbeReducer.Apply(_state, new ProbeFailed($"{operation}: {exception.Message}"));
        CaptureButton.IsEnabled = false;
        PreviewLabel.Text = "Probe failed — see state";
        PreviewLabel.Visibility = Visibility.Visible;
        LogEvent($"FAILED {operation}: {exception.GetType().Name} — {exception.Message}");
        _ = SaveReportAsync();
    }

    private async Task SaveReportAsync()
    {
        if (_report is null || _runFolder is null)
        {
            return;
        }

        try
        {
            var path = Path.Combine(_runFolder.Path, "probe-report.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(_report, _jsonOptions));
        }
        catch
        {
            // This is a disposable evidence writer; the visible storage gate already reports failures.
        }
    }

    private async Task<CameraIdentity?> LoadBindingAsync()
    {
        if (!File.Exists(BindingPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CameraIdentity>(await File.ReadAllTextAsync(BindingPath));
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveBindingAsync(CameraIdentity identity)
    {
        await File.WriteAllTextAsync(BindingPath, JsonSerializer.Serialize(identity, _jsonOptions));
    }

    private static CameraIdentity ToIdentity(DeviceInformation device) => new(
        device.Id,
        device.Name,
        PropertyText(device, "System.Devices.DeviceInstanceId"),
        PropertyText(device, "System.Devices.ContainerId"));

    private static string? PropertyText(DeviceInformation device, string key) =>
        device.Properties.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static IMediaEncodingProperties FindProperty(
        IEnumerable<IMediaEncodingProperties> available,
        StreamCapability selected) => available.First(properties =>
        {
            var candidate = StreamCapability.From(selected.Stream, properties);
            return candidate.Subtype == selected.Subtype
                && candidate.Width == selected.Width
                && candidate.Height == selected.Height
                && Math.Abs(candidate.FramesPerSecond - selected.FramesPerSecond) < 0.01;
        });

    private static async Task<bool> ProbeStorageAsync(StorageFolder folder)
    {
        try
        {
            var file = await folder.CreateFileAsync(".write-probe", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, "FotoHAVN prototype storage probe");
            await file.DeleteAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> Sha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        var digest = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static string FormatCapability(StreamCapability value) =>
        $"{value.Stream,-7} {value.Width,4}×{value.Height,-4} {value.FramesPerSecond,5:0.##} fps {value.Subtype}";

    private static string Slug(string value) => string.Concat(value
        .ToLowerInvariant()
        .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-'))
        .Trim('-');

    private static string Mark(bool passed) => passed ? "PASS" : "—";
}
