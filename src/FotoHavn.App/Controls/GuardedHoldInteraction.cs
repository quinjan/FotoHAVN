namespace FotoHavn.App.Controls;

public enum GuardedHoldState
{
    Idle,
    Holding,
    Cancelled,
    Completed,
}

public readonly record struct GuardedHoldUpdate(GuardedHoldState State, double Progress);

public sealed class GuardedHoldInteraction
{
    private readonly TimeSpan requiredDuration;
    private DateTimeOffset? startedAt;
    private bool completed;

    public GuardedHoldInteraction(TimeSpan requiredDuration)
    {
        if (requiredDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredDuration));
        }

        this.requiredDuration = requiredDuration;
    }

    public void Begin(DateTimeOffset now)
    {
        if (startedAt is null && !completed)
        {
            startedAt = now;
        }
    }

    public GuardedHoldUpdate Update(DateTimeOffset now)
    {
        if (completed)
        {
            return new(GuardedHoldState.Completed, 1);
        }

        if (startedAt is not { } start)
        {
            return new(GuardedHoldState.Idle, 0);
        }

        var progress = Math.Clamp((now - start).TotalMilliseconds / requiredDuration.TotalMilliseconds, 0, 1);
        if (progress >= 1)
        {
            completed = true;
            return new(GuardedHoldState.Completed, 1);
        }

        return new(GuardedHoldState.Holding, progress);
    }

    public GuardedHoldUpdate Cancel()
    {
        startedAt = null;
        completed = false;
        return new(GuardedHoldState.Cancelled, 0);
    }

    public static double ResolveIndicatorAngle(
        GuardedHoldUpdate update,
        bool animationsEnabled) =>
        animationsEnabled && update.State is GuardedHoldState.Holding or GuardedHoldState.Completed
            ? update.Progress * 360
            : 0;
}
