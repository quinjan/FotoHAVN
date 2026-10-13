using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.Brains;

public sealed class CameraSurfaceTests
{
    private static readonly CameraDiagnosticsView Ready = new(
        [new CameraOption("fake-camera", "Fake camera")],
        "fake-camera",
        CameraState.Ready,
        null,
        new CameraFormatView(1280, 720, 30),
        false,
        null);

    [Theory]
    [InlineData(Role.Operator)]
    [InlineData(Role.Guest)]
    public void A_frame_ack_that_reaches_the_brain_changes_nothing(Role role)
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Handshake(role);
        var revision = Revision(harness, client);

        client.Send("""{"type":"frameAck","seq":42}""");
        harness.Run();

        Assert.Empty(client.Raw);
        Assert.Equal(revision, Revision(harness, client));
    }

    [Fact]
    public void A_frame_ack_before_the_handshake_is_ignored_too()
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(Role.Guest);

        client.Send("""{"type":"frameAck","seq":1}""");
        harness.Run();

        Assert.Empty(client.Raw);
    }

    [Fact]
    public void A_malformed_frame_ack_is_an_invalid_envelope()
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(Role.Guest);

        client.Send("""{"type":"frameAck","seq":-1}""");
        harness.Run();

        client.AssertReceived(new ErrorMessage(null, EnvelopeError.InvalidEnvelope));
    }

    [Fact]
    public void The_camera_diagnostics_surface_goes_to_the_operator_only()
    {
        using var harness = new BrainHarness();
        var operatorClient = harness.Connect(Role.Operator);
        operatorClient.Hello();
        harness.Run();
        var welcomeRevision = operatorClient.Single<OperatorWelcomeMessage>().Revision;
        operatorClient.Clear();
        var guest = harness.Handshake(Role.Guest);

        harness.Brain.ShowCameraDiagnostics(Ready);
        harness.Run();

        var message = Assert.IsType<OperatorPresentationMessage>(operatorClient.Single());
        Assert.Equal(welcomeRevision + 1, message.Revision);
        Assert.Equal(OperatorSurface.CameraDiagnostics, message.Presentation.Surface);
        Assert.Equal(ProtocolJson.Serialize(Ready), ProtocolJson.Serialize(message.Presentation.CameraDiagnostics));
        Assert.Null(message.Presentation.EventList);
        Assert.Empty(guest.Raw);
    }

    [Fact]
    public void Hiding_the_camera_diagnostics_returns_to_the_event_list_and_a_fatal_error_wins_over_it()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);
        harness.Brain.ShowCameraDiagnostics(Ready);
        harness.Run();
        client.Clear();

        harness.Brain.ShowCameraDiagnostics(null);
        harness.Run();
        Assert.Equal(OperatorSurface.EventList, Assert.IsType<OperatorPresentationMessage>(client.Single()).Presentation.Surface);
        client.Clear();

        harness.Brain.ShowCameraDiagnostics(Ready);
        harness.Brain.ShowFatal("Stopped.");
        harness.Run();

        Assert.Equal(OperatorSurface.Fatal, Assert.IsType<OperatorPresentationMessage>(client.Messages[^1]).Presentation.Surface);
    }

    [Fact]
    public void Diagnostics_name_the_camera_diagnostics_surface_in_camel_case()
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Handshake(Role.Operator);
        harness.Brain.ShowCameraDiagnostics(Ready);
        harness.Run();
        client.Clear();

        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();
        harness.Run();

        var result = Assert.IsType<TestResultMessage>(client.Single());
        var diagnostics = ProtocolJson.Deserialize<DiagnosticsResult>(result.Result!.Value);
        Assert.Equal("cameraDiagnostics", diagnostics.Presentations[0].Surface);
    }

    private static long Revision(BrainHarness harness, TestClient client)
    {
        client.Clear();
        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();
        harness.Run();
        var result = Assert.IsType<TestResultMessage>(client.Messages[^1]);
        var diagnostics = ProtocolJson.Deserialize<DiagnosticsResult>(result.Result!.Value);
        client.Clear();
        return diagnostics.Presentations.Single(presentation => presentation.Role == client.Role).Revision;
    }
}
