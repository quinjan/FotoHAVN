using FotoHavn.Core;
using FotoHavn.Core.Hardening;

namespace FotoHavn.Host.Windows.Hardening;

/// <summary>What the windows' close handling asks of the brain: whether an Event is held, and the close request of a window or of Windows.</summary>
internal interface ICloseRequests
{
    /// <summary>Whether an Event is held. Safe from any thread, so a window's <c>Closing</c> handler can answer at once.</summary>
    bool IsEventActive { get; }

    Task<CloseRequestOutcome> RequestClose(CloseSource source);
}

/// <summary>
/// The windows' close handling (M10, contract section 7). Nothing in a <c>Closing</c> handler waits for the brain: the Operator window
/// cancels its own close and asks the brain, which answers through <see cref="IAppLifetime.Shutdown"/> (the host then calls
/// <see cref="AllowClose"/> and exits); the Guest window asks <see cref="CloseRequestRules"/> with the brain's volatile flag. Windows
/// ending the session is the one place the host waits, for a bounded time, because Windows gives the app seconds, not a round trip.
/// </summary>
internal sealed class WindowCloseController(ICloseRequests requests, Action<Action> postToUi)
{
    /// <summary>How long Windows ending the session may wait for the brain to end the Event.</summary>
    public static readonly TimeSpan SessionEndWait = TimeSpan.FromSeconds(5);

    private volatile bool _closing;

    /// <summary>Called on the UI thread with the brain's answer to the Operator window's close request (the host brings the window forward when the brain asked the question).</summary>
    public Action<CloseRequestOutcome>? OperatorOutcome { get; set; }

    /// <summary>Whether the app is closing for real: no window may veto its own close from now on.</summary>
    public bool IsClosing => _closing;

    /// <summary>The brain, Windows, or <c>Shutdown</c> decided the app closes: closing handlers let every window go.</summary>
    public void AllowClose() => _closing = true;

    /// <summary>The Operator window's <c>Closing</c>: returns whether to cancel it. It cancels and asks the brain, unless the app is already closing.</summary>
    public bool ShouldCancelOperatorClose()
    {
        if (_closing)
        {
            return false;
        }

        requests.RequestClose(CloseSource.OperatorWindow).ContinueWith(
            completed =>
            {
                if (completed.IsCompletedSuccessfully)
                {
                    postToUi(() => OperatorOutcome?.Invoke(completed.Result));
                }
                else
                {
                    _ = completed.Exception;
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return true;
    }

    /// <summary>The Guest window's <c>Closing</c> (not the host closing it): returns whether to cancel. It refuses while an Event is held.</summary>
    public bool ShouldCancelGuestClose() =>
        !_closing && CloseRequestRules.Decide(requests.IsEventActive, CloseSource.GuestWindow) == CloseRequestOutcome.Refuse;

    /// <summary>
    /// Windows is ending the session (UI thread): asks the brain to end the Event and close, waits for it at most <see cref="SessionEndWait"/>,
    /// and lets every window go either way. It never cancels the session end. Returns whether the brain finished in time.
    /// </summary>
    public bool OnSessionEnding(TimeSpan? wait = null)
    {
        var request = requests.RequestClose(CloseSource.SessionEnd);
        bool finished;
        try
        {
            finished = request.Wait(wait ?? SessionEndWait);
        }
        catch (AggregateException)
        {
            finished = false;
        }

        _closing = true;
        return finished;
    }
}

/// <summary><see cref="IAppLifetime"/> for the WPF host: allows every window to close, then ends the application on the UI thread with the success exit code.</summary>
internal sealed class WindowsAppLifetime(WindowCloseController controller, Action<Action> postToUi, Action<int> exit) : IAppLifetime
{
    public void Shutdown()
    {
        controller.AllowClose();
        postToUi(() => exit(HostExitCodes.Success));
    }
}

/// <summary>
/// <see cref="ICloseRequests"/> over the brain, which is made after the windows (they are its host services) but before any window is
/// shown, so <see cref="Brain"/> is set before the first <c>Closing</c> can run.
/// </summary>
internal sealed class BrainCloseRequests : ICloseRequests
{
    public Brain? Brain { get; set; }

    public bool IsEventActive => Brain?.IsEventActive ?? false;

    public Task<CloseRequestOutcome> RequestClose(CloseSource source) =>
        Brain?.RequestClose(source) ?? Task.FromResult(CloseRequestOutcome.CloseNow);
}
