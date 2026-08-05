using FotoHavn.Core;

namespace FotoHavn.App;

internal static class CameraPreviewRenderPolicy
{
    public const double CropAspectRatio = CaptureCropPolicy.AspectRatio;

    public static CameraMirrorTransform CreateMirror(double viewportWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(viewportWidth);
        return new CameraMirrorTransform(ScaleX: -1, CenterX: viewportWidth / 2);
    }
}

internal sealed record CameraMirrorTransform(double ScaleX, double CenterX)
{
    public double TransformX(double x) => CenterX + ((x - CenterX) * ScaleX);
}
