using System.Text.Json;
using FotoHavn.Core.Events;
using FotoHavn.Core.Gallery;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Gallery;

public sealed class GalleryRulesTests
{
    private static readonly GuestCycleEndReason[] AllReasons = [.. Enum.GetValues<GuestCycleEndReason>()];

    private static readonly int[] ArrangementOrder = [2, 4, 1, 3];

    [Theory]
    [InlineData(true, 1, true)]
    [InlineData(true, 40, true)]
    [InlineData(true, 0, false)]
    [InlineData(false, 5, false)]
    [InlineData(false, 0, false)]
    public void The_Start_link_needs_the_setting_and_a_completed_Photo_Strip(bool enabled, int strips, bool expected) =>
        Assert.Equal(expected, GalleryRules.GuestLinkVisible(enabled, strips));

    [Theory]
    [InlineData(GuestCycleEndReason.Completed, GalleryOutcome.Completed)]
    [InlineData(GuestCycleEndReason.StartedOver, GalleryOutcome.StartedOver)]
    [InlineData(GuestCycleEndReason.OperatorExit, GalleryOutcome.EndedByOperator)]
    [InlineData(GuestCycleEndReason.AppClosed, GalleryOutcome.AppClosed)]
    [InlineData(GuestCycleEndReason.Unrecoverable, GalleryOutcome.Unrecoverable)]
    public void Every_end_reason_reads_as_its_outcome(GuestCycleEndReason reason, GalleryOutcome outcome) =>
        Assert.Equal(outcome, GalleryRules.OutcomeOf(reason));

    [Fact]
    public void Every_end_reason_has_an_outcome_and_no_two_share_one() =>
        Assert.Equal(AllReasons.Length, AllReasons.Select(GalleryRules.OutcomeOf).Distinct().Count());

