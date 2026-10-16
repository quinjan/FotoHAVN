using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FotoHavn.Core.Hardening;

/// <summary>How a structured log property is written.</summary>
public enum LogFieldKind
{
    /// <summary>An opaque identifier (Event, Guest Cycle, Capture, Template, Look): written when it looks like an id, redacted otherwise.</summary>
    Id,

    /// <summary>A number, flag, enum, or short code word: written (control characters removed, length capped).</summary>
    Plain,

    /// <summary>A path inside the Data folder: written relative to it with <c>/</c> separators; a path outside it is replaced.</summary>
    DataPath,

    /// <summary>An address: written without its user info, query, and fragment.</summary>
    Uri,

    /// <summary>A folder of the installation or the machine's runtime (Data folder, WebView2 user data, runtime): written in full.</summary>
    InstallPath,

    /// <summary>Everything else: replaced by <see cref="LogRedaction.RedactedText"/>.</summary>
    Redacted,
}

/// <summary>A log entry after redaction: the message re-rendered from the redacted values, and those values.</summary>
/// <param name="Message">The message with every placeholder filled from <paramref name="Properties"/>.</param>
/// <param name="Properties">The redacted properties, in the order they were logged, without the message template.</param>
public sealed record ScrubbedLogEntry(string Message, IReadOnlyList<KeyValuePair<string, object?>> Properties);

/// <summary>
/// What may be logged (M10, D-logging): Event IDs and Guest Cycle IDs, codes, numbers, and hardware names, never an Event's name,
/// Customer or venue metadata, the PIN, a file's content, or anything a guest typed or photographed. It works by allow-listing
/// property names, not by searching text for secrets: a property whose name is not listed is replaced, and the log sink writes the
/// message from the replaced values, so a value can never reach the log through its template. Free text (<c>Message</c>,
/// <c>Detail</c>) and exception messages are not listed, so they never reach the log; exceptions are logged as type, HRESULT, and
/// stack frames. Pure: the host's log sink calls <see cref="Scrub"/> for every entry.
/// </summary>
public static partial class LogRedaction
{
    /// <summary>What a redacted value is replaced with.</summary>
    public const string RedactedText = "[redacted]";

    /// <summary>What a path outside the Data folder is replaced with.</summary>
    public const string OutsideDataFolder = "[outside the Data folder]";

    /// <summary>The key under which the logging extensions pass the message template.</summary>
    public const string OriginalFormatKey = "{OriginalFormat}";

    /// <summary>Longest plain or install-path text written; the rest is cut off.</summary>
    public const int MaxTextLength = 300;

    private const int MaxExceptionDepth = 8;

