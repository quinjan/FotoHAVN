using FotoHavn.Core.Events;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Core.Tests.GuestCycles.Orchestration;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.Gallery;

/// <summary>The guest gallery's Phase Timer (plan section 7, Phase timers): <c>guest_gallery</c>, 60 s by default, never off, from the moment the gallery opens; browsing never resets it.</summary>
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

        Assert.Equal(new GuestCycleTimerDiagnostics(PhaseTimer.GuestGallery, 60_000, false, false), Timer(rig));
        rig.Advance(TimeSpan.FromSeconds(25), Second);

        Assert.Equal(new GuestCycleTimerDiagnostics(PhaseTimer.GuestGallery, 35_000, false, false), Timer(rig));
        Assert.Null(rig.GuestView.Overlays.PhaseTimerWarning);
    }

    [Fact]
    public async Task The_warning_shows_in_the_last_twenty_seconds_once_a_second_and_the_guest_is_back_on_Start_at_zero()
    {
        var (rig, _) = await OpenAsync();
        using var lifetime = rig;
        rig.Advance(TimeSpan.FromSeconds(39), TimeSpan.FromMilliseconds(250));
        Assert.Null(rig.GuestView.Overlays.PhaseTimerWarning);

        var seen = new List<int>();
        for (var step = 0; step < 21 * 4; step++)
        {
            rig.Advance(TimeSpan.FromMilliseconds(250));
            if (rig.GuestView.Overlays.PhaseTimerWarning is { } warning && (seen.Count == 0 || seen[^1] != warning.RemainingSeconds))
            {
                Assert.Equal(PhaseTimer.GuestGallery, warning.Phase);
                seen.Add(warning.RemainingSeconds);
            }
        }

        Assert.Equal(Enumerable.Range(1, 20).Reverse().Take(19), seen.Take(19));
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
        Assert.Null(rig.GuestView.Overlays.PhaseTimerWarning);
        Assert.True(rig.GuestView.Start!.GuestGalleryLinkVisible);
        Assert.Null(rig.Diagnostics().Gallery);
    }

    [Fact]
    public async Task The_warning_is_shown_on_a_strip_detail_too_and_expiry_there_returns_to_Start()
    {
        var (rig, seeded) = await OpenAsync();
        using var lifetime = rig;
        rig.OpenStrip(seeded[0].Id).ShouldBeAccepted();

        rig.Advance(TimeSpan.FromSeconds(45), Second);

        Assert.Equal(GuestSurface.GuestStripDetail, rig.GuestView.Surface);
        Assert.Equal(new PhaseTimerWarningOverlay(PhaseTimer.GuestGallery, 15), rig.GuestView.Overlays.PhaseTimerWarning);
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
        Assert.Equal(new PhaseTimerWarningOverlay(PhaseTimer.GuestGallery, 20), rig.GuestView.Overlays.PhaseTimerWarning);
        rig.Advance(TimeSpan.FromSeconds(30), Second);
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
    }

    [Fact]
    public async Task More_time_during_the_warning_restarts_the_full_time_and_is_otherwise_not_available()
    {
        var (rig, seeded) = await OpenAsync();
        using var lifetime = rig;
        rig.Advance(TimeSpan.FromSeconds(30), Second);
        rig.Guest("MoreTime").ShouldBeRejected();

        rig.Advance(TimeSpan.FromSeconds(20), Second);
        Assert.NotNull(rig.GuestView.Overlays.PhaseTimerWarning);
        rig.Guest("MoreTime").ShouldBeAccepted();

        Assert.Null(rig.GuestView.Overlays.PhaseTimerWarning);
        Assert.Equal(new GuestCycleTimerDiagnostics(PhaseTimer.GuestGallery, 60_000, false, false), Timer(rig));
        rig.Advance(TimeSpan.FromSeconds(55), Second);
        Assert.Equal(GuestSurface.GuestGallery, rig.GuestView.Surface);
        Assert.NotNull(rig.GuestView.Overlays.PhaseTimerWarning);

        rig.OpenStrip(seeded[0].Id);
        rig.Guest("MoreTime").ShouldBeAccepted();
        Assert.Equal(60_000, Timer(rig).RemainingMs);
        Assert.Equal(GuestSurface.GuestStripDetail, rig.GuestView.Surface);
        rig.Advance(TimeSpan.FromSeconds(60), Second);
        Assert.Equal(GuestSurface.Start, rig.GuestView.Surface);
    }

    [Fact]
    public async Task The_Events_setting_decides_the_time()
    {
        var (rig, _) = await OpenAsync(Seconds(30));
        using var lifetime = rig;

        Assert.Equal(30_000, Timer(rig).RemainingMs);
        rig.Advance(TimeSpan.FromSeconds(10), Second);
        Assert.Equal(new PhaseTimerWarningOverlay(PhaseTimer.GuestGallery, 20), rig.GuestView.Overlays.PhaseTimerWarning);
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
        rig.Settle();

        Assert.Equal(30_000, Timer(rig).RemainingMs);
        Assert.False(rig.Strips.LoadingMore);
    }
}
