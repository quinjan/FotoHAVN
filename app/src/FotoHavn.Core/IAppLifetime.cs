namespace FotoHavn.Core;

/// <summary>
/// How the brain asks the host to close FOTOHAVN (M10). The brain calls <see cref="Shutdown"/> on its executor only after the
/// running Event has ended cleanly (an unfinished Guest Cycle closed as <c>operator_exit</c>, Captures kept, the wake lock released);
/// the host then closes its windows and exits the process. It must return at once and be safe to call twice.
/// </summary>
public interface IAppLifetime
{
    /// <summary>Closes the application: both windows, the brain, the camera, the store, then the process.</summary>
    void Shutdown();
}

/// <summary>The app lifetime for hosts that close themselves some other way (the headless host, tests that do not care): it does nothing, and counts the requests.</summary>
public sealed class NoOpAppLifetime : IAppLifetime
{
    private int _shutdownRequests;

    /// <summary>The shared instance.</summary>
    public static NoOpAppLifetime Instance { get; } = new();

    /// <summary>How many times <see cref="Shutdown"/> was called.</summary>
    public int ShutdownRequests => Volatile.Read(ref _shutdownRequests);

    public void Shutdown() => Interlocked.Increment(ref _shutdownRequests);
}
