using System.Runtime.InteropServices;
using FotoHavn.App;
using FotoHavn.Core;
using Windows.Media.Capture;
using Xunit;

namespace FotoHavn.WindowsIntegrationTests;

public sealed class CameraPolicyIntegrationTests
{
    [Fact]
    public void Opening_uses_the_exact_ID_with_exclusive_video_only_ownership()
    {
        var settings = CameraOpenPolicy.CreateSettings("exact-windows-device-id");

        Assert.Equal("exact-windows-device-id", settings.VideoDeviceId);
        Assert.Equal(StreamingCaptureMode.Video, settings.StreamingCaptureMode);
        Assert.Equal(MediaCaptureSharingMode.ExclusiveControl, settings.SharingMode);
        Assert.Equal(MediaCaptureMemoryPreference.Cpu, settings.MemoryPreference);
    }

    [Fact]
    public void Preview_mirroring_is_render_only_and_uses_the_shared_Photo_Strip_crop()
    {
        const double viewportWidth = 588;
        var mirror = CameraPreviewRenderPolicy.CreateMirror(viewportWidth);

        Assert.Equal(-1, mirror.ScaleX);
        Assert.Equal(viewportWidth / 2, mirror.CenterX);
        Assert.Equal(viewportWidth, mirror.TransformX(0));
        Assert.Equal(0, mirror.TransformX(viewportWidth));
        Assert.Equal(CaptureCropPolicy.AspectRatio, CameraPreviewRenderPolicy.CropAspectRatio);
        Assert.Equal(CaptureCropPolicy.AspectRatio, new PhotoStripCompositionRequest("Event", []).CaptureCropAspectRatio);
    }

    [Fact]
    public void Guest_preview_and_saved_Capture_preserve_the_full_camera_height()
    {
        var preview = CameraPreviewRenderPolicy.CalculateLayout(
            availableWidth: 1232,
            availableHeight: 596);
        var saved = CaptureFrameEncoder.CalculateCenterCrop(
            width: 1920,
            height: 1080,
            preview.SourceCrop);

        Assert.Equal(CaptureCropPolicy.AspectRatio, preview.GuideWidth / preview.GuideHeight, precision: 10);
        Assert.Equal(
            CaptureCropPolicy.AspectRatio,
            (preview.SourceCrop.Width * 1920) / (preview.SourceCrop.Height * 1080),
            precision: 10);
        Assert.Equal(169, preview.GuideLeft);
        Assert.Equal(0, preview.GuideTop);
        Assert.Equal(894, preview.GuideWidth);
        Assert.Equal(596, preview.GuideHeight);
        Assert.Equal(150u, saved.X);
        Assert.Equal(0u, saved.Y);
        Assert.Equal(1620u, saved.Width);
        Assert.Equal(1080u, saved.Height);
        Assert.Equal(0, preview.SourceCrop.Y);
        Assert.Equal(1, preview.SourceCrop.Height);
        Assert.InRange(Math.Abs(preview.SourceCrop.X - ((double)saved.X / 1920)), 0, 1d / 1920);
        Assert.InRange(Math.Abs(preview.SourceCrop.Y - ((double)saved.Y / 1080)), 0, 1d / 1080);
        Assert.InRange(Math.Abs(preview.SourceCrop.Width - ((double)saved.Width / 1920)), 0, 1d / 1920);
        Assert.InRange(Math.Abs(preview.SourceCrop.Height - ((double)saved.Height / 1080)), 0, 1d / 1080);
    }

    [Fact]
    public void Guest_preview_grid_tracks_are_non_negative_at_the_runtime_viewport_size()
    {
        const double viewportWidth = 1232;
        const double viewportHeight = 595;
        var preview = CameraPreviewRenderPolicy.CalculateLayout(viewportWidth, viewportHeight);

        var tracks = new[]
        {
            preview.GuideLeft,
            preview.GuideWidth,
            viewportWidth - preview.GuideLeft - preview.GuideWidth,
            preview.GuideTop,
            preview.GuideHeight,
            viewportHeight - preview.GuideTop - preview.GuideHeight,
        };

        Assert.All(tracks, track => Assert.True(track >= 0, $"Grid track was {track:R}."));
    }

