using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;
using FotoHavn.Core.Fakes;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Tests.Cameras;

public sealed class FakeTestPatternTests
{
    private const int Width = 1280;
    private const int Height = 720;

    [Fact]
    public void The_same_frame_always_draws_the_same_bytes()
    {
        var first = Render(7, rgba: true);
        var second = Render(7, rgba: true);

        Assert.True(first.AsSpan().SequenceEqual(second));
    }

    [Fact]
    public void Different_frames_differ_because_the_block_and_the_number_move()
    {
        var one = Render(1, rgba: true);
        var two = Render(2, rgba: true);

        Assert.False(one.AsSpan().SequenceEqual(two));
    }

    [Fact]
    public void Bgra_is_rgba_with_red_and_blue_swapped_and_alpha_is_opaque()
    {
        var rgba = Render(12, rgba: true);
        var bgra = Render(12, rgba: false);

        for (var offset = 0; offset < rgba.Length; offset += 4)
        {
            Assert.Equal(rgba[offset], bgra[offset + 2]);
            Assert.Equal(rgba[offset + 1], bgra[offset + 1]);
            Assert.Equal(rgba[offset + 2], bgra[offset]);
            Assert.Equal(255, rgba[offset + 3]);
        }
    }

    [Fact]
    public void The_frame_is_not_blank()
    {
        var pixels = Render(3, rgba: true);

        var distinct = new HashSet<int>();
        for (var offset = 0; offset < pixels.Length; offset += 4 * 97)
        {
            distinct.Add((pixels[offset] << 16) | (pixels[offset + 1] << 8) | pixels[offset + 2]);
        }

        Assert.True(distinct.Count > 8);
    }

    [Fact]
    public void The_block_wraps_inside_the_frame_for_any_sequence()
    {
        foreach (var sequence in new long[] { 0, 99, 100, 5_000, long.MaxValue / 24 })
        {
            Assert.Equal(Width * Height * 4, Render(sequence, rgba: true).Length);
        }
    }

    [Fact]
    public void A_narrow_frame_draws_only_the_digits_that_fit_and_a_negative_sequence_draws_zero()
    {
        var pixels = new byte[192 * 192 * 4];

        FakeTestPattern.Render(pixels, 192, 192, 123_456_789, rgba: true);
        var negative = new byte[192 * 192 * 4];
        FakeTestPattern.Render(negative, 192, 192, -5, rgba: true);
        var zero = new byte[192 * 192 * 4];
        FakeTestPattern.Render(zero, 192, 192, 0, rgba: true);

        Assert.True(negative.AsSpan().SequenceEqual(zero));
    }

    [Fact]
    public void The_frame_must_be_big_enough_and_the_buffer_must_hold_it()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FakeTestPattern.Render(new byte[100 * 100 * 4], 100, 100, 1, rgba: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => FakeTestPattern.Render(new byte[10], Width, Height, 1, rgba: true));
    }

    private static byte[] Render(long sequence, bool rgba)
    {
        var pixels = new byte[Width * Height * 4];
        FakeTestPattern.Render(pixels, Width, Height, sequence, rgba);
        return pixels;
    }
}

public sealed class FakePreviewChannelTests
{
    private static PreviewFrame Frame(long sequence) => new(sequence, 2, 2, new byte[16], TimeSpan.FromMilliseconds(sequence));

    [Fact]
    public void A_target_with_no_page_or_the_none_target_accepts_nothing()
    {
        var channel = new FakePreviewChannel();
        channel.SetConnected(PreviewTarget.Guest, false);

        Assert.False(channel.TryPost(PreviewTarget.None, Frame(1)));
        Assert.False(channel.TryPost(PreviewTarget.Guest, Frame(1)));
        Assert.Empty(channel.Posted);
        Assert.Equal(default, channel.GetStats(PreviewTarget.None));
        Assert.Equal(new PreviewFrameStats(0, 0, null, 0), channel.GetStats(PreviewTarget.Guest));
    }

    [Fact]
    public void By_default_every_frame_counts_as_delivered_at_the_clock_time()
    {
        var clock = new FakeClock();
        var channel = new FakePreviewChannel(clock);

        Assert.True(channel.TryPost(PreviewTarget.Operator, Frame(1)));
        Assert.True(channel.TryPost(PreviewTarget.Operator, Frame(2)));

        Assert.Equal(new PreviewFrameStats(2, 0, clock.UtcNow, 0), channel.GetStats(PreviewTarget.Operator));
        Assert.Equal([1L, 2L], channel.Posted.Select(post => post.Sequence));
        Assert.All(channel.Posted, post => Assert.Empty(post.Rgba));
    }

