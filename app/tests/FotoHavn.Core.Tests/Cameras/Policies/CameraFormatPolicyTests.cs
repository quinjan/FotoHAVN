using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;

namespace FotoHavn.Core.Tests.Cameras.Policies;

public sealed class CameraFormatPolicyTests
{
    private const double Ntsc30 = 30000d / 1001d;

    private static CameraFormat F(int width, int height, double fps, string subtype) => new(width, height, fps, subtype);

    // Format lists as webcams report them to MediaCapture (frame source info), recorded or transcribed per device.
    private static readonly CameraFormat[] IntegratedWebcam =
    [
        F(1920, 1080, 30, "NV12"),
        F(1280, 960, 30, "NV12"),
        F(1280, 720, 30, "NV12"),
        F(640, 480, 30, "NV12"),
        F(640, 360, 30, "NV12"),
    ];

    private static readonly CameraFormat[] LogitechC920 =
    [
        F(640, 480, 30, "YUY2"),
        F(1280, 720, 10, "YUY2"),
        F(1920, 1080, 5, "YUY2"),
        F(2304, 1536, 2, "YUY2"),
        F(640, 480, 30, "MJPG"),
        F(1280, 720, 30, "MJPG"),
        F(1920, 1080, 30, "MJPG"),
    ];

    private static readonly CameraFormat[] C920WithoutMjpg1080 =
    [
        F(1920, 1080, 5, "YUY2"),
        F(2304, 1536, 2, "YUY2"),
        F(1280, 720, 10, "YUY2"),
        F(1280, 720, 30, "MJPG"),
        F(640, 480, 30, "MJPG"),
    ];

    private static readonly CameraFormat[] LaptopCamera720P =
    [
        F(1280, 720, 30, "YUY2"),
        F(1280, 720, 30, "NV12"),
        F(960, 540, 30, "NV12"),
        F(640, 480, 30, "NV12"),
        F(640, 360, 30, "NV12"),
        F(320, 240, 30, "NV12"),
    ];

    private static readonly CameraFormat[] Vga480Only =
    [
        F(640, 480, 30, "YUY2"),
        F(640, 480, 15, "YUY2"),
        F(320, 240, 30, "YUY2"),
    ];

    public static TheoryData<string, CameraFormat[], CameraFormat?> RecordedCameras => new()
    {
        { "this machine's Integrated Webcam", IntegratedWebcam, F(1920, 1080, 30, "NV12") },
        { "Logitech C920-like", LogitechC920, F(1920, 1080, 30, "MJPG") },
        { "C920 without a 1080p MJPG mode", C920WithoutMjpg1080, F(1280, 720, 30, "MJPG") },
        { "720p-only laptop camera", LaptopCamera720P, F(1280, 720, 30, "NV12") },
        { "640x480-only camera", Vga480Only, null },
    };

    [Theory]
    [MemberData(nameof(RecordedCameras))]
    public void A_recorded_camera_gets_the_expected_format(string camera, CameraFormat[] formats, CameraFormat? expected)
    {
        Assert.NotNull(camera);
        Assert.Equal(expected, CameraFormatPolicy.Choose(formats));
    }

    [Fact]
    public void The_order_the_camera_lists_its_formats_in_does_not_matter()
    {
        foreach (var formats in new[] { IntegratedWebcam, LogitechC920, C920WithoutMjpg1080, LaptopCamera720P })
        {
            var expected = CameraFormatPolicy.Choose(formats);
            Assert.Equal(expected, CameraFormatPolicy.Choose([.. formats.Reverse()]));
        }
    }

    [Fact]
    public void No_formats_means_no_choice()
    {
        Assert.Null(CameraFormatPolicy.Choose([]));
    }

    [Fact]
    public void A_missing_list_is_a_programming_error()
    {
        Assert.Throws<ArgumentNullException>(() => CameraFormatPolicy.Choose(null!));
    }

    [Fact]
    public void A_null_entry_is_skipped()
    {
        Assert.Equal(F(1280, 720, 30, "NV12"), CameraFormatPolicy.Choose([null!, F(1280, 720, 30, "NV12")]));
    }

    [Theory]
    [InlineData(Ntsc30)]
    [InlineData(29.5)]
    [InlineData(30.5)]
    public void Twenty_nine_ninety_seven_counts_as_thirty_on_the_top_rung(double fps)
    {
        var wanted = F(1920, 1080, fps, "YUY2");
        Assert.Equal(wanted, CameraFormatPolicy.Choose([F(1920, 1080, 15, "NV12"), wanted, F(1280, 720, 30, "NV12")]));
    }

    [Theory]
    [InlineData(29.4)]
    [InlineData(30.6)]
    public void A_rate_outside_the_tolerance_is_not_thirty_and_falls_to_the_any_rate_rung(double fps)
    {
        var slow = F(1920, 1080, fps, "NV12");
        Assert.Equal(slow, CameraFormatPolicy.Choose([F(1280, 720, 30, "NV12"), slow]));
    }

    [Fact]
    public void Full_hd_at_any_rate_from_fifteen_beats_hd_at_thirty()
    {
        var fullHd24 = F(1920, 1080, 24, "MJPG");
        Assert.Equal(fullHd24, CameraFormatPolicy.Choose([F(1280, 720, 30, "NV12"), fullHd24]));
    }

