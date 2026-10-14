using FotoHavn.Core.Events;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Events;

public sealed class EventSetupLimitsTests
{
    [Theory]
    [InlineData(null, EventSetupError.Required)]
    [InlineData("", EventSetupError.Required)]
    [InlineData("   ", EventSetupError.Required)]
    [InlineData("a", null)]
    [InlineData("  Lindqvist wedding  ", null)]
    public void Name_is_required_after_trimming(string? value, EventSetupError? expected)
    {
        Assert.Equal(expected, EventSetupLimits.CheckName(value));
    }

    [Fact]
    public void Name_may_be_80_characters_after_trimming_and_not_81()
    {
        Assert.Null(EventSetupLimits.CheckName(new string('x', 80)));
        Assert.Null(EventSetupLimits.CheckName("  " + new string('x', 80) + "  "));
        Assert.Equal(EventSetupError.TooLong, EventSetupLimits.CheckName(new string('x', 81)));
    }

    [Theory]
    [InlineData(120, 120, true)]
    [InlineData(121, 120, false)]
    [InlineData(1000, 1000, true)]
    [InlineData(1001, 1000, false)]
    [InlineData(0, 120, true)]
    public void Optional_text_has_a_maximum_and_may_be_empty(int length, int maximum, bool valid)
    {
        var value = new string('v', length);

        Assert.Equal(valid ? null : EventSetupError.TooLong, EventSetupLimits.CheckOptionalText(value, maximum));
    }

    [Fact]
    public void Venue_customer_and_notes_use_their_own_limits()
    {
        Assert.Null(EventSetupLimits.CheckVenue(null));
        Assert.Null(EventSetupLimits.CheckVenue(new string('v', EventSetupLimits.VenueMaxLength)));
        Assert.Equal(EventSetupError.TooLong, EventSetupLimits.CheckVenue(new string('v', EventSetupLimits.VenueMaxLength + 1)));
        Assert.Null(EventSetupLimits.CheckCustomer(new string('c', EventSetupLimits.CustomerMaxLength)));
        Assert.Equal(EventSetupError.TooLong, EventSetupLimits.CheckCustomer(new string('c', EventSetupLimits.CustomerMaxLength + 1)));
        Assert.Null(EventSetupLimits.CheckNotes(new string('n', EventSetupLimits.NotesMaxLength)));
        Assert.Equal(EventSetupError.TooLong, EventSetupLimits.CheckNotes(new string('n', EventSetupLimits.NotesMaxLength + 1)));
    }

    [Fact]
    public void Limits_match_the_plan()
    {
        Assert.Equal((1, 80, 120, 120, 1000), (EventSetupLimits.NameMinLength, EventSetupLimits.NameMaxLength, EventSetupLimits.VenueMaxLength, EventSetupLimits.CustomerMaxLength, EventSetupLimits.NotesMaxLength));
        Assert.Equal(5, EventSetupLimits.DefaultCountdownSeconds);
        Assert.True(EventSetupLimits.DefaultMirror);
        Assert.Equal("none", EventSetupLimits.Printer);
    }

    [Theory]
    [InlineData(null, EventSetupError.Required)]
    [InlineData("", EventSetupError.Required)]
    [InlineData("  ", EventSetupError.Required)]
    [InlineData("2026-02-30", EventSetupError.InvalidDate)]
    [InlineData("13/10/2026", EventSetupError.InvalidDate)]
    [InlineData("2026-1-3", EventSetupError.InvalidDate)]
    [InlineData("tomorrow", EventSetupError.InvalidDate)]
    [InlineData("2026-10-13T00:00:00", EventSetupError.InvalidDate)]
    public void Date_must_be_a_real_yyyy_MM_dd_day(string? value, EventSetupError expected)
    {
        Assert.Equal(expected, EventSetupLimits.CheckDate(value, out var date));
        Assert.Equal(default, date);
    }

    [Fact]
    public void A_valid_date_is_parsed_after_trimming_and_formats_back()
    {
        Assert.Null(EventSetupLimits.CheckDate(" 2026-10-13 ", out var date));

        Assert.Equal(new DateOnly(2026, 10, 13), date);
        Assert.Equal("2026-10-13", EventSetupLimits.FormatDate(date));
    }

    [Fact]
    public void Countdown_is_3_5_or_10()
    {
        Assert.Equal([3, 5, 10], EventSetupLimits.Countdowns);
        Assert.All([3, 5, 10], seconds => Assert.True(EventSetupLimits.IsValidCountdown(seconds)));
        Assert.All([0, 1, 4, 6, 9, 11, -5], seconds => Assert.False(EventSetupLimits.IsValidCountdown(seconds)));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Harbour Hall ", "Harbour Hall")]
    public void Optional_text_is_stored_trimmed_or_null(string? value, string? expected)
    {
        Assert.Equal(expected, EventSetupLimits.NormalizeOptional(value));
    }

    [Fact]
    public void Offered_Templates_need_one_and_the_default_among_them()
    {
        Assert.Equal(EventSetupError.AtLeastOneTemplate, EventSetupLimits.CheckTemplates([], "signature-strip"));
        Assert.Equal(EventSetupError.DefaultTemplateNotOffered, EventSetupLimits.CheckTemplates(["panorama-four"], "signature-strip"));
        Assert.Equal(EventSetupError.DefaultTemplateNotOffered, EventSetupLimits.CheckTemplates(["panorama-four"], null));
        Assert.Null(EventSetupLimits.CheckTemplates(["panorama-four", "signature-strip"], "signature-strip"));
    }

    [Fact]
    public void Offered_Looks_need_one_and_the_default_among_them()
    {
        Assert.Equal(EventSetupError.AtLeastOneLook, EventSetupLimits.CheckLooks([], "naturale-v1"));
        Assert.Equal(EventSetupError.DefaultLookNotOffered, EventSetupLimits.CheckLooks(["sepia-v1"], "naturale-v1"));
        Assert.Null(EventSetupLimits.CheckLooks(["sepia-v1", "naturale-v1"], "naturale-v1"));
        Assert.Throws<ArgumentNullException>(() => EventSetupLimits.CheckLooks(null!, "naturale-v1"));
    }

    [Fact]
    public void Every_error_maps_one_to_one_to_its_wire_code()
    {
        var core = Enum.GetNames<EventSetupError>();
        var wire = Enum.GetNames<EventSetupErrorCode>();

        Assert.Equal(core, wire);
        foreach (var error in Enum.GetValues<EventSetupError>())
        {
            Assert.Equal(error.ToString(), EventSetupErrors.ToProtocol(error).ToString());
        }
    }

    [Fact]
    public void The_wire_codes_are_the_plans_strings()
    {
        string[] expected =
        [
            "required", "tooLong", "invalidDate", "atLeastOneTemplate", "defaultTemplateNotOffered", "atLeastOneLook",
            "defaultLookNotOffered", "timerOutOfRange", "pinFormat", "pinMismatch", "pinRequired", "cameraRequired", "displayRequired",
        ];

        Assert.Equal(expected, Enum.GetValues<EventSetupError>().Select(error => FotoHavn.Protocol.ProtocolNames.Of(EventSetupErrors.ToProtocol(error))));
    }

    [Fact]
    public void An_unknown_error_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EventSetupErrors.ToProtocol((EventSetupError)999));
    }
}