    private static readonly FrozenDictionary<string, LogFieldKind> Fields = new Dictionary<string, LogFieldKind>(StringComparer.OrdinalIgnoreCase)
    {
        ["EventId"] = LogFieldKind.Id,
        ["GuestCycleId"] = LogFieldKind.Id,
        ["CaptureId"] = LogFieldKind.Id,
        ["PhotoStripId"] = LogFieldKind.Id,
        ["TemplateId"] = LogFieldKind.Id,
        ["LookId"] = LogFieldKind.Id,

        ["Role"] = LogFieldKind.Plain,
        ["UiVersion"] = LogFieldKind.Plain,
        ["BrainVersion"] = LogFieldKind.Plain,
        ["Version"] = LogFieldKind.Plain,
        ["SchemaVersion"] = LogFieldKind.Plain,
        ["State"] = LogFieldKind.Plain,
        ["Kind"] = LogFieldKind.Plain,
        ["Code"] = LogFieldKind.Plain,
        ["Reason"] = LogFieldKind.Plain,
        ["Check"] = LogFieldKind.Plain,
        ["Stage"] = LogFieldKind.Plain,
        ["Operation"] = LogFieldKind.Plain,
        ["Outcome"] = LogFieldKind.Plain,
        ["ErrorType"] = LogFieldKind.Plain,
        ["ExitCode"] = LogFieldKind.Plain,
        ["HResult"] = LogFieldKind.Plain,
        ["Count"] = LogFieldKind.Plain,
        ["EventCount"] = LogFieldKind.Plain,
        ["Width"] = LogFieldKind.Plain,
        ["Height"] = LogFieldKind.Plain,
        ["Fps"] = LogFieldKind.Plain,
        ["Subtype"] = LogFieldKind.Plain,
        ["Scale"] = LogFieldKind.Plain,
        ["ZoomFactor"] = LogFieldKind.Plain,
        ["SingleScreen"] = LogFieldKind.Plain,
        ["HooksCompiled"] = LogFieldKind.Plain,
        ["Bounds"] = LogFieldKind.Plain,
        ["WindowBounds"] = LogFieldKind.Plain,
        ["Displays"] = LogFieldKind.Plain,
        ["DisplayId"] = LogFieldKind.Plain,
        ["DisplayName"] = LogFieldKind.Plain,
        ["DeviceId"] = LogFieldKind.Plain,
        ["Attempt"] = LogFieldKind.Plain,
        ["ElapsedMs"] = LogFieldKind.Plain,
        ["Surface"] = LogFieldKind.Plain,
        ["Step"] = LogFieldKind.Plain,

        ["Uri"] = LogFieldKind.Uri,
        ["Source"] = LogFieldKind.Uri,

        ["Path"] = LogFieldKind.DataPath,
        ["File"] = LogFieldKind.DataPath,
        ["Folder"] = LogFieldKind.DataPath,

        ["DataFolder"] = LogFieldKind.InstallPath,
        ["UserDataFolder"] = LogFieldKind.InstallPath,
        ["Runtime"] = LogFieldKind.InstallPath,
        ["IndexFile"] = LogFieldKind.InstallPath,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>How the property called <paramref name="name"/> is written: its listed kind, or <see cref="LogFieldKind.Redacted"/> when it is not listed.</summary>
    public static LogFieldKind Classify(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Fields.GetValueOrDefault(name, LogFieldKind.Redacted);
    }

    /// <summary>The value to write for the property <paramref name="name"/>. Value types pass as they are; text follows the property's <see cref="LogFieldKind"/>.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The logged value.</param>
    /// <param name="dataFolder">The Data folder, for <see cref="LogFieldKind.DataPath"/> values; null redacts them.</param>
    public static object? ScrubValue(string name, object? value, string? dataFolder)
    {
        if (value is null)
        {
            return null;
        }

        var kind = Classify(name);
        return kind switch
        {
            LogFieldKind.Redacted => RedactedText,
            LogFieldKind.Id => IsId(value) ? value : RedactedText,
            LogFieldKind.Uri => SanitizeUri(Text(value)),
            LogFieldKind.DataPath => ToDataRelativePath(dataFolder, Text(value)),
            _ => IsScalar(value) ? value : Clean(Text(value)),
        };
    }

    /// <summary>
    /// Redacts one structured log entry: every property by <see cref="ScrubValue"/>, and the message re-rendered from its template with the
    /// redacted values. State that has no template (not logged through <c>LoggerMessage</c> or the logging extensions) has its message replaced.
    /// </summary>
    /// <param name="state">The log state's key-value pairs, including <see cref="OriginalFormatKey"/>.</param>
    /// <param name="dataFolder">The Data folder, for path properties.</param>
    public static ScrubbedLogEntry Scrub(IReadOnlyList<KeyValuePair<string, object?>> state, string? dataFolder)
    {
        ArgumentNullException.ThrowIfNull(state);
        string? template = null;
        var scrubbed = new List<KeyValuePair<string, object?>>(state.Count);
        var byName = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in state)
        {
            if (key == OriginalFormatKey)
            {
                template = value as string;
                continue;
            }

            if (key.Length == 0)
            {
                continue;
            }

            var safe = ScrubValue(key, value, dataFolder);
            scrubbed.Add(new KeyValuePair<string, object?>(key, safe));
            byName.TryAdd(key, safe);
        }

        return new ScrubbedLogEntry(template is null ? RedactedText : Render(template, byName), scrubbed);
    }