    [Fact]
    public void With_acks_held_back_one_frame_is_in_flight_per_target_and_the_rest_drop()
    {
        var channel = new FakePreviewChannel { AutoAcknowledge = false, KeepPixels = true };

        Assert.True(channel.TryPost(PreviewTarget.Operator, Frame(1)));
        Assert.False(channel.TryPost(PreviewTarget.Operator, Frame(2)));
        Assert.True(channel.TryPost(PreviewTarget.Guest, Frame(3)));
        Assert.False(channel.Acknowledge(PreviewTarget.None));
        Assert.True(channel.Acknowledge(PreviewTarget.Operator));
        Assert.False(channel.Acknowledge(PreviewTarget.Operator));
        Assert.True(channel.TryPost(PreviewTarget.Operator, Frame(4)));

        var operatorStats = channel.GetStats(PreviewTarget.Operator);
        Assert.Equal((1L, 1L), (operatorStats.Delivered, operatorStats.Dropped));
        Assert.NotNull(operatorStats.LastDeliveredAt);
        Assert.Equal(new PreviewFrameStats(0, 0, null, 0), channel.GetStats(PreviewTarget.Guest));
        Assert.All(channel.Posted, post => Assert.Equal(16, post.Rgba.Length));
    }

    [Fact]
    public void Disconnecting_a_target_clears_its_frame_in_flight()
    {
        var channel = new FakePreviewChannel { AutoAcknowledge = false };
        channel.TryPost(PreviewTarget.Guest, Frame(1));

        channel.SetConnected(PreviewTarget.Guest, false);
        channel.SetConnected(PreviewTarget.Guest, true);

        Assert.True(channel.TryPost(PreviewTarget.Guest, Frame(2)));
    }

    [Fact]
    public void A_null_channel_accepts_nothing_and_counts_nothing()
    {
        Assert.False(NullPreviewChannel.Instance.TryPost(PreviewTarget.Operator, Frame(1)));
        Assert.Equal(default, NullPreviewChannel.Instance.GetStats(PreviewTarget.Operator));
    }
}

public sealed class CameraContractTests
{
    [Fact]
    public void The_failure_kinds_are_the_agreed_set()
    {
        Assert.Equal(
            ["Stale", "Failed", "Removed", "ExclusiveLost", "InUseByAnotherApp", "PrivacyDenied", "NoSupportedFormat", "NotFound", "Unknown"],
            Enum.GetNames<CameraFailureKind>());
    }

    [Fact]
    public void A_camera_exception_carries_its_kind()
    {
        var inner = new InvalidOperationException("inner");

        Assert.Equal(CameraFailureKind.Unknown, new CameraException().Kind);
        Assert.Equal(CameraFailureKind.Unknown, new CameraException("message").Kind);
        Assert.Equal(CameraFailureKind.Unknown, new CameraException("message", inner).Kind);
        Assert.Same(inner, new CameraException("message", inner).InnerException);
        Assert.Equal(CameraFailureKind.Removed, new CameraException(CameraFailureKind.Removed).Kind);
        Assert.Contains("Removed", new CameraException(CameraFailureKind.Removed).Message, StringComparison.Ordinal);
        var detailed = new CameraException(CameraFailureKind.PrivacyDenied, "denied");
        Assert.Equal((CameraFailureKind.PrivacyDenied, "denied"), (detailed.Kind, detailed.Message));
        var wrapped = new CameraException(CameraFailureKind.NotFound, "gone", inner);
        Assert.Equal((CameraFailureKind.NotFound, "gone"), (wrapped.Kind, wrapped.Message));
        Assert.Same(inner, wrapped.InnerException);
    }

    [Fact]
    public void The_records_compare_by_value()
    {
        Assert.Equal(new AvailableCamera("id", "Name"), new AvailableCamera("id", "Name"));
        Assert.Equal(new CameraFormat(1920, 1080, 30, "NV12"), new CameraFormat(1920, 1080, 30, "NV12"));
        Assert.Equal(new CameraFrameInfo(1, TimeSpan.FromSeconds(1), 1280, 720), new CameraFrameInfo(1, TimeSpan.FromSeconds(1), 1280, 720));
        Assert.Equal(new CameraFailure(CameraFailureKind.Failed, "x"), new CameraFailure(CameraFailureKind.Failed, "x"));
        Assert.Equal(new PreviewFrameStats(1, 2, null, 3), new PreviewFrameStats(1, 2, null, 3));
        var pixels = new byte[4];
        Assert.Equal(new CapturedFrame(pixels, 1, 1, 4, TimeSpan.Zero), new CapturedFrame(pixels, 1, 1, 4, TimeSpan.Zero));
        Assert.Equal(new PreviewFrame(1, 1, 1, pixels, TimeSpan.Zero), new PreviewFrame(1, 1, 1, pixels, TimeSpan.Zero));
    }

