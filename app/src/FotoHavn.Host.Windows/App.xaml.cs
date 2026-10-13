using System.Windows;
using FotoHavn.Host.Windows.Logging;
using FotoHavn.Host.Windows.TestHooks;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows;

public partial class App : Application
{
    private ILoggerFactory? _loggerFactory;
    private ILogger? _logger;

    protected override void OnStartup(StartupEventArgs e)
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

        var paths = HostPaths.Create(AppContext.BaseDirectory, options.DataFolder);
        paths.EnsureCreated();

        _loggerFactory = LoggerFactory.Create(logging => logging
            .SetMinimumLevel(LogLevel.Information)
            .AddProvider(new JsonLinesLoggerProvider(paths.LogFile)));
        _logger = _loggerFactory.CreateLogger("FotoHavn.Host.Windows");

        var version = typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown";
        HostLog.Started(_logger, version, paths.DataFolder, TestHookChannel.IsCompiled);

        var window = new MainWindow(options, paths, _loggerFactory.CreateLogger("FotoHavn.Host.Windows.MainWindow"));
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_logger is not null)
        {
            HostLog.Exited(_logger, e.ApplicationExitCode);
        }

        _loggerFactory?.Dispose();
        base.OnExit(e);
    }
}
