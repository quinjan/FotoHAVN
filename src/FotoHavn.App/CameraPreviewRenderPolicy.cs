using FotoHavn.Core;

namespace FotoHavn.App;

internal static class CameraPreviewRenderPolicy
{
    public const double MirrorScaleX = -1;
    public const double CropAspectRatio = CaptureCropPolicy.AspectRatio;
}
