namespace FotoHavn.Core.Catalog;

/// <summary>
/// The compositor the hosts pass until the SkiaSharp one is composed in.
/// TODO(M7 wave C): delete this class and have both hosts create the real compositor from FotoHavn.Catalog.
/// </summary>
public sealed class NotYetComposingCompositor : ICompositor
{
    public Task<CompositionResult> ComposeAsync(CompositionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromException<CompositionResult>(NotYet());
    }

    public Task<CompositionResult> ComposeLookThumbnailAsync(CompositionPhoto photo, double aspectRatio, string lookId, int width, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(photo);
        return Task.FromException<CompositionResult>(NotYet());
    }

    private static CompositionException NotYet() => new(CompositionErrorKind.Unknown, "The compositor is not composed into this host yet.");
}
