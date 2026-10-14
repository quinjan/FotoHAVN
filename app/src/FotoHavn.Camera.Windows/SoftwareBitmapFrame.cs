using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace FotoHavn.Camera.Windows;

/// <summary>Copies a <see cref="SoftwareBitmap"/> into a <see cref="PinnedBuffer"/> as BGRA8, converting the pixel format when needed.</summary>
internal sealed class SoftwareBitmapCopier
{
    private global::Windows.Storage.Streams.Buffer? _buffer;

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>Copies the bitmap and returns the row stride in bytes. The caller keeps ownership of <paramref name="bitmap"/>.</summary>
    public int Copy(SoftwareBitmap bitmap, PinnedBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(destination);

        var converted = bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8
            ? null
            : SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        try
        {
            var source = converted ?? bitmap;
            Width = source.PixelWidth;
            Height = source.PixelHeight;
            var capacity = checked((uint)(Width * Height * 4));
            if (_buffer is null || _buffer.Capacity < capacity)
            {
                _buffer = new global::Windows.Storage.Streams.Buffer(capacity);
            }

            source.CopyToBuffer(_buffer);
            var length = checked((int)_buffer.Length);
            destination.EnsureCapacity(length);
            _buffer.CopyTo(0, destination.Array, 0, length);
            return length / Height;
        }
        finally
        {
            converted?.Dispose();
        }
    }
}

/// <summary>A camera frame as a software bitmap, adapted to the pipeline.</summary>
internal sealed class SoftwareBitmapFrame(SoftwareBitmap bitmap, SoftwareBitmapCopier copier) : IBgraFrame
{
    public int Width { get; } = bitmap.PixelWidth;

    public int Height { get; } = bitmap.PixelHeight;

    public int CopyTo(PinnedBuffer destination) => copier.Copy(bitmap, destination);
}
