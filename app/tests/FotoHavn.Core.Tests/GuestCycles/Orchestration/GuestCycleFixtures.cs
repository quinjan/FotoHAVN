using FotoHavn.Core.Events.Active;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

/// <summary>
/// The brain-generated presentation fixtures of the Guest Cycle (M8 wave B1): names carry the prefixes <c>guest-cycle-</c>,
/// <c>guest-assistance-</c>, and <c>operator-running-assistance</c>, and are the files in <c>ui/src/fixtures/presentations/</c>.
/// <c>PresentationFixtureTests</c> merges this table into its own and checks every file against it; a duplicate name fails. Wave B1 edits
/// only this file for fixtures. Each one is the welcome a freshly connected UI gets from a real brain in that state.
/// </summary>
internal static class GuestCycleFixtures
{
    private static readonly TimeSpan Fine = TimeSpan.FromMilliseconds(100);

    private static GuestRigOptions OneLook => new() { Tweak = record => record with { OfferedLookIds = ["naturale-v1"] } };

    private static GuestRigOptions Panorama => new() { Tweak = record => record with { DefaultTemplateId = "panorama-four" } };

    public static IReadOnlyDictionary<string, Func<Task<string>>> All { get; } =
        new Dictionary<string, Func<Task<string>>>(StringComparer.Ordinal)
        {
            ["guest-cycle-template"] = () => GuestAsync(rig => rig.Begin()),
            ["guest-cycle-photographs-camera"] = () => GuestAsync(rig =>
            {
                rig.Begin();
                rig.ToPhotographs();
            }),
            ["guest-cycle-photographs-countdown"] = () => GuestAsync(rig =>
            {
                rig.Begin();
                rig.ToPhotographs();
                rig.Guest("TakePhotographs");
                rig.Advance(TimeSpan.FromSeconds(7.5), Fine);
            }),
            ["guest-cycle-photographs-pausing"] = () => GuestAsync(rig =>
            {
                rig.Begin();
                rig.ToPhotographs();
                rig.Guest("TakePhotographs");
                Assert.True(rig.AdvanceUntil(() => rig.Photographs.Sequence.Pausing, TimeSpan.FromSeconds(30), Fine));
            }),
            ["guest-cycle-photographs-arrange"] = () => GuestAsync(rig => rig.ToArrange()),
            ["guest-cycle-photographs-arrange-landscape"] = () => GuestAsync(rig => rig.ToArrange(), Panorama),
            ["guest-cycle-photographs-arrange-selected"] = () => GuestAsync(rig =>
            {
                rig.ToArrange();
                rig.Guest("SelectPosition", """{"index":1}""");
            }),
            ["guest-cycle-photographs-arrange-moving"] = () => GuestAsync(rig =>
            {
                rig.ToArrange();
                rig.Guest("BeginMove", """{"index":0}""");
            }),
            ["guest-cycle-photographs-retaking"] = () => GuestAsync(rig =>
            {
                rig.ToArrange();
                rig.Guest("BeginRetake", """{"index":2}""");
                Assert.True(rig.AdvanceUntil(() => rig.Photographs.RetakeCandidate is not null, TimeSpan.FromSeconds(30), Fine));
            }),
            ["guest-cycle-filters"] = () => GuestAsync(rig =>
            {
                rig.ToArrange();
                rig.ToFilters();
            }),
            ["guest-cycle-filters-composing"] = () => GuestAsync(rig =>
            {
                rig.ToArrange();
                rig.Compositor.StripGate = new TaskCompletionSource();
                rig.Guest("GoToStep", """{"step":"filters"}""");
                // The waiting gate keeps the brain's own count up, so a settle can end before the Look thumbnails exist on a loaded machine: wait for them.
                Assert.True(rig.SettleUntil(() => rig.Filters.Looks.All(look => look.ThumbnailKey is not null), TimeSpan.FromSeconds(30)));
            }),
            ["guest-cycle-filters-landscape"] = () => GuestAsync(rig =>
            {
                rig.ToArrange();
                rig.ToFilters();
            }, Panorama),
            ["guest-cycle-photographs-camera-one-template"] = () => GuestAsync(
                rig => rig.Begin(),
                new GuestRigOptions { Tweak = record => record with { OfferedTemplateIds = ["signature-strip"], DefaultTemplateId = "signature-strip" } }),
            ["guest-cycle-photographs-arrange-one-look"] = () => GuestAsync(rig => rig.ToArrange(), OneLook),
            ["guest-cycle-photographs-arrange-one-look-composing"] = () => GuestAsync(
                rig =>
                {
                    rig.ToArrange();
                    rig.Compositor.StripGate = new TaskCompletionSource();
                    rig.Guest("GoToStep", """{"step":"filters"}""");
                },
                OneLook),
            ["guest-cycle-filters-composition-failed"] = () => GuestAsync(rig =>
            {
                rig.Compositor.FailStrips = 1;
                rig.ToArrange();
                rig.Guest("GoToStep", """{"step":"filters"}""");
                Assert.True(rig.AdvanceUntil(() => rig.Filters.CompositionFailed, TimeSpan.FromSeconds(5), Fine));
            }),
            ["guest-cycle-keepsake"] = () => GuestAsync(Keepsake),
            ["guest-cycle-keepsake-landscape"] = () => GuestAsync(Keepsake, Panorama),
            ["guest-cycle-timer-low"] = () => GuestAsync(rig =>
            {
                rig.Begin();
                rig.Advance(TimeSpan.FromSeconds(15), Fine);
            }),
            ["guest-assistance-camera"] = () => GuestAsync(rig =>
            {
                rig.Begin();
                rig.Active.Camera.Fail(CameraFailureKind.Failed);
                rig.Settle();
            }),
            ["guest-assistance-storage"] = () => GuestAsync(rig =>
            {
                rig.Store.FailStartCycle = true;
                rig.Guest("BeginGuestCycle");
            }),
            ["guest-assistance-display"] = () => GuestAsync(rig =>
            {
                rig.Begin();
                rig.Active.Displays.Disconnect("fake-guest");
                rig.Settle();
            }),
            ["operator-running-assistance-camera"] = () => OperatorAsync(rig =>
            {
                rig.Begin();
                rig.Active.Camera.Fail(CameraFailureKind.Failed);
                rig.Settle();
            }),
            ["operator-running-assistance-storage"] = () => OperatorAsync(rig =>
            {
                rig.Store.FailStartCycle = true;
                rig.Guest("BeginGuestCycle");
            }),
        };

