namespace FotoHavn.Host.Windows.TestHooks;

/// <summary>
/// Whether the host side of the test hooks (<c>FotoHavn.Host.Windows.E2E.WindowsHostTestHooks</c>) is compiled in.
/// Hook messages travel to the brain like every other message; without <c>E2E_HOOKS</c> the brain answers each one with
/// <c>hooksUnavailable</c>, and the host has no hook code at all.
/// </summary>
internal static class TestHookChannel
{
    public static bool IsCompiled => HostBuild.IsE2E;
}
