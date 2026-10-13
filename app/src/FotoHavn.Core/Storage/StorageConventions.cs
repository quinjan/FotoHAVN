using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FotoHavn.Core.Storage;

/// <summary>The SQL strings of the schema's CHECK constraints, and back (plan section 10, Schema).</summary>
public static class StorageSqlNames
{
    public static string ToSql(PhaseTimerPhase phase) => phase switch
    {
        PhaseTimerPhase.Template => "template",
        PhaseTimerPhase.PhotographsCapture => "photographs_capture",
        PhaseTimerPhase.PhotographsReview => "photographs_review",
        PhaseTimerPhase.Filters => "filters",
        PhaseTimerPhase.Keepsake => "keepsake",
        PhaseTimerPhase.GuestGallery => "guest_gallery",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Not a Phase Timer phase."),
    };

    public static PhaseTimerPhase PhaseFromSql(string value) => value switch
    {
        "template" => PhaseTimerPhase.Template,
        "photographs_capture" => PhaseTimerPhase.PhotographsCapture,
        "photographs_review" => PhaseTimerPhase.PhotographsReview,
        "filters" => PhaseTimerPhase.Filters,
        "keepsake" => PhaseTimerPhase.Keepsake,
        "guest_gallery" => PhaseTimerPhase.GuestGallery,
        _ => throw new ArgumentException($"'{value}' is not a Phase Timer phase.", nameof(value)),
    };

    public static string ToSql(GuestCycleEndReason reason) => reason switch
    {
        GuestCycleEndReason.Completed => "completed",
        GuestCycleEndReason.StartedOver => "started_over",
        GuestCycleEndReason.OperatorExit => "operator_exit",
        GuestCycleEndReason.AppClosed => "app_closed",
        GuestCycleEndReason.Unrecoverable => "unrecoverable",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Not an end reason."),
    };

    public static GuestCycleEndReason EndReasonFromSql(string value) => value switch
    {
        "completed" => GuestCycleEndReason.Completed,
        "started_over" => GuestCycleEndReason.StartedOver,
        "operator_exit" => GuestCycleEndReason.OperatorExit,
        "app_closed" => GuestCycleEndReason.AppClosed,
        "unrecoverable" => GuestCycleEndReason.Unrecoverable,
        _ => throw new ArgumentException($"'{value}' is not an end reason.", nameof(value)),
    };

    public static string ToSql(CaptureState state) => state switch
    {
        CaptureState.Kept => "kept",
        CaptureState.Discarded => "discarded",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Not a Capture state."),
    };

    public static CaptureState CaptureStateFromSql(string value) => value switch
    {
        "kept" => CaptureState.Kept,
        "discarded" => CaptureState.Discarded,
        _ => throw new ArgumentException($"'{value}' is not a Capture state.", nameof(value)),
    };
}

/// <summary>
/// How times and dates are written to the database: UTC, fixed width, so text order is time order (the paging cursor
/// and <c>ORDER BY</c> rely on it). Timestamps are <c>yyyy-MM-ddTHH:mm:ss.fffffffZ</c>; dates are <c>yyyy-MM-dd</c>.
/// </summary>
public static class StorageTimestamp
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
    private const string DateFormat = "yyyy-MM-dd";

    public static string Format(DateTimeOffset value) =>
        value.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    /// <summary>Reads a timestamp written by <see cref="Format"/>. Throws <see cref="FormatException"/> for anything else.</summary>
    public static DateTimeOffset Parse(string value) =>
        DateTimeOffset.ParseExact(value, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    public static string FormatDate(DateOnly value) => value.ToString(DateFormat, CultureInfo.InvariantCulture);

    /// <summary>Reads a date written by <see cref="FormatDate"/>. Throws <see cref="FormatException"/> for anything else.</summary>
    public static DateOnly ParseDate(string value) => DateOnly.ParseExact(value, DateFormat, CultureInfo.InvariantCulture);
}

/// <summary>
/// Event, Guest Cycle, and Capture ids are lowercase GUIDs in <c>D</c> format. Ids become folder and file names, so the
/// store refuses anything else (no path separators or traversal can get in).
/// </summary>
public static class StorageIds
{
    public static string NewId() => Guid.NewGuid().ToString("D");

    public static bool IsValid(string? id) =>
        id is { Length: 36 } && Guid.TryParseExact(id, "D", out _) && id.AsSpan().IndexOfAnyInRange('A', 'F') < 0;

