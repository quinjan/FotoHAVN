using FotoHavn.Core.Events;
using FotoHavn.Core.Events.Active;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Storage;
using FotoHavn.Catalog;

namespace FotoHavn.Core.Tests.Events.Active;

/// <summary>Start Event step 2's check of a saved Event against Event setup's field rules.</summary>
public sealed class SavedEventRulesTests
{
    private static readonly CatalogData Catalog = new();

    private static EventRecord Valid() => ActiveRig.SampleEvent();

    private static EventSetupError? Check(EventRecord record) => SavedEventRules.FirstViolation(record, Catalog);

    [Fact]
    public void A_saved_Event_that_setup_would_accept_has_no_violation()
    {
        Assert.Null(Check(Valid()));
    }

    [Fact]
    public void A_saved_Event_with_every_default_Phase_Timer_or_none_listed_is_fine()
    {
        Assert.Null(Check(Valid() with { PhaseTimers = [] }));
        Assert.Null(Check(Valid() with { PhaseTimers = [new PhaseTimerSetting(PhaseTimerPhase.Template, 0)] }));
    }

    [Fact]
    public void The_text_fields_follow_the_setup_limits()
    {
        Assert.Equal(EventSetupError.Required, Check(Valid() with { Name = "   " }));
        Assert.Equal(EventSetupError.TooLong, Check(Valid() with { Name = new string('x', 81) }));
        Assert.Equal(EventSetupError.TooLong, Check(Valid() with { Venue = new string('x', 121) }));
        Assert.Equal(EventSetupError.TooLong, Check(Valid() with { CustomerName = new string('x', 121) }));
        Assert.Equal(EventSetupError.TooLong, Check(Valid() with { Notes = new string('x', 1001) }));
    }

    [Fact]
    public void A_camera_and_a_guest_display_must_be_chosen()
    {
        Assert.Equal(EventSetupError.CameraRequired, Check(Valid() with { CameraDeviceId = null }));
        Assert.Equal(EventSetupError.CameraRequired, Check(Valid() with { CameraDeviceId = string.Empty }));
        Assert.Equal(EventSetupError.DisplayRequired, Check(Valid() with { GuestDisplayId = null }));
    }

    [Fact]
    public void Templates_and_Looks_must_be_offered_with_their_defaults_and_known_to_the_catalog()
    {
        Assert.Equal(EventSetupError.AtLeastOneTemplate, Check(Valid() with { OfferedTemplateIds = [] }));
        Assert.Equal(EventSetupError.DefaultTemplateNotOffered, Check(Valid() with { DefaultTemplateId = "polka-keepsake" }));
        Assert.Equal(EventSetupError.AtLeastOneTemplate, Check(Valid() with { OfferedTemplateIds = ["signature-strip", "no-such-template"] }));
        Assert.Equal(EventSetupError.AtLeastOneLook, Check(Valid() with { OfferedLookIds = [] }));
        Assert.Equal(EventSetupError.DefaultLookNotOffered, Check(Valid() with { DefaultLookId = "classic-v1" }));
        Assert.Equal(EventSetupError.AtLeastOneLook, Check(Valid() with { OfferedLookIds = ["naturale-v1", "no-such-look"] }));
    }

    [Theory]
    [InlineData(PhaseTimerPhase.Keepsake, 0)]
    [InlineData(PhaseTimerPhase.Keepsake, 4)]
    [InlineData(PhaseTimerPhase.GuestGallery, 0)]
    [InlineData(PhaseTimerPhase.Template, 19)]
    [InlineData(PhaseTimerPhase.Filters, 301)]
    public void A_Phase_Timer_out_of_range_is_a_violation(PhaseTimerPhase phase, int seconds)
    {
        Assert.Equal(EventSetupError.TimerOutOfRange, Check(Valid() with { PhaseTimers = [new PhaseTimerSetting(phase, seconds)] }));
    }

    [Fact]
    public void The_countdown_and_the_printer_are_checked_too()
    {
        Assert.NotNull(Check(Valid() with { CountdownSeconds = 4 }));
        Assert.NotNull(Check(Valid() with { Printer = "laser" }));
    }

    [Fact]
    public void The_check_needs_its_inputs()
    {
        Assert.Throws<ArgumentNullException>(() => SavedEventRules.FirstViolation(null!, Catalog));
        Assert.Throws<ArgumentNullException>(() => SavedEventRules.FirstViolation(Valid(), null!));
    }
}
