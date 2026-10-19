using FotoHavn.Core.Events;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Core.Tests.GuestCycles.Orchestration;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.Gallery;

/// <summary>The guest gallery's Phase Timer (plan section 7, Phase timers): <c>guest_gallery</c>, 60 s by default, never off, always visible from the moment the gallery opens; browsing never resets it and guests can't add time.</summary>
public sealed class GuestGalleryTimerTests
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private static GalleryRigOptions Seconds(int seconds) => new()
    {
        Tweak = record => record with
        {
            PhaseTimers = [.. record.PhaseTimers.Where(setting => setting.Phase != PhaseTimerPhase.GuestGallery), new PhaseTimerSetting(PhaseTimerPhase.GuestGallery, seconds)],
        },
    };

    private static async Task<(GalleryRig Rig, List<SeededCycle> Seeded)> OpenAsync(GalleryRigOptions? options = null, int completed = 3)
    {
        var rig = await GalleryRig.CreateAsync(options ?? new GalleryRigOptions());
        var seeded = await rig.SeedManyAsync(completed);
        rig.StartEvent();
        rig.OpenGuestGallery().ShouldBeAccepted();
        return (rig, seeded);
    }

    private static GuestCycleTimerDiagnostics Timer(GalleryRig rig) => rig.Diagnostics().Gallery!.Guest!.Timer;

    [Fact]
    public async Task The_gallery_starts_a_sixty_second_timer_when_it_opens_and_it_runs_down_with_the_clock()
    {
        var (rig, _) = await OpenAsync();
        using var lifetime = rig;

        Assert.Equal(new GuestCycleTimerDiagnostics(PhaseTimer.GuestGallery, 60_000, false), Timer(rig));
        Assert.Equal(new PhaseTimerOverlay(PhaseTimer.GuestGallery, 60, false), rig.GuestView.Overlays.PhaseTimer);
        rig.Advance(TimeSpan.FromSeconds(25), Second);

        Assert.Equal(new GuestCycleTimerDiagnostics(PhaseTimer.GuestGallery, 35_000, false), Timer(rig));
        Assert.Equal(new PhaseTimerOverlay(PhaseTimer.GuestGallery, 35, false), rig.GuestView.Overlays.PhaseTimer);
    }

    [Fact]
    public async Task The_timer_shows_every_second_of_the_phase_and_the_guest_is_back_on_Start_at_zero()
    {
        var (rig, _) = await OpenAsync();
        using var lifetime = rig;

        var seen = new List<int> { rig.GuestView.Overlays.PhaseTimer!.RemainingSeconds };
        for (var step = 0; step < 61 * 4; step++)
        {
            rig.Advance(TimeSpan.FromMilliseconds(250));
            if (rig.GuestView.Overlays.PhaseTimer is { } timer && seen[^1] != timer.RemainingSeconds)
            {
                Assert.Equal(PhaseTimer.GuestGallery, timer.Phase);
                Assert.False(timer.Paused);
                seen.Add(timer.RemainingSeconds);
            }
        }

        Assert.Equal(Enumerable.Range(1, 60).Reverse(), seen);
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        Assert.Null(rig.GuestView.Overlays.PhaseTimer);
        Assert.True(rig.GuestView.Start!.GuestGalleryLinkVisible);
        Assert.Null(rig.Diagnostics().Gallery);
    }

    [Fact]
    public async Task A_part_second_left_shows_as_a_whole_second()
    {
        var (rig, _) = await OpenAsync();
        using var lifetime = rig;

        rig.Advance(TimeSpan.FromMilliseconds(59_100));

        Assert.Equal(new PhaseTimerOverlay(PhaseTimer.GuestGallery, 1, false), rig.GuestView.Overlays.PhaseTimer);
    }

    [Fact]
    public async Task The_timer_is_shown_on_a_strip_detail_too_and_expiry_there_returns_to_Start()
    {
        var (rig, seeded) = await OpenAsync();
        using var lifetime = rig;
        rig.OpenStrip(seeded[0].Id).ShouldBeAccepted();

        rig.Advance(TimeSpan.FromSeconds(45), Second);

        Assert.Equal(GuestSurface.GuestStripDetail, rig.GuestView.Surface);
        Assert.Equal(new PhaseTimerOverlay(PhaseTimer.GuestGallery, 15, false), rig.GuestView.Overlays.PhaseTimer);
        rig.Advance(TimeSpan.FromSeconds(15), Second);
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
    }

    [Fact]
    public async Task Browsing_never_resets_the_timer()
    {
        var (rig, seeded) = await OpenAsync(completed: 45);
        using var lifetime = rig;
        rig.Advance(TimeSpan.FromSeconds(10), Second);
        rig.OpenStrip(seeded[44].Id);
        rig.Advance(TimeSpan.FromSeconds(10), Second);
        rig.Guest("CloseGuestStrip");
        rig.LoadMoreStrips(rig.Strips.NextCursor!);
        rig.Advance(TimeSpan.FromSeconds(10), Second);
        rig.OpenStrip(seeded[43].Id);
        rig.Guest("CloseGuestStrip");

        Assert.Equal(30_000, Timer(rig).RemainingMs);
        rig.Advance(TimeSpan.FromSeconds(10), Second);
        Assert.Equal(new PhaseTimerOverlay(PhaseTimer.GuestGallery, 20, false), rig.GuestView.Overlays.PhaseTimer);
        rig.Advance(TimeSpan.FromSeconds(30), Second);
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
    }

    [Fact]
    public async Task There_is_no_MORE_TIME_the_command_is_unknown_and_the_time_runs_out_regardless()
    {
        var (rig, _) = await OpenAsync();
        using var lifetime = rig;
        rig.Advance(TimeSpan.FromSeconds(50), Second);

        rig.Guest("MoreTime").ShouldBeRejected(RejectReason.UnknownCommand);

        Assert.Equal(10_000, Timer(rig).RemainingMs);
        rig.Advance(TimeSpan.FromSeconds(10), Second);
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
    }

    [Fact]
    public async Task The_Events_setting_decides_the_time()
    {
        var (rig, _) = await OpenAsync(Seconds(30));
        using var lifetime = rig;

        Assert.Equal(30_000, Timer(rig).RemainingMs);
        rig.Advance(TimeSpan.FromSeconds(10), Second);
        Assert.Equal(new PhaseTimerOverlay(PhaseTimer.GuestGallery, 20, false), rig.GuestView.Overlays.PhaseTimer);
        rig.Advance(TimeSpan.FromSeconds(20), Second);
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
    }

    [Fact]
    public async Task A_saved_time_of_zero_is_never_off_and_falls_back_to_the_default()
    {
        var rig = await GalleryRig.CreateAsync();
        using var lifetime = rig;
        await rig.SeedAsync(new CycleSpec());
        var record = (await rig.Store.GetEventAsync(GalleryRig.EventId, TestContext.Current.CancellationToken))! with
        {
            PhaseTimers = [new PhaseTimerSetting(PhaseTimerPhase.GuestGallery, 0)],
        };
        rig.Brain.ShowActiveEvent(new FotoHavn.Core.Events.Active.ActiveEventState(record, ActiveRig.FixedStart, FotoHavn.Core.Fakes.FakeDisplayAdapter.DefaultGuest, singleScreen: false) { CompletedGuestCycles = 1 });
        rig.Settle();

        rig.OpenGuestGallery().ShouldBeAccepted();

        Assert.Equal(60_000, Timer(rig).RemainingMs);
    }

    [Fact]
    public async Task An_Event_with_no_saved_time_uses_the_default()
    {
        var (rig, _) = await OpenAsync(new GalleryRigOptions { Tweak = record => record with { PhaseTimers = [] } });
        using var lifetime = rig;

        Assert.Equal(60_000, Timer(rig).RemainingMs);
    }

    [Fact]
    public async Task Leaving_the_gallery_stops_its_timer_and_the_next_visit_starts_a_new_one()
    {
        var (rig, _) = await OpenAsync();
        using var lifetime = rig;
        rig.Advance(TimeSpan.FromSeconds(50), Second);

        rig.Guest("BackToStart").ShouldBeAccepted();
        rig.Advance(TimeSpan.FromSeconds(30), Second);
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        rig.OpenGuestGallery().ShouldBeAccepted();

        Assert.Equal(60_000, Timer(rig).RemainingMs);
        rig.Advance(TimeSpan.FromSeconds(59), Second);
        Assert.Equal(GuestSurface.GuestGallery, rig.GuestView.Surface);
    }

    [Fact]
    public async Task The_next_visit_after_the_time_ran_out_starts_a_new_timer()
    {
        var (rig, _) = await OpenAsync();
        using var lifetime = rig;
        rig.Advance(TimeSpan.FromSeconds(60), Second);
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);

        rig.OpenGuestGallery().ShouldBeAccepted();

        Assert.Equal(60_000, Timer(rig).RemainingMs);
    }

    [Fact]
    public async Task The_PIN_dialog_over_the_gallery_does_not_stop_the_timer()
    {
        var (rig, _) = await OpenAsync();
        using var lifetime = rig;
        rig.Active.HoldToExit();
        rig.Settle();
        Assert.NotNull(rig.GuestView.Overlays.PinDialog);

        rig.Advance(TimeSpan.FromSeconds(60), Second);

        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        Assert.NotNull(rig.GuestView.Overlays.PinDialog);
    }

    [Fact]
    public async Task The_timer_survives_a_slow_first_page_because_it_starts_when_the_gallery_opens()
    {
        var rig = await GalleryRig.CreateAsync();
        using var lifetime = rig;
        await rig.SeedAsync(new CycleSpec());
        rig.StartEvent();
        var gate = rig.Store.GateNextList();
        rig.OpenGuestGallery();

        rig.Advance(TimeSpan.FromSeconds(30), Second);
        gate.SetResult();

        Assert.True(rig.SettleUntil(() => !rig.Strips.LoadingMore, TimeSpan.FromSeconds(30)));
        Assert.Equal(30_000, Timer(rig).RemainingMs);
    }
}
