namespace FotoHavn.Catalog;

/// <summary>The centre-cover crop of a source image, a port of the website's <c>coverCrop</c>.</summary>
public static class CoverCrop
{
    /// <summary>
    /// The largest rectangle of aspect ratio <paramref name="targetRatio"/> (width over height) centred in the source, in
    /// source coordinates.
    /// </summary>
    /// <exception cref="ArgumentException">The source has no usable dimensions, or the ratio is not a positive finite number.</exception>
    public static (double X, double Y, double Width, double Height) Compute(double sourceWidth, double sourceHeight, double targetRatio)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || !double.IsFinite(sourceWidth + sourceHeight))
        {
            throw new ArgumentException("The photograph has no usable dimensions.");
        }

        if (targetRatio <= 0 || !double.IsFinite(targetRatio))
        {
            throw new ArgumentException("The target ratio must be a positive finite number.", nameof(targetRatio));
        }

        var cropWidth = Math.Min(sourceWidth, sourceHeight * targetRatio);
        var cropHeight = cropWidth / targetRatio;
        return ((sourceWidth - cropWidth) / 2, (sourceHeight - cropHeight) / 2, cropWidth, cropHeight);
    }
}
