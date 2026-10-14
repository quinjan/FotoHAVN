using System.Runtime.InteropServices;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;
using SkiaSharp;

namespace FotoHavn.Camera.Windows;

/// <summary>
/// Encodes a captured BGRA frame as a JPEG at <see cref="JpegEncodingSpec.Quality"/>. The pixels are treated as opaque sRGB and
/// the file carries no metadata (no EXIF, no ICC profile; an untagged JPEG is sRGB).
/// </summary>
public static class JpegEncoder
{
    public static byte[] Encode(CapturedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Width <= 0 || frame.Height <= 0)
        {
            throw new ArgumentException("The frame has no size.", nameof(frame));
        }

        if (frame.Stride < frame.Width * 4 || frame.Bgra.Length < frame.Stride * (frame.Height - 1) + (frame.Width * 4))
        {
            throw new ArgumentException("The frame's pixel data is smaller than its size and stride say.", nameof(frame));
        }

        // Pin the caller's array in place when it is array-backed; otherwise copy it once.
        byte[] array;
        int offset;
        if (MemoryMarshal.TryGetArray(frame.Bgra, out var segment) && segment.Array is not null)
        {
            array = segment.Array;
            offset = segment.Offset;
        }
        else
        {
            array = frame.Bgra.ToArray();
            offset = 0;
        }

        var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
        try
        {
            var pointer = handle.AddrOfPinnedObject() + offset;
            var info = new SKImageInfo(frame.Width, frame.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
            using var pixels = new SKPixmap(info, pointer, frame.Stride);
            var options = new SKJpegEncoderOptions(JpegEncodingSpec.Quality, SKJpegEncoderDownsample.Downsample420, SKJpegEncoderAlphaOption.Ignore);
            using var data = pixels.Encode(options);
            if (data is null)
            {
                throw new InvalidOperationException("Skia could not encode the frame as a JPEG.");
            }

            return data.ToArray();
        }
        finally
        {
            handle.Free();
        }
    }
}
