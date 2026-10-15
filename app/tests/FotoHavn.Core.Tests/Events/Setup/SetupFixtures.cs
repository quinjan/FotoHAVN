using FotoHavn.Core.Camera;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Events.Setup;

/// <summary>
/// The brain-generated presentation fixtures M6 wave B1a adds (Event list and Event setup states). Each entry is a name (the file
/// <c>ui/src/fixtures/presentations/&lt;name&gt;.json</c>, with the prefix <c>operator-event-list-</c> or <c>operator-event-setup-</c>)
/// and a function that drives a real brain to that state and returns the message it sent (see
/// <c>PresentationFixtureTests</c> for the pattern). <c>PresentationFixtureTests</c> merges this table into its own, checks every
/// file against it, and fails on a duplicate name. Wave B1a edits only this file for fixtures. The empty and with-Events lists are
/// <c>PresentationFixtureTests</c>' own.
/// </summary>
internal static class SetupFixtures
{
    private const string GhostId = "44444444-4444-4444-8444-444444444444";
    private static readonly TimeSpan CameraReady = TimeSpan.FromMilliseconds(500);

    public static IReadOnlyDictionary<string, Func<Task<string>>> All { get; } =
        new Dictionary<string, Func<Task<string>>>(StringComparer.Ordinal)
        {
            ["operator-event-list-running"] = ListRunning,
            ["operator-event-list-confirm-delete"] = ListConfirmDelete,
            ["operator-event-setup-new"] = () => Setup(rig => rig.OpenNew()),
            ["operator-event-setup-new-with-errors"] = () => Setup(rig =>
            {
                rig.OpenNew();
                rig.SelectCamera("fake-camera");
                rig.Timer("keepsake", 3);
                rig.Rejects(RejectReason.ValidationFailed, "SaveEvent");
            }),
            ["operator-event-setup-editing"] = () => Setup(
                rig =>
                {
                    rig.OpenEdit(SetupSamples.HarbourId);
                    rig.Advance(CameraReady);
                },
                withPin: true),
            ["operator-event-setup-camera-checking"] = () => Setup(rig =>
            {
                rig.OpenNew();
                rig.SelectCamera("fake-camera");
            }),
            ["operator-event-setup-camera-ready"] = () => Setup(rig =>
            {
                rig.OpenNew();
                rig.SelectCamera("fake-camera");
                rig.Advance(CameraReady);
            }),
            ["operator-event-setup-camera-not-available"] = () => Setup(rig =>
            {
                rig.Fake.Fail(CameraFailureKind.PrivacyDenied);
                rig.OpenNew();
                rig.SelectCamera("fake-camera");
                rig.Advance(CameraReady);
            }),
            ["operator-event-setup-camera-in-use-by-another-app"] = () => Setup(rig =>
            {
                rig.Fake.InUseByAnotherApp("fake-camera");
                rig.OpenNew();
                rig.SelectCamera("fake-camera");
                rig.Advance(CameraReady);
            }),
            ["operator-event-setup-camera-in-use-by-running-event"] = () => Setup(rig =>
            {
                RunHarbour(rig);
                rig.OpenEdit(SetupSamples.WeddingId);
            }),
            ["operator-event-setup-camera-not-checked"] = () => Setup(
                rig =>
                {
                    RunHarbour(rig);
                    rig.OpenNew();
                    rig.SelectCamera("camera-b");
                },
                cameras: [FakeCameraAdapter.DefaultCamera, new AvailableCamera("camera-b", "Second camera")]),
            ["operator-event-setup-camera-missing"] = () => Setup(
                rig => rig.OpenEdit(GhostId),
                extra: Ghost("Ghost camera party") with { CameraDeviceId = "ghost", CameraName = "Integrated Webcam" }),
            ["operator-event-setup-display-missing"] = () => Setup(
                rig =>
                {
                    rig.OpenEdit(GhostId);
                    rig.Advance(CameraReady);
                },
                extra: Ghost("Ghost display party") with { GuestDisplayId = "gone", GuestDisplayName = "DELL P2419H" }),
            ["operator-event-setup-covers-operator-screen"] = () => Setup(rig =>
            {
                rig.OpenNew();
                rig.SelectCamera("fake-camera");
                rig.Advance(CameraReady);
                rig.SelectDisplay("fake-primary");
            }),
            ["operator-event-setup-leave-confirmation"] = () => Setup(rig =>
            {
                rig.OpenEdit(SetupSamples.HarbourId);
                rig.Advance(CameraReady);
                rig.Field("name", "Harbour gala 2026");
                rig.Accepts("LeaveEventSetup");
            }),
            ["operator-event-setup-start-failure-guest-display-missing"] = () => StartFailure(new EventSetupStartFailure(StartFailureCode.GuestDisplayMissing, null)),
            ["operator-event-setup-start-failure-camera-not-eligible"] = () => StartFailure(new EventSetupStartFailure(StartFailureCode.CameraNotEligible, CameraFailureKind.PrivacyDenied)),
            ["operator-event-setup-start-failure-storage-not-writable"] = () => StartFailure(new EventSetupStartFailure(StartFailureCode.StorageNotWritable, null)),
            ["operator-event-setup-start-failure-pin-missing"] = () => StartFailure(new EventSetupStartFailure(StartFailureCode.PinMissing, null)),
            ["operator-event-setup-start-failure-invalid-setup"] = () => StartFailure(new EventSetupStartFailure(StartFailureCode.InvalidSetup, null)),
            ["operator-event-setup-start-failure-event-running"] = () => StartFailure(new EventSetupStartFailure(StartFailureCode.EventRunning, null)),
            ["operator-event-setup-start-failure-guest-screen-not-ready"] = () => StartFailure(new EventSetupStartFailure(StartFailureCode.GuestScreenNotReady, null)),
        };

