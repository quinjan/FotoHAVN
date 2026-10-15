using FotoHavn.Core.Camera;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;
using Microsoft.Extensions.Logging;
using static FotoHavn.Core.Tests.Support.TestDisplays;

namespace FotoHavn.Core.Tests.Brains;

public sealed class HookTests
{
    private const string TwoVirtualDisplays =
        """{"displays":[{"id":"virtual-operator","name":"Virtual operator","bounds":{"x":0,"y":0,"width":960,"height":1080},"isPrimary":true},{"id":"virtual-guest","name":"Virtual guest","bounds":{"x":960,"y":0,"width":960,"height":1080},"isPrimary":false,"scale":1.25}]}""";

    public static TheoryData<string, string?> EveryHookCall =>
    [
        .. TestHookRegistry.All.Select(hook => (hook.Name, (string?)null)),
        ("test.noSuchHook", null),
        (TestHookNames.UseVirtualDisplays, TwoVirtualDisplays),
        (TestHookNames.OpenGuestScreen, """{"displayId":"fake-guest"}"""),
        (TestHookNames.Exit, "[1,2]"),
    ];

#if !E2E_HOOKS
    [Theory]
    [MemberData(nameof(EveryHookCall))]
    public void Without_E2E_hooks_every_test_message_is_hooks_unavailable(string name, string? args)
    {
        var executor = new ManualBrainExecutor();
        var host = new RecordingHostServices("fake-primary");
        using var brain = new Brain(TestDependencies.Create(executor, new FakeClock(), FakeDisplayAdapter.CreateDefault(), new FakeCameraAdapter(new FakeClock()), host, new InMemoryEventStore()));
        var received = new List<string>();
        var connection = brain.Connect(Role.Guest, received.Add);
        var client = new TestClient(Role.Guest) { Connection = connection };

        var id = client.Test(name, args);
        executor.RunPending();

        Assert.Equal(
            ProtocolJson.Serialize(TestResultMessage.Failure(id, TestHookError.HooksUnavailable)),
            Assert.Single(received));
        Assert.Empty(host.Calls);
    }
#endif

    [Fact]
    public void Hooks_work_without_a_handshake_and_answer_the_caller_only()
    {
        using var harness = BrainHarness.WithHooks();
        var caller = harness.Connect(Role.Guest);
        var other = harness.Handshake(Role.Operator);

        var id = caller.Test(TestHookNames.CloseGuestScreen);
        harness.Run();

        caller.AssertReceived(TestResultMessage.Success(id, EmptyHookResult.Instance));
        Assert.Empty(other.Raw);
    }