    [Fact]
    public async Task Release_and_selected_device_removal_dispose_the_owned_stream()
    {
        var firstStream = new FakeOwnedStream();
        var owner = new CameraSessionOwner<FakeOwnedStream>();
        await owner.AdoptAsync("camera-1", "stream-1", firstStream);

        Assert.False(await owner.RemoveAsync("camera-2"));
        Assert.Equal(0, firstStream.DisposeCount);
        Assert.True(await owner.RemoveAsync("camera-1"));
        Assert.Equal(1, firstStream.DisposeCount);
        Assert.Null(owner.StreamId);

        var secondStream = new FakeOwnedStream();
        await owner.AdoptAsync("camera-2", "stream-2", secondStream);
        await owner.ReleaseAsync();
        Assert.Equal(1, secondStream.DisposeCount);
        Assert.Null(owner.DeviceId);
        Assert.Null(owner.StreamId);
    }

    [Fact]
    public async Task A_new_stream_can_be_reconstructed_after_the_previous_stream_is_released()
    {
        var failedStream = new FakeOwnedStream();
        var reconstructedStream = new FakeOwnedStream();
        var owner = new CameraSessionOwner<FakeOwnedStream>();
        await owner.AdoptAsync("camera-1", "failed-stream", failedStream);

        await owner.AdoptAsync("camera-1", "reconstructed-stream", reconstructedStream);

        Assert.Equal(1, failedStream.DisposeCount);
        Assert.Equal("camera-1", owner.DeviceId?.Value);
        Assert.Equal("reconstructed-stream", owner.StreamId);
    }

    [Fact]
    public async Task Runtime_format_fallback_advances_after_a_tier_attempt_fails()
    {
        var attempts = new List<string>();

        var result = await CameraFormatFallback.TryEachAsync<string, FakeOwnedStream>(
            ["1080p30", "1080p15", "720p30"],
            format =>
            {
                attempts.Add(format);
                return format == "1080p15"
                    ? Task.FromResult(new FakeOwnedStream())
                    : Task.FromException<FakeOwnedStream>(new InvalidOperationException("Format failed."));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(["1080p30", "1080p15"], attempts);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public void Format_fallback_is_bounded_and_prefers_one_best_match_per_tier()
    {
        FakeFormat[] formats =
        [
            new("too-large", 3840, 2160, 30),
            new("720p30", 1280, 720, 30),
            new("1080p15", 1920, 1080, 15),
            new("1080p60", 1920, 1080, 60),
            new("too-slow", 1920, 1080, 10),
        ];

        var selected = CameraFormatSelector.SelectOnePerTier(
            formats,
            format => new CameraFormatInfo(format.Width, format.Height, format.FramesPerSecond));

        Assert.Equal(["1080p15", "720p30"], selected.Select(format => format.Name));
    }

    [Fact]
    public void Format_fallback_rejects_streams_outside_the_field_test_bounds()
    {
        FakeFormat[] formats =
        [
            new("too-large", 2560, 1440, 30),
            new("too-small", 1024, 768, 30),
            new("too-slow", 1280, 720, 14),
            new("portrait", 1280, 1920, 30),
        ];

        var selected = CameraFormatSelector.SelectOnePerTier(
            formats,
            format => new CameraFormatInfo(format.Width, format.Height, format.FramesPerSecond));

        Assert.Empty(selected);
    }

    [Theory]
    [InlineData(1920, 1080, true, true)]
    [InlineData(1280, 720, true, true)]
    [InlineData(1920, 1080, false, false)]
    [InlineData(1024, 768, true, false)]
    [InlineData(1080, 1920, true, false)]
    public void Eligibility_requires_a_fresh_decoded_landscape_frame(
        int width,
        int height,
        bool isDecoded,
        bool expected) =>
        Assert.Equal(expected, CameraFrameEligibility.IsEligible(width, height, isDecoded));

    [Theory]
    [InlineData(unchecked((int)0x80070005), CameraOpenResult.AccessDenied)]
    [InlineData(unchecked((int)0xC00D3704), CameraOpenResult.InUse)]
    [InlineData(unchecked((int)0xC00D36D5), CameraOpenResult.Disconnected)]
    public void Windows_failures_map_to_operator_connection_states(int hresult, CameraOpenResult expected)
    {
        Exception exception = hresult == unchecked((int)0x80070005)
            ? new UnauthorizedAccessException()
            : new COMException("Camera failure", hresult);

        Assert.Equal(expected, CameraFailureMapper.Map(exception));
    }

    private sealed record FakeFormat(string Name, uint Width, uint Height, double FramesPerSecond);

    private sealed class FakeOwnedStream : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
