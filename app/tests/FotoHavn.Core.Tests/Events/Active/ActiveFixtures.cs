using FotoHavn.Core.Displays;
using FotoHavn.Core.Events.Active;
using FotoHavn.Core.Fakes;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Tests.Events.Active;

/// <summary>
/// The brain-generated presentation fixtures M6 wave B1b adds (the running Event, the Guest Screen's Start, exit hold, and PIN
/// states). Names carry the prefix <c>operator-running-</c>, <c>operator-guest-screen-</c>, or <c>guest-</c> and are the files in
/// <c>ui/src/fixtures/presentations/</c>. <c>PresentationFixtureTests</c> merges this table into its own and checks every file
/// against it; a duplicate name fails. Wave B1b edits only this file for fixtures. Each one is the welcome a freshly connected UI
/// gets from a real brain in that state, so it never depends on how many updates came before.
/// </summary>
internal static class ActiveFixtures
{
    private const string WrongPin = "0000";

    public static IReadOnlyDictionary<string, Func<Task<string>>> All { get; } =
        new Dictionary<string, Func<Task<string>>>(StringComparer.Ordinal)
        {
            ["operator-running-normal"] = () => OperatorAsync(_ => Task.CompletedTask),
            ["operator-running-display-disconnected"] = () => OperatorAsync(rig =>
            {
                rig.Displays.Disconnect("fake-guest");
                rig.Run();
                return Task.CompletedTask;
            }),
            ["operator-running-camera-not-available"] = () => OperatorAsync(StallTheCamera),
            ["operator-running-single-screen"] = () => OperatorAsync(
                _ => Task.CompletedTask,
                new ActiveRigOptions { WithOtherEvent = true, Tweak = record => record with { GuestDisplayId = "fake-primary", GuestDisplayName = "Fake primary" } },
                FakeDisplayAdapter.DefaultPrimary,
                singleScreen: true),
            ["operator-running-confirm-end"] = () => OperatorAsync(rig =>
            {
                rig.OperatorCommand("EndEvent");
                return Task.CompletedTask;
            }),
            ["operator-guest-screen-active"] = () => OperatorAsync(
                _ => Task.CompletedTask,
                new ActiveRigOptions { Displays = new FakeDisplayAdapter(OnlyDisplay), Tweak = record => record with { GuestDisplayId = OnlyDisplay.Id, GuestDisplayName = OnlyDisplay.Name } },
                OnlyDisplay,
                singleScreen: true),
            ["guest-start-ready"] = () => GuestAsync(_ => Task.CompletedTask),
            ["guest-start-unavailable-camera"] = () => GuestAsync(StallTheCamera),
            ["guest-exit-hold"] = () => GuestAsync(rig =>
            {
                rig.GuestCommand("BeginExitHold");
                return Task.CompletedTask;
            }),
            ["guest-pin-dialog"] = () => GuestAsync(rig =>
            {
                rig.HoldToExit();
                return Task.CompletedTask;
            }),
            ["guest-pin-dialog-wrong"] = () => GuestAsync(rig =>
            {
                rig.HoldToExit();
                rig.SubmitPin(WrongPin);
                rig.SubmitPin(WrongPin);
                return Task.CompletedTask;
            }),
            ["guest-pin-dialog-locked"] = () => GuestAsync(rig =>
            {
                rig.HoldToExit();
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    rig.SubmitPin(WrongPin);
                }

                rig.Advance(TimeSpan.FromSeconds(3));
                return Task.CompletedTask;
            }),
        };

    private static DisplayInfo OnlyDisplay => FakeDisplayAdapter.Create("fake-guest", "Only display", 0, 0, 1920, 1080, isPrimary: true);

    private static Task StallTheCamera(ActiveRig rig)
    {
        rig.Camera.Stall();
        rig.Advance(TimeSpan.FromSeconds(2.5));
        return Task.CompletedTask;
    }

    private static Task<string> OperatorAsync(Func<ActiveRig, Task> arrange, ActiveRigOptions? options = null, DisplayInfo? guestDisplay = null, bool singleScreen = false) =>
        WelcomeAsync(Role.Operator, arrange, options ?? new ActiveRigOptions { WithOtherEvent = true }, guestDisplay, singleScreen);

    private static Task<string> GuestAsync(Func<ActiveRig, Task> arrange) =>
        WelcomeAsync(Role.Guest, arrange, new ActiveRigOptions(), null, singleScreen: false);

    /// <summary>
    /// Puts a real brain in the running state (the Event as saved, started at a fixed time, the camera opened and ready), lets
    /// <paramref name="arrange"/> change it, then connects a fresh UI of <paramref name="role"/> and returns the welcome it received.
    /// </summary>
    private static async Task<string> WelcomeAsync(Role role, Func<ActiveRig, Task> arrange, ActiveRigOptions options, DisplayInfo? guestDisplay, bool singleScreen)
    {
        using var rig = await ActiveRig.CreateAsync(options);
        var record = (await rig.Store.GetEventAsync(ActiveRig.EventId, CancellationToken.None))!;
        rig.Brain.ShowActiveEvent(new ActiveEventState(record, ActiveRig.FixedStart, guestDisplay ?? FakeDisplayAdapter.DefaultGuest, singleScreen));
        rig.Executor.Post(() => rig.Brain.OpenCameraFor("fake-camera", PreviewTarget.Guest));
        rig.Advance(TimeSpan.FromSeconds(1));
        await arrange(rig);

        var client = rig.Connect(role);
        client.Hello();
        rig.Run();
        return Assert.Single(client.Raw);
    }
}
