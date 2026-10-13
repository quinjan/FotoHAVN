#if E2E_HOOKS
using System.Windows;
using System.Windows.Threading;
using FotoHavn.Camera.Windows;
using FotoHavn.Core.Displays;
using FotoHavn.Core.E2E;
using FotoHavn.Host.Windows.Logging;
using FotoHavn.Host.Windows.Screens;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Testing;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows.E2E;

/// <summary>
/// The host side of the test hooks (compiled only with <c>E2E_HOOKS</c>; a field build never contains this type, which
/// the publish guard and the reflection test in FotoHavn.Host.Windows.Tests enforce). The brain calls these on its
/// executor; each returns at once and does the work on the UI thread.
/// </summary>
internal sealed class WindowsHostTestHooks(
    Dispatcher dispatcher,
    ScreenWindows windows,
    Func<Task<IReadOnlyList<VideoCaptureDevice>>> listCameras,
    ILogger logger) : IHostTestHooks
{
    /// <summary>Hooks that end something wait this long, so the brain's reply reaches the page first.</summary>
    private static readonly TimeSpan ReplyDeliveryDelay = TimeSpan.FromMilliseconds(250);

    public Task<IReadOnlyList<TestWindow>> GetWindowsAsync() =>
        dispatcher.InvokeAsync<IReadOnlyList<TestWindow>>(() => [.. windows.DescribeWindows().Select(ToTestWindow)]).Task;

    public Task<TestWindow> SetWindowBoundsAsync(Role role, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return Task.FromException<TestWindow>(new TestHookException(TestHookError.InvalidArgs));
        }

        return dispatcher.InvokeAsync(() =>
            windows.SetWindowSize(role, width, height) is { } facts
                ? ToTestWindow(facts)
                : throw new TestHookException(TestHookError.HookFailed)).Task;
    }

    public bool ScheduleCrashUi(Role role)
    {
        if (!windows.HasWindow(role))
        {
            return false;
        }

        dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(ReplyDeliveryDelay);
            CrashRenderer(role);
        });
        return true;
    }

    public async Task<IReadOnlyList<TestCamera>> ListCamerasAsync()
    {
        var cameras = await listCameras();
        return [.. cameras.Select(camera => new TestCamera(camera.Id, camera.Name))];
    }

    public void ScheduleExit()
    {
        HostLog.ExitRequested(logger);
        dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(ReplyDeliveryDelay);
            Application.Current.Shutdown(0);
        });
    }

    /// <summary>
    /// Crashes only this window's renderer with the DevTools protocol's <c>Page.crash</c>. The browser process (shared
    /// by both windows) keeps running; the window's <c>ProcessFailed</c> (<c>RenderProcessExited</c>) recovery recreates
    /// its WebView2 and brain connection.
    /// </summary>
    private void CrashRenderer(Role role)
    {
        if (windows.WindowFor(role)?.CurrentCoreWebView2 is not { } core)
        {
            return;
        }

        HostLog.CrashUiRequested(logger, role);
        // The call never completes normally (its renderer is gone); observe whatever it ends with.
        _ = core.CallDevToolsProtocolMethodAsync("Page.crash", "{}").ContinueWith(
            task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static TestWindow ToTestWindow(WindowFacts facts) =>
        new(facts.Role, ToPixelRect(facts.Bounds), facts.DisplayId);

    private static PixelRect ToPixelRect(DisplayRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
#endif
