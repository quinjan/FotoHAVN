using System.Windows;
using FotoHavn.Core;
using FotoHavn.Core.Media;
using FotoHavn.Host.Windows.Displays;
using FotoHavn.Host.Windows.Logging;
using FotoHavn.Host.Windows.Screens;
using FotoHavn.Host.Windows.TestHooks;
using FotoHavn.Host.Windows.WebView;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows;

/// <summary>
/// The composition root: options and paths, logging, the display adapter, the two windows (the brain's host services),
/// the brain on its own executor, and in E2E builds the host test hooks. Each window connects to the brain with its role.
/// </summary>
#pragma warning disable CA1001 // The App disposes its runtime in OnExit, the end of the WPF application's life.
public partial class App : Application
{
    private ILoggerFactory? _loggerFactory;
    private ILogger? _logger;
    private WindowsDisplayAdapter? _displays;
    private ScreenWindows? _windows;
    private HostBrainExecutor? _executor;
    private Brain? _brain;

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
        _executor = CoreRuntime.CreateExecutor(exception => HostLog.BrainWorkFailed(logger, exception));

        var dependencies = new BrainDependencies(_executor, CoreRuntime.CreateClock(), _displays, _windows)
        {
            Logger = _loggerFactory.CreateLogger("FotoHavn.Core.Brain"),
#if E2E_HOOKS
            HostTestHooks = new E2E.WindowsHostTestHooks(
                Dispatcher,
                _windows,
                FotoHavn.Camera.Windows.VideoCaptureDeviceList.ListAsync,
                _loggerFactory.CreateLogger("FotoHavn.Host.Windows.E2E")),
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
            EmptyPreviewSource.Instance,
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
        _executor?.Dispose();
        _displays?.Dispose();

        if (_logger is not null)
        {
            HostLog.Exited(_logger, e.ApplicationExitCode);
        }

        _loggerFactory?.Dispose();
        base.OnExit(e);
    }
}
#pragma warning restore CA1001
