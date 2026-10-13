namespace FotoHavn.Storage;

/// <summary>
/// A hook the store calls between the steps of a multi-step change, so the crash-test harness (wave C) can end the process
/// at a chosen step and check what the reconciler finds afterwards. Production passes <see cref="NoCrashPoints.Instance"/>.
/// The step names in <see cref="CrashPoint"/> are stable: tests refer to them by name.
/// </summary>
public interface ICrashPoints
{
    /// <summary>Called right after <paramref name="stepName"/> completed. The harness may never return from it.</summary>
    void Reached(string stepName);
}

/// <summary>The default <see cref="ICrashPoints"/>: does nothing.</summary>
public sealed class NoCrashPoints : ICrashPoints
{
    public static NoCrashPoints Instance { get; } = new();

    public void Reached(string stepName)
    {
    }
}

/// <summary>
/// The stable step names, in the order the store reaches them. A name means "this step has just finished":
/// <list type="bullet">
/// <item><c>capture.*</c> and <c>strip.*</c>: <c>staged</c> (the <c>.staging/&lt;uuid&gt;.tmp</c> file is written; reported from
/// inside the file system's write, after the bytes are written and before they are flushed, so a crash here leaves a staged but
/// unflushed file), <c>flushed</c> (it is flushed to disk), <c>renamed</c> (it has its final name, no row yet), <c>inserted</c>
/// (the row is committed).</item>
/// <item><c>event.*</c>: <c>markedDeleting</c> (<c>deleting_at</c> committed), <c>folderDeleted</c> (the Event folder and
/// its thumbnails are gone), <c>rowsDeleted</c> (the rows are gone).</item>
/// <item><c>cycle.*</c>: the same three steps for a Guest Cycle.</item>
/// </list>
/// </summary>
public static class CrashPoint
{
    public const string CaptureStaged = "capture.staged";
    public const string CaptureFlushed = "capture.flushed";
    public const string CaptureRenamed = "capture.renamed";
    public const string CaptureInserted = "capture.inserted";

    public const string StripStaged = "strip.staged";
    public const string StripFlushed = "strip.flushed";
    public const string StripRenamed = "strip.renamed";
    public const string StripInserted = "strip.inserted";

    public const string EventMarkedDeleting = "event.markedDeleting";
    public const string EventFolderDeleted = "event.folderDeleted";
    public const string EventRowsDeleted = "event.rowsDeleted";

    public const string CycleMarkedDeleting = "cycle.markedDeleting";
    public const string CycleFolderDeleted = "cycle.folderDeleted";
    public const string CycleRowsDeleted = "cycle.rowsDeleted";

    /// <summary>Every step name, grouped by operation.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        CaptureStaged, CaptureFlushed, CaptureRenamed, CaptureInserted,
        StripStaged, StripFlushed, StripRenamed, StripInserted,
        EventMarkedDeleting, EventFolderDeleted, EventRowsDeleted,
        CycleMarkedDeleting, CycleFolderDeleted, CycleRowsDeleted,
    ];
}
