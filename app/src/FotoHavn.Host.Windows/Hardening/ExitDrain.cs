namespace FotoHavn.Host.Windows.Hardening;

/// <summary>
/// The wait <c>OnExit</c> makes, before it disposes the brain and the store, for the brain's pending store writes (the Event's end, its Guest Cycle's
/// end). It runs on the UI thread, which is safe to block here: the brain's wait is done by its executor and the thread pool and never needs the
/// dispatcher, and the wait is bounded, so a stuck write delays the exit by at most the bound.
/// </summary>
internal static class ExitDrain
{
    /// <summary>Extra time beyond the brain's own bound for the wait to come back, so the brain's timeout (which it logs) wins over this one.</summary>
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(1);

    /// <summary>Waits for <paramref name="whenSettled"/> for at most <paramref name="bound"/> plus a margin; false when it did not finish in time or failed. Null means there is nothing to wait for.</summary>
    public static bool Wait(Func<Task>? whenSettled, TimeSpan bound)
    {
        if (whenSettled is null)
        {
            return true;
        }

        try
        {
            return Task.Run(whenSettled).Wait(bound + Margin);
        }
        catch (AggregateException)
        {
            return false;
        }
    }
}
