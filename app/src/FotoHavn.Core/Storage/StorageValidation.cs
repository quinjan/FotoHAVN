namespace FotoHavn.Core.Storage;

/// <summary>
/// The argument rules of <see cref="IEventStore"/>, in one place so the real store and the in-memory fake refuse the
/// same things. Every method throws <see cref="ArgumentException"/> (or a subclass) and does no I/O.
/// </summary>
public static class StorageValidation
{
    public const int MaximumPageSize = 200;
    public const int MaximumArrangementLength = 4;

    private static readonly int[] CountdownValues = [3, 5, 10];

    public static void ValidateEvent(EventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        StorageIds.ThrowIfInvalid(record.Id, nameof(record));
        RequireText(record.Name, "The Event name is required.");
        RequireText(record.DefaultTemplateId, "The default Template is required.");
        RequireText(record.DefaultLookId, "The default Look is required.");
        if (record.Printer != EventRecord.NoPrinter)
        {
            throw new ArgumentException("The printer must be 'none' in v1.", nameof(record));
        }

        if (!CountdownValues.Contains(record.CountdownSeconds))
        {
            throw new ArgumentException("The countdown must be 3, 5, or 10 seconds.", nameof(record));
        }

        ValidateIdList(record.OfferedTemplateIds, "offered Template");
        ValidateIdList(record.OfferedLookIds, "offered Look");
        ArgumentNullException.ThrowIfNull(record.PhaseTimers);
        var seen = new HashSet<PhaseTimerPhase>();
        foreach (var timer in record.PhaseTimers)
        {
            _ = StorageSqlNames.ToSql(timer.Phase);
            if (timer.Seconds < 0)
            {
                throw new ArgumentException("A Phase Timer can't be negative.", nameof(record));
            }

            if (!seen.Add(timer.Phase))
            {
                throw new ArgumentException("A phase has more than one Phase Timer.", nameof(record));
            }
        }
    }

    public static void ValidatePin(string eventId, string pin)
    {
        StorageIds.ThrowIfInvalid(eventId, nameof(eventId));
        ArgumentException.ThrowIfNullOrEmpty(pin);
    }

    /// <summary><see cref="GuestCycleEndReason.Completed"/> is only for a Photo Strip commit.</summary>
    public static void ValidateEndReason(GuestCycleEndReason reason)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Not an end reason.");
        }

        if (reason == GuestCycleEndReason.Completed)
        {
            throw new ArgumentException("A Guest Cycle completes when its Photo Strip is committed.", nameof(reason));
        }
    }

    public static void ValidateCaptureCommit(CaptureCommit commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        StorageIds.ThrowIfInvalid(commit.GuestCycleId, nameof(commit));
        if (commit.Sequence < 1)
        {
            throw new ArgumentException("A Capture sequence starts at 1.", nameof(commit));
        }

        if (commit.Jpeg.IsEmpty)
        {
            throw new ArgumentException("A Capture has bytes.", nameof(commit));
        }

        RequirePositiveSize(commit.Width, commit.Height);
    }

    /// <summary>Checks the shape of a Photo Strip commit; whether the Capture ids belong to the cycle is the store's check.</summary>
    public static void ValidatePhotoStripCommit(PhotoStripCommit commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        StorageIds.ThrowIfInvalid(commit.GuestCycleId, nameof(commit));
        RequireText(commit.TemplateId, "The Template is required.");
        RequireText(commit.LookId, "The Look is required.");
        ArgumentNullException.ThrowIfNull(commit.Arrangement);
        if (commit.Arrangement.Count is < 1 or > MaximumArrangementLength)
        {
            throw new ArgumentException("An arrangement has one to four Captures.", nameof(commit));
        }

        if (commit.Arrangement.Any(id => !StorageIds.IsValid(id)) || commit.Arrangement.Distinct(StringComparer.Ordinal).Count() != commit.Arrangement.Count)
        {
            throw new ArgumentException("An arrangement lists distinct Capture ids.", nameof(commit));
        }

        if (commit.Png.IsEmpty)
        {
            throw new ArgumentException("A Photo Strip has bytes.", nameof(commit));
        }

        RequirePositiveSize(commit.Width, commit.Height);
    }

    public static void ValidatePaging(string eventId, GuestCycleFilter filter, string? cursor, int pageSize)
    {
        StorageIds.ThrowIfInvalid(eventId, nameof(eventId));
        if (!Enum.IsDefined(filter))
        {
            throw new ArgumentOutOfRangeException(nameof(filter), filter, "Not a filter.");
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "A page has 1 to 200 rows.");
        }

        if (cursor is not null && !GuestCycleCursor.TryDecode(cursor, out _, out _))
        {
            throw new ArgumentException("The cursor isn't one this store issued.", nameof(cursor));
        }
    }

    public static void ValidateThumbnailRequest(string sourceId, int width)
    {
        StorageIds.ThrowIfInvalid(sourceId, nameof(sourceId));
        if (width is < ThumbnailName.MinimumWidth or > ThumbnailName.MaximumWidth)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A thumbnail is 64 to 2048 pixels wide.");
        }
    }

    private static void RequireText(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message);
        }
    }

    private static void RequirePositiveSize(int width, int height)
    {
        if (width < 1 || height < 1)
        {
            throw new ArgumentException("The width and height are above zero.");
        }
    }

    private static void ValidateIdList(IReadOnlyList<string> ids, string what)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
        {
            throw new ArgumentException($"The {what} ids must be non-empty and distinct.");
        }
    }
}
