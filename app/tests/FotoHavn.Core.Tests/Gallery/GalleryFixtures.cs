using FotoHavn.Core.Camera;
using FotoHavn.Core.Events.Active;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Core.Tests.GuestCycles.Orchestration;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Tests.Gallery;

/// <summary>
/// The brain-generated presentation fixtures of the Event Gallery (M9 wave B1): names carry the prefixes <c>operator-gallery-</c>,
/// <c>operator-guest-cycle-detail-</c>, <c>guest-gallery-</c>, <c>guest-strip-detail-</c>, and <c>guest-start-gallery-link</c>, and are the files in
/// <c>ui/src/fixtures/presentations/</c>. <c>PresentationFixtureTests</c> merges this table into its own and checks every file against it; a duplicate
/// name fails. Wave B1 edits only this file for fixtures. Each one is the welcome a freshly connected UI gets from a real brain in that state. Times
/// are instants the store's fake clock wrote, and ids are the in-memory store's sequential ones, so no fixture depends on the machine.
/// </summary>
internal static class GalleryFixtures
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private static readonly CycleSpec Landscape = new(Template: "panorama-four", Look: "sepia-v1", StripWidth: 1800, StripHeight: 600);

    private static readonly CycleSpec[] Mixed =
    [
        new(),
        new(GuestCycleEndReason.StartedOver, Captures: 3),
        Landscape,
        new(GuestCycleEndReason.OperatorExit, Captures: 2),
        new(),
        new(GuestCycleEndReason.AppClosed, Captures: 1),
        new(GuestCycleEndReason.Unrecoverable, Captures: 0),
        new(Look: "monochrome-v1"),
    ];

    public static IReadOnlyDictionary<string, Func<Task<string>>> All { get; } =
        new Dictionary<string, Func<Task<string>>>(StringComparer.Ordinal)
        {
            ["operator-gallery-empty"] = () => OperatorAsync(rig =>
            {
                rig.OpenGallery().ShouldBeAccepted();
                return Task.CompletedTask;
            }),
            ["operator-gallery-partial"] = () => OperatorAsync(async rig =>
            {
                await rig.SeedAsync(new CycleSpec(), new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 3), new CycleSpec(), Landscape, new CycleSpec());
                rig.OpenGallery().ShouldBeAccepted();
            }),
            ["operator-gallery-large"] = () => OperatorAsync(async rig =>
            {
                await rig.SeedAsync([.. Enumerable.Range(0, 45).Select(index => index % 9 == 4 ? new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 2) : new CycleSpec())]);
                rig.OpenGallery().ShouldBeAccepted();
            }),
            ["operator-gallery-loading"] = () => OperatorAsync(async rig =>
            {
                await rig.SeedAsync(new CycleSpec());
                rig.Store.GateNextList();
                rig.OpenGallery().ShouldBeAccepted();
            }),
            ["operator-gallery-load-failed"] = () => OperatorAsync(async rig =>
            {
                await rig.SeedAsync(new CycleSpec());
                rig.Store.FailLists = 1;
                rig.OpenGallery().ShouldBeAccepted();
            }),
            ["operator-gallery-mixed-outcomes"] = () => OperatorAsync(async rig =>
            {
                var seeded = await rig.SeedAsync(Mixed);
                rig.Store.MissingThumbnails.Add(seeded[4].Id);
                rig.OpenGallery().ShouldBeAccepted();
            }),
            ["operator-gallery-running-live"] = () => RunningOperatorAsync(async rig =>
            {
                await rig.SeedAsync(Mixed.Take(4).ToArray());
                rig.OpenGallery().ShouldBeAccepted();
            }),
            ["operator-guest-cycle-detail-completed"] = () => OperatorAsync(async rig =>
            {
                var seeded = await rig.SeedAsync(new CycleSpec(Discarded: 1));
                rig.OpenGallery();
                rig.OpenCycle(seeded[0].Id).ShouldBeAccepted();
            }),
            ["operator-guest-cycle-detail-no-strip"] = () => OperatorAsync(async rig =>
            {
                var seeded = await rig.SeedAsync(new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 3, Discarded: 1));
                rig.OpenGallery();
                rig.OpenCycle(seeded[0].Id).ShouldBeAccepted();
            }),
            ["operator-guest-cycle-detail-delete-confirmation"] = () => OperatorAsync(async rig =>
            {
                var seeded = await rig.SeedAsync(new CycleSpec());
                rig.OpenGallery();
                rig.OpenCycle(seeded[0].Id).ShouldBeAccepted();
                rig.DeleteCycle(seeded[0].Id).ShouldBeAccepted();
            }),
            ["guest-gallery-one"] = () => GuestAsync([new CycleSpec()], rig => rig.OpenGuestGallery().ShouldBeAccepted()),
            ["guest-gallery-four"] = () => GuestAsync([new CycleSpec(), Landscape, new CycleSpec(GuestCycleEndReason.StartedOver, Captures: 2), new CycleSpec(Look: "monochrome-v1"), new CycleSpec()], rig => rig.OpenGuestGallery().ShouldBeAccepted()),
            ["guest-gallery-many"] = () => GuestAsync([.. Enumerable.Range(0, 45).Select(index => index % 11 == 5 ? Landscape : new CycleSpec())], rig => rig.OpenGuestGallery().ShouldBeAccepted()),
            ["guest-gallery-timer-warning"] = () => GuestAsync([new CycleSpec(), Landscape, new CycleSpec(), new CycleSpec()], rig =>
            {
                rig.OpenGuestGallery().ShouldBeAccepted();
                rig.Advance(TimeSpan.FromSeconds(45), Second);
            }),
            ["guest-strip-detail"] = () => GuestAsync([new CycleSpec(), new CycleSpec(), new CycleSpec(Discarded: 1)], rig =>
            {
                rig.OpenGuestGallery().ShouldBeAccepted();
                rig.OpenStrip(rig.StripIds[0]).ShouldBeAccepted();
            }),
            ["guest-strip-detail-timer-warning"] = () => GuestAsync([new CycleSpec(), new CycleSpec(), new CycleSpec(Discarded: 1)], rig =>
            {
                rig.OpenGuestGallery().ShouldBeAccepted();
                rig.OpenStrip(rig.StripIds[0]).ShouldBeAccepted();
                rig.Advance(TimeSpan.FromSeconds(50), Second);
            }),
            ["guest-start-gallery-link"] = () => GuestAsync([new CycleSpec(), new CycleSpec()], _ => { }),
        };

    /// <summary>The Operator's welcome on an Event that isn't running, after the arrangement.</summary>
    private static async Task<string> OperatorAsync(Func<GalleryRig, Task> arrange)
    {
        using var rig = await GalleryRig.CreateAsync();
        await arrange(rig);
        return Welcome(rig, Role.Operator);
    }

    /// <summary>
    /// The Operator's welcome while the Event runs: it is made the running one with a fixed start time (as the M6 and M8 fixtures do), so the panel
    /// never depends on how long the start took.
    /// </summary>
    private static async Task<string> RunningOperatorAsync(Func<GalleryRig, Task> arrange)
    {
        using var rig = await GalleryRig.CreateAsync();
        var record = (await rig.Store.GetEventAsync(ActiveRig.EventId, CancellationToken.None))!;
        rig.Brain.ShowActiveEvent(new ActiveEventState(record, ActiveRig.FixedStart, FakeDisplayAdapter.DefaultGuest, singleScreen: false) { CompletedGuestCycles = 2 });
        rig.Active.Executor.Post(() => rig.Brain.OpenCameraFor("fake-camera", PreviewTarget.Guest));
        rig.Advance(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(100));
        await arrange(rig);
        return Welcome(rig, Role.Operator);
    }

    /// <summary>The Guest's welcome on a running Event whose store holds <paramref name="cycles"/> (only the completed ones are Photo Strips a guest sees).</summary>
    private static async Task<string> GuestAsync(CycleSpec[] cycles, Action<GalleryRig> arrange)
    {
        using var rig = await GalleryRig.CreateAsync();
        await rig.SeedAsync(cycles);
        rig.StartEvent();
        arrange(rig);
        return Welcome(rig, Role.Guest);
    }

    private static string Welcome(GalleryRig rig, Role role)
    {
        var client = rig.Active.Connect(role);
        client.Hello();
        rig.Settle();
        return Assert.Single(client.Raw);
    }
}
