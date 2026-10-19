using System.Text.Json;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Events.Setup;

public sealed class EventSetupCommandTests
{
    private static async Task<SetupRig> RigAsync(HookedStore? store = null, bool hooks = false) =>
        new(store ?? new HookedStore(await SetupSamples.SeededStoreAsync()), hooks: hooks);

    private static HookedStore StoreOf(SetupRig rig) => (HookedStore)rig.Store;

    private static string[] ErrorsOf(SetupRig rig) => [.. rig.Setup.FieldErrors.Select(error => $"{error.Field}:{error.Code}")];

    private static void FillValid(SetupRig rig, string name = "Quay party")
    {
        rig.Field("name", name);
        rig.SelectCamera("fake-camera");
        rig.SelectDisplay("fake-guest");
    }

    [Fact]
    public async Task Text_is_kept_as_typed_and_a_half_typed_date_stays_visible_with_its_error()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.Field("name", "  Quay party  ");
        rig.Field("date", "2026-1-5");
        rig.Field("venue", "The Quay");
        rig.Field("customer", "Ana");
        rig.Field("notes", "Props");

        Assert.Equal(new EventSetupFieldValues("  Quay party  ", "2026-1-5", "The Quay", "Ana", "Props"), rig.Setup.Fields);
        Assert.Contains("date:InvalidDate", ErrorsOf(rig));
        Assert.DoesNotContain("name:Required", ErrorsOf(rig));
    }

    [Fact]
    public async Task Errors_are_computed_from_the_start_but_only_shown_after_a_save_attempt()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        Assert.False(rig.Setup.ShowErrors);
        Assert.Equal(["name:Required", "camera:CameraRequired", "guestDisplay:DisplayRequired", "pin:PinRequired"], ErrorsOf(rig));

        rig.Rejects(RejectReason.ValidationFailed, "SaveEvent");

        Assert.True(rig.Setup.ShowErrors);
        Assert.Equal(OperatorSurface.EventSetup, rig.Latest.Surface);
        Assert.Equal(3, (await rig.Store.ListEventsAsync(TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task A_pristine_draft_is_clean_and_any_change_makes_it_dirty_until_it_is_typed_back()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        Assert.False(rig.Setup.Dirty);

        rig.Field("name", "x");
        Assert.True(rig.Setup.Dirty);

        rig.Field("name", string.Empty);
        Assert.False(rig.Setup.Dirty);
    }

    public static TheoryData<string, string> EditCommands => new()
    {
        { "ChangeEventField", """{"field":"name","value":"x"}""" },
        { "SelectCamera", """{"deviceId":"fake-camera"}""" },
        { "SelectGuestDisplay", """{"displayId":"fake-guest"}""" },
        { "SetMirror", """{"enabled":false}""" },
        { "SetCountdown", """{"seconds":3}""" },
        { "SetOfferedTemplates", """{"templateIds":["signature-strip"]}""" },
        { "SetDefaultTemplate", """{"templateId":"signature-strip"}""" },
        { "SetOfferedLooks", """{"lookIds":["naturale-v1"]}""" },
        { "SetDefaultLook", """{"lookId":"naturale-v1"}""" },
        { "SetPhaseTimer", """{"phase":"keepsake","seconds":10}""" },
        { "SetGuestGallery", """{"enabled":true}""" },
        { "SetGuestSounds", """{"enabled":false}""" },
        { "SetPin", """{"pin":"1234","confirmation":"1234"}""" },
        { "SaveEvent", "{}" },
        { "SaveAndStartEvent", "{}" },
        { "LeaveEventSetup", "{}" },
        { "ConfirmLeave", """{"choice":"save"}""" },
    };

    [Theory]
    [MemberData(nameof(EditCommands))]
    public async Task Without_a_draft_every_setup_command_is_not_available(string name, string payload)
    {
        using var rig = await RigAsync();

        rig.Rejects(RejectReason.NotAvailable, name, payload);
    }

    [Fact]
    public async Task IdentifyDisplays_is_still_only_for_the_setup_surface()
    {
        using var rig = await RigAsync();

        rig.Rejects(RejectReason.NotAvailable, "IdentifyDisplays");
        rig.OpenNew();
        rig.Accepts("IdentifyDisplays");

        Assert.Equal([1, 2], rig.Latest.Overlays.IdentifyDisplays!.Displays.Select(display => display.Number));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task The_countdown_takes_3_5_or_10(int seconds)
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.Accepts("SetCountdown", $$"""{"seconds":{{seconds}}}""");

        Assert.Equal(seconds, rig.Setup.CountdownSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-5)]
    [InlineData(11)]
    public async Task Any_other_countdown_is_validationFailed_and_changes_nothing(int seconds)
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.Rejects(RejectReason.ValidationFailed, "SetCountdown", $$"""{"seconds":{{seconds}}}""");

        Assert.Equal(5, rig.Setup.CountdownSeconds);
        Assert.False(rig.Setup.Dirty);
    }

    [Fact]
    public async Task Mirror_and_the_guest_gallery_follow_their_commands()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.Accepts("SetMirror", """{"enabled":false}""");
        rig.Accepts("SetGuestGallery", """{"enabled":true}""");
        rig.Accepts("SetGuestSounds", """{"enabled":false}""");

        Assert.False(rig.Setup.Mirror);
        Assert.False(rig.Setup.Camera.Mirrored);
        Assert.True(rig.Setup.GuestGalleryEnabled);
        Assert.False(rig.Setup.GuestSoundsEnabled);
    }

    [Fact]
    public async Task Offering_fewer_Templates_keeps_the_default_when_it_stays_offered_and_flags_it_when_it_does_not()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.Accepts("SetOfferedTemplates", """{"templateIds":["panorama-four","signature-strip","ghost"]}""");
        Assert.Equal(["signature-strip", "panorama-four"], rig.Setup.Templates.Where(template => template.Offered).Select(template => template.Id));
        Assert.Equal("signature-strip", rig.Setup.Templates.Single(template => template.IsDefault).Id);

        rig.Accepts("SetOfferedTemplates", """{"templateIds":["panorama-four"]}""");
        Assert.DoesNotContain(rig.Setup.Templates, template => template.IsDefault);
        Assert.Contains("defaultTemplate:DefaultTemplateNotOffered", ErrorsOf(rig));

        rig.Accepts("SetDefaultTemplate", """{"templateId":"panorama-four"}""");
        Assert.DoesNotContain("defaultTemplate:DefaultTemplateNotOffered", ErrorsOf(rig));

        rig.Accepts("SetOfferedTemplates", """{"templateIds":[]}""");
        Assert.Contains("templates:AtLeastOneTemplate", ErrorsOf(rig));
        Assert.All(rig.Setup.Templates, template => Assert.False(template.Offered));
    }

    [Fact]
    public async Task Offering_fewer_Looks_works_the_same_way()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.Accepts("SetOfferedLooks", """{"lookIds":["monochrome-v1","sepia-v1"]}""");
        Assert.Contains("defaultLook:DefaultLookNotOffered", ErrorsOf(rig));
        Assert.DoesNotContain(rig.Setup.Looks, look => look.IsDefault);

        rig.Accepts("SetDefaultLook", """{"lookId":"sepia-v1"}""");
        Assert.Equal("sepia-v1", rig.Setup.Looks.Single(look => look.IsDefault).Id);
        Assert.DoesNotContain("defaultLook:DefaultLookNotOffered", ErrorsOf(rig));

        rig.Accepts("SetOfferedLooks", """{"lookIds":[]}""");
        Assert.Contains("looks:AtLeastOneLook", ErrorsOf(rig));
    }

    [Fact]
    public async Task A_default_the_catalog_does_not_have_is_not_available()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.Rejects(RejectReason.NotAvailable, "SetDefaultTemplate", """{"templateId":"ghost"}""");
        rig.Rejects(RejectReason.NotAvailable, "SetDefaultLook", """{"lookId":"ghost"}""");

        Assert.False(rig.Setup.Dirty);
    }

    [Fact]
    public async Task The_form_lists_every_Template_and_Look_with_the_catalog_facts_and_the_six_timers_with_their_rules()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        var setup = rig.Setup;
        Assert.Equal(["signature-strip", "panorama-four", "butter-gingham", "polka-keepsake"], setup.Templates.Select(template => template.Id));
        Assert.Equal(7, setup.Looks.Count);
        Assert.Equal("naturale-v1", setup.Looks.Single(look => look.IsDefault).Id);
        Assert.All(setup.Templates, template => Assert.Equal(template.Width > template.Height, template.Landscape));
        Assert.Equal(
            [PhaseTimer.Template, PhaseTimer.PhotographsCapture, PhaseTimer.PhotographsReview, PhaseTimer.Filters, PhaseTimer.Keepsake, PhaseTimer.GuestGallery],
            setup.PhaseTimers.Select(timer => timer.Phase));
        Assert.Equal(new PhaseTimerOption(PhaseTimer.Keepsake, 12, 12, 5, 60, false), setup.PhaseTimers[4]);
        Assert.Equal(new PhaseTimerOption(PhaseTimer.Template, 30, 30, 20, 300, true), setup.PhaseTimers[0]);
    }

    [Fact]
    public async Task Timers_are_kept_as_typed_and_out_of_range_values_are_reported_per_phase()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.Timer("keepsake", 3);
        Assert.Equal(3, rig.Setup.PhaseTimers.Single(timer => timer.Phase == PhaseTimer.Keepsake).Seconds);
        Assert.Contains("timers.keepsake:TimerOutOfRange", ErrorsOf(rig));

        rig.Timer("keepsake", 0);
        Assert.Contains("timers.keepsake:TimerOutOfRange", ErrorsOf(rig));

        rig.Timer("keepsake", 60);
        Assert.DoesNotContain("timers.keepsake:TimerOutOfRange", ErrorsOf(rig));

        rig.Timer("template", 0);
        rig.Timer("photographsCapture", 301);
        rig.Timer("guestGallery", 0);
        Assert.Equal(["timers.photographsCapture:TimerOutOfRange", "timers.guestGallery:TimerOutOfRange"], ErrorsOf(rig).Where(error => error.StartsWith("timers.", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task The_PIN_pair_is_held_checked_and_reported_without_its_digits()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        rig.Pin("12", "12");
        Assert.Contains("pin:PinFormat", ErrorsOf(rig));
        Assert.Equal(new EventSetupPinView(false, true), rig.Setup.Pin);

        rig.Pin("1234", "1235");
        Assert.Contains("pin:PinMismatch", ErrorsOf(rig));

        rig.Pin("1234", "1234");
        Assert.DoesNotContain(ErrorsOf(rig), error => error.StartsWith("pin:", StringComparison.Ordinal));
        Assert.Equal(new EventSetupPinView(false, true), rig.Setup.Pin);
        Assert.True(rig.Setup.Dirty);

        rig.Pin(string.Empty, string.Empty);
        Assert.Contains("pin:PinRequired", ErrorsOf(rig));
        Assert.Equal(new EventSetupPinView(false, false), rig.Setup.Pin);
        Assert.False(rig.Setup.Dirty);
    }

    [Fact]
    public async Task The_PIN_digits_never_appear_in_a_presentation_a_diagnostics_result_or_a_log()
    {
        var store = new HookedStore(await SetupSamples.SeededStoreAsync());
        using var rig = await RigAsync(store, hooks: true);
        rig.OpenNew();
        FillValid(rig);
        rig.Pin(SetupSamples.Pin, SetupSamples.Pin);
        rig.Pin(SetupSamples.Pin, SetupSamples.Pin[..4]);
        rig.Pin(SetupSamples.Pin, SetupSamples.Pin);
        store.Before = method => method == nameof(IEventStore.SetPinAsync) ? Task.FromException(new StorageException(StorageErrorKind.Locked, "Locked.")) : Task.CompletedTask;
        rig.Accepts("SaveEvent");
        store.Before = null;
        rig.Accepts("SaveEvent");

        Assert.Equal(OperatorSurface.EventList, rig.Latest.Surface);
        var diagnostics = JsonSerializer.Serialize(rig.Diagnostics());
        Assert.DoesNotContain(SetupSamples.Pin, diagnostics, StringComparison.Ordinal);
        Assert.All(rig.Operator.Raw, json => Assert.DoesNotContain(SetupSamples.Pin, json, StringComparison.Ordinal));
        Assert.All(rig.Harness.Logger.Entries, entry =>
        {
            Assert.DoesNotContain(SetupSamples.Pin, entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(SetupSamples.Pin, entry.Exception?.ToString() ?? string.Empty, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Save_writes_a_new_Event_trimmed_and_returns_to_the_list_with_the_row_and_no_camera_held()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        FillValid(rig, "  Quay party  ");
        rig.Field("venue", " The Quay ");
        rig.Field("notes", "   ");
        rig.Accepts("SetCountdown", """{"seconds":10}""");
        rig.Accepts("SetGuestSounds", """{"enabled":false}""");
        rig.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(EventSetupCameraState.Ready, rig.Setup.Camera.State);

        rig.Accepts("SaveEvent");

        Assert.Equal(OperatorSurface.EventList, rig.Latest.Surface);
        var row = rig.List.Events.Single(row => row.Name == "Quay party");
        Assert.Equal("The Quay", row.Venue);
        Assert.Equal(4, rig.List.Events.Count);
        var saved = (await rig.Store.GetEventAsync(row.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal(("Quay party", "The Quay", null, 10, true), (saved.Name, saved.Venue, saved.Notes, saved.CountdownSeconds, saved.Mirror));
        Assert.False(saved.GuestSoundsEnabled);
        Assert.Equal(("fake-camera", "Fake camera", "fake-guest", "Fake guest"), (saved.CameraDeviceId, saved.CameraName, saved.GuestDisplayId, saved.GuestDisplayName));
        Assert.Equal(["signature-strip", "panorama-four", "butter-gingham", "polka-keepsake"], saved.OfferedTemplateIds);
        Assert.Equal(6, saved.PhaseTimers.Count);
        Assert.False(saved.HasPin);
        rig.WaitUntil(() => rig.Fake.OpenSessionCount == 0);
        Assert.Equal(Protocol.Cameras.CameraState.Idle, rig.Brain.CameraSlotState);
    }

    [Fact]
    public async Task Save_stores_a_pending_PIN_so_it_verifies()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        FillValid(rig);
        rig.Pin(SetupSamples.Pin, SetupSamples.Pin);

        rig.Accepts("SaveEvent");

        var id = rig.List.Events.Single(row => row.Name == "Quay party").Id;
        Assert.True(await rig.Store.VerifyPinAsync(id, SetupSamples.Pin, TestContext.Current.CancellationToken));
        Assert.False(await rig.Store.VerifyPinAsync(id, "000000", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_without_a_PIN_is_fine_and_leaves_the_Event_without_one()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        FillValid(rig);

        rig.Accepts("SaveEvent");

        var id = rig.List.Events.Single(row => row.Name == "Quay party").Id;
        Assert.False(await rig.Store.VerifyPinAsync(id, "1234", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Saving_an_edit_updates_the_same_Event_and_keeps_its_PIN()
    {
        var inner = await SetupSamples.SeededStoreAsync();
        await inner.SetPinAsync(SetupSamples.HarbourId, "2468", CancellationToken.None);
        using var rig = await RigAsync(new HookedStore(inner));
        rig.OpenEdit(SetupSamples.HarbourId);
        Assert.Equal(new EventSetupPinView(true, false), rig.Setup.Pin);

        rig.Field("name", "Harbour gala 2");
        rig.Accepts("SaveEvent");

        Assert.Equal(OperatorSurface.EventList, rig.Latest.Surface);
        Assert.Equal(3, rig.List.Events.Count);
        Assert.Equal("Harbour gala 2", rig.List.Events.Single(row => row.Id == SetupSamples.HarbourId).Name);
        Assert.True(await rig.Store.VerifyPinAsync(SetupSamples.HarbourId, "2468", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_failed_save_keeps_the_draft_open_with_a_notice_and_a_retry_works()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        FillValid(rig);
        StoreOf(rig).Inner.FailNext(StorageErrorKind.DiskFull);

        rig.Accepts("SaveEvent");

        Assert.Equal(OperatorSurface.EventSetup, rig.Latest.Surface);
        Assert.Equal(OperatorNoticeCode.SaveFailed, rig.Latest.Overlays.Notice!.Code);
        Assert.False(rig.Setup.Saving);
        Assert.True(rig.Setup.IsNew);
        Assert.True(rig.Setup.Dirty);
        Assert.Equal(3, (await rig.Store.ListEventsAsync(TestContext.Current.CancellationToken)).Count);

        rig.Accepts("DismissNotice");
        rig.Accepts("SaveEvent");

        Assert.Equal(OperatorSurface.EventList, rig.Latest.Surface);
        Assert.Equal(4, rig.List.Events.Count);
    }

    [Fact]
    public async Task A_PIN_that_fails_to_store_after_the_row_was_written_makes_the_retry_an_update()
    {
        var store = new HookedStore(await SetupSamples.SeededStoreAsync());
        using var rig = await RigAsync(store);
        rig.OpenNew();
        FillValid(rig);
        rig.Pin(SetupSamples.Pin, SetupSamples.Pin);
        store.Before = method => method == nameof(IEventStore.SetPinAsync)
            ? Task.FromException(new StorageException(StorageErrorKind.Locked, "Locked."))
            : Task.CompletedTask;

        rig.Accepts("SaveEvent");

        Assert.Equal(OperatorNoticeCode.SaveFailed, rig.Latest.Overlays.Notice!.Code);
        Assert.False(rig.Setup.IsNew);
        Assert.NotNull(rig.Setup.EventId);
        Assert.Equal(new EventSetupPinView(false, true), rig.Setup.Pin);
        Assert.Equal(4, (await rig.Store.ListEventsAsync(TestContext.Current.CancellationToken)).Count);

        store.Before = null;
        rig.Accepts("DismissNotice");
        rig.Accepts("SaveEvent");

        Assert.Equal(4, rig.List.Events.Count);
        Assert.True(await rig.Store.VerifyPinAsync(rig.List.Events.Single(row => row.Name == "Quay party").Id, SetupSamples.Pin, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_store_that_throws_something_unexpected_is_a_save_notice_and_the_log_has_no_message_text()
    {
        var store = new HookedStore(await SetupSamples.SeededStoreAsync());
        using var rig = await RigAsync(store);
        rig.OpenNew();
        FillValid(rig);
        store.Before = method => method == nameof(IEventStore.CreateEventAsync)
            ? Task.FromException(new InvalidOperationException("secret detail"))
            : Task.CompletedTask;

        rig.Accepts("SaveEvent");

        Assert.Equal(OperatorNoticeCode.SaveFailed, rig.Latest.Overlays.Notice!.Code);
        var entry = Assert.Single(rig.Harness.Logger.Entries, entry => entry.Message.StartsWith("Saving an Event failed", StringComparison.Ordinal));
        Assert.Contains("InvalidOperationException", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret detail", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_an_Event_that_vanished_says_so_and_keeps_the_draft()
    {
        using var rig = await RigAsync();
        rig.OpenEdit(SetupSamples.OlderId);
        await rig.Store.MarkEventDeletingAsync(SetupSamples.OlderId, TestContext.Current.CancellationToken);
        rig.Field("name", "Older party 2");

        rig.Accepts("SaveEvent");

        Assert.Equal(OperatorNoticeCode.EventNotFound, rig.Latest.Overlays.Notice!.Code);
        Assert.Equal(OperatorSurface.EventSetup, rig.Latest.Surface);
        Assert.False(rig.Setup.IsNew);
    }

    [Fact]
    public async Task While_a_save_is_in_flight_the_draft_cannot_change_and_setup_cannot_be_left()
    {
        var store = new HookedStore(await SetupSamples.SeededStoreAsync());
        using var rig = await RigAsync(store);
        rig.OpenNew();
        FillValid(rig);
        var gate = new TaskCompletionSource();
        store.Before = method => method == nameof(IEventStore.CreateEventAsync) ? gate.Task : Task.CompletedTask;

        rig.Accepts("SaveEvent");

        Assert.True(rig.Setup.Saving);
        rig.Rejects(RejectReason.NotAvailable, "ChangeEventField", """{"field":"name","value":"x"}""");
        rig.Rejects(RejectReason.NotAvailable, "SaveEvent");
        rig.Rejects(RejectReason.NotAvailable, "LeaveEventSetup");
        rig.Rejects(RejectReason.NotAvailable, "SaveAndStartEvent");
        rig.Rejects(RejectReason.NotAvailable, "SetPin", """{"pin":"1234","confirmation":"1234"}""");

        gate.SetResult();
        rig.WaitUntil(() => rig.Latest.Surface == OperatorSurface.EventList);

        Assert.Equal(4, rig.List.Events.Count);
    }

    [Fact]
    public async Task A_save_that_finishes_after_the_surface_was_left_is_dropped()
    {
        var store = new HookedStore(await SetupSamples.SeededStoreAsync());
        using var rig = await RigAsync(store);
        rig.OpenNew();
        FillValid(rig);
        var gate = new TaskCompletionSource();
        store.Before = method => method == nameof(IEventStore.CreateEventAsync) ? gate.Task : Task.CompletedTask;
        rig.Accepts("SaveEvent");
        rig.Brain.ShowOperatorSurface(OperatorSurface.EventList);
        rig.Harness.Run();
        store.Before = null;
        rig.OpenNew();
        rig.Field("name", "Second draft");

        gate.SetResult();
        Thread.Sleep(50);
        rig.Harness.Run();

        Assert.Equal(OperatorSurface.EventSetup, rig.Latest.Surface);
        Assert.Equal("Second draft", rig.Setup.Fields.Name);
        Assert.False(rig.Setup.Saving);
    }

    [Fact]
    public async Task A_valid_Event_can_be_saved_while_another_runs()
    {
        using var rig = await RigAsync();
        rig.StartRunning(await SetupSamples.LoadAsync(rig.Store, SetupSamples.HarbourId));
        rig.OpenEdit(SetupSamples.WeddingId);
        rig.Field("venue", "Elsewhere");

        rig.Accepts("SaveEvent");

        Assert.Equal(OperatorSurface.EventList, rig.Latest.Surface);
        Assert.Equal("Elsewhere", rig.List.Events.Single(row => row.Id == SetupSamples.WeddingId).Venue);
        Assert.Equal(SetupSamples.HarbourId, rig.Brain.ActiveEventId);
    }

    [Fact]
    public async Task Leaving_a_pristine_draft_goes_straight_back_and_hides_the_identify_overlays()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        rig.Accepts("IdentifyDisplays");

        rig.Accepts("LeaveEventSetup");

        Assert.Equal(OperatorSurface.EventList, rig.Latest.Surface);
        Assert.Null(rig.Latest.Overlays.Confirmation);
        Assert.Null(rig.Latest.Overlays.IdentifyDisplays);
        Assert.Equal(1, rig.Host.HideIdentifyCount);
        rig.Rejects(RejectReason.NotAvailable, "LeaveEventSetup");
    }

    [Fact]
    public async Task Leaving_a_dirty_draft_asks_what_to_do_and_keep_editing_closes_the_question()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        rig.Field("name", "  Quay party ");

        rig.Accepts("LeaveEventSetup");

        Assert.Equal(new ConfirmationOverlay(ConfirmationKind.LeaveSetup, "Quay party", null, null), rig.Latest.Overlays.Confirmation);
        Assert.Equal(OperatorSurface.EventSetup, rig.Latest.Surface);

        rig.Accepts("ConfirmLeave", """{"choice":"keepEditing"}""");

        Assert.Null(rig.Latest.Overlays.Confirmation);
        Assert.Equal("  Quay party ", rig.Setup.Fields.Name);
        rig.Rejects(RejectReason.NotAvailable, "ConfirmLeave", """{"choice":"keepEditing"}""");
    }

    [Fact]
    public async Task A_dirty_draft_without_a_name_asks_without_a_name()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        rig.Accepts("SetMirror", """{"enabled":false}""");

        rig.Accepts("LeaveEventSetup");

        Assert.Equal(new ConfirmationOverlay(ConfirmationKind.LeaveSetup, null, null, null), rig.Latest.Overlays.Confirmation);
    }

    [Fact]
    public async Task Discarding_closes_setup_without_saving_and_releases_the_camera()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        FillValid(rig);
        rig.Advance(TimeSpan.FromMilliseconds(500));
        rig.Accepts("IdentifyDisplays");
        rig.Accepts("LeaveEventSetup");

        rig.Accepts("ConfirmLeave", """{"choice":"discardChanges"}""");

        Assert.Equal(OperatorSurface.EventList, rig.Latest.Surface);
        Assert.Equal(3, rig.List.Events.Count);
        Assert.Null(rig.Latest.Overlays.Confirmation);
        Assert.Null(rig.Latest.Overlays.IdentifyDisplays);
        rig.WaitUntil(() => rig.Fake.OpenSessionCount == 0);
        Assert.Equal(Protocol.Cameras.CameraState.Idle, rig.Brain.CameraSlotState);
    }

    [Fact]
    public async Task Choosing_save_when_leaving_saves_and_returns_to_the_list()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        FillValid(rig);
        rig.Accepts("LeaveEventSetup");

        rig.Accepts("ConfirmLeave", """{"choice":"save"}""");

        Assert.Equal(OperatorSurface.EventList, rig.Latest.Surface);
        Assert.Equal(4, rig.List.Events.Count);
        Assert.Null(rig.Latest.Overlays.Confirmation);
    }

    [Fact]
    public async Task Choosing_save_when_the_draft_is_invalid_is_validationFailed_with_the_errors_showing()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        rig.Field("venue", "Somewhere");
        rig.Accepts("LeaveEventSetup");

        rig.Rejects(RejectReason.ValidationFailed, "ConfirmLeave", """{"choice":"save"}""");

        Assert.Equal(OperatorSurface.EventSetup, rig.Latest.Surface);
        Assert.True(rig.Setup.ShowErrors);
        Assert.Null(rig.Latest.Overlays.Confirmation);
    }

    [Fact]
    public async Task Another_confirmation_replaces_the_leave_question_and_then_ConfirmLeave_is_not_available()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        rig.Field("name", "x");
        rig.Accepts("LeaveEventSetup");
        rig.OnBrain(() => rig.Brain.SetConfirmation(new ConfirmationOverlay(ConfirmationKind.EndEvent, null, null, null)));

        rig.Rejects(RejectReason.NotAvailable, "ConfirmLeave", """{"choice":"discardChanges"}""");
    }

    [Fact]
    public async Task Editing_an_Event_loads_the_record_into_a_clean_draft_and_composes_its_blank_previews()
    {
        var inner = await SetupSamples.SeededStoreAsync();
        await inner.SetPinAsync(SetupSamples.HarbourId, "2468", CancellationToken.None);
        using var rig = await RigAsync(new HookedStore(inner));

        rig.OpenEdit(SetupSamples.HarbourId);

        var setup = rig.Setup;
        Assert.False(setup.IsNew);
        Assert.Equal(SetupSamples.HarbourId, setup.EventId);
        Assert.False(setup.Dirty);
        Assert.Equal(new EventSetupFieldValues("Harbour gala", "2026-06-12", "Harbour Hall", "Ana Lindqvist", string.Empty), setup.Fields);
        Assert.Equal(new EventSetupPinView(true, false), setup.Pin);
        Assert.Empty(setup.FieldErrors);
        Assert.Equal(
            ["signature-strip.naturale-v1.r0", "panorama-four.naturale-v1.r0", "butter-gingham.naturale-v1.r0", "polka-keepsake.naturale-v1.r0"],
            setup.Templates.Select(template => template.BlankPreviewKey));
    }

    [Fact]
    public async Task An_Event_that_is_gone_when_it_loads_gives_a_notice_and_a_fresh_list()
    {
        using var rig = await RigAsync();
        await rig.Store.MarkEventDeletingAsync(SetupSamples.OlderId, TestContext.Current.CancellationToken);

        rig.Accepts("EditEvent", $$"""{"eventId":"{{SetupSamples.OlderId}}"}""");

        Assert.Equal(OperatorSurface.EventList, rig.Latest.Surface);
        Assert.Equal(OperatorNoticeCode.EventNotFound, rig.Latest.Overlays.Notice!.Code);
        Assert.Equal([SetupSamples.WeddingId, SetupSamples.HarbourId], rig.List.Events.Select(row => row.Id));
        rig.Accepts("OpenNewEvent");
    }

    [Fact]
    public async Task An_Event_that_cannot_be_read_is_treated_as_not_found()
    {
        var store = new HookedStore(await SetupSamples.SeededStoreAsync());
        using var rig = await RigAsync(store);
        store.Before = method => method == nameof(IEventStore.GetEventAsync)
            ? Task.FromException(new StorageException(StorageErrorKind.Locked, "Locked."))
            : Task.CompletedTask;

        rig.Accepts("EditEvent", $$"""{"eventId":"{{SetupSamples.OlderId}}"}""");

        Assert.Equal(OperatorNoticeCode.EventNotFound, rig.Latest.Overlays.Notice!.Code);
        Assert.Contains(rig.Harness.Logger.Entries, entry => entry.Message.StartsWith("Reading an Event for Event setup failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_second_EditEvent_while_the_first_loads_is_not_available()
    {
        var store = new HookedStore(await SetupSamples.SeededStoreAsync());
        using var rig = await RigAsync(store);
        var gate = new TaskCompletionSource();
        store.Before = method => method == nameof(IEventStore.GetEventAsync) ? gate.Task : Task.CompletedTask;

        rig.Accepts("EditEvent", $$"""{"eventId":"{{SetupSamples.OlderId}}"}""");
        rig.Rejects(RejectReason.NotAvailable, "EditEvent", $$"""{"eventId":"{{SetupSamples.HarbourId}}"}""");
        rig.Rejects(RejectReason.NotAvailable, "OpenNewEvent");
        gate.SetResult();
        rig.WaitUntil(() => rig.Latest.Surface == OperatorSurface.EventSetup);

        Assert.Equal(SetupSamples.OlderId, rig.Setup.EventId);
    }

    [Fact]
    public async Task A_failed_Start_opens_the_Event_in_setup_with_the_reason_until_something_changes()
    {
        using var rig = await RigAsync();

        rig.OnBrain(() => rig.Brain.OpenEventSetupWithStartFailure(SetupSamples.HarbourId, new EventSetupStartFailure(StartFailureCode.GuestDisplayMissing, null)));
        rig.WaitUntil(() => rig.Latest.Surface == OperatorSurface.EventSetup);

        Assert.Equal(SetupSamples.HarbourId, rig.Setup.EventId);
        Assert.Equal(new EventSetupStartFailure(StartFailureCode.GuestDisplayMissing, null), rig.Setup.StartFailure);
        Assert.False(rig.Setup.Dirty);

        rig.Field("notes", "x");

        Assert.Null(rig.Setup.StartFailure);
    }

    [Fact]
    public async Task A_start_failure_for_an_Event_that_is_gone_gives_a_notice()
    {
        using var rig = await RigAsync();
        await rig.Store.MarkEventDeletingAsync(SetupSamples.OlderId, TestContext.Current.CancellationToken);

        rig.OnBrain(() => rig.Brain.OpenEventSetupWithStartFailure(SetupSamples.OlderId, new EventSetupStartFailure(StartFailureCode.PinMissing, null)));

        Assert.Equal(OperatorNoticeCode.EventNotFound, rig.Latest.Overlays.Notice!.Code);
    }

    [Fact]
    public async Task A_start_failure_does_not_replace_a_draft_that_is_open()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        rig.Field("name", "Mine");

        rig.OnBrain(() => rig.Brain.OpenEventSetupWithStartFailure(SetupSamples.HarbourId, new EventSetupStartFailure(StartFailureCode.InvalidSetup, null)));
        rig.Harness.Run();

        Assert.Equal("Mine", rig.Setup.Fields.Name);
        Assert.Null(rig.Setup.StartFailure);
    }

    [Fact]
    public async Task A_start_failure_arriving_for_a_stale_draft_is_dropped_and_arguments_are_checked()
    {
        using var rig = await RigAsync();
        rig.OpenNew();
        rig.Brain.ShowOperatorSurface(OperatorSurface.EventList);
        rig.Harness.Run();

        rig.OnBrain(() => rig.Brain.OpenEventSetupWithStartFailure(SetupSamples.HarbourId, new EventSetupStartFailure(StartFailureCode.InvalidSetup, null)));
        rig.WaitUntil(() => rig.Latest.Surface == OperatorSurface.EventSetup);

        Assert.Equal(SetupSamples.HarbourId, rig.Setup.EventId);
        Assert.Throws<ArgumentException>(() => rig.Brain.OpenEventSetupWithStartFailure(string.Empty, new EventSetupStartFailure(StartFailureCode.InvalidSetup, null)));
        Assert.Throws<ArgumentNullException>(() => rig.Brain.OpenEventSetupWithStartFailure(SetupSamples.HarbourId, null!));
    }

    [Fact]
    public async Task The_blank_Template_previews_are_composed_when_setup_opens()
    {
        using var rig = await RigAsync();

        rig.OpenNew();

        Assert.Equal(
            ["signature-strip.naturale-v1.r0", "panorama-four.naturale-v1.r0", "butter-gingham.naturale-v1.r0", "polka-keepsake.naturale-v1.r0"],
            rig.Setup.Templates.Select(template => template.BlankPreviewKey));
        Assert.All(rig.Setup.Templates, template =>
        {
            Assert.True(rig.Harness.Previews.TryGet(template.BlankPreviewKey!, out var png));
            Assert.False(png.IsEmpty);
        });
    }

    [Fact]
    public async Task A_Template_preview_that_fails_is_just_missing_and_never_blocks_setup()
    {
        var compositor = new FotoHavn.Core.Fakes.FakeCompositor(new FotoHavn.Catalog.CatalogData()) { Failure = new InvalidOperationException("no compositing today") };
        using var rig = new SetupRig(new HookedStore(await SetupSamples.SeededStoreAsync()), compositor: compositor);

        rig.Accepts("OpenNewEvent");
        Thread.Sleep(100);
        rig.Harness.Run();

        Assert.All(rig.Setup.Templates, template => Assert.Null(template.BlankPreviewKey));
        rig.Field("name", "Still works");
        Assert.Equal("Still works", rig.Setup.Fields.Name);
    }

    [Fact]
    public async Task A_preview_that_arrives_after_setup_closed_is_dropped()
    {
        var gated = new GatedCompositor();
        using var rig = new SetupRig(new HookedStore(await SetupSamples.SeededStoreAsync()), compositor: gated);
        rig.Accepts("OpenNewEvent");
        rig.Accepts("LeaveEventSetup");

        gated.Gate.SetResult();
        Thread.Sleep(100);
        rig.Harness.Run();

        Assert.Equal(OperatorSurface.EventList, rig.Latest.Surface);
        Assert.Null(rig.Latest.EventSetup);
    }
}
