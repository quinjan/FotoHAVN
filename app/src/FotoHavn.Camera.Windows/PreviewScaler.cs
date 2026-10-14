using System.Runtime.InteropServices;
using SkiaSharp;

namespace FotoHavn.Camera.Windows;

/// <summary>
/// Turns a full-size BGRA frame into a preview frame: at most 1280 px wide (aspect kept, never upscaled), RGBA, alpha 255. The
/// output buffer is reused, so the result is valid only until the next call.
/// </summary>
internal sealed class PreviewScaler
{
    public const int MaximumWidth = 1280;

    private const uint OpaqueAlpha = 0xFF000000u;

    private readonly PinnedBuffer _output = new();

    /// <summary>How many times the output buffer was (re)allocated: once per distinct larger size.</summary>
    public int Allocations { get; private set; }

    public static (int Width, int Height) TargetSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (width <= MaximumWidth)
        {
            return (width, height);
        }

        var scaledHeight = (int)Math.Round(height * (double)MaximumWidth / width, MidpointRounding.AwayFromZero);
        return (MaximumWidth, Math.Max(1, scaledHeight));
    }

    public (ReadOnlyMemory<byte> Rgba, int Width, int Height) Scale(PinnedBuffer source, int width, int height, int stride)
    {
        ArgumentNullException.ThrowIfNull(source);
        var (targetWidth, targetHeight) = TargetSize(width, height);
        var targetStride = targetWidth * 4;
        var length = targetStride * targetHeight;
        if (_output.EnsureCapacity(length))
        {
            Allocations++;
        }

        var sourceInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var targetInfo = new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var sourcePixels = new SKPixmap(sourceInfo, source.Pointer, stride);

        bool done;
        if (targetWidth == width && targetHeight == height)
        {
            done = sourcePixels.ReadPixels(targetInfo, _output.Pointer, targetStride);
        }
        else
        {
            using var targetPixels = new SKPixmap(targetInfo, _output.Pointer, targetStride);
            done = sourcePixels.ScalePixels(targetPixels, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }

        if (!done)
        {
            throw new InvalidOperationException($"Skia could not convert a {width}x{height} BGRA frame to a {targetWidth}x{targetHeight} preview.");
        }

        // A camera's BGRA frames may carry any alpha; the preview is opaque by contract.
        var pixels = MemoryMarshal.Cast<byte, uint>(_output.AsSpan(length));
        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] |= OpaqueAlpha;
        }

        return (_output.AsMemory(length), targetWidth, targetHeight);
    }
}
