namespace FotoHavn.Core.Media;

/// <summary>
/// Maps a <c>https://media.fotohavn/&lt;path&gt;</c> request to a file inside the Data folder, or refuses it. Both hosts
/// (the WebView2 virtual host and the headless HTTP route) use this policy and never reimplement the path rules. It does
/// no file I/O: the host checks that the file exists and serves it read-only.
/// </summary>
/// <remarks>
/// <para>Accepted shapes, and nothing else:</para>
/// <list type="bullet">
///   <item><c>events/&lt;eventId&gt;/guest-cycles/&lt;guestCycleId&gt;/&lt;file&gt;</c> →
///   <c>&lt;dataRoot&gt;/Events/&lt;eventId&gt;/GuestCycles/&lt;guestCycleId&gt;/&lt;file&gt;</c></item>
///   <item><c>thumbnails/&lt;file&gt;</c> → <c>&lt;dataRoot&gt;/Cache/thumbnails/&lt;file&gt;</c></item>
/// </list>
/// <para>
/// The request path is the URL path as received, still percent-encoded, with an optional leading <c>/</c> and without
/// the query or fragment (the caller strips those; <c>?v=&lt;sha&gt;</c> is only a cache buster). The policy refuses any
/// percent-encoded <c>/</c>, <c>\</c>, or <c>.</c> (<c>%2F</c>, <c>%5C</c>, <c>%2E</c>, either case) and any malformed
/// escape, then decodes the remaining escapes. A path the caller already decoded is handled the same way, because valid
/// segments contain only unreserved characters.
/// </para>
/// <para>
/// Segment rules: the fixed segments match exactly (ordinal, lower case); ids are <c>[A-Za-z0-9-]{1,64}</c>; file names
/// are <c>[A-Za-z0-9._-]{1,128}</c>, don't start or end with a dot (so never <c>.</c> or <c>..</c>), and aren't Windows
/// device names (<c>CON</c>, <c>NUL</c>, <c>COM1</c>, …, with or without an extension; this applies to ids too). Empty
/// segments, backslashes, colons (drive letters and alternate data streams), and every other character are refused. As
/// defense in depth, the combined path is normalized with <see cref="Path.GetFullPath(string)"/> and must still lie
/// inside the allowed root.
/// </para>
/// </remarks>
public static class MediaPathPolicy
{
    public const int MaxIdLength = 64;
    public const int MaxFileNameLength = 128;

    private static readonly string[] DeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>Resolves <paramref name="requestPath"/> to a full file path inside <paramref name="dataRoot"/>.</summary>
    /// <param name="dataRoot">The fully qualified Data folder.</param>
    /// <param name="requestPath">The raw URL path of the <c>media.fotohavn</c> request (see the remarks).</param>
    /// <param name="fullPath">The normalized file path when the request is accepted; otherwise <see cref="string.Empty"/>.</param>
    /// <returns>Whether the request has an accepted shape. False means the host answers 404.</returns>
    /// <exception cref="ArgumentException"><paramref name="dataRoot"/> isn't a fully qualified path.</exception>
    public static bool TryResolve(string dataRoot, string requestPath, out string fullPath)
    {
        ArgumentNullException.ThrowIfNull(dataRoot);
        ArgumentNullException.ThrowIfNull(requestPath);
        if (!Path.IsPathFullyQualified(dataRoot))
        {
            throw new ArgumentException("The Data folder must be a fully qualified path.", nameof(dataRoot));
        }

        fullPath = string.Empty;
        if (!TryDecode(requestPath, out var decoded))
        {
            return false;
        }

        var segments = (decoded.StartsWith('/') ? decoded[1..] : decoded).Split('/');
        string root;
        string candidate;
        if (segments is ["events", var eventId, "guest-cycles", var guestCycleId, var eventFile]
            && IsId(eventId) && IsId(guestCycleId) && IsFileName(eventFile))
        {
            root = Path.GetFullPath(Path.Join(dataRoot, "Events"));
            candidate = Path.Join(root, eventId, "GuestCycles", guestCycleId, eventFile);
        }
        else if (segments is ["thumbnails", var thumbnailFile] && IsFileName(thumbnailFile))
        {
            root = Path.GetFullPath(Path.Join(dataRoot, "Cache", "thumbnails"));
            candidate = Path.Join(root, thumbnailFile);
        }
        else
        {
            return false;
        }

        return TryNormalizeInside(root, candidate, out fullPath);
    }

    /// <summary>
    /// Normalizes <paramref name="candidate"/> and accepts it only if it lies strictly below <paramref name="root"/>
    /// (already normalized). Defense in depth: after the segment rules, a refusal here would mean a rule mistake.
    /// </summary>
    internal static bool TryNormalizeInside(string root, string candidate, out string fullPath)
    {
        var normalized = Path.GetFullPath(candidate);
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        if (normalized.Length > prefix.Length && normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            fullPath = normalized;
            return true;
        }

        fullPath = string.Empty;
        return false;
    }

    private static bool TryDecode(string path, out string decoded)
    {
        var builder = new System.Text.StringBuilder(path.Length);
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] != '%')
            {
                builder.Append(path[i]);
                continue;
            }

            if (i + 2 >= path.Length || !char.IsAsciiHexDigit(path[i + 1]) || !char.IsAsciiHexDigit(path[i + 2]))
            {
                decoded = string.Empty;
                return false;
            }

            var value = (char)Convert.ToByte(path.Substring(i + 1, 2), 16);
            if (value is '/' or '\\' or '.')
            {
                decoded = string.Empty;
                return false;
            }

            builder.Append(value);
            i += 2;
        }

        decoded = builder.ToString();
        return true;
    }

    private static bool IsId(string segment) =>
        segment.Length is > 0 and <= MaxIdLength
        && segment.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')
        && !IsDeviceName(segment);

    private static bool IsFileName(string segment) =>
        segment.Length is > 0 and <= MaxFileNameLength
        && segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')
        && !segment.StartsWith('.')
        && !segment.EndsWith('.')
        && !IsDeviceName(segment);

    private static bool IsDeviceName(string segment)
    {
        var dot = segment.IndexOf('.', StringComparison.Ordinal);
        var stem = dot < 0 ? segment : segment[..dot];
        return DeviceNames.Contains(stem, StringComparer.OrdinalIgnoreCase);
    }
}