    [Fact]
    public void The_freshness_limits_are_two_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), FrameFreshness.EligibleMaxAge);
        Assert.Equal(TimeSpan.FromSeconds(2), FrameFreshness.StaleAfter);
    }

    [Fact]
    public void The_format_ladder_runs_from_1080p_to_720p()
    {
        Assert.Equal((1920, 1080, 1280, 720), (CameraFormatPolicy.PreferredWidth, CameraFormatPolicy.PreferredHeight, CameraFormatPolicy.MinimumWidth, CameraFormatPolicy.MinimumHeight));
    }

    [Fact]
    public void A_capture_is_a_jpeg_at_quality_92_in_srgb_with_no_metadata()
    {
        Assert.Equal(92, JpegEncodingSpec.Quality);
        Assert.Equal("sRGB", JpegEncodingSpec.ColorSpace);
        Assert.False(JpegEncodingSpec.WritesMetadata);
    }

    [Fact]
    public void The_back_pressure_windows_are_two_seconds_and_one_second()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), PreviewBackPressure.RateWindow);
        Assert.Equal(TimeSpan.FromSeconds(1), PreviewBackPressure.InFlightTimeout);
    }

    [Theory]
    [InlineData(CameraFailureKind.InUseByAnotherApp, CameraState.InUseByAnotherApp)]
    [InlineData(CameraFailureKind.PrivacyDenied, CameraState.NotAvailable)]
    [InlineData(CameraFailureKind.Stale, CameraState.NotAvailable)]
    [InlineData(CameraFailureKind.Removed, CameraState.NotAvailable)]
    [InlineData(CameraFailureKind.ExclusiveLost, CameraState.NotAvailable)]
    [InlineData(CameraFailureKind.Failed, CameraState.NotAvailable)]
    [InlineData(CameraFailureKind.NoSupportedFormat, CameraState.NotAvailable)]
    [InlineData(CameraFailureKind.NotFound, CameraState.NotAvailable)]
    [InlineData(CameraFailureKind.Unknown, CameraState.NotAvailable)]
    public void A_failure_kind_maps_to_a_camera_state_and_keeps_its_reason(CameraFailureKind kind, CameraState state)
    {
        Assert.Equal((state, kind), CameraStateMapping.For(kind));
    }

    [Fact]
    public void The_hresults_are_the_documented_signed_values()
    {
        Assert.Equal(unchecked((int)0x80070005), CameraHResults.AccessDenied);
        Assert.Equal(unchecked((int)0xC00D3704), CameraHResults.HwMftFailedStartStreaming);
        Assert.Equal(unchecked((int)0xC00D3EA2), CameraHResults.VideoRecordingDeviceInvalidated);
        Assert.Equal(unchecked((int)0xC00D3EA3), CameraHResults.VideoRecordingDevicePreempted);
        Assert.Equal(unchecked((int)0x80070020), CameraHResults.SharingViolation);
        Assert.Equal(unchecked((int)0xC00D4E24), CameraHResults.VideoDeviceLocked);
        Assert.Equal(unchecked((int)0x8007048F), CameraHResults.DeviceNotConnected);
        Assert.Equal(unchecked((int)0x80070651), CameraHResults.DeviceRemoved);
        Assert.Equal(unchecked((int)0x80070002), CameraHResults.FileNotFound);
        Assert.Equal(unchecked((int)0x80070490), CameraHResults.NotFound);
        Assert.Equal(unchecked((int)0xC00D36D5), CameraHResults.MediaFoundationNotFound);
        Assert.Equal(unchecked((int)0xC00D36B4), CameraHResults.InvalidMediaType);
        Assert.Equal(unchecked((int)0xC00D36B9), CameraHResults.NoMoreTypes);
        Assert.Equal(unchecked((int)0xC00D3E98), CameraHResults.UnsupportedFormat);
    }

    [Fact]
    public void Every_policy_signature_exists_for_the_wave_B_agents()
    {
        // Compile-time contract: wave B1 replaces the stubs; these delegates fail to compile if a signature changes.
        Func<IReadOnlyList<CameraFormat>, CameraFormat?> choose = CameraFormatPolicy.Choose;
        Func<TimeSpan, CameraFrameInfo?, TimeSpan, bool> isFresh = FrameFreshness.IsFresh;
        Func<TimeSpan, TimeSpan, bool> isEligible = CaptureFrameSelector.IsEligible;
        Func<IReadOnlyList<TimeSpan>, TimeSpan, int?> select = CaptureFrameSelector.Select;
        Func<int, CameraOperation, CameraFailureKind> classifyHResult = CameraFailureClassifier.Classify;
        Func<Exception, CameraOperation, CameraFailureKind> classifyException = CameraFailureClassifier.Classify;
        Func<CameraFailureKind, bool> retryable = CameraRetryPolicy.IsRetryable;
        var backPressure = new PreviewBackPressure();
        Func<DateTimeOffset, bool> inFlight = backPressure.IsInFlight;
        Func<long, DateTimeOffset, bool> begin = backPressure.TryBegin;
        Func<uint, DateTimeOffset, bool> acknowledge = backPressure.Acknowledge;
        Action reset = backPressure.Reset;
        Func<DateTimeOffset, PreviewFrameStats> snapshot = backPressure.Snapshot;
        MirrorBgra mirror = MirrorTransform.MirrorBgraInPlace;

        Assert.NotNull(new object[] { choose, isFresh, isEligible, select, classifyHResult, classifyException, retryable, inFlight, begin, acknowledge, reset, snapshot, mirror });
    }

    private delegate void MirrorBgra(Span<byte> bgra, int width, int height, int stride);
}