    /// <summary>
    /// <paramref name="path"/> relative to <paramref name="dataFolder"/>, with <c>/</c> separators (<c>.</c> for the folder itself);
    /// <see cref="OutsideDataFolder"/> when it is elsewhere; <see cref="RedactedText"/> when either path is unusable. Null stays null.
    /// </summary>
    public static string? ToDataRelativePath(string? dataFolder, string? path)
    {
        if (path is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(dataFolder) || string.IsNullOrWhiteSpace(path))
        {
            return RedactedText;
        }

        try
        {
            var root = TrimSeparators(Path.GetFullPath(dataFolder));
            var full = TrimSeparators(Path.GetFullPath(path));
            if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            {
                return ".";
            }

            var prefix = root.EndsWith('\\') ? root : root + "\\";
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? full[prefix.Length..].Replace('\\', '/')
                : OutsideDataFolder;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return RedactedText;
        }
    }

    /// <summary>
    /// An exception as type name, HRESULT, and stack frames (without file paths), with its inner exceptions the same way. Never its
    /// message, which can carry a file name, a value, or text the guest typed.
    /// </summary>
    public static string DescribeException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var text = new StringBuilder();
        Describe(text, exception, 0);
        return text.ToString();
    }

    private static void Describe(StringBuilder text, Exception exception, int depth)
    {
        var indent = new string(' ', depth * 2);
        text.Append(indent).Append(exception.GetType().FullName);
        if (exception.HResult != 0)
        {
            text.Append(" (HRESULT 0x").Append(exception.HResult.ToString("X8", CultureInfo.InvariantCulture)).Append(')');
        }

        foreach (var line in (exception.StackTrace ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            text.AppendLine().Append(indent).Append("  ").Append(SourceLocation().Replace(line, string.Empty));
        }

        if (depth >= MaxExceptionDepth)
        {
            return;
        }

        IEnumerable<Exception> inner = exception is AggregateException aggregate
            ? aggregate.InnerExceptions
            : exception.InnerException is { } single ? [single] : [];
        foreach (var child in inner)
        {
            text.AppendLine();
            Describe(text, child, depth + 1);
        }
    }

    private static string Render(string template, Dictionary<string, object?> values)
    {
        var text = new StringBuilder(template.Length + 32);
        var index = 0;
        while (index < template.Length)
        {
            var character = template[index];
            if (character == '{' && index + 1 < template.Length && template[index + 1] == '{')
            {
                text.Append('{');
                index += 2;
            }
            else if (character == '}' && index + 1 < template.Length && template[index + 1] == '}')
            {
                text.Append('}');
                index += 2;
            }
            else if (character == '{' && template.IndexOf('}', index) is var close and > 0)
            {
                var name = PlaceholderName(template.AsSpan(index + 1, close - index - 1));
                text.Append(values.TryGetValue(name, out var value) ? Text(value) ?? "(null)" : RedactedText);
                index = close + 1;
            }
            else
            {
                text.Append(character);
                index++;
            }
        }

        return text.ToString();
    }

    private static string PlaceholderName(ReadOnlySpan<char> placeholder)
    {
        var end = placeholder.IndexOfAny(',', ':');
        var name = end < 0 ? placeholder : placeholder[..end];
        return name.TrimStart("@$".AsSpan()).ToString();
    }

    private static string? Text(object? value) => value switch
    {
        null => null,
        string text => text,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture),
    };

    private static bool IsScalar(object value) => value is not string
        && (value.GetType().IsPrimitive || value.GetType().IsEnum || value is decimal or DateTime or DateTimeOffset or TimeSpan or Guid);

    private static bool IsId(object value) => Text(value) is { } text && IdPattern().IsMatch(text);

    private static string? Clean(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var cleaned = new string([.. text.Select(character => char.IsControl(character) ? ' ' : character)]);
        return cleaned.Length > MaxTextLength ? cleaned[..MaxTextLength] + "…" : cleaned;
    }

    private static string SanitizeUri(string? text)
    {
        if (text is null || !Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return RedactedText;
        }

        var port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port.ToString(CultureInfo.InvariantCulture);
        return Clean($"{uri.Scheme}://{uri.Host}{port}{uri.AbsolutePath}")!;
    }

    private static string TrimSeparators(string path)
    {
        var rootLength = Path.GetPathRoot(path)?.Length ?? 0;
        while (path.Length > rootLength && (path[^1] is '\\' or '/'))
        {
            path = path[..^1];
        }

        return path;
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"\s+in\s+.+:line\s+\d+$")]
    private static partial Regex SourceLocation();
}
