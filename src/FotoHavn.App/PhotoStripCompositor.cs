using FotoHavn.Core;

namespace FotoHavn.App;

internal sealed class PhotoStripCompositor : IPhotoStripCompositor
{
    public Task<PhotoStripCompositionResult> ComposeAsync(
        PhotoStripCompositionRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new PhotoStripCompositionResult(false, ReadOnlyMemory<byte>.Empty, 0, 0));
}
