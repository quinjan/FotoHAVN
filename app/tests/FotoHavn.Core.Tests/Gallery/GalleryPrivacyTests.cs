using System.Text.Json;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Core.Tests.GuestCycles.Orchestration;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Gallery;

/// <summary>What must never reach a guest, a presentation, or a log, and which commands a guest can never reach.</summary>
public sealed class GalleryPrivacyTests
{
    private static readonly HashSet<string> GuestGalleryNames = new(StringComparer.Ordinal)
    {
        "strips",
        "nextCursor",
        "loadingMore",
        "guestCycleId",
        "thumbnailUrl",
        "width",
        "height",
        "landscape",
        "alt",
        "strip",
        "captures",
        "imageUrl",
    };

    private static readonly string[] OperatorOnly =
    [
        "startedAt",
        "outcome",
        "templateName",
        "lookName",
        "eventName",
        "fileCount",
        "canDelete",
        "canOpenFolder",
        "unavailable",
        "keptCaptureCount",
        "endReason",
        "Studio open day",
        "Harbour Hall",
        "Ana Lindqvist",
        "Signature Strip",
        "Naturale",
        "Started over",
        "FOTOHAVN-test",
    ];

    private static async Task<GalleryRig> BrowsingAsync()
    {
        var rig = await GalleryRig.CreateAsync();
        await rig.SeedAsync(new CycleSpec(), new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 2), new CycleSpec(StripWidth: 1800, StripHeight: 600), new CycleSpec(Discarded: 1));
        rig.StartEvent();
        return rig;
    }

    private static void CollectNames(JsonElement element, HashSet<string> names)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    names.Add(property.Name);
                    CollectNames(property.Value, names);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectNames(item, names);
                }

                break;
            default:
                break;
        }
    }

    private static string GuestPresentationJson(GalleryRig rig) => ActiveRig.LatestRaw(rig.Active.Guest!);

    [Fact]
    public async Task The_guest_gallery_and_strip_detail_carry_only_the_allowed_properties()
    {
        using var rig = await BrowsingAsync();
        rig.OpenGuestGallery();
        var gallery = GuestPresentationJson(rig);
        rig.OpenStrip(rig.StripIds[0]);
        var detail = GuestPresentationJson(rig);

        foreach (var (json, property) in new[] { (gallery, "guestGallery"), (detail, "guestStripDetail") })
        {
            using var document = JsonDocument.Parse(json);
            var view = document.RootElement.GetProperty("presentation").GetProperty(property);
            var names = new HashSet<string>(StringComparer.Ordinal);
            CollectNames(view, names);
            Assert.Empty(names.Except(GuestGalleryNames));
        }
    }

    [Fact]
    public async Task Nothing_an_operator_sees_reaches_the_guest_screen_in_any_gallery_state()
    {
        using var rig = await BrowsingAsync();
        var seen = new List<string> { GuestPresentationJson(rig) };
        rig.OpenGuestGallery();
        seen.Add(GuestPresentationJson(rig));
        rig.OpenStrip(rig.StripIds[1]);
        seen.Add(GuestPresentationJson(rig));
        rig.Advance(TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(1));
        seen.Add(GuestPresentationJson(rig));
        rig.Active.HoldToExit();
        rig.Settle();
        seen.Add(GuestPresentationJson(rig));

        foreach (var json in seen)
        {
            foreach (var forbidden in OperatorOnly)
            {
                Assert.DoesNotContain(forbidden, json, StringComparison.Ordinal);
            }

            Assert.DoesNotContain(ActiveRig.Pin, json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Neither_screen_ever_receives_the_PIN_or_the_Data_folder_path()
    {
        using var rig = await BrowsingAsync();
        rig.OpenGuestGallery();
        rig.OpenStrip(rig.StripIds[0]);
        rig.Active.HoldToExit();
        rig.Settle();
        rig.OpenGallery();
        var cycleId = rig.ItemIds[0];
        rig.OpenCycle(cycleId);
        rig.Operator("OpenGuestCycleFolder", GalleryRig.Json("guestCycleId", cycleId));
        rig.Active.SubmitPin("7351");

        var everything = rig.PresentationsSent();

        Assert.DoesNotContain(ActiveRig.Pin, everything, StringComparison.Ordinal);
        Assert.DoesNotContain("7351", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("FOTOHAVN-test", everything, StringComparison.Ordinal);
        Assert.DoesNotContain(rig.Active.Logger.Entries, entry => entry.Message.Contains("FOTOHAVN-test", StringComparison.Ordinal) || entry.Message.Contains(ActiveRig.Pin, StringComparison.Ordinal));
        Assert.Single(rig.Diagnostics().OpenFolderRequests);
    }

    [Theory]
    [InlineData("OpenGallery", """{"eventId":"11111111-1111-4111-8111-111111111111"}""")]
    [InlineData("LoadMoreGuestCycles", """{"cursor":"c"}""")]
    [InlineData("OpenGuestCycle", """{"guestCycleId":"00000000-0000-0000-0000-000000000001"}""")]
    [InlineData("CloseGuestCycle", "{}")]
    [InlineData("OpenGuestCycleFolder", """{"guestCycleId":"00000000-0000-0000-0000-000000000001"}""")]
    [InlineData("DeleteGuestCycle", """{"guestCycleId":"00000000-0000-0000-0000-000000000001"}""")]
    [InlineData("ConfirmDeleteGuestCycle", "{}")]
    [InlineData("CancelDeleteGuestCycle", "{}")]
    [InlineData("CloseGallery", "{}")]
    public async Task A_guest_browsing_can_not_reach_an_operator_gallery_command(string name, string payload)
    {
        using var rig = await BrowsingAsync();
        rig.OpenGuestGallery();
        var before = rig.StripIds;
        var marked = rig.Store.Marked.Count;

        var result = rig.Guest(name, payload);

        result.ShouldBeRejected(RejectReason.WrongRole);
        Assert.Equal(before, rig.StripIds);
        Assert.Equal(marked, rig.Store.Marked.Count);
        Assert.Empty(rig.Active.Recorded.OpenedFolders);
        Assert.Null(rig.Diagnostics().Gallery!.Operator);
    }

    [Theory]
    [InlineData("OpenGuestGallery", "{}")]
    [InlineData("LoadMoreGuestStrips", """{"cursor":"c"}""")]
    [InlineData("OpenGuestStrip", """{"guestCycleId":"00000000-0000-0000-0000-000000000001"}""")]
    [InlineData("CloseGuestStrip", "{}")]
    [InlineData("BackToStart", "{}")]
    public async Task An_operator_can_not_reach_a_guest_gallery_command(string name, string payload)
    {
        using var rig = await BrowsingAsync();

        rig.Operator(name, payload).ShouldBeRejected(RejectReason.WrongRole);

        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        Assert.Null(rig.Diagnostics().Gallery);
    }

    [Fact]
    public async Task The_guest_view_models_are_complete_snapshots_that_do_not_change_when_nothing_did()
    {
        using var rig = await BrowsingAsync();
        rig.OpenGuestGallery();
        var revision = rig.Diagnostics().Presentations.Single(entry => entry.Role == Role.Guest).Revision;

        rig.Guest("LoadMoreGuestStrips", """{"cursor":"nope"}""").ShouldBeRejected();
        rig.Settle();

        Assert.Equal(revision, rig.Diagnostics().Presentations.Single(entry => entry.Role == Role.Guest).Revision);
    }
}
