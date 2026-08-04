using Windows.Media.MediaProperties;

namespace FotoHavn.CameraRigPrototype;

public sealed record StreamCapability(
    string Stream,
    string Subtype,
    uint Width,
    uint Height,
    double FramesPerSecond)
{
    public static StreamCapability From(string stream, IMediaEncodingProperties properties)
    {
        var video = properties as VideoEncodingProperties;
        var fps = video?.FrameRate.Denominator > 0
            ? (double)video.FrameRate.Numerator / video.FrameRate.Denominator
            : 0;

        return new StreamCapability(
            stream,
            properties.Subtype ?? "unknown",
            video?.Width ?? 0,
            video?.Height ?? 0,
            fps);
    }
}

public sealed record CapabilityVerdict(
    bool PreviewPassed,
    bool PhotoPassed,
    StreamCapability? SelectedPreview,
    StreamCapability? SelectedPhoto,
    IReadOnlyList<StreamCapability> Observed);

public static class CameraCapability
{
    // Field-test minimums. A 1280x720 Capture leaves
    // downsampling headroom for each roughly 600x400 landscape print slot.
    public const uint MinimumPreviewWidth = 640;
    public const uint MinimumPreviewHeight = 480;
    public const double MinimumPreviewFramesPerSecond = 15;
    public const uint MinimumPhotoWidth = 1280;
    public const uint MinimumPhotoHeight = 720;

    public static CapabilityVerdict Evaluate(
        IEnumerable<IMediaEncodingProperties> previewProperties,
        IEnumerable<IMediaEncodingProperties> photoProperties)
    {
        var previews = previewProperties
            .Select(value => StreamCapability.From("preview", value))
            .ToList();
        var photos = photoProperties
            .Select(value => StreamCapability.From("photo", value))
            .ToList();

        var selectedPreview = previews
            .Where(value => value.Width >= MinimumPreviewWidth
                && value.Height >= MinimumPreviewHeight
                && value.FramesPerSecond >= MinimumPreviewFramesPerSecond)
            .OrderBy(value => (long)value.Width * value.Height)
            .ThenBy(value => value.FramesPerSecond)
            .FirstOrDefault();

        var selectedPhoto = photos
            .Where(value => value.Width >= MinimumPhotoWidth
                && value.Height >= MinimumPhotoHeight)
            .OrderByDescending(value => (long)value.Width * value.Height)
            .FirstOrDefault();

        return new CapabilityVerdict(
            selectedPreview is not null,
            selectedPhoto is not null,
            selectedPreview,
            selectedPhoto,
            previews.Concat(photos).ToList());
    }
}