    private static void Keepsake(GuestCycleRig rig)
    {
        rig.ToArrange();
        rig.ToFilters();
        rig.Guest("FinishStrip");
        Assert.True(rig.AdvanceUntil(() => rig.Surface == GuestSurface.Keepsake, TimeSpan.FromSeconds(10), Fine));
    }

    /// <summary>A real brain with a started Event on the fake camera and display, arranged into a state, then a fresh Guest UI's welcome.</summary>
    private static async Task<string> GuestAsync(Action<GuestCycleRig> arrange, GuestRigOptions? options = null)
    {
        using var rig = await GuestCycleRig.CreateAsync(options);
        arrange(rig);
        var client = rig.Active.Connect(Role.Guest);
        client.Hello();
        rig.Active.Run();
        return Assert.Single(client.Raw);
    }

    /// <summary>
    /// The Operator's welcome: the Event is made the running one with a fixed start time (as M6's fixtures do), so the panel never depends on how
    /// long the start took.
    /// </summary>
    private static async Task<string> OperatorAsync(Action<GuestCycleRig> arrange)
    {
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions { Start = false });
        var record = (await rig.Store.GetEventAsync(ActiveRig.EventId, CancellationToken.None))!;
        rig.Brain.ShowActiveEvent(new ActiveEventState(record, ActiveRig.FixedStart, FakeDisplayAdapter.DefaultGuest, singleScreen: false));
        rig.Active.Executor.Post(() => rig.Brain.OpenCameraFor("fake-camera", PreviewTarget.Guest));
        rig.Advance(TimeSpan.FromSeconds(1), Fine);
        arrange(rig);
        var client = rig.Active.Connect(Role.Operator);
        client.Hello();
        rig.Active.Run();
        return Assert.Single(client.Raw);
    }
}
