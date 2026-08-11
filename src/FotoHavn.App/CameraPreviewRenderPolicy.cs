using FotoHavn.Core;

namespace FotoHavn.App;

internal static class CameraPreviewRenderPolicy
{
    public const double StreamAspectRatio = 16d / 9d;
    public const double CropAspectRatio = CaptureCropPolicy.AspectRatio;

    public static CameraPreviewLayout CalculateLayout(double availableWidth, double availableHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(availableWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(availableHeight);

        var sourceWidth = Math.Min(availableWidth, availableHeight * StreamAspectRatio);
        var sourceHeight = Math.Min(availableHeight, sourceWidth / StreamAspectRatio);
        var sourceLeft = (availableWidth - sourceWidth) / 2;
        var sourceTop = (availableHeight - sourceHeight) / 2;

        var guideWidth = Math.Min(sourceWidth, sourceHeight * CropAspectRatio);
        var guideHeight = Math.Min(sourceHeight, guideWidth / CropAspectRatio);
        var guideLeft = sourceLeft + ((sourceWidth - guideWidth) / 2);
        var guideTop = sourceTop + ((sourceHeight - guideHeight) / 2);
        var sourceCrop = new CameraSourceCrop(
            (guideLeft - sourceLeft) / sourceWidth,
            (guideTop - sourceTop) / sourceHeight,
            guideWidth / sourceWidth,
            guideHeight / sourceHeight);
        return new CameraPreviewLayout(
            guideLeft,
            guideTop,
            guideWidth,
            guideHeight,
            sourceCrop);
    }

    public static CameraMirrorTransform CreateMirror(double viewportWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(viewportWidth);
        return new CameraMirrorTransform(ScaleX: -1, CenterX: viewportWidth / 2);
    }
}

internal sealed record CameraPreviewLayout(
    double GuideLeft,
    double GuideTop,
    double GuideWidth,
    double GuideHeight,
    CameraSourceCrop SourceCrop);

internal sealed record CameraSourceCrop(
    double X,
    double Y,
    double Width,
    double Height);

internal sealed record CameraMirrorTransform(double ScaleX, double CenterX)
{
    public double TransformX(double x) => CenterX + ((x - CenterX) * ScaleX);
}
