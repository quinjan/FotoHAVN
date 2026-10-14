using System.Windows;
using FotoHavn.Camera.Windows;
using FotoHavn.Catalog;
using FotoHavn.Core;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Catalog;
#if E2E_HOOKS
using FotoHavn.Core.Hooks;
#endif
using FotoHavn.Core.Media;
using FotoHavn.Host.Windows.Displays;
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
/// The composition root: options and paths, logging, the display adapter, the two windows (the brain's host services),
/// the brain on its own executor, and in E2E builds the host test hooks. Each window connects to the brain with its role.
/// </summary>
#pragma warning disable CA1001 // The App disposes its runtime in OnExit, the end of the WPF application's life.
public partial class App : Application
{
    private static readonly TimeSpan StoreDisposeTimeout = TimeSpan.FromSeconds(2);

    private ILoggerFactory? _loggerFactory;
    private ILogger? _logger;
    private WindowsDisplayAdapter? _displays;
    private ScreenWindows? _windows;
    private DedicatedThreadBrainExecutor? _executor;
    private Brain? _brain;
    private WindowsCameraAdapter? _cameraAdapter;
    private SqliteEventStore? _store;

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
            Shutdown(2);
            return;
        }

        var paths = HostPaths.Create(AppContext.BaseDirectory, options.DataFolder, options.WebView2RuntimeFolder);
        paths.EnsureCreated();

        _loggerFactory = LoggerFactory.Create(logging => logging
            .SetMinimumLevel(LogLevel.Information)
            .AddProvider(new JsonLinesLoggerProvider(paths.LogFile)));
        _logger = _loggerFactory.CreateLogger("FotoHavn.Host.Windows");
        var logger = _logger;

        var version = typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown";
        HostLog.Started(logger, version, paths.DataFolder, TestHookChannel.IsCompiled);
        HostLog.WebViewEnvironment(logger, paths.WebView2RuntimeFolder ?? "Evergreen", paths.WebViewUserDataFolder);

        _displays = new WindowsDisplayAdapter(_loggerFactory.CreateLogger("FotoHavn.Host.Windows.Displays"));
        _windows = new ScreenWindows(Dispatcher, _displays, _loggerFactory.CreateLogger("FotoHavn.Host.Windows.Screens"));
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
        var dependencies = new BrainDependencies(_executor, SystemClock.Instance, _displays, camera, _windows, _store, new CatalogData(), new SkiaCompositor(), previewCache)
        {
            Logger = _loggerFactory.CreateLogger("FotoHavn.Core.Brain"),
            PreviewStats = frames,
#if E2E_HOOKS
            CameraFaults = cameraFaults,
            HostTestHooks = new E2E.WindowsHostTestHooks(
                Dispatcher,
                _windows,
                paths.DataFolder,
                _loggerFactory.CreateLogger("FotoHavn.Host.Windows.E2E")),
            StorageTestHooks = storageHooks,
#endif
        };
        _brain = new Brain(dependencies);

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
            _loggerFactory.CreateLogger("FotoHavn.Host.Windows.WebView"));

        try
        {
            await _windows.StartAsync(services);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.WebViewFailed(logger, FotoHavn.Protocol.Role.Operator, exception);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _windows?.CloseGuest();
        _brain?.Dispose();
        _cameraAdapter?.Dispose();
        _executor?.Dispose();
        DisposeStore();
        _displays?.Dispose();

        if (_logger is not null)
        {
            HostLog.Exited(_logger, e.ApplicationExitCode);
        }

        _loggerFactory?.Dispose();
        base.OnExit(e);
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
