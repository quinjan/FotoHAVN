using Windows.Media.Capture.Frames;

namespace FotoHavn.App;

internal static class CameraFormatSelector
{
    private static readonly (uint Width, uint Height, double FramesPerSecond)[] Tiers =
    [
        (1920, 1080, 30),
        (1920, 1080, 15),
        (1280, 720, 30),
        (1280, 720, 15),
    ];

    public static IReadOnlyList<MediaFrameFormat> SelectOnePerTier(IEnumerable<MediaFrameFormat> formats) =>
        SelectOnePerTier(formats, format => new CameraFormatInfo(
            format.VideoFormat?.Width ?? 0,
            format.VideoFormat?.Height ?? 0,
            FramesPerSecond(format)));

    internal static IReadOnlyList<T> SelectOnePerTier<T>(
        IEnumerable<T> formats,
        Func<T, CameraFormatInfo> describe) where T : class
    {
        var candidates = formats.Select(format => (Format: format, Info: describe(format)))
            .Where(candidate => CameraStreamBounds.IsSuitable(
                candidate.Info.Width,
                candidate.Info.Height,
                candidate.Info.FramesPerSecond))
            .ToArray();

        var selected = new List<T>(Tiers.Length);
        foreach (var tier in Tiers)
        {
            var match = candidates
                .Where(candidate => candidate.Info.Width == tier.Width && candidate.Info.Height == tier.Height)
                .Where(candidate => Math.Abs(candidate.Info.FramesPerSecond - tier.FramesPerSecond) <= 0.5)
                .OrderBy(candidate => Math.Abs(candidate.Info.FramesPerSecond - tier.FramesPerSecond))
                .FirstOrDefault();
            if (match.Format is not null)
            {
                selected.Add(match.Format);
            }
        }

        return selected;
    }

    private static double FramesPerSecond(MediaFrameFormat format) =>
        format.FrameRate.Denominator == 0
            ? 0
            : (double)format.FrameRate.Numerator / format.FrameRate.Denominator;
}

internal sealed record CameraFormatInfo(uint Width, uint Height, double FramesPerSecond);

internal static class CameraStreamBounds
{
    public static bool IsSuitable(uint width, uint height, double framesPerSecond) =>
        width is >= 1280 and <= 1920 &&
        height is >= 720 and <= 1080 &&
        width > height &&
        framesPerSecond >= 15;
}

internal static class CameraFrameEligibility
{
    public static bool IsEligible(int width, int height, bool isDecoded) =>
        isDecoded && CameraStreamBounds.IsSuitable((uint)width, (uint)height, framesPerSecond: 15);
}

internal sealed record CameraFormatFallbackResult<T>(T? Value, Exception? LastFailure) where T : class;

internal static class CameraFormatFallback
{
    public static async Task<CameraFormatFallbackResult<TResult>> TryEachAsync<TCandidate, TResult>(
        IEnumerable<TCandidate> candidates,
        Func<TCandidate, Task<TResult>> tryCandidate,
        CancellationToken cancellationToken) where TResult : class
    {
        Exception? lastFailure = null;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new CameraFormatFallbackResult<TResult>(
                    await tryCandidate(candidate).ConfigureAwait(false),
                    lastFailure);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lastFailure = exception;
            }
        }

        return new CameraFormatFallbackResult<TResult>(null, lastFailure);
    }
}
