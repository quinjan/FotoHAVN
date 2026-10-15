using FotoHavn.Core.Events.Active;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Events.Setup;

public sealed class EventSetupStartTests
{
    private static async Task<SetupRig> RigAsync(HookedStore? store = null) =>
        new(store ?? new HookedStore(await SetupSamples.SeededStoreAsync()));

    private static void FillValid(SetupRig rig, bool withPin = true)
    {
        rig.Field("name", "Quay party");
        rig.SelectCamera("fake-camera");
        rig.SelectDisplay("fake-guest");
        if (withPin)
        {
            rig.Pin(SetupSamples.Pin, SetupSamples.Pin);
        }
    }

    private static void StartWith(SetupRig rig, StartEventResult result, List<string>? started = null) =>
        rig.Brain.UseStartEventRunner(eventId =>
        {
            started?.Add(eventId);
            return Task.FromResult(result);
        });

    [Fact]
    public async Task Save_and_start_saves_then_starts_the_saved_Event_and_returns_to_the_list()
    {
        using var rig = await RigAsync();
        var started = new List<string>();
        StartWith(rig, StartEventResult.Started, started);
        rig.OpenNew();
        FillValid(rig);

        rig.Accepts("SaveAndStartEvent");

        Assert.Equal(OperatorSurface.EventList, rig.Latest.Surface);
        var saved = rig.List.Events.Single(row => row.Name == "Quay party");
        Assert.Equal([saved.Id], started);
        Assert.True(await rig.Store.VerifyPinAsync(saved.Id, SetupSamples.Pin, TestContext.Current.CancellationToken));
        Assert.Equal(4, rig.List.Events.Count);
    }

    [Fact]
    public async Task Save_and_start_of_an_Event_with_a_saved_PIN_needs_no_new_one()
    {
        var inner = await SetupSamples.SeededStoreAsync();
        await inner.SetPinAsync(SetupSamples.HarbourId, "2468", CancellationToken.None);
        using var rig = await RigAsync(new HookedStore(inner));
        var started = new List<string>();
        StartWith(rig, StartEventResult.Started, started);
        rig.OpenEdit(SetupSamples.HarbourId);

        rig.Accepts("SaveAndStartEvent");

        Assert.Equal([SetupSamples.HarbourId], started);
        Assert.Equal(OperatorSurface.EventList, rig.Latest.Surface);
    }

    [Fact]
    public async Task Save_and_start_needs_a_PIN_and_saves_nothing_without_one()
    {
        using var rig = await RigAsync();
        var started = new List<string>();
        StartWith(rig, StartEventResult.Started, started);
        rig.OpenNew();
        FillValid(rig, withPin: false);

        rig.Rejects(RejectReason.ValidationFailed, "SaveAndStartEvent");

        Assert.True(rig.Setup.ShowErrors);
        Assert.Contains(rig.Setup.FieldErrors, error => error is { Field: "pin", Code: EventSetupErrorCode.PinRequired });
        Assert.Empty(started);
        Assert.Equal(3, (await rig.Store.ListEventsAsync(TestContext.Current.CancellationToken)).Count);
        Assert.Equal(EventSetupCameraState.Checking, rig.Setup.Camera.State);
    }

