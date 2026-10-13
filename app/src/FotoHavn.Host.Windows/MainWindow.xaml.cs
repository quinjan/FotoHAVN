using System.Windows;
using FotoHavn.Host.Windows.Bridge;
using FotoHavn.Host.Windows.Logging;
using FotoHavn.Host.Windows.TestHooks;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace FotoHavn.Host.Windows;

/// <summary>M1 placeholder window: one maximized WebView2 that loads the UI and signals ready.</summary>
public partial class MainWindow : Window
{
    private static readonly TimeSpan ReplyDeliveryDelay = TimeSpan.FromMilliseconds(250);

    private readonly HostOptions _options;
    private readonly HostPaths _paths;
    private readonly ILogger _logger;
    private readonly WebMessageRouter _router;

    internal MainWindow(HostOptions options, HostPaths paths, ILogger logger)
    {
        _options = options;
        _paths = paths;
        _logger = logger;
        _router = new WebMessageRouter(TestHookChannel.Create(), () => HostLog.UiReady(_logger));

        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await InitializeWebViewAsync();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.WebViewFailed(_logger, exception);
            Application.Current.Shutdown(1);
        }
    }

    private async Task InitializeWebViewAsync()
    {
        var environmentOptions = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = WebViewSettings.AdditionalBrowserArguments(_options),
        };
        var environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: _paths.WebViewUserDataFolder,
            options: environmentOptions);
        await WebView.EnsureCoreWebView2Async(environment);

        var core = WebView.CoreWebView2;
        core.WebMessageReceived += OnWebMessageReceived;

        if (File.Exists(_paths.UiIndexFile))
        {
            core.SetVirtualHostNameToFolderMapping(
                WebViewSettings.AppHostName,
                _paths.WebRoot,
                CoreWebView2HostResourceAccessKind.Deny);
            core.Navigate(WebViewSettings.StartUri.AbsoluteUri);
        }
        else
        {
            HostLog.UiNotBuilt(_logger, _paths.UiIndexFile);
            core.NavigateToString(FallbackPage.Html);
        }
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!WebViewSettings.IsAppOrigin(e.Source))
        {
            HostLog.WebMessageRejected(_logger, e.Source);
            return;
        }

        var outcome = await _router.HandleAsync(e.WebMessageAsJson);
        if (outcome.ReplyJson is not null)
        {
            WebView.CoreWebView2?.PostWebMessageAsJson(outcome.ReplyJson);
        }

        if (outcome.ExitRequested)
        {
            HostLog.ExitRequested(_logger);
            await Task.Delay(ReplyDeliveryDelay);
            Application.Current.Shutdown(0);
        }
    }
}
