using FotoHavn.Catalog;
using FotoHavn.Core.Events;
using FotoHavn.Core.Events.Setup;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Events.Setup;

public sealed class EventSetupValidatorTests
{
    private static readonly CatalogData Catalog = new();

    private static EventSetupDraft Valid()
    {
        var draft = EventSetupDraft.CreateNew(Catalog, new DateOnly(2026, 10, 13));
        draft.SetField(EventField.Name, "Harbour gala");
        draft.BindCamera("camera-1", "Camera");
        draft.BindGuestDisplay("display-1", "Display");
        draft.SetPin(SetupSamples.Pin, SetupSamples.Pin);
        return draft;
    }

    private static string[] Errors(EventSetupDraft draft) =>
        [.. EventSetupValidator.Validate(draft).Select(error => $"{error.Field}:{error.Code}")];

    [Fact]
    public void A_complete_draft_has_no_errors_and_can_save_and_start()
    {
        var errors = EventSetupValidator.Validate(Valid());

        Assert.Empty(errors);
        Assert.True(EventSetupValidator.CanSave(errors));
        Assert.True(EventSetupValidator.CanStart(errors));
    }

    [Fact]
    public void A_pristine_new_draft_lists_what_is_missing_in_form_order()
    {
        var draft = EventSetupDraft.CreateNew(Catalog, new DateOnly(2026, 10, 13));

        Assert.Equal(["name:Required", "camera:CameraRequired", "guestDisplay:DisplayRequired", "pin:PinRequired"], Errors(draft));
    }

    [Theory]
    [InlineData("", "name:Required")]
    [InlineData("   ", "name:Required")]
    public void The_name_is_required_after_trimming(string name, string expected)
    {
        var draft = Valid();
        draft.SetField(EventField.Name, name);

        Assert.Equal([expected], Errors(draft));
    }

    [Fact]
    public void The_name_is_trimmed_before_its_length_is_checked()
    {
        var draft = Valid();
        draft.SetField(EventField.Name, " " + new string('a', EventSetupLimits.NameMaxLength) + " ");
        Assert.Empty(Errors(draft));

        draft.SetField(EventField.Name, new string('a', EventSetupLimits.NameMaxLength + 1));
        Assert.Equal(["name:TooLong"], Errors(draft));
    }

    [Theory]
    [InlineData("", "date:Required")]
    [InlineData("2026-13-01", "date:InvalidDate")]
    [InlineData("13/10/2026", "date:InvalidDate")]
    [InlineData("2026-02-30", "date:InvalidDate")]
    public void The_date_is_required_and_must_be_a_real_yyyy_MM_dd_day(string date, string expected)
    {
        var draft = Valid();
        draft.SetField(EventField.Date, date);

        Assert.Equal([expected], Errors(draft));
    }

    [Theory]
    [InlineData(EventField.Venue, EventSetupLimits.VenueMaxLength, "venue")]
    [InlineData(EventField.Customer, EventSetupLimits.CustomerMaxLength, "customer")]
    [InlineData(EventField.Notes, EventSetupLimits.NotesMaxLength, "notes")]
    public void The_optional_texts_may_be_empty_and_have_a_limit(EventField field, int limit, string name)
    {
        var draft = Valid();
        draft.SetField(field, new string('x', limit));
        Assert.Empty(Errors(draft));

        draft.SetField(field, new string('x', limit + 1));
        Assert.Equal([$"{name}:TooLong"], Errors(draft));

        draft.SetField(field, string.Empty);
        Assert.Empty(Errors(draft));
    }

    [Fact]
    public void A_camera_and_a_guest_display_must_be_chosen_whether_or_not_they_are_connected()
    {
        var draft = EventSetupDraft.CreateNew(Catalog, new DateOnly(2026, 10, 13));
        draft.SetField(EventField.Name, "Gala");
        draft.SetPin("1234", "1234");

        Assert.Equal(["camera:CameraRequired", "guestDisplay:DisplayRequired"], Errors(draft));

        draft.BindCamera("long gone", "Unplugged camera");
        draft.BindGuestDisplay("long gone", "Unplugged display");
        Assert.Empty(Errors(draft));
    }

    [Fact]
    public void At_least_one_Template_and_one_Look_must_be_offered()
    {
        var draft = Valid();
        draft.SetOfferedTemplates([]);
        draft.SetOfferedLooks([]);

        Assert.Equal(["templates:AtLeastOneTemplate", "looks:AtLeastOneLook"], Errors(draft));
    }

    [Fact]
    public void Narrowing_the_offered_set_clears_a_default_that_is_no_longer_offered_and_flags_it()
    {
        var draft = Valid();
        draft.SetOfferedTemplates(["panorama-four", "butter-gingham"]);
        draft.SetOfferedLooks(["monochrome-v1"]);

        Assert.Null(draft.DefaultTemplateId);
        Assert.Null(draft.DefaultLookId);
        Assert.Equal(["defaultTemplate:DefaultTemplateNotOffered", "defaultLook:DefaultLookNotOffered"], Errors(draft));

        Assert.True(draft.SetDefaultTemplate("butter-gingham"));
        Assert.True(draft.SetDefaultLook("monochrome-v1"));
        Assert.Empty(Errors(draft));
    }

    [Fact]
    public void A_default_that_stays_offered_is_kept()
    {
        var draft = Valid();
        draft.SetOfferedTemplates(["panorama-four", "signature-strip"]);

        Assert.Equal("signature-strip", draft.DefaultTemplateId);
        Assert.Equal(["signature-strip", "panorama-four"], draft.OfferedTemplateIds);
    }