    private static EventRecord Ghost(string name) => SetupSamples.Event(GhostId, name);

    // A brain on the seeded store (plus an Event, an Event PIN, or other cameras when asked), driven to the state; the JSON is the last presentation it sent.
    private static async Task<string> Setup(Action<SetupRig> drive, EventRecord? extra = null, bool withPin = false, IEnumerable<AvailableCamera>? cameras = null)
    {
        var store = await SetupSamples.SeededStoreAsync();
        if (extra is not null)
        {
            await store.CreateEventAsync(extra, CancellationToken.None);
        }

        if (withPin)
        {
            await store.UpdateEventAsync(await SetupSamples.LoadAsync(store, SetupSamples.HarbourId) with { Notes = "Bring the props." }, CancellationToken.None);
            await store.SetPinAsync(SetupSamples.HarbourId, "2468", CancellationToken.None);
        }

        using var rig = new SetupRig(store, cameras);
        drive(rig);
        return rig.LatestJson;
    }

    // The scaffold's stand-in for a started Event: Harbour gala runs and holds the camera for the Guest Screen.
    private static void RunHarbour(SetupRig rig)
    {
        var harbour = SetupSamples.LoadAsync(rig.Store, SetupSamples.HarbourId).GetAwaiter().GetResult();
        rig.StartRunning(harbour);
        rig.OnBrain(() => rig.Brain.OpenCameraFor("fake-camera", PreviewTarget.Guest));
        rig.Advance(CameraReady);
    }

    private static Task<string> ListRunning() => Setup(RunHarbour);

    private static async Task<string> ListConfirmDelete()
    {
        var store = await SetupSamples.SeededStoreAsync();
        for (var cycle = 0; cycle < 3; cycle++)
        {
            var started = await store.StartGuestCycleAsync(SetupSamples.WeddingId, CancellationToken.None);
            var captures = new List<string>();
            for (var sequence = 1; sequence <= 3; sequence++)
            {
                captures.Add((await store.CommitCaptureAsync(StorageSamples.CaptureCommit(started.Id, sequence), CancellationToken.None)).Id);
            }

            if (cycle < 2)
            {
                await store.CommitPhotoStripAsync(StorageSamples.StripCommit(started.Id, captures), CancellationToken.None);
            }
        }

        using var rig = new SetupRig(store);
        rig.Accepts("DeleteEvent", $$"""{"eventId":"{{SetupSamples.WeddingId}}"}""");
        return rig.LatestJson;
    }

    private static Task<string> StartFailure(EventSetupStartFailure failure) => Setup(rig =>
    {
        rig.OnBrain(() => rig.Brain.OpenEventSetupWithStartFailure(SetupSamples.HarbourId, failure));
        rig.WaitUntil(() => rig.Latest.Surface == OperatorSurface.EventSetup);
        rig.WaitForPreviews();
    });
}
