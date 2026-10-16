using FotoHavn.Core.Hardening;

namespace FotoHavn.Host.Windows.Hardening;

/// <summary>
/// The field build's own check, <c>--smoke</c> (M10): the app starts as usual, and the first of two things ends it. The Operator UI finishing
/// its handshake exits <see cref="HostExitCodes.Success"/>; the time running out exits <see cref="HostExitCodes.SmokeTimeout"/>. Whichever
/// comes first wins, and the other is ignored. A WebView2 failure exits <see cref="HostExitCodes.Failure"/> through the host's usual path.
/// </summary>
internal sealed class SmokeRun : IDisposable
{
    /// <summary>How long the Operator UI has to become ready.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private readonly Action<int> _exit;
    private readonly Timer _timer;
    private int _finished;

    public SmokeRun(Action<int> exit, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(exit);
        _exit = exit;
        _timer = new Timer(_ => Finish(HostExitCodes.SmokeTimeout));
        _timer.Change(timeout, Timeout.InfiniteTimeSpan);
    }

    /// <summary>The Operator UI answered its handshake: the app is ready.</summary>
    public void OperatorReady() => Finish(HostExitCodes.Success);

    public void Dispose() => _timer.Dispose();

    private void Finish(int exitCode)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        _timer.Dispose();
        _exit(exitCode);
    }
}
