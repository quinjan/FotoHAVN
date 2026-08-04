using FotoHavn.Core;

namespace FotoHavn.App;

internal sealed class CameraBoundary : ICameraBoundary
{
    // Launching Saved Events must not acquire Camera hardware.
    public Task<CameraReadiness> GetReadinessAsync(CancellationToken cancellationToken) =>
        Task.FromResult(CameraReadiness.NotChecked);
}