    [Fact]
    public async Task Save_and_start_is_rejected_while_an_Event_runs_and_the_form_says_so()
    {
        using var rig = await RigAsync();
        rig.StartRunning(await SetupSamples.LoadAsync(rig.Store, SetupSamples.HarbourId));
        rig.OpenNew();
        FillValid(rig);

        rig.Rejects(RejectReason.EventRunning, "SaveAndStartEvent");

        Assert.False(rig.Setup.SaveAndStartAvailable);
        Assert.Equal(StartDisabledReason.EventRunning, rig.Setup.SaveAndStartDisabledReason);
        Assert.Equal(3, (await rig.Store.ListEventsAsync(TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task Save_and_start_is_available_while_nothing_runs()
    {
        using var rig = await RigAsync();
        rig.OpenNew();

        Assert.True(rig.Setup.SaveAndStartAvailable);
        Assert.Null(rig.Setup.SaveAndStartDisabledReason);
    }

    public static TheoryData<StartFailureCode, CameraFailureKind?> Failures => new()
    {
        { StartFailureCode.EventRunning, null },
        { StartFailureCode.GuestDisplayMissing, null },
        { StartFailureCode.CameraNotEligible, CameraFailureKind.PrivacyDenied },
        { StartFailureCode.StorageNotWritable, null },
        { StartFailureCode.PinMissing, null },
        { StartFailureCode.InvalidSetup, null },
        { StartFailureCode.GuestScreenNotReady, null },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task A_failed_start_keeps_the_saved_draft_open_with_the_reason(StartFailureCode code, CameraFailureKind? cameraReason)
    {
        using var rig = await RigAsync();
        var started = new List<string>();
        StartWith(rig, StartEventResult.Failed(code, cameraReason), started);
        rig.OpenNew();
        FillValid(rig);

        rig.Accepts("SaveAndStartEvent");

        Assert.Equal(OperatorSurface.EventSetup, rig.Latest.Surface);
        var setup = rig.Setup;
        Assert.Equal(new EventSetupStartFailure(code, cameraReason), setup.StartFailure);
        Assert.False(setup.Saving);
        Assert.False(setup.IsNew);
        Assert.False(setup.Dirty);
        Assert.Equal(new EventSetupPinView(true, false), setup.Pin);
        Assert.Equal(started.Single(), setup.EventId);
        Assert.NotNull(await rig.Store.GetEventAsync(setup.EventId!, TestContext.Current.CancellationToken));
        Assert.Equal(EventSetupCameraState.Checking, setup.Camera.State);
    }

    [Fact]
    public async Task Changing_the_draft_clears_the_reason_and_the_next_save_and_start_updates_the_same_Event()
    {
        using var rig = await RigAsync();
        var started = new List<string>();
        StartWith(rig, StartEventResult.Failed(StartFailureCode.GuestDisplayMissing), started);
        rig.OpenNew();
        FillValid(rig);
        rig.Accepts("SaveAndStartEvent");
        var id = rig.Setup.EventId!;

        rig.Field("notes", "Fixed it");
        Assert.Null(rig.Setup.StartFailure);
        StartWith(rig, StartEventResult.Started, started);
        rig.Accepts("SaveAndStartEvent");

        Assert.Equal([id, id], started);
        Assert.Equal(4, rig.List.Events.Count);
        Assert.Equal("Fixed it", (await rig.Store.GetEventAsync(id, TestContext.Current.CancellationToken))!.Notes);
    }

    [Fact]
    public async Task A_start_that_faults_is_an_invalid_setup()
    {
        using var rig = await RigAsync();
        rig.Brain.UseStartEventRunner(_ => Task.FromException<StartEventResult>(new InvalidOperationException("boom")));
        rig.OpenNew();
        FillValid(rig);

        rig.Accepts("SaveAndStartEvent");

        Assert.Equal(new EventSetupStartFailure(StartFailureCode.InvalidSetup, null), rig.Setup.StartFailure);
        Assert.False(rig.Setup.Saving);
    }

    [Fact]
    public async Task A_failed_save_does_not_start_anything()
    {
        var store = new HookedStore(await SetupSamples.SeededStoreAsync());
        using var rig = await RigAsync(store);
        var started = new List<string>();
        StartWith(rig, StartEventResult.Started, started);
        rig.OpenNew();
        FillValid(rig);
        store.Inner.FailNext(StorageErrorKind.DiskFull);

        rig.Accepts("SaveAndStartEvent");

        Assert.Empty(started);
        Assert.Equal(OperatorNoticeCode.SaveFailed, rig.Latest.Overlays.Notice!.Code);
        Assert.Null(rig.Setup.StartFailure);
    }

    [Fact]
    public async Task A_start_result_that_arrives_after_the_surface_was_left_is_dropped()
    {
        using var rig = await RigAsync();
        var gate = new TaskCompletionSource<StartEventResult>();
        rig.Brain.UseStartEventRunner(_ => gate.Task);
        rig.OpenNew();
        FillValid(rig);
        rig.Accepts("SaveAndStartEvent");
        Assert.True(rig.Setup.Saving);
        rig.Brain.ShowOperatorSurface(OperatorSurface.EventList);
        rig.Harness.Run();
        rig.OpenNew();
        rig.Field("name", "Second draft");

        gate.SetResult(StartEventResult.Failed(StartFailureCode.InvalidSetup));
        Thread.Sleep(50);
        rig.Harness.Run();

        Assert.Equal("Second draft", rig.Setup.Fields.Name);
        Assert.Null(rig.Setup.StartFailure);
    }

    [Fact]
    public async Task A_faulted_start_that_arrives_after_the_surface_was_left_is_observed_and_dropped()
    {
        using var rig = await RigAsync();
        var gate = new TaskCompletionSource<StartEventResult>();
        rig.Brain.UseStartEventRunner(_ => gate.Task);
        rig.OpenNew();
        FillValid(rig);
        rig.Accepts("SaveAndStartEvent");
        rig.Brain.ShowOperatorSurface(OperatorSurface.EventList);
        rig.Harness.Run();
        rig.OpenNew();

        gate.SetException(new InvalidOperationException("boom"));
        Thread.Sleep(50);
        rig.Harness.Run();

        Assert.Null(rig.Setup.StartFailure);
    }

    [Fact]
    public async Task A_running_Event_that_started_keeps_the_camera_when_setup_closes()
    {
        using var rig = await RigAsync();
        rig.Brain.UseStartEventRunner(eventId =>
        {
            // What Start Event does on success: the Event holds the camera and becomes the running Event.
            var record = rig.Store.GetEventAsync(eventId, CancellationToken.None).GetAwaiter().GetResult()!;
            rig.Brain.OpenCameraFor("fake-camera", PreviewTarget.None);
            rig.Brain.ShowActiveEvent(new ActiveEventState(record, rig.Clock.UtcNow, FotoHavn.Core.Fakes.FakeDisplayAdapter.DefaultGuest, false));
            return Task.FromResult(StartEventResult.Started);
        });
        rig.OpenNew();
        FillValid(rig);
        rig.Advance(TimeSpan.FromMilliseconds(500));

        rig.Accepts("SaveAndStartEvent");

        Assert.Equal(OperatorSurface.EventList, rig.Latest.Surface);
        Assert.NotNull(rig.Latest.RunningEvent);
        Assert.Equal("fake-camera", rig.Brain.CameraSlotDeviceId);
        Assert.Equal(CameraState.Checking, rig.Brain.CameraSlotState);
    }
}
