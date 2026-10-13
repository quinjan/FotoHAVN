#if E2E_HOOKS
using FotoHavn.Core.Hooks;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.E2E;

/// <summary>
/// The E2E-only glue between the host's <see cref="IHostTestHooks"/> and the brain's hook dispatcher: plain delegation
/// plus the mapping of <see cref="TestHookException"/> to its error. Everything else about the hooks lives outside this
/// namespace and is unit-tested in the normal build.
/// </summary>
internal sealed class HostTestHooksPort(IHostTestHooks hooks) : IHostHookPort
{
    public Task<IReadOnlyList<TestWindow>> GetWindowsAsync() => hooks.GetWindowsAsync();

    public Task<TestWindow> SetWindowBoundsAsync(Role role, int width, int height) => hooks.SetWindowBoundsAsync(role, width, height);

    public bool ScheduleCrashUi(Role role) => hooks.ScheduleCrashUi(role);

    public Task<IReadOnlyList<TestCamera>> ListCamerasAsync() => hooks.ListCamerasAsync();

    public void ScheduleExit() => hooks.ScheduleExit();

    public TestHookError Classify(Exception exception) =>
        exception is TestHookException hookException ? hookException.Error : TestHookError.HookFailed;
}
#endif
