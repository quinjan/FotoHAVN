namespace FotoHavn.Core;

/// <summary>
/// Keeps Windows awake while an Event runs (plan section 8, Start Event step 7). The brain calls it only on its executor:
/// <see cref="Acquire"/> when the Event starts, <see cref="Release"/> when it ends. M10 implements the real one with the
/// host; until then <see cref="NoOpWakeLock"/> stands in.
/// </summary>
public interface IWakeLock
{
    /// <summary>Whether the lock is held. Acquiring twice, or releasing without holding, is harmless.</summary>
    bool IsHeld { get; }

    /// <summary>Starts keeping the system awake.</summary>
    void Acquire();

    /// <summary>Stops keeping the system awake.</summary>
    void Release();
}

/// <summary>
/// The wake lock before M10: it keeps no system awake, only tracks whether the brain asked, so <see cref="IsHeld"/> (and the
/// <c>test.getDiagnostics</c> <c>wakeLock</c> field) still show the Start and End Event sequence. Thread-safe.
/// </summary>
public sealed class NoOpWakeLock : IWakeLock
{
    private int _held;

    public bool IsHeld => Volatile.Read(ref _held) != 0;

    public void Acquire() => Volatile.Write(ref _held, 1);

    public void Release() => Volatile.Write(ref _held, 0);
}
