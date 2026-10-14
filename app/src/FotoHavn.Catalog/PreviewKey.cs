namespace FotoHavn.Catalog;

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
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{templateId}.{lookId}.r{arrangementRevision}");
    }

    private static void ValidateId(string id, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(id, name);
        if (!id.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-'))
        {
            throw new ArgumentException("An ID uses only lowercase letters, digits, and '-'.", name);
        }
    }
}