    [Fact]
    public void A_default_that_is_not_offered_is_flagged_and_an_unknown_one_is_refused()
    {
        var draft = Valid();
        draft.SetOfferedTemplates(["signature-strip"]);

        Assert.True(draft.SetDefaultTemplate("polka-keepsake"));
        Assert.Equal(["defaultTemplate:DefaultTemplateNotOffered"], Errors(draft));
        Assert.False(draft.SetDefaultTemplate("no-such-template"));
        Assert.False(draft.SetDefaultLook("no-such-look"));
        Assert.Equal("polka-keepsake", draft.DefaultTemplateId);
    }

    [Fact]
    public void Offered_ids_the_catalog_does_not_have_are_dropped_and_the_rest_follow_catalog_order()
    {
        var draft = Valid();
        draft.SetOfferedTemplates(["polka-keepsake", "ghost", "signature-strip", "polka-keepsake"]);
        draft.SetOfferedLooks(["sepia-v1", "naturale-v1", "ghost"]);

        Assert.Equal(["signature-strip", "polka-keepsake"], draft.OfferedTemplateIds);
        Assert.All(draft.OfferedLookIds, id => Assert.NotNull(Catalog.FindLook(id)));
        Assert.DoesNotContain("ghost", draft.OfferedLookIds);
        Assert.Equal(Catalog.Looks.Where(look => draft.OfferedLookIds.Contains(look.Id)).Select(look => look.Id), draft.OfferedLookIds);
    }

    public static TheoryData<PhaseTimerPhase, int, bool> TimerCases()
    {
        var cases = new TheoryData<PhaseTimerPhase, int, bool>();
        foreach (var phase in PhaseTimerRules.All)
        {
            cases.Add(phase, PhaseTimerRules.Min(phase), true);
            cases.Add(phase, PhaseTimerRules.Max(phase), true);
            cases.Add(phase, PhaseTimerRules.Min(phase) - 1, false);
            cases.Add(phase, PhaseTimerRules.Max(phase) + 1, false);
            cases.Add(phase, -1, false);
            cases.Add(phase, 0, PhaseTimerRules.CanBeOff(phase));
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(TimerCases))]
    public void Every_Phase_Timer_is_checked_against_its_range_and_never_off_phases_refuse_zero(PhaseTimerPhase phase, int seconds, bool valid)
    {
        var draft = Valid();
        draft.SetTimer(phase, seconds);

        var field = EventSetupFieldNames.Timer(PhaseTimerRules.ToProtocol(phase));
        Assert.Equal(valid ? [] : [$"{field}:TimerOutOfRange"], Errors(draft));
    }

    [Fact]
    public void Keepsake_and_guest_gallery_can_never_be_off()
    {
        var draft = Valid();
        draft.SetTimer(PhaseTimerPhase.Keepsake, 0);
        draft.SetTimer(PhaseTimerPhase.GuestGallery, 0);
        draft.SetTimer(PhaseTimerPhase.Template, 0);

        Assert.Equal(["timers.keepsake:TimerOutOfRange", "timers.guestGallery:TimerOutOfRange"], Errors(draft));
    }

    [Fact]
    public void A_pending_PIN_is_checked_for_format_and_match()
    {
        var draft = Valid();

        draft.SetPin("12", "12");
        Assert.Equal(["pin:PinFormat"], Errors(draft));

        draft.SetPin("1234567", "1234567");
        Assert.Equal(["pin:PinFormat"], Errors(draft));

        draft.SetPin("12a4", "12a4");
        Assert.Equal(["pin:PinFormat"], Errors(draft));

        draft.SetPin("1234", "1235");
        Assert.Equal(["pin:PinMismatch"], Errors(draft));

        draft.SetPin("1234", string.Empty);
        Assert.Equal(["pin:PinMismatch"], Errors(draft));

        draft.SetPin(string.Empty, "1234");
        Assert.Equal(["pin:PinFormat"], Errors(draft));

        draft.SetPin("123456", "123456");
        Assert.Empty(Errors(draft));
    }

    [Fact]
    public void Without_a_pending_PIN_an_Event_with_no_saved_PIN_needs_one_to_start_but_not_to_save()
    {
        var draft = Valid();
        draft.SetPin(string.Empty, string.Empty);
        var errors = EventSetupValidator.Validate(draft);

        Assert.Equal(["pin:PinRequired"], Errors(draft));
        Assert.True(EventSetupValidator.CanSave(errors));
        Assert.False(EventSetupValidator.CanStart(errors));
    }

    [Fact]
    public void Another_error_blocks_saving_even_when_the_PIN_is_missing()
    {
        var draft = Valid();
        draft.SetPin(string.Empty, string.Empty);
        draft.SetField(EventField.Name, string.Empty);

        Assert.False(EventSetupValidator.CanSave(EventSetupValidator.Validate(draft)));
    }

    [Fact]
    public void An_Event_with_a_saved_PIN_needs_no_new_one()
    {
        var record = SetupSamples.Event(SetupSamples.HarbourId) with { HasPin = true };
        var draft = EventSetupDraft.FromRecord(Catalog, record);

        Assert.Empty(Errors(draft));
        Assert.True(draft.HasSavedPin);
        Assert.False(draft.HasPendingPin);
    }

    [Fact]
    public void Every_error_maps_to_its_protocol_code()
    {
        var all = Enum.GetValues<EventSetupError>();

        Assert.All(all, error => Assert.Equal(EventSetupErrors.ToProtocol(error), new SetupFieldError("x", error).ToProtocol().Code));
    }

    [Fact]
    public void The_validator_refuses_a_missing_argument()
    {
        Assert.Throws<ArgumentNullException>(() => EventSetupValidator.Validate(null!));
        Assert.Throws<ArgumentNullException>(() => EventSetupValidator.CanSave(null!));
        Assert.Throws<ArgumentNullException>(() => EventSetupValidator.CanStart(null!));
    }
}
