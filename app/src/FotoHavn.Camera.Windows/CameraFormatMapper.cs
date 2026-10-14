using FotoHavn.Core.Camera;
using FotoHavn.Protocol.Cameras;
using Windows.Media.Capture.Frames;

namespace FotoHavn.Camera.Windows;

/// <summary>Maps Windows media-frame types to the Core port's types.</summary>
internal static class CameraFormatMapper
{
    public static CameraFormat Create(string? subtype, uint width, uint height, uint frameRateNumerator, uint frameRateDenominator) =>
        new(
            checked((int)width),
            checked((int)height),
            frameRateDenominator == 0 ? 0 : (double)frameRateNumerator / frameRateDenominator,
            (subtype ?? string.Empty).Trim().ToUpperInvariant());

    public static CameraFormat Create(MediaFrameFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        var video = format.VideoFormat;
        return Create(format.Subtype, video?.Width ?? 0, video?.Height ?? 0, format.FrameRate.Numerator, format.FrameRate.Denominator);
    }

    /// <summary>The index of the first mapped format equal to <paramref name="chosen"/>, or -1.</summary>
    public static int IndexOf(IReadOnlyList<CameraFormat> mapped, CameraFormat chosen)
    {
        for (var index = 0; index < mapped.Count; index++)
        {
            if (mapped[index] == chosen)
            {
                return index;
            }
        }

        return -1;
    }

    public static CameraException? ForStartStatus(MediaFrameReaderStartStatus status) => status switch
    {
        MediaFrameReaderStartStatus.Success => null,
        MediaFrameReaderStartStatus.ExclusiveControlNotAvailable =>
            new CameraException(CameraFailureKind.InUseByAnotherApp, "Another app holds exclusive control of the camera."),
        MediaFrameReaderStartStatus.DeviceNotAvailable =>
            new CameraException(CameraFailureKind.InUseByAnotherApp, "The camera is not available to start streaming."),
        MediaFrameReaderStartStatus.OutputFormatNotSupported =>
            new CameraException(CameraFailureKind.NoSupportedFormat, "The frame reader does not support the camera's output format."),
        _ => new CameraException(CameraFailureKind.Unknown, $"The frame reader failed to start: {status}."),
    };
}