    [Fact]
    public void An_unknown_end_reason_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => GalleryRules.OutcomeOf((GuestCycleEndReason)99));

    [Fact]
    public void The_operator_list_shows_ended_cycles_and_never_an_open_one()
    {
        var rows = new[]
        {
            GalleryKit.Summary(5, reason: null),
            GalleryKit.Summary(4, GuestCycleEndReason.StartedOver, withStrip: false),
            GalleryKit.Summary(3),
            GalleryKit.Summary(2, GuestCycleEndReason.Unrecoverable, withStrip: false),
        };

        Assert.Equal([GalleryKit.Id(4), GalleryKit.Id(3), GalleryKit.Id(2)], GalleryRules.OperatorRows(rows).Select(row => row.Cycle.Id));
        Assert.False(GalleryRules.IsOperatorListed(rows[0]));
    }

    [Fact]
    public void The_guest_list_shows_only_completed_cycles_with_a_Photo_Strip()
    {
        var rows = new[]
        {
            GalleryKit.Summary(6, reason: null),
            GalleryKit.Summary(5, GuestCycleEndReason.StartedOver),
            GalleryKit.Summary(4, GuestCycleEndReason.OperatorExit, withStrip: false),
            GalleryKit.Summary(3, GuestCycleEndReason.AppClosed),
            GalleryKit.Summary(2, GuestCycleEndReason.Unrecoverable),
            GalleryKit.Summary(1, GuestCycleEndReason.Completed, withStrip: false),
            GalleryKit.Summary(7),
        };

        var visible = GalleryRules.GuestRows(rows);

        Assert.Equal([GalleryKit.Id(7)], visible.Select(row => row.Cycle.Id));
        Assert.All(rows.Where(row => row.Cycle.Id != GalleryKit.Id(7)), row => Assert.False(GalleryRules.IsGuestVisible(row)));
    }

    [Fact]
    public void A_guest_can_open_only_a_completed_Guest_Cycle_with_a_Photo_Strip()
    {
        Assert.True(GalleryRules.IsGuestVisible(GalleryKit.Detail(1)));
        Assert.False(GalleryRules.IsGuestVisible(GalleryKit.Detail(2, GuestCycleEndReason.StartedOver)));
        Assert.False(GalleryRules.IsGuestVisible(GalleryKit.Detail(3, withStrip: false)));
        Assert.False(GalleryRules.IsGuestVisible(GalleryKit.Detail(4, reason: null)));
    }

    [Fact]
    public void The_arrangement_orders_the_kept_Captures_and_leaves_out_discarded_ones()
    {
        var discarded = GalleryKit.Capture(1, 5, CaptureState.Discarded);
        var detail = GalleryKit.Detail(1, arranged: [3, 1, 4, 2], extra: [discarded]);

        Assert.Equal([3, 1, 4, 2], GalleryRules.ArrangedCaptures(detail).Select(capture => capture.Sequence));
    }

    [Fact]
    public void Without_a_Photo_Strip_the_kept_Captures_come_by_sequence()
    {
        var detail = GalleryKit.Detail(1, GuestCycleEndReason.StartedOver, withStrip: false);
        var shuffled = detail with { Captures = [detail.Captures[2], detail.Captures[0], detail.Captures[3], detail.Captures[1], GalleryKit.Capture(1, 9, CaptureState.Discarded)] };

        Assert.Equal([1, 2, 3, 4], GalleryRules.ArrangedCaptures(shuffled).Select(capture => capture.Sequence));
    }

    [Fact]
    public void A_Capture_the_strip_names_but_the_cycle_no_longer_keeps_is_skipped()
    {
        var detail = GalleryKit.Detail(1, arranged: [3, 1, 4, 2]);
        var withoutThree = detail with { Captures = [.. detail.Captures.Where(capture => capture.Sequence != 3)] };

        Assert.Equal([1, 4, 2], GalleryRules.ArrangedCaptures(withoutThree).Select(capture => capture.Sequence));
    }

    [Fact]
    public void Deleting_counts_the_kept_Captures_and_the_Photo_Strip()
    {
        Assert.Equal(5, GalleryRules.FileCount(GalleryKit.Detail(1)));
        Assert.Equal(4, GalleryRules.FileCount(GalleryKit.Detail(2, GuestCycleEndReason.StartedOver, withStrip: false)));
        Assert.Equal(5, GalleryRules.FileCount(GalleryKit.Detail(3, extra: [GalleryKit.Capture(3, 6, CaptureState.Discarded)])));
    }

    [Fact]
    public void The_delete_confirmation_names_the_time_and_counts_the_files()
    {
        var confirmation = GalleryRules.DeleteConfirmation(GalleryKit.Detail(1));

        Assert.Equal(
            new ConfirmationOverlay(ConfirmationKind.DeleteGuestCycle, null, null, new DeleteGuestCycleConfirmation(GalleryKit.Id(1), GalleryKit.Noon, 5)),
            confirmation);
    }

    [Fact]
    public void An_operator_item_carries_the_outcome_and_the_names_and_hides_Captures_behind_a_strip()
    {
        var summary = GalleryKit.Summary(1);
        var strip = GalleryRules.StripThumbnail(summary.PhotoStrip!, "https://media.fotohavn/thumbnails/a.jpg");
        var capture = GalleryRules.CaptureThumbnail(GalleryKit.Capture(1, 1), "https://media.fotohavn/thumbnails/b.jpg");

        var withStrip = GalleryRules.OperatorItem(summary.Cycle, "Signature Strip", "Naturale", strip, [capture], unavailable: false);
        var withoutStrip = GalleryRules.OperatorItem(GalleryKit.Cycle(2, GuestCycleEndReason.StartedOver), null, null, null, [capture], unavailable: false);
        var missing = GalleryRules.OperatorItem(GalleryKit.Cycle(3, GuestCycleEndReason.AppClosed), "Signature Strip", "Naturale", null, [], unavailable: true);

        Assert.Equal(new GalleryItemView(GalleryKit.Id(1), GalleryKit.Noon, GalleryOutcome.Completed, "Signature Strip", "Naturale", strip, [], false), withStrip);
        Assert.Equal(new GalleryStripThumbnailView("https://media.fotohavn/thumbnails/a.jpg", 600, 1800, false), strip);
        Assert.Equal(new GalleryCaptureThumbnailView("https://media.fotohavn/thumbnails/b.jpg", 1640, 1230), capture);
        Assert.Equal(GalleryOutcome.StartedOver, withoutStrip.Outcome);
        Assert.Null(withoutStrip.Strip);
        Assert.Equal([capture], withoutStrip.Captures);
        Assert.True(missing.Unavailable);
        Assert.Empty(missing.Captures);
    }

    [Fact]
    public void A_landscape_strip_is_marked_landscape_in_every_view()
    {
        var panorama = GalleryKit.Summary(1) with { PhotoStrip = GalleryKit.Strip(1, [GalleryKit.Capture(1, 1)], 1800, 600) };
        var detail = GalleryKit.Detail(1) with { PhotoStrip = panorama.PhotoStrip };

        Assert.True(GalleryRules.StripThumbnail(panorama.PhotoStrip!, "u").Landscape);
        Assert.True(GalleryRules.GuestStrip(panorama, "u").Landscape);
        Assert.True(GalleryRules.OperatorDetail(GalleryKit.EventId, "Gala", detail, "Panorama Four", "Naturale", false, true).Strip!.Landscape);
        Assert.True(GalleryRules.GuestStripDetail(GalleryKit.EventId, detail).Strip.Landscape);
    }

    [Fact]
    public void An_open_Guest_Cycle_can_not_be_listed_or_opened()
    {
        var open = GalleryKit.Cycle(1, reason: null);

        Assert.Throws<ArgumentException>(() => GalleryRules.OperatorItem(open, null, null, null, [], false));
        Assert.Throws<ArgumentException>(() => GalleryRules.OperatorDetail(GalleryKit.EventId, "Gala", GalleryKit.Detail(1, reason: null), null, null, false, true));
    }

    [Fact]
    public void The_operator_detail_has_the_full_files_in_arrangement_order_and_its_actions()
    {
        var detail = GalleryKit.Detail(1, arranged: ArrangementOrder);

        var view = GalleryRules.OperatorDetail(GalleryKit.EventId, "Harbour gala", detail, "Signature Strip", "Naturale", false, true);

        Assert.Equal(GalleryKit.EventId, view.EventId);
        Assert.Equal("Harbour gala", view.EventName);
        Assert.Equal(GalleryKit.Id(1), view.GuestCycleId);
        Assert.Equal(GalleryOutcome.Completed, view.Outcome);
        Assert.Equal($"https://media.fotohavn/events/{GalleryKit.EventId}/guest-cycles/{GalleryKit.Id(1)}/photo-strip.png?v=00000001", view.Strip!.ImageUrl);
        Assert.Equal(
            ArrangementOrder.Select(sequence => $"https://media.fotohavn/events/{GalleryKit.EventId}/guest-cycles/{GalleryKit.Id(1)}/capture-{sequence}.jpg?v={sequence:x8}"),
            view.Captures.Select(capture => capture.ImageUrl));
        Assert.True(view.CanOpenFolder);
        Assert.True(view.CanDelete);
        Assert.False(view.Unavailable);
    }

    [Fact]
    public void The_operator_detail_of_a_cycle_without_a_strip_shows_its_Captures_and_can_be_unavailable()
    {
        var view = GalleryRules.OperatorDetail(GalleryKit.EventId, "Gala", GalleryKit.Detail(2, GuestCycleEndReason.OperatorExit, withStrip: false), null, null, unavailable: true, canDelete: false);

        Assert.Null(view.Strip);
        Assert.Equal(4, view.Captures.Count);
        Assert.Null(view.TemplateName);
        Assert.True(view.Unavailable);
        Assert.False(view.CanDelete);
    }

    [Fact]
    public void A_guest_strip_carries_the_image_its_proportion_and_the_alt_text()
    {
        var view = GalleryRules.GuestStrip(GalleryKit.Summary(1), "https://media.fotohavn/thumbnails/a.jpg");

        Assert.Equal(new GuestStripView(GalleryKit.Id(1), "https://media.fotohavn/thumbnails/a.jpg", 600, 1800, false, "Photo Strip from this Event"), view);
    }

    [Fact]
    public void A_guest_strip_is_refused_for_a_cycle_a_guest_must_not_see()
    {
        Assert.Throws<ArgumentException>(() => GalleryRules.GuestStrip(GalleryKit.Summary(1, GuestCycleEndReason.StartedOver), "u"));
        Assert.Throws<ArgumentException>(() => GalleryRules.GuestStrip(GalleryKit.Summary(1, withStrip: false), "u"));
        Assert.Throws<ArgumentException>(() => GalleryRules.GuestStripDetail(GalleryKit.EventId, GalleryKit.Detail(1, GuestCycleEndReason.OperatorExit)));
    }

    [Fact]
    public void The_guest_detail_has_the_strip_and_only_the_arranged_Captures()
    {
        var discarded = GalleryKit.Capture(1, 5, CaptureState.Discarded);
        var detail = GalleryKit.Detail(1, arranged: [3, 1, 4], extra: [discarded]);

        var view = GalleryRules.GuestStripDetail(GalleryKit.EventId, detail);

        Assert.Equal("Photo Strip from this Event", view.Strip.Alt);
        Assert.Equal([3, 1, 4], view.Captures.Select(capture => int.Parse(capture.ImageUrl.Split("capture-")[1].Split('.')[0], System.Globalization.CultureInfo.InvariantCulture)));
        Assert.All(view.Captures, capture => Assert.Equal("Photograph from this Event", capture.Alt));
        Assert.DoesNotContain(view.Captures, capture => capture.ImageUrl.Contains("capture-5", StringComparison.Ordinal));
    }

    private static readonly string[] GuestGalleryNames = ["strips", "nextCursor", "loadingMore", "guestCycleId", "thumbnailUrl", "width", "height", "landscape", "alt"];

    private static readonly string[] GuestDetailNames = ["strip", "captures", "imageUrl", "width", "height", "landscape", "alt"];

    [Fact]
    public void The_guest_gallery_view_serializes_only_images_proportions_and_alt_text()
    {
        var view = new GuestGalleryView([GalleryRules.GuestStrip(GalleryKit.Summary(1), "u"), GalleryRules.GuestStrip(GalleryKit.Summary(2), "u")], "cursor", false);

        Assert.Subset(GuestGalleryNames.ToHashSet(StringComparer.Ordinal), PropertyNames(ProtocolJson.Serialize(view)));
    }

    [Fact]
    public void The_guest_strip_detail_view_serializes_only_images_proportions_and_alt_text()
    {
        var view = GalleryRules.GuestStripDetail(GalleryKit.EventId, GalleryKit.Detail(1));

        Assert.Subset(GuestDetailNames.ToHashSet(StringComparer.Ordinal), PropertyNames(ProtocolJson.Serialize(view)));
    }

    [Fact]
    public void A_guest_presentation_of_the_gallery_surfaces_carries_no_metadata_or_operator_data()
    {
        string[] allowed =
        [
            "surface", "start", "startUnavailable", "overlays", "fatal", "guestCycle", "template", "photographs", "filters", "keepsake", "operatorAssistance", "guestGallery", "guestStripDetail",
            "exitHold", "pinDialog", "phaseTimer", "notice", "phase", "remainingSeconds", "paused",
            .. GuestGalleryNames,
            .. GuestDetailNames,
        ];
        var overlays = new GuestOverlays(null, null, new PhaseTimerOverlay(PhaseTimer.GuestGallery, 12, false), null);
        var list = new GuestPresentation(
            GuestSurface.GuestGallery,
            null,
            null,
            overlays,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            new GuestGalleryView([GalleryRules.GuestStrip(GalleryKit.Summary(1), "u")], null, false),
            null);
        var detail = list with { Surface = GuestSurface.GuestStripDetail, GuestGallery = null, GuestStripDetail = GalleryRules.GuestStripDetail(GalleryKit.EventId, GalleryKit.Detail(1)) };

        Assert.Subset(allowed.ToHashSet(StringComparer.Ordinal), PropertyNames(ProtocolJson.Serialize(list)));
        Assert.Subset(allowed.ToHashSet(StringComparer.Ordinal), PropertyNames(ProtocolJson.Serialize(detail)));
    }

    [Fact]
    public void The_guest_views_declare_exactly_the_allowed_members()
    {
        Assert.Equal(["Strips", "NextCursor", "LoadingMore"], Members<GuestGalleryView>());
        Assert.Equal(["GuestCycleId", "ThumbnailUrl", "Width", "Height", "Landscape", "Alt"], Members<GuestStripView>());
        Assert.Equal(["Strip", "Captures"], Members<GuestStripDetailView>());
        Assert.Equal(["ImageUrl", "Width", "Height", "Landscape", "Alt"], Members<GuestStripImageView>());
        Assert.Equal(["ImageUrl", "Width", "Height", "Alt"], Members<GuestCaptureImageView>());
    }

    [Fact]
    public void The_guest_gallery_Phase_Timer_is_60_seconds_between_20_and_300_and_never_off()
    {
        var phase = PhaseTimerPhase.GuestGallery;

        Assert.Equal(60, PhaseTimerRules.Default(phase));
        Assert.Equal(20, PhaseTimerRules.Min(phase));
        Assert.Equal(300, PhaseTimerRules.Max(phase));
        Assert.False(PhaseTimerRules.CanBeOff(phase));
        Assert.Equal(PhaseTimer.GuestGallery, PhaseTimerRules.ToProtocol(phase));
        Assert.Equal("guestGallery", ProtocolNames.Of(PhaseTimer.GuestGallery));
        Assert.Equal("guest_gallery", StorageSqlNames.ToSql(phase));
    }

    [Fact]
    public void The_page_size_is_forty() => Assert.Equal(40, GalleryRules.PageSize);

    [Fact]
    public void The_Guest_Cycle_folder_is_named_by_ids_under_the_Event_folder()
    {
        Assert.Equal(
            Path.Combine(@"C:\FOTOHAVN\Data", "Events", GalleryKit.EventId, "GuestCycles", GalleryKit.Id(1)),
            GalleryRules.GuestCycleFolder(@"C:\FOTOHAVN\Data", GalleryKit.EventId, GalleryKit.Id(1)));
    }

    [Fact]
    public void The_Guest_Cycle_folder_refuses_an_empty_root_and_ids_that_are_not_ids()
    {
        Assert.Throws<ArgumentException>(() => GalleryRules.GuestCycleFolder(string.Empty, GalleryKit.EventId, GalleryKit.Id(1)));
        Assert.Throws<ArgumentException>(() => GalleryRules.GuestCycleFolder(@"C:\Data", "..", GalleryKit.Id(1)));
        Assert.Throws<ArgumentException>(() => GalleryRules.GuestCycleFolder(@"C:\Data", GalleryKit.EventId, @"..\x"));
    }

    private static string[] Members<T>() =>
        [.. typeof(T).GetConstructors().Single().GetParameters().Select(parameter => parameter.Name!)];

    private static HashSet<string> PropertyNames(string json)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(json);
        Collect(document.RootElement, names);
        return names;
    }

    private static void Collect(JsonElement element, HashSet<string> names)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    names.Add(property.Name);
                    Collect(property.Value, names);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, names);
                }

                break;
        }
    }
}
