#if E2E_HOOKS
using FotoHavn.Core.E2E;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Testing;
using Microsoft.Extensions.Hosting;

namespace FotoHavn.Host.Headless.E2E;

/// <summary>
/// The headless host's side of the test hooks (E2E builds only). There are no windows: <c>windows</c> is empty and
/// <c>test.setWindowBounds</c> is <c>notSupported</c>; <c>test.crashUi</c> aborts that role's sockets; the camera list is
/// the fake camera; <c>test.exit</c> stops the host.
/// </summary>
internal sealed class HeadlessTestHooks(BridgeSessions sessions, IHostApplicationLifetime lifetime) : IHostTestHooks
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(250);

    public static TestCamera FakeCamera { get; } = new("fake-camera", "Fake camera");

    public Task<IReadOnlyList<TestWindow>> GetWindowsAsync() => Task.FromResult<IReadOnlyList<TestWindow>>([]);

    public Task<TestWindow> SetWindowBoundsAsync(Role role, int width, int height) =>
        Task.FromException<TestWindow>(new TestHookException(TestHookError.NotSupported));

    public bool ScheduleCrashUi(Role role)
    {
        if (sessions.Count(role) == 0)
        {
            return false;
        }

        _ = Task.Delay(Delay).ContinueWith(_ => sessions.AbortRole(role), TaskScheduler.Default);
        return true;
    }

    public Task<IReadOnlyList<TestCamera>> ListCamerasAsync() => Task.FromResult<IReadOnlyList<TestCamera>>([FakeCamera]);

    public void ScheduleExit() =>
        _ = Task.Delay(Delay).ContinueWith(_ => lifetime.StopApplication(), TaskScheduler.Default);
}
#endif
