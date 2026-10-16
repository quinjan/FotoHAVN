using System.Text.Json;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Protocol.Tests;

public sealed class MessageTests
{
    private const string Id = ProtocolSamples.Id;

    [Fact]
    public void Protocol_version_is_nine()
    {
        Assert.Equal(9, ProtocolVersion.Current);
    }

    [Fact]
    public void Messages_are_at_most_256_KiB()
    {
        Assert.Equal(262_144, ProtocolLimits.MaxMessageBytes);
    }

    [Fact]
    public void Envelopes_write_type_then_role_first()
    {
        var json = ProtocolJson.Serialize(new GuestPresentationMessage(7, ProtocolSamples.GuestStart));

        Assert.StartsWith("""{"type":"presentation","role":"guest","revision":7,"presentation":{"surface":"start",""", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_message_reports_its_wire_type()
    {
        var types = ProtocolSamples.Messages.Select(sample => sample.Message.Type).ToHashSet();

        Assert.Equal(
            new[]
            {
                MessageTypes.Hello, MessageTypes.Test, MessageTypes.Welcome, MessageTypes.Presentation, MessageTypes.CommandResult,
                MessageTypes.ProtocolMismatch, MessageTypes.Error, MessageTypes.TestResult, MessageTypes.FrameAck, MessageTypes.PreviewFrame,
            }.Order(StringComparer.Ordinal),
            types.Order(StringComparer.Ordinal));
        Assert.Equal(MessageTypes.Command, CommandMessage.Create(Id, new OpenNewEventPayload()).Type);
    }

    [Fact]
    public void Role_messages_report_their_role()
    {
        Assert.Equal(Role.Operator, new OperatorWelcomeMessage(1, 1, ProtocolSamples.EmptyEventList).Role);
        Assert.Equal(Role.Guest, new GuestWelcomeMessage(1, 1, ProtocolSamples.GuestStart).Role);
        Assert.Equal(Role.Operator, new OperatorPresentationMessage(1, ProtocolSamples.EmptyEventList).Role);
        Assert.Equal(Role.Guest, new GuestPresentationMessage(1, ProtocolSamples.GuestStart).Role);
    }

    [Fact]
    public void Create_names_a_command_from_its_payload()
    {
        var command = CommandMessage.Create(Id, new StartEventPayload("event-9"));

        Assert.Equal("StartEvent", command.Name);
        Assert.Equal("""{"eventId":"event-9"}""", command.Payload.GetRawText());
        Assert.Equal(
            $$$"""{"type":"command","id":"{{{Id}}}","name":"StartEvent","payload":{"eventId":"event-9"}}""",
            ProtocolJson.Serialize(command));
    }

    [Fact]
    public void Command_results_carry_a_reason_only_when_rejected()
    {
        Assert.Equal(new CommandResultMessage(Id, CommandStatus.Accepted, null), CommandResultMessage.Accepted(Id));
        Assert.Equal(new CommandResultMessage(Id, CommandStatus.Rejected, RejectReason.EventRunning), CommandResultMessage.Rejected(Id, RejectReason.EventRunning));
    }

    [Fact]
    public void Test_results_carry_a_result_or_an_error()
    {
        var success = TestResultMessage.Success(Id, new ListCamerasResult([new TestCamera("id", "Camera")]));
        var failure = TestResultMessage.Failure(Id, TestHookError.UnknownHook);

        Assert.True(success.Ok);
        Assert.Null(success.Error);
        Assert.Equal("""{"cameras":[{"id":"id","name":"Camera"}]}""", success.Result!.Value.GetRawText());
        Assert.False(failure.Ok);
        Assert.Null(failure.Result);
        Assert.Equal(TestHookError.UnknownHook, failure.Error);
    }

    [Fact]
    public void Reject_reasons_are_the_closed_wire_set()
    {
        var names = Enum.GetValues<RejectReason>().Select(reason => ProtocolJson.Serialize(reason).Trim('"'));

        Assert.Equal(
            ["unknownCommand", "wrongRole", "invalidPayload", "notAvailable", "eventRunning", "protocolMismatch", "payloadTooLarge", "validationFailed", "pinLocked"],
            names);
    }

    [Fact]
    public void Presentation_overlays_default_to_none()
    {
        Assert.Equal(new OperatorOverlays(null, null, null), OperatorOverlays.None);
        Assert.Equal(new GuestOverlays(null, null, null, null, null), GuestOverlays.None);
    }

    [Fact]
    public void Serialize_requires_a_message()
    {
        Assert.Throws<ArgumentNullException>(() => ProtocolJson.Serialize((ProtocolMessage)null!));
    }
}

public sealed class ServerMessageReaderTests
{
    [Fact]
    public void Reads_every_server_message_back_into_its_record()
    {
        foreach (var (name, message) in ProtocolSamples.Messages.Where(sample => sample.Message is ServerMessage))
        {
            var json = ProtocolJson.Serialize(message);

            var read = ServerMessageReader.Read(json);

            Assert.True(message.GetType() == read.GetType(), name);
            Assert.Equal(json, ProtocolJson.Serialize(read));
        }
    }

    [Theory]
    [InlineData("""[]""")]
    [InlineData("""{"id":"x"}""")]
    [InlineData("""{"type":1}""")]
    [InlineData("""{"type":"hello","protocolVersion":1}""")]
    [InlineData("""{"type":"presentation","revision":1}""")]
    [InlineData("""{"type":"presentation","role":1,"revision":1}""")]
    [InlineData("""{"type":"welcome","role":"admin","revision":1}""")]
    [InlineData("""{"type":"commandResult","id":"x","status":"accepted"}""")]
    [InlineData("""{"type":"commandResult","id":"x","status":"accepted","reason":null,"extra":1}""")]
    [InlineData("""{"type":"error","id":null,"reason":"nope"}""")]
    [InlineData("""null""")]
    public void Throws_for_anything_else(string json)
    {
        Assert.ThrowsAny<JsonException>(() => ServerMessageReader.Read(json));
    }

    [Fact]
    public void Requires_a_message()
    {
        Assert.Throws<ArgumentNullException>(() => ServerMessageReader.Read(null!));
    }
}

public sealed class TestHookRegistryTests
{
    [Fact]
    public void Every_hook_name_is_registered_once_with_the_test_prefix()
    {
        var constants = typeof(TestHookNames).GetFields()
            .Where(field => field.IsLiteral && field.Name != nameof(TestHookNames.Prefix))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        Assert.Equal(constants, TestHookRegistry.All.Select(hook => hook.Name).Order(StringComparer.Ordinal));
        Assert.All(TestHookRegistry.All, hook => Assert.StartsWith(TestHookNames.Prefix, hook.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Brain_and_host_hooks_are_split_as_documented()
    {
        string[] brain =
        [
            TestHookNames.GetDiagnostics, TestHookNames.UseVirtualDisplays, TestHookNames.DisconnectDisplay,
            TestHookNames.ConnectDisplay, TestHookNames.OpenGuestScreen, TestHookNames.CloseGuestScreen,
            TestHookNames.InjectStorageFault, TestHookNames.ClearStorageFault, TestHookNames.ListCameras,
            TestHookNames.OpenCameraDiagnostics, TestHookNames.SelectDiagnosticsCamera, TestHookNames.SetDiagnosticsMirror,
            TestHookNames.CaptureDiagnostics, TestHookNames.InjectCameraFault, TestHookNames.ClearCameraFault,
            TestHookNames.ComposeDiagnostics, TestHookNames.ComposeBlankPreview, TestHookNames.SetClockScale,
        ];

        Assert.All(TestHookRegistry.All, hook => Assert.Equal(brain.Contains(hook.Name) ? TestHookSide.Brain : TestHookSide.Host, hook.Side));
    }

    [Fact]
    public void Reads_camera_hook_arguments_and_rejects_unknown_fault_kinds()
    {
        Assert.True(TestHookRegistry.TryGet(TestHookNames.InjectCameraFault, out var inject));
        Assert.True(TestHookRegistry.TryGet(TestHookNames.SelectDiagnosticsCamera, out var select));
        Assert.True(TestHookRegistry.TryGet(TestHookNames.SetDiagnosticsMirror, out var mirror));

        Assert.True(TestHookRegistry.TryReadArgs(inject, Element("""{"kind":"exclusiveLost"}"""), out var fault));
        Assert.Equal(new InjectCameraFaultArgs(CameraFaultKind.ExclusiveLost), fault);
        Assert.False(TestHookRegistry.TryReadArgs(inject, Element("""{"kind":"melted"}"""), out _));
        Assert.True(TestHookRegistry.TryReadArgs(select, Element("""{"deviceId":"fake-camera"}"""), out var selected));
        Assert.Equal(new SelectDiagnosticsCameraArgs("fake-camera"), selected);
        Assert.False(TestHookRegistry.TryReadArgs(select, Element("""{}"""), out _));
        Assert.True(TestHookRegistry.TryReadArgs(mirror, Element("""{"mirrored":true}"""), out var mirrored));
        Assert.Equal(new SetDiagnosticsMirrorArgs(true), mirrored);
        Assert.False(TestHookRegistry.TryReadArgs(mirror, Element("""{"mirrored":"yes"}"""), out _));
    }

    [Fact]
    public void Reads_composition_hook_arguments()
    {
        Assert.True(TestHookRegistry.TryGet(TestHookNames.ComposeDiagnostics, out var diagnostics));
        Assert.True(TestHookRegistry.TryGet(TestHookNames.ComposeBlankPreview, out var blank));

        Assert.True(TestHookRegistry.TryReadArgs(diagnostics, Element("""{"templateId":"signature-strip","lookId":"sepia-v1"}"""), out var composed));
        Assert.Equal(new ComposeDiagnosticsArgs("signature-strip", "sepia-v1"), composed);
        Assert.False(TestHookRegistry.TryReadArgs(diagnostics, Element("""{"templateId":"signature-strip"}"""), out _));
        Assert.True(TestHookRegistry.TryReadArgs(blank, Element("""{"templateId":"panorama-four"}"""), out var blanked));
        Assert.Equal(new ComposeBlankPreviewArgs("panorama-four"), blanked);
        Assert.Equal(typeof(ComposePreviewResult), diagnostics.ResultType);
        Assert.Equal(typeof(ComposePreviewResult), blank.ResultType);
    }

    [Fact]
    public void Finds_hooks_by_exact_name()
    {
        Assert.True(TestHookRegistry.TryGet("test.crashUi", out var hook));
        Assert.Equal(typeof(CrashUiArgs), hook.ArgsType);
        Assert.False(TestHookRegistry.TryGet("test.CrashUi", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    public void A_hook_without_arguments_accepts_none(string? args)
    {
        Assert.True(TestHookRegistry.TryGet(TestHookNames.GetDiagnostics, out var hook));

        Assert.True(TestHookRegistry.TryReadArgs(hook, Element(args), out var value));
        Assert.Null(value);
    }

    [Theory]
    [InlineData("""{"role":"guest"}""")]
    [InlineData("[]")]
    [InlineData("1")]
    public void A_hook_without_arguments_rejects_some(string args)
    {
        Assert.True(TestHookRegistry.TryGet(TestHookNames.Exit, out var hook));

        Assert.False(TestHookRegistry.TryReadArgs(hook, Element(args), out _));
    }

    [Fact]
    public void Reads_arguments_into_their_record()
    {
        Assert.True(TestHookRegistry.TryGet(TestHookNames.SetWindowBounds, out var hook));

        Assert.True(TestHookRegistry.TryReadArgs(hook, Element("""{"role":"guest","width":1280,"height":720}"""), out var value));
        Assert.Equal(new SetWindowBoundsArgs(Role.Guest, 1280, 720), value);
    }

    [Fact]
    public void Virtual_display_scale_is_optional()
    {
        Assert.True(TestHookRegistry.TryGet(TestHookNames.UseVirtualDisplays, out var hook));

        Assert.True(TestHookRegistry.TryReadArgs(
            hook,
            Element("""{"displays":[{"id":"a","name":"A","bounds":{"x":0,"y":0,"width":10,"height":10},"isPrimary":true}]}"""),
            out var value));
        Assert.Null(Assert.IsType<UseVirtualDisplaysArgs>(value).Displays[0].Scale);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("[]")]
    [InlineData("""{"role":"admin","width":1,"height":1}""")]
    [InlineData("""{"role":"guest","width":1}""")]
    [InlineData("""{"role":"guest","width":1,"height":1,"x":0}""")]
    public void Rejects_arguments_that_do_not_match(string? args)
    {
        Assert.True(TestHookRegistry.TryGet(TestHookNames.SetWindowBounds, out var hook));

        Assert.False(TestHookRegistry.TryReadArgs(hook, Element(args), out var value));
        Assert.Null(value);
    }

    [Fact]
    public void Reads_storage_fault_arguments_and_rejects_unknown_kinds()
    {
        Assert.True(TestHookRegistry.TryGet(TestHookNames.InjectStorageFault, out var hook));

        Assert.True(TestHookRegistry.TryReadArgs(hook, Element("""{"kind":"accessDenied","scope":"nextWrite"}"""), out var value));
        Assert.Equal(new InjectStorageFaultArgs(StorageFaultKind.AccessDenied, StorageFaultScope.NextWrite), value);
        Assert.False(TestHookRegistry.TryReadArgs(hook, Element("""{"kind":"corrupt","scope":"nextWrite"}"""), out _));
        Assert.False(TestHookRegistry.TryReadArgs(hook, Element("""{"kind":"locked"}"""), out _));
    }

    [Fact]
    public void Requires_a_hook()
    {
        Assert.Throws<ArgumentNullException>(() => TestHookRegistry.TryReadArgs(null!, null, out _));
    }

    private static JsonElement? Element(string? json) => json is null ? null : JsonDocument.Parse(json).RootElement.Clone();
}
