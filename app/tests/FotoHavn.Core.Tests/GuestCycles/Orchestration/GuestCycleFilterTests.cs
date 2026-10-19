using FotoHavn.Core.Catalog;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

/// <summary>Filters, the final Photo Strip, and the Keepsake (plan section 7, Steps and Failures): Look thumbnails, strip previews, composition failures, and completion.</summary>
public sealed class GuestCycleFilterTests
{
    private static async Task<GuestCycleRig> OnFiltersAsync(GuestRigOptions? options = null)
    {
        var rig = await GuestCycleRig.CreateAsync(options);
        rig.ToArrange();
        rig.ToFilters();
        return rig;
    }

    private static bool Cached(GuestCycleRig rig, string? key) => key is not null && rig.Active.Previews.TryGet(key, out _);

    private static async Task<GuestCycleRig> OnKeepsakeAsync(GuestRigOptions? options = null)
    {
        var rig = await OnFiltersAsync(options);
        rig.Guest("FinishStrip").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50)));
        return rig;
    }

    [Fact]
    public async Task Filters_offers_the_Events_Looks_with_thumbnails_from_the_first_photograph_and_the_strip_preview()
    {
        using var rig = await OnFiltersAsync();

        var filters = rig.Filters;
        Assert.Equal(["naturale-v1", "monochrome-v1"], filters.Looks.Select(look => look.Id));
        Assert.All(filters.Looks, look =>
        {
            Assert.False(string.IsNullOrEmpty(look.Name));
            Assert.False(string.IsNullOrEmpty(look.Description));
            Assert.True(PreviewKey.IsValid(look.ThumbnailKey));
            Assert.True(Cached(rig, look.ThumbnailKey));
        });
        Assert.Equal("naturale-v1", filters.SelectedLookId);
        Assert.Equal((600, 1800, false), (filters.Width, filters.Height, filters.Landscape));
        Assert.True(PreviewKey.IsValid(filters.StripPreviewKey));
        Assert.True(Cached(rig, filters.StripPreviewKey));
        Assert.False(filters.Composing);
        Assert.False(filters.CompositionFailed);
        Assert.True(filters.CanFinish);

        var thumbnails = rig.Compositor.ThumbnailRequests;
        Assert.Equal(2, thumbnails.Count);
        Assert.All(thumbnails, thumbnail => Assert.Equal(600.0 / 450.0, thumbnail.AspectRatio, 6));
        Assert.Equal(["naturale-v1", "monochrome-v1"], thumbnails.Select(thumbnail => thumbnail.LookId).Order(StringComparer.Ordinal).Reverse());
        var strip = rig.Compositor.Requests.Last(request => request.Photos.Any(photo => photo is not null));
        Assert.Equal(("signature-strip", "naturale-v1"), (strip.TemplateId, strip.LookId));
        Assert.Equal(4, strip.Photos.Count);
        Assert.All(strip.Photos, photo => Assert.IsType<CompositionPhoto.Encoded>(photo));
        Assert.Equal([GuestStepState.Done, GuestStepState.Done, GuestStepState.Current, GuestStepState.Upcoming], rig.View.GuestCycle!.Steps.Select(step => step.State));
    }

    [Fact]
    public async Task Choosing_a_Look_composes_its_strip_keeping_the_previous_preview_meanwhile()
    {
        using var rig = await OnFiltersAsync();
        var naturale = rig.Filters.StripPreviewKey;
        rig.Compositor.StripGate = new TaskCompletionSource();

        rig.Guest("SelectLook", """{"lookId":"monochrome-v1"}""").ShouldBeAccepted();

        Assert.Equal("monochrome-v1", rig.Filters.SelectedLookId);
        Assert.True(rig.Filters.Composing);
        Assert.False(rig.Filters.CanFinish);
        Assert.Equal(naturale, rig.Filters.StripPreviewKey);
        rig.Compositor.StripGate.SetResult();
        rig.Compositor.StripGate = null;
        Assert.True(rig.AdvanceUntil(() => !rig.Filters.Composing, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50)));
        Assert.NotEqual(naturale, rig.Filters.StripPreviewKey);
        Assert.True(Cached(rig, rig.Filters.StripPreviewKey));
    }

    [Fact]
    public async Task A_strip_composed_for_an_arrangement_and_Look_is_reused_not_composed_again()
    {
        using var rig = await OnFiltersAsync();
        var naturale = rig.Filters.StripPreviewKey;
        rig.Guest("SelectLook", """{"lookId":"monochrome-v1"}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => !rig.Filters.Composing, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50)));
        var requests = rig.Compositor.Requests.Count;

        rig.Guest("SelectLook", """{"lookId":"naturale-v1"}""").ShouldBeAccepted();

        Assert.Equal(naturale, rig.Filters.StripPreviewKey);
        Assert.False(rig.Filters.Composing);
        Assert.Equal(requests, rig.Compositor.Requests.Count);
    }

    [Fact]
    public async Task A_preview_for_a_Look_the_guest_has_already_left_is_dropped()
    {
        using var rig = await OnFiltersAsync();
        var naturale = rig.Filters.StripPreviewKey;
        rig.Compositor.StripGate = new TaskCompletionSource();
        rig.Guest("SelectLook", """{"lookId":"monochrome-v1"}""").ShouldBeAccepted();

        rig.Guest("SelectLook", """{"lookId":"naturale-v1"}""").ShouldBeAccepted();
        rig.Compositor.StripGate.SetResult();
        rig.Compositor.StripGate = null;
        rig.Advance(TimeSpan.FromMilliseconds(200));

        Assert.Equal(naturale, rig.Filters.StripPreviewKey);
        Assert.False(rig.Filters.Composing);
    }

    [Fact]
    public async Task Choosing_the_selected_Look_is_a_no_op_and_an_unoffered_Look_is_invalid()
    {
        using var rig = await OnFiltersAsync();
        var requests = rig.Compositor.Requests.Count;

        rig.Guest("SelectLook", """{"lookId":"naturale-v1"}""").ShouldBeAccepted();
        rig.Guest("SelectLook", """{"lookId":"sepia-v1"}""").ShouldBeRejected(RejectReason.InvalidPayload);
        rig.Guest("SelectLook", """{"lookId":"nope"}""").ShouldBeRejected(RejectReason.InvalidPayload);

        Assert.Equal(requests, rig.Compositor.Requests.Count);
        Assert.Equal("naturale-v1", rig.Filters.SelectedLookId);
    }

    [Fact]
    public async Task A_missing_thumbnail_is_just_missing()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Compositor.FailThumbnails = true;
        rig.ToArrange();
        rig.ToFilters();

        Assert.All(rig.Filters.Looks, look => Assert.Null(look.ThumbnailKey));
        Assert.False(rig.Filters.CompositionFailed);
        Assert.True(rig.Filters.CanFinish);
    }

    [Fact]
    public async Task A_failed_strip_preview_shows_the_error_keeps_the_previous_preview_and_TRY_AGAIN_composes_again()
    {
        using var rig = await OnFiltersAsync();
        var naturale = rig.Filters.StripPreviewKey;
        rig.Compositor.FailStrips = 1;

        rig.Guest("SelectLook", """{"lookId":"monochrome-v1"}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Filters.CompositionFailed, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50)));

        var failed = rig.Filters;
        Assert.False(failed.Composing);
        Assert.False(failed.CanFinish);
        Assert.Equal(naturale, failed.StripPreviewKey);
        Assert.NotNull(failed.Looks[0].ThumbnailKey);
        rig.Guest("FinishStrip").ShouldBeRejected();

        rig.Guest("RetryComposition").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => !rig.Filters.Composing, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50)));
        Assert.False(rig.Filters.CompositionFailed);
        Assert.NotEqual(naturale, rig.Filters.StripPreviewKey);
        Assert.True(rig.Filters.CanFinish);
    }

    [Fact]
    public async Task Another_Look_can_be_chosen_while_a_failure_shows()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Compositor.FailStrips = 1;
        rig.ToArrange();
        rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Filters.CompositionFailed, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50)));
        Assert.Null(rig.Filters.StripPreviewKey);

        rig.Guest("SelectLook", """{"lookId":"monochrome-v1"}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => !rig.Filters.Composing, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50)));
        Assert.False(rig.Filters.CompositionFailed);
        Assert.NotNull(rig.Filters.StripPreviewKey);
    }

    [Fact]
    public async Task RetryComposition_is_refused_when_nothing_failed()
    {
        using var rig = await OnFiltersAsync();

        rig.Guest("RetryComposition").ShouldBeRejected();
    }

    [Fact]
    public async Task Filters_cannot_be_left_for_Photographs_even_while_it_composes()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.Compositor.StripGate = new TaskCompletionSource();
        rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeAccepted();
        Assert.True(rig.Filters.Composing);

        rig.Guest("GoToStep", """{"step":"photographs"}""").ShouldBeRejected();
        rig.Compositor.StripGate.SetResult();
        rig.Compositor.StripGate = null;

        Assert.True(rig.AdvanceUntil(() => !rig.Filters.Composing, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50)));
        Assert.Equal(GuestSurface.Filters, rig.Surface);
        Assert.NotNull(rig.Filters.StripPreviewKey);
    }

    [Fact]
    public async Task FINISH_composes_the_arranged_Captures_in_window_order_commits_the_strip_and_shows_the_Keepsake()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.Guest("BeginMove", """{"index":0}""").ShouldBeAccepted();
        rig.Guest("MoveTo", """{"index":3}""").ShouldBeAccepted();
        var ids = rig.Photographs.Windows.Select(window => window.CaptureId!).ToArray();
        rig.ToFilters();
        rig.Guest("SelectLook", """{"lookId":"monochrome-v1"}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => !rig.Filters.Composing, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50)));

        rig.Guest("FinishStrip").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50)));

        var detail = await rig.OnlyCycleAsync();
        Assert.Equal(GuestCycleEndReason.Completed, detail.Cycle.EndReason);
        var strip = detail.PhotoStrip!;
        Assert.Equal(ids, strip.Arrangement);
        Assert.Equal(("signature-strip", "monochrome-v1", 600, 1800), (strip.TemplateId, strip.LookId, strip.Width, strip.Height));
        var final = rig.Compositor.Requests[^1];
        Assert.Equal("monochrome-v1", final.LookId);

        var keepsake = rig.View.Keepsake!;
        Assert.Equal($"https://media.fotohavn/events/{ActiveRigIds.Event}/guest-cycles/{detail.Cycle.Id}/photo-strip.png?v={strip.Sha256[..8]}", keepsake.StripUrl);
        Assert.Equal((600, 1800, false), (keepsake.Width, keepsake.Height, keepsake.Landscape));
        Assert.Null(rig.View.Filters);
        Assert.Equal(1, rig.OperatorView.RunningEvent!.CompletedGuestCycles);
    }

    [Fact]
    public async Task The_Keepsake_shows_its_timer_and_the_composed_previews_are_gone()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.ToFilters();
        var keys = rig.Filters.Looks.Select(look => look.ThumbnailKey!).Append(rig.Filters.StripPreviewKey!).ToArray();
        rig.Guest("FinishStrip").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50)));

        Assert.Equal(new PhaseTimerOverlay(PhaseTimer.Keepsake, 12, false), rig.View.Overlays.PhaseTimer);
        Assert.Equal(GuestStepState.Current, rig.View.GuestCycle!.Steps[3].State);
        Assert.All(keys, key => Assert.False(Cached(rig, key)));
        Assert.Equal(PhaseTimer.Keepsake, rig.CycleDiagnostics().Timer.Phase);
    }

    [Fact]
    public async Task A_landscape_Template_makes_a_landscape_Filters_print_at_its_native_size()
    {
        using var rig = await OnFiltersAsync(new GuestRigOptions { Tweak = record => record with { DefaultTemplateId = "panorama-four" } });

        var filters = rig.Filters;
        Assert.Equal((1800, 600, true), (filters.Width, filters.Height, filters.Landscape));
    }

    [Fact]
    public async Task A_landscape_Template_makes_a_landscape_Keepsake_at_its_native_size()
    {
        using var rig = await OnKeepsakeAsync(new GuestRigOptions { Tweak = record => record with { DefaultTemplateId = "panorama-four" } });

        var keepsake = rig.View.Keepsake!;
        Assert.Equal((1800, 600, true), (keepsake.Width, keepsake.Height, keepsake.Landscape));
    }

    [Fact]
    public async Task While_the_final_strip_is_made_the_guest_cannot_act_and_no_timer_runs()
    {
        using var rig = await OnFiltersAsync();
        rig.Compositor.StripGate = new TaskCompletionSource();

        rig.Guest("FinishStrip").ShouldBeAccepted();

        Assert.True(rig.Filters.Composing);
        Assert.False(rig.Filters.CanFinish);
        rig.Guest("FinishStrip").ShouldBeRejected();
        rig.Guest("SelectLook", """{"lookId":"monochrome-v1"}""").ShouldBeRejected();
        rig.Guest("GoToStep", """{"step":"photographs"}""").ShouldBeRejected();
        Assert.Null(rig.CycleDiagnostics().Timer.Phase);
        Assert.Null(rig.View.Overlays.PhaseTimer);

        rig.Compositor.StripGate.SetResult();
        rig.Compositor.StripGate = null;
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public async Task A_failed_final_composition_shows_the_error_with_nothing_lost_and_TRY_AGAIN_finishes()
    {
        using var rig = await OnFiltersAsync();
        rig.Compositor.FailStrips = 1;

        rig.Guest("FinishStrip").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Filters.CompositionFailed, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50)));

        Assert.Equal(GuestSurface.Filters, rig.Surface);
        Assert.False(rig.Filters.CanFinish);
        Assert.NotNull(rig.Filters.StripPreviewKey);
        Assert.Equal(PhaseTimer.Filters, rig.CycleDiagnostics().Timer.Phase);
        Assert.Equal(4, (await rig.OnlyCycleAsync()).Captures.Count);

        rig.Guest("RetryComposition").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50)));
        Assert.NotNull((await rig.OnlyCycleAsync()).PhotoStrip);
    }

    [Fact]
    public async Task One_Look_means_continuing_from_Photographs_finishes_the_strip_with_that_Look()
    {
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions { Tweak = record => record with { OfferedLookIds = ["sepia-v1"], DefaultLookId = "sepia-v1" } });
        rig.ToArrange();

        rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50)));

        var detail = await rig.OnlyCycleAsync();
        Assert.Equal(GuestCycleEndReason.Completed, detail.Cycle.EndReason);
        Assert.Equal("sepia-v1", detail.PhotoStrip!.LookId);
        Assert.Equal([GuestStepState.Done, GuestStepState.Done, GuestStepState.Skipped, GuestStepState.Current], rig.View.GuestCycle!.Steps.Select(step => step.State));
    }

    [Fact]
    public async Task With_one_Look_the_guest_stays_on_Photographs_with_nothing_to_press_while_the_strip_is_made()
    {
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions { Tweak = record => record with { OfferedLookIds = ["naturale-v1"] } });
        rig.ToArrange();
        rig.Compositor.StripGate = new TaskCompletionSource();

        rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeAccepted();

        Assert.Equal(GuestSurface.Photographs, rig.Surface);
        Assert.False(rig.Photographs.CanContinue);
        Assert.False(rig.Photographs.CanReview);
        Assert.False(rig.Photographs.CanTakePhotographs);
        rig.Guest("SelectPosition", """{"index":0}""").ShouldBeRejected();
        rig.Compositor.StripGate.SetResult();
        rig.Compositor.StripGate = null;
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public async Task With_one_Look_a_failed_final_composition_shows_Filters_with_TRY_AGAIN()
    {
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions { Tweak = record => record with { OfferedLookIds = ["naturale-v1"] } });
        rig.ToArrange();
        rig.Compositor.FailStrips = 1;

        rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Filters, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50)));
        Assert.True(rig.Filters.CompositionFailed);
        Assert.Single(rig.Filters.Looks);

        rig.Guest("RetryComposition").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50)));
        Assert.Equal(GuestCycleEndReason.Completed, (await rig.OnlyCycleAsync()).Cycle.EndReason);
    }

    [Fact]
    public async Task FINISH_and_the_Filters_commands_are_refused_off_the_Filters_step()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();

        rig.Guest("FinishStrip").ShouldBeRejected();
        rig.Guest("SelectLook", """{"lookId":"monochrome-v1"}""").ShouldBeRejected();
        rig.Guest("RetryComposition").ShouldBeRejected();
    }
}

internal static class ActiveRigIds
{
    public const string Event = "11111111-1111-4111-8111-111111111111";
}