    [Fact]
    public void Full_hd_at_sixty_is_taken_when_there_is_no_thirty()
    {
        var sixty = F(1920, 1080, 60, "MJPG");
        Assert.Equal(sixty, CameraFormatPolicy.Choose([F(1920, 1080, 20, "MJPG"), sixty, F(1280, 720, 30, "MJPG")]));
    }

    [Fact]
    public void Within_the_any_rate_rung_the_higher_rate_wins_for_the_same_subtype()
    {
        Assert.Equal(F(1920, 1080, 25, "MJPG"), CameraFormatPolicy.Choose([F(1920, 1080, 15, "MJPG"), F(1920, 1080, 25, "MJPG"), F(1920, 1080, 20, "MJPG")]));
        Assert.Equal(F(1920, 1080, 25, "MJPG"), CameraFormatPolicy.Choose([F(1920, 1080, 20, "MJPG"), F(1920, 1080, 25, "MJPG")]));
    }

    [Fact]
    public void Hd_at_fifteen_is_the_last_resort()
    {
        var last = F(1280, 720, 15, "YUY2");
        Assert.Equal(last, CameraFormatPolicy.Choose([F(1280, 720, 10, "YUY2"), last, F(640, 480, 30, "YUY2"), F(1920, 1080, 10, "YUY2")]));
    }

    [Theory]
    [InlineData(14.985, true)]
    [InlineData(15.0, true)]
    [InlineData(14.5, true)]
    [InlineData(14.4, false)]
    [InlineData(10.0, false)]
    public void Fifteen_frames_per_second_is_the_floor_with_the_same_tolerance(double fps, bool accepted)
    {
        var chosen = CameraFormatPolicy.Choose([F(1280, 720, fps, "NV12")]);

        Assert.Equal(accepted, chosen is not null);
    }

    [Fact]
    public void A_larger_or_odd_size_is_never_chosen()
    {
        Assert.Null(CameraFormatPolicy.Choose([F(2304, 1536, 30, "MJPG"), F(1280, 960, 30, "NV12"), F(3840, 2160, 30, "NV12"), F(1920, 1088, 30, "NV12"), F(1366, 768, 30, "NV12")]));
    }

    [Theory]
    [InlineData("NV12", "YUY2", "NV12")]
    [InlineData("YUY2", "MJPG", "YUY2")]
    [InlineData("NV12", "MJPG", "NV12")]
    [InlineData("MJPG", "RGB32", "MJPG")]
    [InlineData("YUY2", "I420", "YUY2")]
    [InlineData("YUY2", "NV12", "NV12")]
    [InlineData("MJPG", "YUY2", "YUY2")]
    [InlineData("RGB32", "NV12", "NV12")]
    public void Within_a_rung_the_subtype_preference_is_nv12_then_yuy2_then_mjpg_then_anything_else(string a, string b, string expected)
    {
        var chosen = CameraFormatPolicy.Choose([F(1920, 1080, 30, a), F(1920, 1080, 30, b)]);

        Assert.Equal(expected, chosen!.Subtype);
    }

    [Fact]
    public void A_preferred_subtype_beats_a_higher_rate_in_the_same_rung()
    {
        var nv12 = F(1920, 1080, 15, "NV12");
        Assert.Equal(nv12, CameraFormatPolicy.Choose([F(1920, 1080, 60, "MJPG"), nv12]));
    }

    [Fact]
    public void Other_subtypes_are_compared_in_ordinal_order()
    {
        Assert.Equal("I420", CameraFormatPolicy.Choose([F(1280, 720, 30, "RGB32"), F(1280, 720, 30, "I420")])!.Subtype);
        Assert.Equal("I420", CameraFormatPolicy.Choose([F(1280, 720, 30, "I420"), F(1280, 720, 30, "RGB32")])!.Subtype);
    }

    [Fact]
    public void The_same_other_subtype_is_decided_by_the_rate()
    {
        Assert.Equal(F(1920, 1080, 25, "RGB32"), CameraFormatPolicy.Choose([F(1920, 1080, 25, "RGB32"), F(1920, 1080, 20, "RGB32")]));
        Assert.Equal(F(1920, 1080, 25, "RGB32"), CameraFormatPolicy.Choose([F(1920, 1080, 20, "RGB32"), F(1920, 1080, 25, "RGB32")]));
    }

    [Fact]
    public void A_full_tie_keeps_the_first_format_the_camera_listed()
    {
        var first = F(1920, 1080, 30, "NV12");
        var duplicate = F(1920, 1080, 30, "NV12");

        Assert.Same(first, CameraFormatPolicy.Choose([first, duplicate]));
    }

    [Fact]
    public void Subtype_names_are_matched_ignoring_case_and_a_missing_name_is_just_another_subtype()
    {
        Assert.Equal("nv12", CameraFormatPolicy.Choose([F(1280, 720, 30, "MJPG"), F(1280, 720, 30, "nv12")])!.Subtype);
        Assert.Equal("MJPG", CameraFormatPolicy.Choose([F(1280, 720, 30, null!), F(1280, 720, 30, "MJPG")])!.Subtype);
        Assert.Null(CameraFormatPolicy.Choose([F(1280, 720, 30, null!)])!.Subtype);
    }

    [Fact]
    public void The_ladder_constants_are_the_documented_sizes()
    {
        Assert.Equal((1920, 1080, 1280, 720), (CameraFormatPolicy.PreferredWidth, CameraFormatPolicy.PreferredHeight, CameraFormatPolicy.MinimumWidth, CameraFormatPolicy.MinimumHeight));
    }
}
