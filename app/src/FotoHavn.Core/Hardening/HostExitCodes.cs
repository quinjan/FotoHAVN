namespace FotoHavn.Core.Hardening;

/// <summary>The Windows host's process exit codes (M10). The end-to-end specs and the portable-launch check read them.</summary>
public static class HostExitCodes
{
    /// <summary>A normal exit, and the exit of <c>--smoke</c> once the Operator UI has finished its handshake with the brain.</summary>
    public const int Success = 0;

    /// <summary>The host could not start (for example WebView2 failed to start the Operator window).</summary>
    public const int Failure = 1;

    /// <summary>The command line was not understood.</summary>
    public const int InvalidArguments = 2;

    /// <summary>Another FOTOHAVN already runs on the same Data folder: this launch asked it to come forward and exited.</summary>
    public const int AlreadyRunning = 3;

    /// <summary><c>--smoke</c> never reached the ready state within its time limit.</summary>
    public const int SmokeTimeout = 4;
}
