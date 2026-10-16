using FotoHavn.Catalog;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

/// <summary>
/// The progress property (plan section 7, Phase timers, Outcome; Q23): with every Phase Timer on and nobody touching anything after STEP
/// INSIDE, a cycle runs through to a committed Photo Strip and returns to Start, recorded exactly like one the guest finished (D28); and the
/// brain-level safety property: no Capture is lost across failures and Retry.
/// </summary>
public sealed class GuestCycleProgressTests
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);
    private static readonly CatalogData Catalog = new();

    private static int Pick(Random random, params int[] values) => values[random.Next(values.Length)];

    private static (GuestRigOptions Options, string TemplateId, string LookId, TimeSpan Budget) RandomEvent(Random random)
    {
        string[][] templateSets = [["signature-strip"], ["butter-gingham"], ["signature-strip", "panorama-four"], ["butter-gingham", "polka-keepsake"], ["panorama-four", "signature-strip", "butter-gingham"]];
        string[][] lookSets = [["naturale-v1"], ["monochrome-v1", "naturale-v1", "sepia-v1"], ["classic-v1", "naturale-v1"]];
        var templates = templateSets[random.Next(templateSets.Length)];
        var looks = lookSets[random.Next(lookSets.Length)];
        var template = templates[random.Next(templates.Length)];
        var look = looks[random.Next(looks.Length)];
        var countdown = Pick(random, 3, 5, 10);
        var template30 = Pick(random, 20, 30, 60);
        var capture = Pick(random, 20, 30, 45);
        var review = Pick(random, 20, 45, 90);
        var filters = Pick(random, 20, 30, 120);
        var keepsake = Pick(random, 5, 12, 30);
        var windows = Catalog.FindTemplate(template)!.WindowCount;
        var timers = GuestCycleRig.Timers(template30, capture, review, filters, keepsake);
        var budget = TimeSpan.FromSeconds(template30 + capture + review + filters + keepsake + (windows * (countdown + 3)) + 120);
        var options = new GuestRigOptions
        {
            Tweak = record => timers(record with
            {
                CountdownSeconds = countdown,
                OfferedTemplateIds = templates,
                DefaultTemplateId = template,
                OfferedLookIds = looks,
                DefaultLookId = look,
            }),
        };
        return (options, template, look, budget);
    }

    private static async Task AssertUntouchedCycleCompletesAsync(GuestRigOptions options, string templateId, string lookId, TimeSpan budget)
    {
        using var rig = await GuestCycleRig.CreateAsync(options);
        var template = Catalog.FindTemplate(templateId)!;
        rig.Guest("BeginGuestCycle").ShouldBeAccepted();

        var sawKeepsake = false;
        var finished = rig.AdvanceUntil(
            () =>
            {
                sawKeepsake |= rig.Surface == GuestSurface.Keepsake;
                return sawKeepsake && rig.Surface == GuestSurface.Start;
            },
            budget,
            Second);

        Assert.True(finished, $"The untouched cycle never returned to Start within {budget} (surface {rig.Surface}).");
        var page = await rig.Memory.ListGuestCyclesAsync(ActiveRig.EventId, GuestCycleFilter.All, null, 50, CancellationToken.None);
        var summary = Assert.Single(page.Items);
        Assert.Equal(GuestCycleEndReason.Completed, summary.Cycle.EndReason);
        var detail = await rig.DetailAsync(summary.Cycle.Id);
        var strip = detail.PhotoStrip!;
        Assert.Equal((templateId, lookId, template.Width, template.Height), (strip.TemplateId, strip.LookId, strip.Width, strip.Height));
        Assert.Equal(template.WindowCount, detail.Captures.Count);
        Assert.Equal(detail.Captures.OrderBy(capture => capture.Sequence).Select(capture => capture.Id), strip.Arrangement);
        Assert.Equal(1, rig.OperatorView.RunningEvent!.CompletedGuestCycles);
        Assert.Empty(rig.Store.Ended);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task An_untouched_cycle_with_random_Event_settings_reaches_a_committed_Photo_Strip_and_returns_to_Start(int seed)
    {
        var (options, template, look, budget) = RandomEvent(new Random(seed));

        await AssertUntouchedCycleCompletesAsync(options, template, look, budget);
    }

    [Fact]
    public async Task An_untouched_cycle_with_the_default_settings_reaches_a_committed_Photo_Strip()
    {
        await AssertUntouchedCycleCompletesAsync(new GuestRigOptions(), "signature-strip", "naturale-v1", TimeSpan.FromMinutes(6));
    }

    [Fact]
    public async Task An_untouched_cycle_that_meets_a_failed_composition_still_gets_there()
    {
        var options = new GuestRigOptions();
        using var rig = await GuestCycleRig.CreateAsync(options);
        rig.Compositor.FailStrips = 2;
        rig.Guest("BeginGuestCycle").ShouldBeAccepted();

        var sawKeepsake = false;
        Assert.True(rig.AdvanceUntil(
            () =>
            {
                sawKeepsake |= rig.Surface == GuestSurface.Keepsake;
                return sawKeepsake && rig.Surface == GuestSurface.Start;
            },
            TimeSpan.FromMinutes(8),
            Second));

        Assert.Equal(GuestCycleEndReason.Completed, (await rig.OnlyCycleAsync()).Cycle.EndReason);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    public async Task No_Capture_is_lost_across_failures_and_Retry(int seed)
    {
        var random = new Random(seed);
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions { Tweak = record => record with { CountdownSeconds = 3 } });
        rig.Begin();
        rig.ToPhotographs();
        var faults = 0;
        var elapsed = TimeSpan.Zero;

        while (rig.Surface != GuestSurface.Keepsake && elapsed < TimeSpan.FromMinutes(20))
        {
            if (rig.Surface == GuestSurface.OperatorAssistance)
            {
                Repair(rig);
            }
            else if (rig.Surface == GuestSurface.Photographs && rig.Photographs.CanTakePhotographs)
            {
                rig.Guest("TakePhotographs").ShouldBeAccepted();
            }
            else if (rig.Surface == GuestSurface.Photographs && rig.Photographs.CanContinue)
            {
                rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeAccepted();
            }
            else if (rig.Surface == GuestSurface.Filters && rig.Filters.CanFinish)
            {
                rig.Guest("FinishStrip").ShouldBeAccepted();
            }

            if (faults < 6 && rig.Surface != GuestSurface.OperatorAssistance && random.Next(8) == 0)
            {
                Inject(rig, random.Next(3));
                faults++;
            }

            rig.Advance(Second, TimeSpan.FromMilliseconds(250));
            elapsed += Second;
        }

        Assert.Equal(GuestSurface.Keepsake, rig.Surface);
        var detail = await rig.OnlyCycleAsync();
        Assert.Equal(GuestCycleEndReason.Completed, detail.Cycle.EndReason);
        Assert.Equal(4, detail.Captures.Count);
        Assert.Equal(4, detail.Captures.Select(capture => capture.Id).Distinct().Count());
        Assert.Equal(detail.Captures.OrderBy(capture => capture.Sequence).Select(capture => capture.Id), detail.PhotoStrip!.Arrangement);
    }

    private static void Inject(GuestCycleRig rig, int kind)
    {
        switch (kind)
        {
            case 0:
                rig.Active.Camera.Fail(CameraFailureKind.Failed);
                break;
            case 1:
                rig.Store.FailCaptureCommits = 1;
                break;
            default:
                rig.Active.Displays.Disconnect("fake-guest");
                break;
        }

        rig.Settle();
    }

    private static void Repair(GuestCycleRig rig)
    {
        rig.Active.Camera.Recover();
        rig.Active.Displays.Connect(FakeDisplays.Guest);
        rig.Store.FailCaptureCommits = 0;
        rig.Settle();
        rig.Operator("RetryAssistance");
        rig.Advance(Second, TimeSpan.FromMilliseconds(250));
    }
}

internal static class FakeDisplays
{
    public static Core.Displays.DisplayInfo Guest => Core.Fakes.FakeDisplayAdapter.DefaultGuest;
}
