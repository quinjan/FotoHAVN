using System.Windows;
using FotoHavn.Camera.Windows;
using FotoHavn.Catalog;
using FotoHavn.Core;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Catalog;
using FotoHavn.Core.Hardening;
#if E2E_HOOKS
using FotoHavn.Core.Hooks;
#endif
using FotoHavn.Core.Media;
using FotoHavn.Host.Windows.Displays;
using FotoHavn.Host.Windows.Hardening;
using FotoHavn.Host.Windows.Interop;
using FotoHavn.Host.Windows.Logging;
using FotoHavn.Host.Windows.Preview;
using FotoHavn.Host.Windows.Screens;
using FotoHavn.Host.Windows.TestHooks;
using FotoHavn.Host.Windows.WebView;
using FotoHavn.Storage;
using FotoHavn.Storage.DataPath;
using FotoHavn.Storage.FileSystem;
using FotoHavn.Storage.Reconciliation;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows;

/// <summary>
/// The composition root: options and paths, the single-instance check, logging, the display adapter, the two windows (the brain's host
/// services), the brain on its own executor, the wake lock, the close handling and input guards, and in E2E builds the host test hooks.
/// Each window connects to the brain with its role.
/// </summary>
#pragma warning disable CA1001 // The App disposes its runtime in OnExit, the end of the WPF application's life.
public partial class App : Application
{
    private static readonly TimeSpan StoreDisposeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ActivationSignalPatience = TimeSpan.FromSeconds(2);

    private SingleInstanceGuard? _instance;
    private ILoggerFactory? _loggerFactory;
    private ILogger? _logger;
    private WindowsDisplayAdapter? _displays;
    private ScreenWindows? _windows;
    private DedicatedThreadBrainExecutor? _executor;
    private Brain? _brain;
    private WindowsCameraAdapter? _cameraAdapter;
    private SqliteEventStore? _store;
    private WindowsWakeLock? _wakeLock;
    private WindowCloseController? _closeController;
    private GuestInputGuard? _inputGuard;
    private SmokeRun? _smoke;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        HostOptions options;
        try
        {
            options = HostOptions.Parse(e.Args);
        }
        catch (HostOptionsException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Shutdown(HostExitCodes.InvalidArguments);
            return;
        }

        var paths = HostPaths.Create(AppContext.BaseDirectory, options.DataFolder, options.WebView2RuntimeFolder);

        // One FOTOHAVN per Data folder, decided before any window, log file, or brain exists. A second launch only signals the first and leaves.
        var names = SingleInstancePolicy.NamesFor(paths.DataFolder);
        _instance = SingleInstanceGuard.Acquire(names);
        if (_instance.Decision == SingleInstanceDecision.ActivateExisting)
        {
            // The person just launched this process, so it may hand the foreground to the first instance.
            _ = NativeMethods.AllowSetForegroundWindow(NativeMethods.AsfwAny);
            _ = SingleInstanceGuard.SignalExisting(names, ActivationSignalPatience);
            Shutdown(HostExitCodes.AlreadyRunning);
            return;
        }

        paths.EnsureCreated();

        _loggerFactory = LoggerFactory.Create(logging => logging
            .SetMinimumLevel(LogLevel.Information)
            .AddProvider(new JsonLinesLoggerProvider(paths.LogFile, paths.DataFolder)));
        _logger = _loggerFactory.CreateLogger("FotoHavn.Host.Windows");
        var logger = _logger;

        var version = typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown";
        HostLog.Started(logger, version, paths.DataFolder, TestHookChannel.IsCompiled);
        HostLog.WebViewEnvironment(logger, paths.WebView2RuntimeFolder ?? "Evergreen", paths.WebViewUserDataFolder);

        _instance.ListenForActivation(() => Dispatcher.InvokeAsync(ActivateOperatorWindow));

        _displays = new WindowsDisplayAdapter(_loggerFactory.CreateLogger("FotoHavn.Host.Windows.Displays"));
        _windows = new ScreenWindows(
            Dispatcher,
            _displays,
            _loggerFactory.CreateLogger("FotoHavn.Host.Windows.Screens"),
            new FolderOpener(paths.DataFolder, HostBuild.LaunchesExplorer, _loggerFactory.CreateLogger("FotoHavn.Host.Windows.Folders")));
        _executor = new DedicatedThreadBrainExecutor(exception => HostLog.BrainWorkFailed(logger, exception));

        IStorageFileSystem fileSystem = new PhysicalStorageFileSystem(paths.DataFolder);
#if E2E_HOOKS
        var faults = new FaultInjectingStorageFileSystem(fileSystem);
        fileSystem = faults;
        var storageHooks = new E2E.WindowsStorageTestHooks(faults);
        if (options.E2EStorageFault is { } startupFault)
        {
            // Before the brain exists, so the store's first write (OpenAsync) fails.
            storageHooks.InjectAtStartup(startupFault);
        }
#endif
        _store = new SqliteEventStore(paths.DataFolder, SystemClock.Instance, fileSystem, new WindowsDataPathProbe(), new StartupReconciler(SystemClock.Instance));
        var frames = new SharedBufferPreviewChannel(
            work => Dispatcher.InvokeAsync(work),
            TimeProvider.System,
            _loggerFactory.CreateLogger("FotoHavn.Host.Windows.Preview"));
        var cameraLogger = _loggerFactory.CreateLogger("FotoHavn.Camera.Windows");
        _cameraAdapter = new WindowsCameraAdapter(frames, cameraLogger);
        ICameraAdapter camera = _cameraAdapter;
#if E2E_HOOKS
        var cameraFaults = new FaultInjectingCameraAdapter(_cameraAdapter);
        camera = cameraFaults;
#endif
        var previewCache = new PreviewCache();

