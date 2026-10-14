using FotoHavn.Core.Camera;
using FotoHavn.Protocol.Cameras;
using Windows.Graphics.Imaging;
using Windows.Media.Capture.Frames;

namespace FotoHavn.Camera.Windows.Tests;

public sealed class CameraFormatMapperTests
{
    [Fact]
    public void Maps_the_subtype_to_upper_case_and_the_frame_rate_to_fps()
    {
        Assert.Equal(new CameraFormat(1920, 1080, 30, "NV12"), CameraFormatMapper.Create("nv12", 1920, 1080, 30, 1));
        Assert.Equal(new CameraFormat(1280, 720, 29.97, "YUY2"), CameraFormatMapper.Create(" yuy2 ", 1280, 720, 2997, 100));
        Assert.Equal(new CameraFormat(640, 480, 7.5, "MJPG"), CameraFormatMapper.Create("MJPG", 640, 480, 15, 2));
    }

    [Fact]
    public void A_zero_denominator_gives_zero_fps_and_a_missing_subtype_is_empty()
    {
        Assert.Equal(new CameraFormat(640, 480, 0, string.Empty), CameraFormatMapper.Create(null, 640, 480, 30, 0));
    }

    [Fact]
    public void Index_of_returns_the_first_equal_format_or_minus_one()
    {
        CameraFormat[] formats =
        [
            new(640, 480, 30, "YUY2"),
            new(1920, 1080, 30, "NV12"),
            new(1920, 1080, 30, "NV12"),
        ];

        Assert.Equal(1, CameraFormatMapper.IndexOf(formats, new CameraFormat(1920, 1080, 30, "NV12")));
        Assert.Equal(-1, CameraFormatMapper.IndexOf(formats, new CameraFormat(1280, 720, 30, "NV12")));
        Assert.Equal(-1, CameraFormatMapper.IndexOf([], new CameraFormat(1, 1, 1, "X")));
    }

    [Fact]
    public void Success_is_no_failure()
    {
        Assert.Null(CameraFormatMapper.ForStartStatus(MediaFrameReaderStartStatus.Success));
    }

    [Theory]
    [InlineData(MediaFrameReaderStartStatus.ExclusiveControlNotAvailable, CameraFailureKind.InUseByAnotherApp)]
    [InlineData(MediaFrameReaderStartStatus.DeviceNotAvailable, CameraFailureKind.InUseByAnotherApp)]
    [InlineData(MediaFrameReaderStartStatus.OutputFormatNotSupported, CameraFailureKind.NoSupportedFormat)]
    [InlineData(MediaFrameReaderStartStatus.UnknownFailure, CameraFailureKind.Unknown)]
    public void A_failed_start_status_maps_to_a_camera_failure_kind(MediaFrameReaderStartStatus status, CameraFailureKind expected)
    {
        var failure = CameraFormatMapper.ForStartStatus(status);

        Assert.NotNull(failure);
        Assert.Equal(expected, failure.Kind);
    }
}

public sealed class SoftwareBitmapCopierTests
{
    [Fact]
    public void Copies_a_bgra_bitmap_as_it_is()
    {
        using var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 16, 8, BitmapAlphaMode.Premultiplied);
        var pixels = new byte[16 * 8 * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 10;
            pixels[index + 1] = 20;
            pixels[index + 2] = 30;
            pixels[index + 3] = 255;
        }

        bitmap.CopyFromBuffer(System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(pixels));
        var destination = new PinnedBuffer();

        var stride = new SoftwareBitmapCopier().Copy(bitmap, destination);

        Assert.Equal(16 * 4, stride);
        Assert.Equal(pixels, destination.AsSpan(pixels.Length).ToArray());
    }

    [Fact]
    public void Converts_another_pixel_format_to_bgra_and_leaves_the_source_alone()
    {
        using var bitmap = new SoftwareBitmap(BitmapPixelFormat.Rgba8, 16, 8, BitmapAlphaMode.Premultiplied);
        var pixels = new byte[16 * 8 * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 30; // R
            pixels[index + 1] = 20; // G
            pixels[index + 2] = 10; // B
            pixels[index + 3] = 255;
        }

        bitmap.CopyFromBuffer(System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(pixels));
        var destination = new PinnedBuffer();

        var stride = new SoftwareBitmapCopier().Copy(bitmap, destination);

        Assert.Equal(16 * 4, stride);
        Assert.Equal([(byte)10, (byte)20, (byte)30, (byte)255], destination.AsSpan(4).ToArray());
        Assert.Equal(BitmapPixelFormat.Rgba8, bitmap.BitmapPixelFormat);
    }

    [Fact]
    public void Converts_nv12_which_is_what_most_webcams_deliver()
    {
        using var bitmap = new SoftwareBitmap(BitmapPixelFormat.Nv12, 32, 16, BitmapAlphaMode.Ignore);
        var destination = new PinnedBuffer();

        var stride = new SoftwareBitmapCopier().Copy(bitmap, destination);

        Assert.Equal(32 * 4, stride);
        Assert.True(destination.Capacity >= 32 * 16 * 4);
    }

    [Fact]
    public void Reuses_its_buffer_for_frames_of_the_same_size_and_grows_for_larger_ones()
    {
        var copier = new SoftwareBitmapCopier();
        var destination = new PinnedBuffer();
        using var small = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 16, 8, BitmapAlphaMode.Premultiplied);
        using var large = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 64, 32, BitmapAlphaMode.Premultiplied);

        Assert.Equal(64, copier.Copy(small, destination));
        Assert.Equal(64, copier.Copy(small, destination));
        Assert.Equal(256, copier.Copy(large, destination));
        Assert.Equal(64, copier.Copy(small, destination));
    }

    [Fact]
    public void The_frame_adapter_reports_the_bitmap_size_and_copies_on_demand()
    {
        using var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 16, 8, BitmapAlphaMode.Premultiplied);
        var frame = new SoftwareBitmapFrame(bitmap, new SoftwareBitmapCopier());

        Assert.Equal((16, 8), (frame.Width, frame.Height));
        Assert.Equal(64, frame.CopyTo(new PinnedBuffer()));
    }

    [Fact]
    public void Rejects_nulls()
    {
        var copier = new SoftwareBitmapCopier();
        Assert.Throws<ArgumentNullException>(() => copier.Copy(null!, new PinnedBuffer()));
        using var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 4, 4);
        Assert.Throws<ArgumentNullException>(() => copier.Copy(bitmap, null!));
    }
}

public sealed class PinnedBufferTests
{
    [Fact]
    public void Grows_only_when_needed_and_exposes_a_stable_pointer()
    {
        var buffer = new PinnedBuffer();
        Assert.Equal(0, buffer.Capacity);

        Assert.True(buffer.EnsureCapacity(100));
        var pointer = buffer.Pointer;
        Assert.False(buffer.EnsureCapacity(50));
        Assert.False(buffer.EnsureCapacity(100));
        Assert.Equal(pointer, buffer.Pointer);
        Assert.True(buffer.EnsureCapacity(101));
        Assert.True(buffer.Capacity >= 101);
        Assert.Equal(10, buffer.AsSpan(10).Length);
        Assert.Equal(10, buffer.AsMemory(10).Length);
    }

    [Fact]
    public void Rejects_a_negative_length() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PinnedBuffer().EnsureCapacity(-1));
}