    /// <summary>Throws <see cref="ArgumentException"/> unless <paramref name="id"/> is valid.</summary>
    public static void ThrowIfInvalid(string? id, string paramName)
    {
        if (!IsValid(id))
        {
            throw new ArgumentException("The id isn't a lowercase GUID.", paramName);
        }
    }
}

/// <summary>
/// The opaque paging cursor of <see cref="IEventStore.ListGuestCyclesAsync"/>: base64url of <c>&lt;started_at&gt;|&lt;id&gt;</c> for
/// the last row of the page. The real store and the in-memory fake both use it.
/// </summary>
public static class GuestCycleCursor
{
    public static string Encode(DateTimeOffset startedAt, string guestCycleId)
    {
        ArgumentNullException.ThrowIfNull(guestCycleId);
        var raw = Encoding.UTF8.GetBytes(StorageTimestamp.Format(startedAt) + "|" + guestCycleId);
        return Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryDecode(string? cursor, out DateTimeOffset startedAt, out string guestCycleId)
    {
        startedAt = default;
        guestCycleId = string.Empty;
        if (string.IsNullOrEmpty(cursor) || cursor.Length > 200)
        {
            return false;
        }

        var padded = cursor.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        byte[] raw;
        try
        {
            raw = Convert.FromBase64String(padded);
        }
        catch (FormatException)
        {
            return false;
        }

        var text = Encoding.UTF8.GetString(raw);
        var parts = text.Split('|');
        if (parts.Length != 2 || !StorageIds.IsValid(parts[1]) || !TryParseTimestamp(parts[0], out startedAt))
        {
            return false;
        }

        guestCycleId = parts[1];
        return Encode(startedAt, guestCycleId) == cursor;
    }

    private static bool TryParseTimestamp(string value, out DateTimeOffset result)
    {
        try
        {
            result = StorageTimestamp.Parse(value);
            return true;
        }
        catch (FormatException)
        {
            result = default;
            return false;
        }
    }
}

/// <summary>
/// Thumbnail file names, <c>&lt;sourceId&gt;-&lt;sha8&gt;-&lt;width&gt;.jpg</c>, where sha8 is the first 8 hex characters of the
/// source file's SHA-256 (plan section 10, Thumbnails). They live in <c>Data/Cache/thumbnails/</c>.
/// </summary>
public static partial class ThumbnailName
{
    public const string Folder = "Cache/thumbnails";
    public const int MinimumWidth = 64;
    public const int MaximumWidth = 2048;

    public static string Sha8(string sha256Hex)
    {
        ArgumentNullException.ThrowIfNull(sha256Hex);
        if (sha256Hex.Length < 8)
        {
            throw new ArgumentException("A SHA-256 hex string has at least 8 characters.", nameof(sha256Hex));
        }

        return sha256Hex[..8].ToLowerInvariant();
    }

    public static string Format(string sourceId, string sha256Hex, int width)
    {
        StorageIds.ThrowIfInvalid(sourceId, nameof(sourceId));
        ArgumentOutOfRangeException.ThrowIfLessThan(width, MinimumWidth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, MaximumWidth);
        return $"{sourceId}-{Sha8(sha256Hex)}-{width.ToString(CultureInfo.InvariantCulture)}.jpg";
    }

    /// <summary>The path <see cref="IEventStore.GetThumbnailPathAsync"/> returns: relative to the Data folder, forward slashes.</summary>
    public static string RelativePath(string sourceId, string sha256Hex, int width) =>
        Folder + "/" + Format(sourceId, sha256Hex, width);

    /// <summary>Splits a thumbnail file name. False when the name isn't one (the reconciler leaves such files alone).</summary>
    public static bool TryParse(string fileName, out string sourceId, out string sha8, out int width)
    {
        sourceId = string.Empty;
        sha8 = string.Empty;
        width = 0;
        var match = NamePattern().Match(fileName ?? string.Empty);
        if (!match.Success)
        {
            return false;
        }

        sourceId = match.Groups["id"].Value;
        sha8 = match.Groups["sha"].Value;
        width = int.Parse(match.Groups["width"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
        return true;
    }

    [GeneratedRegex(@"^(?<id>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})-(?<sha>[0-9a-f]{8})-(?<width>[0-9]{1,5})\.jpg$")]
    private static partial Regex NamePattern();
}
