using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Tests.Cameras.Policies;

public sealed class FrameFreshnessTests
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(2);

    private static CameraFrameInfo Frame(TimeSpan timestamp) => new(1, timestamp, 1280, 720);

    [Fact]
    public void No_frame_is_never_fresh_however_generous_the_limit()
    {
        Assert.False(FrameFreshness.IsFresh(TimeSpan.FromSeconds(100), null, TimeSpan.MaxValue));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(1999, true)]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    [InlineData(60_000, false)]
    public void A_frame_is_fresh_up_to_and_including_the_limit(int ageMilliseconds, bool expected)
    {
        var timestamp = TimeSpan.FromSeconds(10);
        var now = timestamp + TimeSpan.FromMilliseconds(ageMilliseconds);

        Assert.Equal(expected, FrameFreshness.IsFresh(now, Frame(timestamp), MaxAge));
    }

    [Fact]
    public void A_frame_stamped_after_now_has_age_zero_even_with_a_zero_limit()
    {
        Assert.True(FrameFreshness.IsFresh(TimeSpan.FromSeconds(5), Frame(TimeSpan.FromSeconds(5.001)), TimeSpan.Zero));
        Assert.True(FrameFreshness.IsFresh(TimeSpan.FromSeconds(5), Frame(TimeSpan.FromHours(1)), MaxAge));
    }

    [Fact]
    public void A_negative_limit_is_never_met()
    {
        Assert.False(FrameFreshness.IsFresh(TimeSpan.FromSeconds(5), Frame(TimeSpan.FromSeconds(5)), TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void The_limit_is_a_parameter_so_the_readiness_proof_and_the_staleness_limit_share_the_rule()
    {
        var latest = Frame(TimeSpan.FromSeconds(10));
        var now = TimeSpan.FromSeconds(10.5);

        Assert.True(FrameFreshness.IsFresh(now, latest, FrameFreshness.EligibleMaxAge));
        Assert.True(FrameFreshness.IsFresh(now, latest, FrameFreshness.StaleAfter));
        Assert.False(FrameFreshness.IsFresh(now, latest, TimeSpan.FromMilliseconds(499)));
        Assert.True(FrameFreshness.IsFresh(now, latest, TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public void A_camera_that_stalls_goes_stale_exactly_two_seconds_after_its_last_frame_on_the_session_clock()
    {
        var latest = Frame(TimeSpan.FromSeconds(3));
        var now = latest.Timestamp + FrameFreshness.StaleAfter;
        Assert.True(FrameFreshness.IsFresh(now, latest, FrameFreshness.StaleAfter));

        now += TimeSpan.FromTicks(1);
        Assert.False(FrameFreshness.IsFresh(now, latest, FrameFreshness.StaleAfter));
    }
}

public sealed class CaptureFrameSelectorTests
{
    private static TimeSpan Ms(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    [Theory]
    [InlineData(999, false)]
    [InlineData(1000, true)]
    [InlineData(1001, true)]
    [InlineData(0, false)]
    public void A_frame_is_eligible_at_or_after_the_end_of_the_countdown(double timestampMs, bool expected)
    {
        Assert.Equal(expected, CaptureFrameSelector.IsEligible(Ms(timestampMs), Ms(1000)));
    }

    [Fact]
    public void The_first_eligible_frame_is_selected()
    {
        Assert.Equal(2, CaptureFrameSelector.Select([Ms(900), Ms(950), Ms(1000), Ms(1033), Ms(1066)], Ms(1000)));
    }

    [Fact]
    public void A_frame_exactly_at_the_limit_qualifies_and_one_just_under_does_not()
    {
        Assert.Equal(1, CaptureFrameSelector.Select([Ms(999.999), Ms(1000)], Ms(1000)));
        Assert.Equal(0, CaptureFrameSelector.Select([Ms(1000), Ms(1000.001)], Ms(1000)));
    }

    [Fact]
    public void Nothing_before_the_limit_is_ever_selected_so_none_means_wait()
    {
        Assert.Null(CaptureFrameSelector.Select([Ms(10), Ms(500), Ms(999)], Ms(1000)));
        Assert.Null(CaptureFrameSelector.Select([], Ms(1000)));
    }

    [Fact]
    public void Arrival_order_decides_when_timestamps_are_out_of_order()
    {
        // A late-arriving earlier frame never wins; the first eligible by arrival does.
        Assert.Equal(1, CaptureFrameSelector.Select([Ms(980), Ms(1100), Ms(1010), Ms(990)], Ms(1000)));
        Assert.Equal(0, CaptureFrameSelector.Select([Ms(1100), Ms(900), Ms(1000)], Ms(1000)));
        Assert.Equal(1, CaptureFrameSelector.Select([Ms(900), Ms(1100), Ms(1000)], Ms(1000)));
    }

    [Fact]
    public void A_stale_frame_arriving_late_is_skipped_however_many_eligible_frames_follow()
    {
        Assert.Equal(1, CaptureFrameSelector.Select([Ms(10), Ms(2000), Ms(3000)], Ms(1000)));
    }

    [Fact]
    public void Equal_timestamps_select_the_first_of_them()
    {
        Assert.Equal(1, CaptureFrameSelector.Select([Ms(500), Ms(1000), Ms(1000), Ms(1000)], Ms(1000)));
    }

    [Fact]
    public void A_missing_list_is_a_programming_error()
    {
        Assert.Throws<ArgumentNullException>(() => CaptureFrameSelector.Select(null!, Ms(1000)));
    }
}

public sealed class CameraFailureClassifierTests
{
    public static TheoryData<int, CameraFailureKind, CameraFailureKind> HResultTable => new()
    {
        { CameraHResults.AccessDenied, CameraFailureKind.PrivacyDenied, CameraFailureKind.PrivacyDenied },
        { CameraHResults.SharingViolation, CameraFailureKind.InUseByAnotherApp, CameraFailureKind.ExclusiveLost },
        { CameraHResults.HwMftFailedStartStreaming, CameraFailureKind.InUseByAnotherApp, CameraFailureKind.Failed },
        { CameraHResults.VideoDeviceLocked, CameraFailureKind.InUseByAnotherApp, CameraFailureKind.ExclusiveLost },
        { CameraHResults.VideoRecordingDevicePreempted, CameraFailureKind.InUseByAnotherApp, CameraFailureKind.ExclusiveLost },
        { CameraHResults.VideoRecordingDeviceInvalidated, CameraFailureKind.NotFound, CameraFailureKind.Removed },
        { CameraHResults.DeviceNotConnected, CameraFailureKind.NotFound, CameraFailureKind.Removed },
        { CameraHResults.DeviceRemoved, CameraFailureKind.NotFound, CameraFailureKind.Removed },
        { CameraHResults.FileNotFound, CameraFailureKind.NotFound, CameraFailureKind.Failed },
        { CameraHResults.NotFound, CameraFailureKind.NotFound, CameraFailureKind.Failed },
        { CameraHResults.MediaFoundationNotFound, CameraFailureKind.NotFound, CameraFailureKind.Failed },
        { CameraHResults.InvalidMediaType, CameraFailureKind.NoSupportedFormat, CameraFailureKind.Failed },
        { CameraHResults.NoMoreTypes, CameraFailureKind.NoSupportedFormat, CameraFailureKind.Failed },
        { CameraHResults.UnsupportedFormat, CameraFailureKind.NoSupportedFormat, CameraFailureKind.Failed },
        { 0, CameraFailureKind.Unknown, CameraFailureKind.Failed },
        { unchecked((int)0x80004005), CameraFailureKind.Unknown, CameraFailureKind.Failed },
        { int.MinValue, CameraFailureKind.Unknown, CameraFailureKind.Failed },
        { int.MaxValue, CameraFailureKind.Unknown, CameraFailureKind.Failed },
    };

    [Theory]
    [MemberData(nameof(HResultTable))]
    public void An_hresult_means_what_the_contract_table_says_for_each_operation(int hresult, CameraFailureKind open, CameraFailureKind streaming)
    {
        Assert.Equal(open, CameraFailureClassifier.Classify(hresult, CameraOperation.Open));
        Assert.Equal(streaming, CameraFailureClassifier.Classify(hresult, CameraOperation.Streaming));
    }

    [Fact]
    public void A_camera_exception_keeps_its_kind_whatever_the_operation()
    {
        foreach (var kind in Enum.GetValues<CameraFailureKind>())
        {
            Assert.Equal(kind, CameraFailureClassifier.Classify(new CameraException(kind), CameraOperation.Open));
            Assert.Equal(kind, CameraFailureClassifier.Classify(new CameraException(kind, "x", new InvalidOperationException()), CameraOperation.Streaming));
        }
    }

    [Fact]
    public void Winrts_unauthorized_access_is_privacy_denied()
    {
        Assert.Equal(CameraFailureKind.PrivacyDenied, CameraFailureClassifier.Classify(new UnauthorizedAccessException(), CameraOperation.Open));
        Assert.Equal(CameraFailureKind.PrivacyDenied, CameraFailureClassifier.Classify(new UnauthorizedAccessException(), CameraOperation.Streaming));
    }

    [Fact]
    public void Any_other_exception_is_classified_by_its_hresult()
    {
        var inUse = new IOException("sharing violation") { HResult = CameraHResults.SharingViolation };
        var denied = new InvalidOperationException("denied") { HResult = CameraHResults.AccessDenied };

        Assert.Equal(CameraFailureKind.InUseByAnotherApp, CameraFailureClassifier.Classify(inUse, CameraOperation.Open));
        Assert.Equal(CameraFailureKind.ExclusiveLost, CameraFailureClassifier.Classify(inUse, CameraOperation.Streaming));
        Assert.Equal(CameraFailureKind.PrivacyDenied, CameraFailureClassifier.Classify(denied, CameraOperation.Open));
    }

    [Fact]
    public void An_exception_with_no_known_hresult_is_unknown_when_opening_and_failed_when_streaming()
    {
        var other = new InvalidOperationException("boom");

        Assert.Equal(CameraFailureKind.Unknown, CameraFailureClassifier.Classify(other, CameraOperation.Open));
        Assert.Equal(CameraFailureKind.Failed, CameraFailureClassifier.Classify(other, CameraOperation.Streaming));
    }

    [Fact]
    public void A_missing_exception_is_a_programming_error()
    {
        Assert.Throws<ArgumentNullException>(() => CameraFailureClassifier.Classify(null!, CameraOperation.Open));
    }
}

public sealed class CameraRetryPolicyTests
{
    [Theory]
    [InlineData(CameraFailureKind.Stale, true)]
    [InlineData(CameraFailureKind.Failed, true)]
    [InlineData(CameraFailureKind.Removed, true)]
    [InlineData(CameraFailureKind.ExclusiveLost, true)]
    [InlineData(CameraFailureKind.InUseByAnotherApp, true)]
    [InlineData(CameraFailureKind.PrivacyDenied, true)]
    [InlineData(CameraFailureKind.NoSupportedFormat, false)]
    [InlineData(CameraFailureKind.NotFound, true)]
    [InlineData(CameraFailureKind.Unknown, true)]
    public void Every_kind_is_retryable_except_no_supported_format(CameraFailureKind kind, bool expected)
    {
        Assert.Equal(expected, CameraRetryPolicy.IsRetryable(kind));
    }

    [Fact]
    public void The_table_covers_every_kind()
    {
        Assert.Equal(9, Enum.GetValues<CameraFailureKind>().Length);
    }
}
