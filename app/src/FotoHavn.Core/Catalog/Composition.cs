using FotoHavn.Core.Camera;

namespace FotoHavn.Core.Catalog;

// The compositor port (plan section 12, ADR 0006). The brain composes Photo Strips and Look thumbnails through ICompositor,
// off its executor, and posts the result back. FotoHavn.Catalog has the SkiaSharp implementation; Core has only the contract.

/// <summary>One photograph handed to the compositor.</summary>
public abstract record CompositionPhoto
{
    private CompositionPhoto()
    {
    }

    /// <summary>A JPEG or PNG the compositor decodes (a saved Capture). Decode failure is <see cref="CompositionErrorKind.UndecodablePhoto"/>.</summary>
    public sealed record Encoded(ReadOnlyMemory<byte> Bytes) : CompositionPhoto;

    /// <summary>A raw BGRA frame, already mirrored when the Event mirrors (diagnostics captures).</summary>
    public sealed record Bgra(CapturedFrame Frame) : CompositionPhoto;
}

/// <summary>One Photo Strip to compose.</summary>
/// <param name="TemplateId">The Template's ID.</param>
/// <param name="LookId">The Look's ID, applied to photograph windows only.</param>
/// <param name="Photos">One entry per Template window, in window order. Null is an empty window; all null is a blank preview.</param>
public sealed record CompositionRequest(string TemplateId, string LookId, IReadOnlyList<CompositionPhoto?> Photos);

/// <summary>A composed image: an opaque, lossless PNG.</summary>
/// <param name="Png">The PNG bytes.</param>
/// <param name="Width">The image width in pixels.</param>
/// <param name="Height">The image height in pixels.</param>
public sealed record CompositionResult(ReadOnlyMemory<byte> Png, int Width, int Height);

/// <summary>
/// Composes Photo Strips and Look thumbnails. Implementations are thread-safe and never touch the brain executor: the brain
/// calls them off the executor and posts the result back.
/// </summary>
public interface ICompositor
{
    /// <summary>Composes the Template with the Look and the photos at the Template's native size.</summary>
    /// <exception cref="CompositionException">The request is wrong (unknown Template or Look, wrong photo count), a photo can't be decoded, or composing failed.</exception>
    Task<CompositionResult> ComposeAsync(CompositionRequest request, CancellationToken ct);

    /// <summary>
    /// Centre-covers <paramref name="photo"/> to <paramref name="aspectRatio"/> (width over height), scales it to
    /// <paramref name="width"/>, and applies the Look: the Filters step's thumbnail.
    /// </summary>
    /// <exception cref="CompositionException">The Look is unknown, the photo can't be decoded, or composing failed.</exception>
    Task<CompositionResult> ComposeLookThumbnailAsync(CompositionPhoto photo, double aspectRatio, string lookId, int width, CancellationToken ct);
}

/// <summary>Why composing failed.</summary>
public enum CompositionErrorKind
{
    UnknownTemplate,
    UnknownLook,
    WrongPhotoCount,
    UndecodablePhoto,
    Unknown,
}

/// <summary>Composing failed with a classified cause.</summary>
public sealed class CompositionException : Exception
{
    public CompositionException()
        : this(CompositionErrorKind.Unknown)
    {
    }

    public CompositionException(string message)
        : base(message)
    {
        Kind = CompositionErrorKind.Unknown;
    }

    public CompositionException(string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = CompositionErrorKind.Unknown;
    }

    public CompositionException(CompositionErrorKind kind)
        : base($"Composing failed: {kind}.")
    {
        Kind = kind;
    }

    public CompositionException(CompositionErrorKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public CompositionException(CompositionErrorKind kind, string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }

    /// <summary>The classified cause.</summary>
    public CompositionErrorKind Kind { get; }
}
