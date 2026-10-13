using FotoHavn.Core.Camera;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Hooks;

/// <summary>
/// The brain's view of the host test hooks. It mirrors <c>FotoHavn.Core.E2E.IHostTestHooks</c>, which exists only in
/// E2E builds, so the brain's hook dispatcher compiles (and is unit-tested) in every build. In field builds nothing
/// creates a port and the brain answers every <c>test</c> message with <c>hooksUnavailable</c> before reaching it.
/// </summary>
internal interface IHostHookPort
{
    Task<IReadOnlyList<TestWindow>> GetWindowsAsync();

    Task<TestWindow> SetWindowBoundsAsync(Role role, int width, int height);

    bool ScheduleCrashUi(Role role);

    Task<CameraCaptureView> SaveDiagnosticsCaptureAsync(CapturedFrame frame, bool mirrored);

    void ScheduleExit();

    /// <summary>The hook error a host exception stands for (<c>TestHookException.Error</c>, else <c>hookFailed</c>).</summary>
    TestHookError Classify(Exception exception);
}