        var closeRequests = new BrainCloseRequests();
        _closeController = new WindowCloseController(closeRequests, work => Dispatcher.InvokeAsync(work))
        {
            OperatorOutcome = outcome =>
            {
                // The brain asked "End the running Event and close FOTOHAVN?": the dialog is in the Operator window, so bring it forward.
                if (outcome == CloseRequestOutcome.AskToEndEvent)
                {
                    _windows?.ActivateOperator();
                }
            },
        };
        _inputGuard = new GuestInputGuard(
            new LowLevelKeyboardHook(),
            () => closeRequests.IsEventActive,
            NativeMethods.GetForegroundWindow,
            key => (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0);
        _wakeLock = new WindowsWakeLock(new NativePowerRequestApi(), _loggerFactory.CreateLogger("FotoHavn.Host.Windows.Hardening"));

        var dependencies = new BrainDependencies(_executor, ClockComposition.ForBuild(SystemClock.Instance), _displays, camera, _windows, _store, new CatalogData(), new SkiaCompositor(), previewCache, _wakeLock)
        {
            Logger = _loggerFactory.CreateLogger("FotoHavn.Core.Brain"),
            AppLifetime = new WindowsAppLifetime(_closeController, work => Dispatcher.InvokeAsync(work), code => Shutdown(code)),
            SingleInstance = names,
            PreviewStats = frames,
            CaptureEncoder = new Diagnostics.HostCaptureEncoder(),
#if E2E_HOOKS
            CameraFaults = cameraFaults,
            HostTestHooks = new E2E.WindowsHostTestHooks(
                Dispatcher,
                _windows,
                paths.DataFolder,
                _loggerFactory.CreateLogger("FotoHavn.Host.Windows.E2E"),
                ExitWith),
            StorageTestHooks = storageHooks,
#endif
        };
        _brain = new Brain(dependencies);
        closeRequests.Brain = _brain;

        if (options.Smoke)
        {
            _smoke = new SmokeRun(
                code =>
                {
                    if (code == HostExitCodes.Success)
                    {
                        HostLog.SmokeReady(logger);
                    }
                    else
                    {
                        HostLog.SmokeTimedOut(logger, (long)SmokeRun.DefaultTimeout.TotalMilliseconds);
                    }

                    ExitWith(code);
                },
                SmokeRun.DefaultTimeout);
        }

        var environments = new WebViewEnvironmentProvider(
            paths.WebViewUserDataFolder,
            paths.WebView2RuntimeFolder,
            WebViewSettings.AdditionalBrowserArguments(options));
        var services = new BoothWindowServices(
            _brain,
            environments,
            paths,
            previewCache,
            frames,
            TimeProvider.System,
            _loggerFactory.CreateLogger("FotoHavn.Host.Windows.WebView"),
            _closeController,
            _inputGuard,
            role =>
            {
                if (role == FotoHavn.Protocol.Role.Operator)
                {
                    _smoke?.OperatorReady();
                }
            });

        try
        {
            await _windows.StartAsync(services);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.WebViewFailed(logger, FotoHavn.Protocol.Role.Operator, exception);
            ExitWith(HostExitCodes.Failure);
        }
    }

    /// <summary>Windows is logging off or shutting down: the Event ends and the app closes without asking, within a bounded wait. The session end is never cancelled.</summary>
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        if (_closeController is { } controller && _logger is { } logger)
        {
            var finished = controller.OnSessionEnding();
            HostLog.SessionEnding(logger, e.ReasonSessionEnding, finished ? "finished" : "did not finish");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _windows?.CloseOverlays();
        _windows?.CloseGuest();
        _inputGuard?.Dispose();
        _smoke?.Dispose();
        _brain?.Dispose();
        _wakeLock?.Release();
        _cameraAdapter?.Dispose();
        _executor?.Dispose();
        DisposeStore();
        _displays?.Dispose();

        if (_logger is not null)
        {
            HostLog.Exited(_logger, e.ApplicationExitCode);
        }

        _loggerFactory?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Ends the app with <paramref name="exitCode"/> from any thread: every window may close (no confirmation), then the application shuts down on the UI thread.</summary>
    private void ExitWith(int exitCode)
    {
        _closeController?.AllowClose();
        Dispatcher.InvokeAsync(() => Shutdown(exitCode));
    }

    /// <summary>A second launch asked for this window (UI thread).</summary>
    private void ActivateOperatorWindow()
    {
        var foreground = _windows?.ActivateOperator() ?? false;
        if (_logger is not null)
        {
            HostLog.InstanceActivated(_logger, foreground ? "foreground" : "raised");
        }
    }

    /// <summary>Disposes the store after the brain; its background deletion gets a bounded moment to finish (the reconciler completes the rest at the next start).</summary>
    private void DisposeStore()
    {
        if (_store is null)
        {
            return;
        }

        var disposing = Task.Run(() => _store.DisposeAsync().AsTask());
        if (!disposing.Wait(StoreDisposeTimeout) && _logger is not null)
        {
            HostLog.StoreDisposeTimedOut(_logger);
        }
    }
}
#pragma warning restore CA1001
