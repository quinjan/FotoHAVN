#if E2E_HOOKS
using FotoHavn.Protocol;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.E2E;

/// <summary>
/// The host side of the test hooks (compiled only with <c>E2E_HOOKS</c>). The brain receives every <c>test</c> message,
/// runs the brain hooks itself, and forwards the host hooks here. Called on the brain's executor thread; methods return
/// at once and finish on the host's UI thread. The brain posts each task's completion back to its executor and then
/// answers the page. A method throws <see cref="TestHookException"/> to fail with a specific
/// <see cref="TestHookError"/>; any other exception fails the hook with <see cref="TestHookError.HookFailed"/>.
/// </summary>
public interface IHostTestHooks
{
    /// <summary>The open windows with their roles and bounds, for <c>test.getDiagnostics</c>. The headless host returns none.</summary>
    Task<IReadOnlyList<TestWindow>> GetWindowsAsync();

    /// <summary>
    /// <c>test.setWindowBounds</c>: resizes the role's window to <paramref name="width"/> × <paramref name="height"/>
    /// physical pixels on its current display, keeping its top-left corner, and returns its new bounds. Throws
    /// <see cref="TestHookException"/> with <see cref="TestHookError.HookFailed"/> when no window has the role, or
    /// <see cref="TestHookError.NotSupported"/> in the headless host.
    /// </summary>
    Task<TestWindow> SetWindowBoundsAsync(Role role, int width, int height);

    /// <summary>
    /// <c>test.crashUi</c>: returns false when no window has the role. Otherwise it schedules the end of that window's
    /// WebView2 browser process at least 250 ms later and returns true, so the brain's reply reaches the page first.
    /// The headless host aborts that role's WebSockets instead.
    /// </summary>
    bool ScheduleCrashUi(Role role);

    /// <summary><c>test.listCameras</c>: the Available Cameras. The headless host lists its fake camera.</summary>
    Task<IReadOnlyList<TestCamera>> ListCamerasAsync();

    /// <summary><c>test.exit</c>: shuts the app down with exit code 0 at least 250 ms later, after the brain's reply.</summary>
    void ScheduleExit();
}

/// <summary>Fails a host hook with a specific error.</summary>
public sealed class TestHookException : Exception
{
    public TestHookException()
        : this(TestHookError.HookFailed)
    {
    }

    public TestHookException(string message)
        : base(message)
    {
        Error = TestHookError.HookFailed;
    }

    public TestHookException(string message, Exception innerException)
        : base(message, innerException)
    {
        Error = TestHookError.HookFailed;
    }

    public TestHookException(TestHookError error)
        : base($"Test hook failed: {error}.")
    {
        Error = error;
    }

    public TestHookError Error { get; }
}
#endif