    [Fact]
    public void Unknown_hooks_and_invalid_args_are_rejected()
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);

        var unknown = client.Test("test.noSuchHook");
        var argsForNone = client.Test(TestHookNames.GetDiagnostics, """{"verbose":true}""");
        var missingArgs = client.Test(TestHookNames.OpenGuestScreen);
        var wrongArgs = client.Test(TestHookNames.SetWindowBounds, """{"role":"operator","width":"wide","height":720}""");
        harness.Run();

        Assert.Equal(
            [
                TestResultMessage.Failure(unknown, TestHookError.UnknownHook),
                TestResultMessage.Failure(argsForNone, TestHookError.InvalidArgs),
                TestResultMessage.Failure(missingArgs, TestHookError.InvalidArgs),
                TestResultMessage.Failure(wrongArgs, TestHookError.InvalidArgs),
            ],
            client.Messages);
    }

    [Fact]
    public void Diagnostics_report_the_brain_state_and_the_host_windows()
    {
        using var harness = BrainHarness.WithHooks();
        var window = new TestWindow(Role.Operator, new PixelRect(0, 0, 1920, 1040), "fake-primary");
        harness.HostHooks!.Windows = () => Task.FromResult<IReadOnlyList<TestWindow>>([window]);
        var guest = harness.Handshake(Role.Guest);
        harness.Connect(Role.Operator);
        var rejected = guest.Command("OpenNewEvent");
        guest.Test(TestHookNames.OpenGuestScreen, """{"displayId":"fake-guest"}""");
        harness.Brain.ShowFatal("Stopped.");
        guest.Clear();

        var id = guest.Test(TestHookNames.GetDiagnostics);
        harness.Run();
        harness.Run();

        var result = Assert.IsType<TestResultMessage>(guest.Messages[^1]);
        Assert.Equal(id, result.Id);
        var diagnostics = ProtocolJson.Deserialize<DiagnosticsResult>(result.Result!.Value);
        Assert.Equal(ProtocolVersion.Current, diagnostics.ProtocolVersion);
        Assert.Equal([Role.Guest], diagnostics.ConnectedRoles);
        Assert.Equal(
            [new PresentationDiagnostics(Role.Operator, 3, "fatal"), new PresentationDiagnostics(Role.Guest, 2, "fatal")],
            diagnostics.Presentations);
        Assert.Equal(new CommandResultDiagnostics(rejected, "OpenNewEvent", Role.Guest, CommandStatus.Rejected, RejectReason.WrongRole), Assert.Single(diagnostics.LastCommandResults));
        Assert.Equal([window], diagnostics.Windows);
        Assert.False(diagnostics.Displays.Virtual);
        Assert.Equal(["fake-primary", "fake-guest"], diagnostics.Displays.Displays.Select(display => display.Id));
        Assert.True(diagnostics.GuestScreenOpen);
    }

    [Fact]
    public void Diagnostics_list_both_roles_operator_first_and_surfaces_by_their_wire_names()
    {
        using var harness = BrainHarness.WithHooks();
        harness.Handshake(Role.Guest);
        var client = harness.Handshake(Role.Operator);

        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();

        var diagnostics = Diagnostics(client);
        Assert.Equal([Role.Operator, Role.Guest], diagnostics.ConnectedRoles);
        Assert.Equal(["eventList", "start"], diagnostics.Presentations.Select(presentation => presentation.Surface));
        Assert.Equal([2L, 1L], diagnostics.Presentations.Select(presentation => presentation.Revision));
        Assert.False(diagnostics.GuestScreenOpen);
    }

    [Fact]
    public void Diagnostics_keep_the_last_20_command_results_oldest_first()
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Handshake(Role.Operator);
        var ids = Enumerable.Range(0, 25).Select(_ => client.Command("CloseGallery")).ToList();

        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();

        var results = Diagnostics(client).LastCommandResults;
        Assert.Equal(ids.Skip(5), results.Select(result => result.Id));
        Assert.All(results, result => Assert.Equal(new CommandResultDiagnostics(result.Id, "CloseGallery", Role.Operator, CommandStatus.Rejected, RejectReason.NotAvailable), result));
    }

    [Fact]
    public void Diagnostics_without_host_hooks_list_no_windows()
    {
        using var harness = new BrainHarness(hooks: true);
        var client = harness.Connect(Role.Operator);

        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();

        Assert.Empty(Diagnostics(client).Windows);
    }

    [Fact]
    public void Virtual_displays_are_mapped_onto_the_real_primary_display()
    {
        var primary = Display("real-primary", x: -1920, y: 200, primary: true, scale: 1.5);
        using var harness = BrainHarness.WithHooks(new FakeDisplayAdapter(Display("real-other", x: 0), primary));
        var client = harness.Connect(Role.Operator);

        var id = client.Test(TestHookNames.UseVirtualDisplays, TwoVirtualDisplays);
        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();

        var expected = new[]
        {
            new TestDisplay("virtual-operator", "Virtual operator", new PixelRect(-1920, 200, 960, 1080), new PixelRect(-1920, 200, 960, 1080), 1.5, true),
            new TestDisplay("virtual-guest", "Virtual guest", new PixelRect(-960, 200, 960, 1080), new PixelRect(-960, 200, 960, 1080), 1.25, false),
        };
        var result = Assert.IsType<TestResultMessage>(client.Messages[0]);
        Assert.Equal(id, result.Id);
        Assert.Equal(expected, ProtocolJson.Deserialize<UseVirtualDisplaysResult>(result.Result!.Value).Displays);
        var diagnostics = Diagnostics(client);
        Assert.True(diagnostics.Displays.Virtual);
        Assert.Equal(expected, diagnostics.Displays.Displays);
    }

    [Theory]
    [InlineData("""{"displays":[]}""")]
    [InlineData("""{"displays":[null]}""")]
    [InlineData("""{"displays":[{"id":"","name":"A","bounds":{"x":0,"y":0,"width":10,"height":10},"isPrimary":true}]}""")]
    [InlineData("""{"displays":[{"id":"a","name":"A","bounds":{"x":0,"y":0,"width":10,"height":10},"isPrimary":true},{"id":"a","name":"B","bounds":{"x":10,"y":0,"width":10,"height":10},"isPrimary":false}]}""")]
    [InlineData("""{"displays":[{"id":"a","name":"A","bounds":{"x":0,"y":0,"width":10,"height":10},"isPrimary":false}]}""")]
    [InlineData("""{"displays":[{"id":"a","name":"A","bounds":{"x":0,"y":0,"width":10,"height":10},"isPrimary":true},{"id":"b","name":"B","bounds":{"x":10,"y":0,"width":10,"height":10},"isPrimary":true}]}""")]
    [InlineData("""{"displays":[{"id":"a","name":"A","bounds":{"x":-1,"y":0,"width":10,"height":10},"isPrimary":true}]}""")]
    [InlineData("""{"displays":[{"id":"a","name":"A","bounds":{"x":0,"y":-1,"width":10,"height":10},"isPrimary":true}]}""")]
    [InlineData("""{"displays":[{"id":"a","name":"A","bounds":{"x":0,"y":0,"width":0,"height":10},"isPrimary":true}]}""")]
    [InlineData("""{"displays":[{"id":"a","name":"A","bounds":{"x":0,"y":0,"width":10,"height":0},"isPrimary":true}]}""")]
    [InlineData("""{"displays":[{"id":"a","name":"A","bounds":{"x":1000,"y":0,"width":921,"height":10},"isPrimary":true}]}""")]
    [InlineData("""{"displays":[{"id":"a","name":"A","bounds":{"x":0,"y":1000,"width":10,"height":81},"isPrimary":true}]}""")]
    [InlineData("""{"displays":[{"id":"a","name":"A","bounds":{"x":2147483647,"y":0,"width":2147483647,"height":10},"isPrimary":true}]}""")]
    [InlineData("""{"displays":[{"id":"a","name":"A","bounds":{"x":0,"y":0,"width":10,"height":10},"isPrimary":true,"scale":0}]}""")]
    [InlineData("""{"displays":[{"id":"a","name":"A","bounds":{"x":0,"y":0,"width":10,"height":10},"isPrimary":true,"scale":-1.25}]}""")]
    [InlineData("""{"displays":[{"id":"a","name":"A","bounds":{"x":0,"y":0,"width":10,"height":10},"isPrimary":true,"bogus":1}]}""")]
    public void Invalid_virtual_displays_are_invalid_args_and_change_nothing(string args)
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);

        var id = client.Test(TestHookNames.UseVirtualDisplays, args);
        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();

        Assert.Equal(TestResultMessage.Failure(id, TestHookError.InvalidArgs), client.Messages[0]);
        Assert.False(Diagnostics(client).Displays.Virtual);
    }

    [Fact]
    public void A_virtual_display_may_fill_the_real_primary_exactly()
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);

        client.Test(TestHookNames.UseVirtualDisplays, """{"displays":[{"id":"a","name":"A","bounds":{"x":0,"y":0,"width":1920,"height":1080},"isPrimary":true,"scale":2}]}""");
        harness.Run();

        Assert.True(Assert.IsType<TestResultMessage>(client.Single()).Ok);
    }

    [Fact]
    public void Virtual_displays_need_a_real_primary_display()
    {
        using var harness = BrainHarness.WithHooks(new FakeDisplayAdapter(Display("not-primary")));
        var client = harness.Connect(Role.Operator);

        var id = client.Test(TestHookNames.UseVirtualDisplays, TwoVirtualDisplays);
        harness.Run();

        Assert.Equal(TestResultMessage.Failure(id, TestHookError.HookFailed), client.Single());
    }

    [Fact]
    public void Real_display_changes_are_ignored_while_displays_are_virtual()
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);
        client.Test(TestHookNames.UseVirtualDisplays, TwoVirtualDisplays);
        client.Test(TestHookNames.OpenGuestScreen, """{"displayId":"virtual-guest"}""");
        harness.Run();

        harness.Displays.Disconnect("fake-guest");
        harness.Displays.Connect(Display("fake-third", x: 3840));
        client.Clear();
        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();

        var diagnostics = Diagnostics(client);
        Assert.Equal(["virtual-operator", "virtual-guest"], diagnostics.Displays.Displays.Select(display => display.Id));
        Assert.True(diagnostics.GuestScreenOpen);
        Assert.Equal(["OpenGuestScreen(virtual-guest)"], harness.Host.Calls);
    }

    [Fact]
    public void Disconnecting_and_connecting_virtual_displays()
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);
        client.Test(TestHookNames.UseVirtualDisplays, TwoVirtualDisplays);
        client.Test(TestHookNames.OpenGuestScreen, """{"displayId":"virtual-guest"}""");
        harness.Run();
        client.Clear();

        var disconnect = client.Test(TestHookNames.DisconnectDisplay, """{"displayId":"virtual-guest"}""");
        var again = client.Test(TestHookNames.DisconnectDisplay, """{"displayId":"virtual-guest"}""");
        var openOnGone = client.Test(TestHookNames.OpenGuestScreen, """{"displayId":"virtual-guest"}""");
        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();

        Assert.Equal(Wire.Json(TestResultMessage.Success(disconnect, EmptyHookResult.Instance)), client.Raw[0]);
        Assert.Equal(TestResultMessage.Failure(again, TestHookError.UnknownDisplay), client.Messages[1]);
        Assert.Equal(TestResultMessage.Failure(openOnGone, TestHookError.UnknownDisplay), client.Messages[2]);
        var diagnostics = Diagnostics(client);
        Assert.Equal(["virtual-operator"], diagnostics.Displays.Displays.Select(display => display.Id));
        Assert.False(diagnostics.GuestScreenOpen);
        Assert.Equal(["OpenGuestScreen(virtual-guest)", "CloseGuestScreen"], harness.Host.Calls);

        client.Clear();
        var connect = client.Test(TestHookNames.ConnectDisplay, """{"displayId":"virtual-guest"}""");
        var connectAgain = client.Test(TestHookNames.ConnectDisplay, """{"displayId":"virtual-guest"}""");
        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();

        Assert.Equal(Wire.Json(TestResultMessage.Success(connect, EmptyHookResult.Instance)), client.Raw[0]);
        Assert.Equal(TestResultMessage.Failure(connectAgain, TestHookError.UnknownDisplay), client.Messages[1]);
        Assert.Equal(["virtual-operator", "virtual-guest"], Diagnostics(client).Displays.Displays.Select(display => display.Id));
    }

    [Fact]
    public void Calling_use_virtual_displays_again_replaces_the_list_and_clears_disconnections()
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);
        client.Test(TestHookNames.UseVirtualDisplays, TwoVirtualDisplays);
        client.Test(TestHookNames.DisconnectDisplay, """{"displayId":"virtual-guest"}""");

        client.Test(TestHookNames.UseVirtualDisplays, TwoVirtualDisplays);
        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();

        Assert.Equal(["virtual-operator", "virtual-guest"], Diagnostics(client).Displays.Displays.Select(display => display.Id));
    }

    [Theory]
    [InlineData(TestHookNames.DisconnectDisplay, "fake-guest")]
    [InlineData(TestHookNames.ConnectDisplay, "fake-guest")]
    [InlineData(TestHookNames.DisconnectDisplay, "nowhere")]
    public void Connect_and_disconnect_need_virtual_displays(string hook, string displayId)
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);
        if (displayId == "nowhere")
        {
            client.Test(TestHookNames.UseVirtualDisplays, TwoVirtualDisplays);
            harness.Run();
            client.Clear();
        }

        var id = client.Test(hook, $$"""{"displayId":"{{displayId}}"}""");
        harness.Run();

        Assert.Equal(TestResultMessage.Failure(id, TestHookError.UnknownDisplay), client.Single());
    }

    [Fact]
    public void Opening_the_Guest_Screen_asks_the_host_with_the_display_from_the_current_list()
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);
        client.Test(TestHookNames.UseVirtualDisplays, TwoVirtualDisplays);

        var unknown = client.Test(TestHookNames.OpenGuestScreen, """{"displayId":"fake-guest"}""");
        var opened = client.Test(TestHookNames.OpenGuestScreen, """{"displayId":"virtual-guest"}""");
        var closed = client.Test(TestHookNames.CloseGuestScreen);
        harness.Run();

        Assert.Equal(TestResultMessage.Failure(unknown, TestHookError.UnknownDisplay), client.Messages[1]);
        Assert.Equal(Wire.Json(TestResultMessage.Success(opened, EmptyHookResult.Instance)), client.Raw[2]);
        Assert.Equal(Wire.Json(TestResultMessage.Success(closed, EmptyHookResult.Instance)), client.Raw[3]);
        var display = Assert.Single(harness.Host.OpenedGuestScreens);
        Assert.Equal(new DisplayRect(960, 0, 960, 1080), display.Bounds);
        Assert.Equal(1.25, display.Scale);
        Assert.Equal(["OpenGuestScreen(virtual-guest)", "CloseGuestScreen"], harness.Host.Calls);
    }

    [Theory]
    [InlineData("""{"displays":[{"id":"only","name":"Only","bounds":{"x":0,"y":0,"width":960,"height":1080},"isPrimary":true}]}""", "only", null, true)]
    [InlineData(TwoVirtualDisplays, "virtual-guest", "fake-guest", false)]
    [InlineData(TwoVirtualDisplays, "virtual-operator", "fake-guest", true)]
    public void Single_screen_detection_uses_the_virtual_primary_while_displays_are_virtual(string displays, string guestDisplayId, string? hostOperatorDisplayId, bool singleScreen)
    {
        using var harness = BrainHarness.WithHooks();
        harness.Host.OperatorDisplayId = hostOperatorDisplayId;
        var client = harness.Connect(Role.Operator);

        client.Test(TestHookNames.UseVirtualDisplays, displays);
        client.Test(TestHookNames.OpenGuestScreen, $$"""{"displayId":"{{guestDisplayId}}"}""");
        harness.Run();

        Assert.Contains(harness.Logger.Entries, entry => entry.Message.EndsWith($"(single screen: {singleScreen}).", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("fake-primary", "fake-guest", false)]
    [InlineData("fake-guest", "fake-guest", true)]
    [InlineData(null, "fake-primary", true)]
    public void Single_screen_detection_uses_the_operator_window_display_with_real_displays(string? operatorDisplayId, string guestDisplayId, bool singleScreen)
    {
        using var harness = BrainHarness.WithHooks();
        harness.Host.OperatorDisplayId = operatorDisplayId;
        var client = harness.Connect(Role.Operator);

        client.Test(TestHookNames.OpenGuestScreen, $$"""{"displayId":"{{guestDisplayId}}"}""");
        harness.Run();

        Assert.Contains(harness.Logger.Entries, entry => entry.Level == LogLevel.Information && entry.Message.EndsWith($"(single screen: {singleScreen}).", StringComparison.Ordinal));
    }

    [Fact]
    public void Set_window_bounds_is_forwarded_to_the_host()
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Guest);

        var id = client.Test(TestHookNames.SetWindowBounds, """{"role":"operator","width":1366,"height":768}""");
        harness.Run();
        harness.Run();

        Assert.Equal(["SetWindowBounds(Operator, 1366, 768)"], harness.HostHooks!.Calls);
        client.AssertReceived(TestResultMessage.Success(id, new TestWindow(Role.Operator, new PixelRect(0, 0, 1366, 768), "fake-primary")));
    }

    [Fact]
    public void List_cameras_comes_from_the_camera_adapter()
    {
        using var harness = BrainHarness.WithHooks();
        harness.Camera.AddCamera(new AvailableCamera("second-camera", "Second camera"));
        var client = harness.Connect(Role.Operator);

        var id = client.Test(TestHookNames.ListCameras);
        harness.Run();
        harness.Run();

        client.AssertReceived(TestResultMessage.Success(id, new ListCamerasResult([new TestCamera("fake-camera", "Fake camera"), new TestCamera("second-camera", "Second camera")])));
        Assert.Empty(harness.HostHooks!.Calls);
    }

    [Fact]
    public void List_cameras_works_without_host_hooks()
    {
        using var harness = new BrainHarness(hooks: true);
        var client = harness.Connect(Role.Operator);

        var id = client.Test(TestHookNames.ListCameras);
        harness.Run();
        harness.Run();

        client.AssertReceived(TestResultMessage.Success(id, new ListCamerasResult([new TestCamera("fake-camera", "Fake camera")])));
    }

    [Fact]
    public void A_failing_camera_list_is_a_hook_failure()
    {
        var cameras = new FaultingCameras();
        var executor = new ManualBrainExecutor();
        using var brain = new Brain(TestDependencies.Create(executor, new FakeClock(), FakeDisplayAdapter.CreateDefault(), cameras, new RecordingHostServices("fake-primary"), new InMemoryEventStore()), hooksEnabled: true, hostHooks: null);
        var client = new TestClient(Role.Operator);
        client.Connection = brain.Connect(Role.Operator, client.Receive);

        cameras.Failure = () => throw new InvalidOperationException("No cameras.");
        var thrown = client.Test(TestHookNames.ListCameras);
        executor.RunPending();
        cameras.Failure = () => Task.FromException<IReadOnlyList<AvailableCamera>>(new InvalidOperationException("No cameras."));
        var faulted = client.Test(TestHookNames.ListCameras);
        executor.RunPending();
        executor.RunPending();

        Assert.Equal(
            [TestResultMessage.Failure(thrown, TestHookError.HookFailed), TestResultMessage.Failure(faulted, TestHookError.HookFailed)],
            client.Messages);
    }

    [Fact]
    public void Camera_hooks_reject_arguments_that_do_not_match()
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);

        var unknownKind = client.Test(TestHookNames.InjectCameraFault, """{"kind":"melted"}""");
        var missingId = client.Test(TestHookNames.SelectDiagnosticsCamera);
        harness.Run();

        Assert.Equal(
            [TestResultMessage.Failure(unknownKind, TestHookError.InvalidArgs), TestResultMessage.Failure(missingId, TestHookError.InvalidArgs)],
            client.Messages);
    }

    [Fact]
    public void Diagnostics_report_an_idle_camera_until_the_brain_opens_one()
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);

        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();
        harness.Run();

        Assert.Equal(new CameraDiagnostics(CameraState.Idle, null, null, null, null, null, PreviewTarget.None), Diagnostics(client).Camera);
    }

    [Fact]
    public void Host_hook_failures_become_hook_errors()
    {
        using var harness = BrainHarness.WithHooks();
        var hooks = harness.HostHooks!;
        var client = harness.Connect(Role.Operator);

        hooks.SetBounds = (_, _, _) => Task.FromException<TestWindow>(new HookFailure(TestHookError.NotSupported));
        var notSupported = client.Test(TestHookNames.SetWindowBounds, """{"role":"guest","width":1280,"height":720}""");
        harness.Run();
        harness.Run();
        hooks.SetBounds = (_, _, _) => throw new HookFailure(TestHookError.HookFailed);
        var thrown = client.Test(TestHookNames.SetWindowBounds, """{"role":"guest","width":1280,"height":720}""");
        harness.Run();
        harness.Run();
        hooks.Windows = () => Task.FromCanceled<IReadOnlyList<TestWindow>>(new CancellationToken(canceled: true));
        var canceled = client.Test(TestHookNames.GetDiagnostics);
        harness.Run();
        harness.Run();

        Assert.Equal(
            [
                TestResultMessage.Failure(notSupported, TestHookError.NotSupported),
                TestResultMessage.Failure(thrown, TestHookError.HookFailed),
                TestResultMessage.Failure(canceled, TestHookError.HookFailed),
            ],
            client.Messages);
    }

    [Fact]
    public void Crash_ui_reports_whether_the_host_has_a_window_with_the_role()
    {
        using var harness = BrainHarness.WithHooks();
        var hooks = harness.HostHooks!;
        var client = harness.Connect(Role.Operator);

        var crashed = client.Test(TestHookNames.CrashUi, """{"role":"guest"}""");
        harness.Run();
        hooks.CrashUi = _ => false;
        var noWindow = client.Test(TestHookNames.CrashUi, """{"role":"guest"}""");
        harness.Run();
        hooks.CrashUi = _ => throw new HookFailure(TestHookError.NotSupported);
        var thrown = client.Test(TestHookNames.CrashUi, """{"role":"operator"}""");
        harness.Run();

        Assert.Equal(["CrashUi(Guest)", "CrashUi(Guest)", "CrashUi(Operator)"], hooks.Calls);
        client.AssertReceived(
            TestResultMessage.Success(crashed, EmptyHookResult.Instance),
            TestResultMessage.Failure(noWindow, TestHookError.HookFailed),
            TestResultMessage.Failure(thrown, TestHookError.NotSupported));
    }

    [Fact]
    public void Exit_replies_before_it_asks_the_host_to_exit()
    {
        using var harness = BrainHarness.WithHooks();
        var hooks = harness.HostHooks!;
        var client = harness.Connect(Role.Operator);
        var repliedBeforeExit = false;
        hooks.Exit = () => repliedBeforeExit = client.Raw.Count == 1;

        var id = client.Test(TestHookNames.Exit);
        harness.Run();

        Assert.True(repliedBeforeExit);
        client.AssertReceived(TestResultMessage.Success(id, EmptyHookResult.Instance));
    }

    [Fact]
    public void A_failing_exit_is_logged()
    {
        using var harness = BrainHarness.WithHooks();
        harness.HostHooks!.Exit = () => throw new InvalidOperationException("Can't exit.");
        var client = harness.Connect(Role.Operator);

        client.Test(TestHookNames.Exit);
        harness.Run();

        Assert.True(Assert.IsType<TestResultMessage>(client.Single()).Ok);
        Assert.Contains(harness.Logger.Entries, entry => entry.Level == LogLevel.Error && entry.Exception is InvalidOperationException);
    }

    [Theory]
    [InlineData(TestHookNames.SetWindowBounds, """{"role":"guest","width":1280,"height":720}""")]
    [InlineData(TestHookNames.CrashUi, """{"role":"guest"}""")]
    [InlineData(TestHookNames.Exit, null)]
    public void Host_hooks_without_host_hooks_are_not_supported(string name, string? args)
    {
        using var harness = new BrainHarness(hooks: true);
        var client = harness.Connect(Role.Operator);

        var id = client.Test(name, args);
        harness.Run();

        Assert.Equal(TestResultMessage.Failure(id, TestHookError.NotSupported), client.Single());
    }

    [Fact]
    public void A_host_hook_answer_after_the_brain_is_disposed_is_dropped()
    {
        using var harness = BrainHarness.WithHooks();
        var windows = new TaskCompletionSource<IReadOnlyList<TestWindow>>();
        harness.HostHooks!.Windows = () => windows.Task;
        var client = harness.Connect(Role.Operator);
        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();

        harness.Dispose();
        windows.SetResult([]);
        harness.Run();

        Assert.Empty(client.Raw);
    }

    private static DiagnosticsResult Diagnostics(TestClient client) =>
        ProtocolJson.Deserialize<DiagnosticsResult>(Assert.IsType<TestResultMessage>(client.Messages[^1]).Result!.Value);

    private sealed class FaultingCameras : ICameraAdapter
    {
        public Func<Task<IReadOnlyList<AvailableCamera>>>? Failure { get; set; }

        public event EventHandler? CamerasChanged
        {
            add { }
            remove { }
        }

        public Task<IReadOnlyList<AvailableCamera>> ListAsync(CancellationToken cancellationToken) =>
            Failure?.Invoke() ?? Task.FromResult<IReadOnlyList<AvailableCamera>>([]);

        public Task<ICameraSession> OpenAsync(string deviceId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
