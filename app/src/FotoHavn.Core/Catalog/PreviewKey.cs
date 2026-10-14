using System.Globalization;

namespace FotoHavn.Core.Catalog;

/// <summary>The preview cache key for a composed Template, Look, and arrangement revision.</summary>
public static class PreviewKey
{
    /// <summary>
    /// A deterministic single path segment, <c>&lt;templateId&gt;.&lt;lookId&gt;.r&lt;revision&gt;</c>, for example
    /// <c>signature-strip.naturale-v1.r42</c>. It uses only lowercase letters, digits, <c>.</c>, and <c>-</c>, so it is
    /// a valid segment of <c>https://preview.fotohavn/&lt;key&gt;.png</c>.
    /// </summary>
    /// <exception cref="ArgumentException">An ID is empty or has a character outside lowercase letters, digits, and <c>-</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The revision is negative.</exception>
    public static string For(string templateId, string lookId, long arrangementRevision)
    {
        ValidateId(templateId, nameof(templateId));
        ValidateId(lookId, nameof(lookId));
        ArgumentOutOfRangeException.ThrowIfNegative(arrangementRevision);
        return string.Create(CultureInfo.InvariantCulture, $"{templateId}.{lookId}.r{arrangementRevision}");
    }

    /// <summary>
    /// Whether <paramref name="key"/> has exactly the shape <see cref="For"/> makes. The hosts answer 404 to any other key
    /// without looking in the cache.
    /// </summary>
    public static bool IsValid(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var parts = key.Split('.');
        return parts.Length == 3
            && IsId(parts[0])
            && IsId(parts[1])
            && parts[2].Length > 1
            && parts[2][0] == 'r'
            && parts[2].AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0
            && long.TryParse(parts[2].AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    private static void ValidateId(string id, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(id, name);
        if (!IsId(id))
        {
            throw new ArgumentException("An ID uses only lowercase letters, digits, and '-'.", name);
        }
    }

    private static bool IsId(string id) =>
        id.Length > 0 && id.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-');
}
