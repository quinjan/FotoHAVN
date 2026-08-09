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

        var viewportAspectRatio = availableWidth / availableHeight;
        var visibleSource = viewportAspectRatio > StreamAspectRatio
            ? new CameraSourceCrop(
                X: 0,
                Y: (1 - (StreamAspectRatio / viewportAspectRatio)) / 2,
                Width: 1,
                Height: StreamAspectRatio / viewportAspectRatio)
            : new CameraSourceCrop(
                X: (1 - (viewportAspectRatio / StreamAspectRatio)) / 2,
                Y: 0,
                Width: viewportAspectRatio / StreamAspectRatio,
                Height: 1);

        var guideWidth = Math.Min(availableWidth, availableHeight * CropAspectRatio);
        var guideHeight = guideWidth / CropAspectRatio;
        var guideLeft = (availableWidth - guideWidth) / 2;
        var guideTop = (availableHeight - guideHeight) / 2;
        var sourceCrop = new CameraSourceCrop(
            visibleSource.X + ((guideLeft / availableWidth) * visibleSource.Width),
            visibleSource.Y + ((guideTop / availableHeight) * visibleSource.Height),
            (guideWidth / availableWidth) * visibleSource.Width,
            (guideHeight / availableHeight) * visibleSource.Height);
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
