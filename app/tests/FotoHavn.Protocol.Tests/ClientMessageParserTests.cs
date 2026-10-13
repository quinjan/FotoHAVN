using System.Text.Json;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Protocol.Tests;

public sealed class ClientMessageParserTests
{
    private const string Id = ProtocolSamples.Id;

    [Fact]
    public void Reads_a_hello()
    {
        var result = ClientMessageParser.Parse("""{"type":"hello","protocolVersion":1}""", Role.Operator);

        Assert.Equal(new ParsedHello(new HelloMessage(1)), result);
    }

    [Theory]
    [InlineData("""{"type":"hello"}""")]
    [InlineData("""{"type":"hello","protocolVersion":"1"}""")]
    [InlineData("""{"type":"hello","protocolVersion":1.5}""")]
    [InlineData("""{"type":"hello","protocolVersion":1,"role":"operator"}""")]
    public void Rejects_a_malformed_hello(string json)
    {
        AssertInvalid(json, null, EnvelopeError.InvalidEnvelope);
    }

    [Fact]
    public void Reads_a_command_with_its_payload()
    {
        var result = ClientMessageParser.Parse(
            $$$"""{"type":"command","id":"{{{Id}}}","name":"EditEvent","payload":{"eventId":"event-1"}}""",
            Role.Operator);

        var command = Assert.IsType<ParsedCommand>(result);
        Assert.Equal(Id, command.Id);
        Assert.Equal("EditEvent", command.Command.Name);
        Assert.Equal(new EditEventPayload("event-1"), command.Payload);
    }

    [Fact]
    public void Reads_fields_in_any_order()
    {
        var result = ClientMessageParser.Parse(
            $$$"""{"payload":{},"name":"OpenNewEvent","id":"{{{Id}}}","type":"command"}""",
            Role.Operator);

        Assert.IsType<OpenNewEventPayload>(Assert.IsType<ParsedCommand>(result).Payload);
    }

    [Fact]
    public void Reads_RetryAssistance_from_both_roles()
    {
        var json = $$$"""{"type":"command","id":"{{{Id}}}","name":"RetryAssistance","payload":{}}""";

        Assert.IsType<ParsedCommand>(ClientMessageParser.Parse(json, Role.Operator));
        Assert.IsType<ParsedCommand>(ClientMessageParser.Parse(json, Role.Guest));
    }

    [Fact]
    public void Rejects_every_command_from_a_role_that_may_not_send_it()
    {
        foreach (var command in CommandRegistry.All)
        {
            var payload = ProtocolJson.Serialize<object>(ProtocolSamples.Payload(command));
            var json = $$$"""{"type":"command","id":"{{{Id}}}","name":"{{{command.Name}}}","payload":{{{payload}}}}""";
            foreach (var role in Enum.GetValues<Role>())
            {
                var result = ClientMessageParser.Parse(json, role);
                if (command.IsAllowedFrom(role))
                {
                    Assert.Equal(command, Assert.IsType<ParsedCommand>(result).Command);
                }
                else
                {
                    Assert.Equal(new RejectedCommand(Id, command.Name, RejectReason.WrongRole), result);
                }
            }
        }
    }

    [Fact]
    public void Checks_the_role_before_the_payload()
    {
        var result = ClientMessageParser.Parse(
            $$$"""{"type":"command","id":"{{{Id}}}","name":"OpenNewEvent","payload":{"bogus":true}}""",
            Role.Guest);

        Assert.Equal(new RejectedCommand(Id, "OpenNewEvent", RejectReason.WrongRole), result);
    }

    [Fact]
    public void Rejects_an_unknown_command()
    {
        var result = ClientMessageParser.Parse(
            $$$"""{"type":"command","id":"{{{Id}}}","name":"openNewEvent","payload":{}}""",
            Role.Operator);

        Assert.Equal(new RejectedCommand(Id, "openNewEvent", RejectReason.UnknownCommand), result);
    }

    [Theory]
    [InlineData(""","payload":null""")]
    [InlineData(""","payload":[]""")]
    [InlineData(""","payload":"event-1" """)]
    [InlineData("")]
    public void Rejects_a_missing_or_non_object_payload(string payload)
    {
        var result = ClientMessageParser.Parse(
            $$$"""{"type":"command","id":"{{{Id}}}","name":"EditEvent"{{{payload}}}}""",
            Role.Operator);

        Assert.Equal(new RejectedCommand(Id, "EditEvent", RejectReason.InvalidPayload), result);
    }

    [Theory]
    [InlineData("EditEvent", """{}""")]
    [InlineData("EditEvent", """{"eventId":null}""")]
    [InlineData("EditEvent", """{"eventId":7}""")]
    [InlineData("EditEvent", """{"eventId":"event-1","extra":1}""")]
    [InlineData("EditEvent", """{"EventId":"event-1"}""")]
    [InlineData("OpenNewEvent", """{"eventId":"event-1"}""")]
    [InlineData("SetCountdown", """{"seconds":"5"}""")]
    [InlineData("SetCountdown", """{"seconds":5.5}""")]
    [InlineData("SetMirror", """{"enabled":"true"}""")]
    [InlineData("ConfirmLeave", """{"choice":0}""")]
    [InlineData("ConfirmLeave", """{"choice":"throwAway"}""")]
    [InlineData("SetOfferedLooks", """{"lookIds":"naturale"}""")]
    [InlineData("DragPlace", """{"sourceIndex":0,"captureId":"c","revision":1}""")]
    public void Rejects_a_payload_that_does_not_match_its_record(string name, string payload)
    {
        var result = ClientMessageParser.Parse(
            $$$"""{"type":"command","id":"{{{Id}}}","name":"{{{name}}}","payload":{{{payload}}}}""",
            CommandRegistry.TryGet(name, out var command) && command.IsAllowedFrom(Role.Operator) ? Role.Operator : Role.Guest);

        Assert.Equal(new RejectedCommand(Id, name, RejectReason.InvalidPayload), result);
    }

    [Theory]
    [InlineData("""{"type":"command","name":"OpenNewEvent","payload":{}}""", null)]
    [InlineData("""{"type":"command","id":7,"name":"OpenNewEvent","payload":{}}""", null)]
    [InlineData("""{"type":"command","id":"","name":"OpenNewEvent","payload":{}}""", "")]
    [InlineData("""{"type":"command","id":"1","name":"OpenNewEvent","payload":{}}""", "1")]
    [InlineData("""{"type":"command","id":"6f1c2a8e3b4d4e5f9a0b1c2d3e4f5a6b","name":"OpenNewEvent","payload":{}}""", "6f1c2a8e3b4d4e5f9a0b1c2d3e4f5a6b")]
    [InlineData("""{"type":"test","name":"test.getDiagnostics"}""", null)]
    [InlineData("""{"type":"test","id":"not-a-uuid","name":"test.getDiagnostics"}""", "not-a-uuid")]
    public void Rejects_a_missing_or_invalid_id(string json, string? echoedId)
    {
        AssertInvalid(json, echoedId, EnvelopeError.InvalidId);
    }

    [Theory]
    [InlineData("""{"type":"command","id":"ID","payload":{}}""")]
    [InlineData("""{"type":"command","id":"ID","name":7,"payload":{}}""")]
    [InlineData("""{"type":"command","id":"ID","name":"OpenNewEvent","payload":{},"role":"operator"}""")]
    [InlineData("""{"type":"command","id":"ID","id":"ID","name":"OpenNewEvent","payload":{}}""")]
    [InlineData("""{"type":"test","id":"ID"}""")]
    [InlineData("""{"type":"test","id":"ID","name":"getDiagnostics"}""")]
    [InlineData("""{"type":"test","id":"ID","name":7}""")]
    [InlineData("""{"type":"test","id":"ID","name":"test.getDiagnostics","role":"guest"}""")]
    public void Rejects_missing_mistyped_or_unknown_fields(string json)
    {
        AssertInvalid(json.Replace("ID", Id, StringComparison.Ordinal), Id, EnvelopeError.InvalidEnvelope);
    }

    [Theory]
    [InlineData("""{"type":"presentation","id":"ID"}""", Id)]
    [InlineData("""{"type":"Command","id":"ID"}""", Id)]
    [InlineData("""{"type":"welcome"}""", null)]
    [InlineData("""{"type":"test.listCameras","id":"ID"}""", Id)]
    public void Rejects_an_unknown_type(string json, string? echoedId)
    {
        AssertInvalid(json.Replace("ID", Id, StringComparison.Ordinal), echoedId, EnvelopeError.UnknownType);
    }

    [Theory]
    [InlineData("""[]""", null)]
    [InlineData("\"hello\"", null)]
    [InlineData("""{"id":"ID"}""", Id)]
    [InlineData("""{"type":7,"id":"ID"}""", Id)]
    [InlineData("""{"type":null}""", null)]
    public void Rejects_a_message_without_a_string_type(string json, string? echoedId)
    {
        AssertInvalid(json.Replace("ID", Id, StringComparison.Ordinal), echoedId, EnvelopeError.InvalidEnvelope);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("""{"type":"hello",}""")]
    [InlineData("""{"type":"hello"} // comment""")]
    public void Rejects_text_that_is_not_json(string json)
    {
        AssertInvalid(json, null, EnvelopeError.InvalidJson);
    }

    [Fact]
    public void Rejects_json_nested_deeper_than_the_options_allow()
    {
        var depth = ProtocolJson.Options.MaxDepth + 1;
        var json = new string('[', depth) + new string(']', depth);

        AssertInvalid(json, null, EnvelopeError.InvalidJson);
    }

    [Fact]
    public void Accepts_a_message_of_exactly_the_maximum_size()
    {
        var prefix = $$$"""{"type":"command","id":"{{{Id}}}","name":"SubmitPin","payload":{"digits":""" + "\"";
        const string suffix = "\"}}";
        var json = prefix + new string('1', ProtocolLimits.MaxMessageBytes - prefix.Length - suffix.Length) + suffix;

        Assert.Equal(ProtocolLimits.MaxMessageBytes, json.Length);
        Assert.IsType<ParsedCommand>(ClientMessageParser.Parse(json, Role.Guest));
    }

    [Fact]
    public void Rejects_a_message_over_the_maximum_size_in_characters()
    {
        var json = $$$"""{"type":"command","id":"{{{Id}}}","name":"SubmitPin","payload":{"digits":"{{{new string('1', ProtocolLimits.MaxMessageBytes)}}}"}}""";

        AssertInvalid(json, null, EnvelopeError.PayloadTooLarge);
    }

    [Fact]
    public void Rejects_a_message_over_the_maximum_size_in_utf8_bytes()
    {
        // Each "é" is one UTF-16 character but two UTF-8 bytes.
        var json = $$$"""{"type":"command","id":"{{{Id}}}","name":"SubmitPin","payload":{"digits":"{{{new string('é', ProtocolLimits.MaxMessageBytes / 2)}}}"}}""";

        Assert.True(json.Length <= ProtocolLimits.MaxMessageBytes);
        AssertInvalid(json, null, EnvelopeError.PayloadTooLarge);
    }

    [Fact]
    public void Reads_a_test_message_without_args()
    {
        var result = ClientMessageParser.Parse($$$"""{"type":"test","id":"{{{Id}}}","name":"test.getDiagnostics"}""", Role.Guest);

        var test = Assert.IsType<ParsedTest>(result).Message;
        Assert.Equal(Id, test.Id);
        Assert.Equal(TestHookNames.GetDiagnostics, test.Name);
        Assert.Null(test.Args);
    }

    [Fact]
    public void Reads_null_test_args_as_none()
    {
        var result = ClientMessageParser.Parse($$$"""{"type":"test","id":"{{{Id}}}","name":"test.exit","args":null}""", Role.Operator);

        Assert.Null(Assert.IsType<ParsedTest>(result).Message.Args);
    }

    [Fact]
    public void Reads_test_args_that_outlive_the_parsed_document()
    {
        var result = ClientMessageParser.Parse(
            $$$"""{"type":"test","id":"{{{Id}}}","name":"test.openGuestScreen","args":{"displayId":"virtual-guest"}}""",
            Role.Operator);

        var args = Assert.IsType<ParsedTest>(result).Message.Args!.Value;
        Assert.Equal("virtual-guest", args.GetProperty("displayId").GetString());
    }

    [Fact]
    public void Reads_unknown_hook_names_for_the_brain_to_answer()
    {
        var result = ClientMessageParser.Parse($$$"""{"type":"test","id":"{{{Id}}}","name":"test.nope"}""", Role.Operator);

        Assert.Equal("test.nope", Assert.IsType<ParsedTest>(result).Message.Name);
    }

    [Theory]
    [InlineData("6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b", true)]
    [InlineData("6F1C2A8E-3B4D-4E5F-9A0B-1C2D3E4F5A6B", true)]
    [InlineData("{6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b}", false)]
    [InlineData("6f1c2a8e3b4d4e5f9a0b1c2d3e4f5a6b", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Ids_are_hyphenated_uuids(string? id, bool valid)
    {
        Assert.Equal(valid, ClientMessageParser.IsValidId(id));
    }

    [Fact]
    public void Round_trips_what_the_samples_serialize()
    {
        foreach (var command in CommandRegistry.All)
        {
            var json = ProtocolJson.Serialize(new CommandMessage(Id, command.Name, ProtocolJson.ToElement<object>(ProtocolSamples.Payload(command))));
            var role = command.IsAllowedFrom(Role.Operator) ? Role.Operator : Role.Guest;

            var parsed = Assert.IsType<ParsedCommand>(ClientMessageParser.Parse(json, role));
            Assert.Equal(ProtocolJson.Serialize<object>(ProtocolSamples.Payload(command)), ProtocolJson.Serialize<object>(parsed.Payload));
        }

        var hello = ClientMessageParser.Parse(ProtocolJson.Serialize(new HelloMessage(ProtocolVersion.Current)), Role.Guest);
        Assert.Equal(ProtocolVersion.Current, Assert.IsType<ParsedHello>(hello).Message.ProtocolVersion);
    }

    [Fact]
    public void Requires_a_message()
    {
        Assert.Throws<ArgumentNullException>(() => ClientMessageParser.Parse(null!, Role.Operator));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(42L)]
    [InlineData(4_294_967_295L)]
    public void Reads_a_frame_ack_from_either_role(long seq)
    {
        foreach (var role in new[] { Role.Operator, Role.Guest })
        {
            var result = ClientMessageParser.Parse($$$"""{"type":"frameAck","seq":{{{seq}}}}""", role);

            Assert.Equal(new ParsedFrameAck(new FrameAckMessage(seq)), result);
        }
    }

    [Theory]
    [InlineData("""{"type":"frameAck"}""")]
    [InlineData("""{"type":"frameAck","seq":-1}""")]
    [InlineData("""{"type":"frameAck","seq":4294967296}""")]
    [InlineData("""{"type":"frameAck","seq":1.5}""")]
    [InlineData("""{"type":"frameAck","seq":"7"}""")]
    [InlineData("""{"type":"frameAck","seq":null}""")]
    [InlineData("""{"type":"frameAck","seq":7,"id":"6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b"}""")]
    [InlineData("""{"type":"frameAck","seq":7,"seq":7}""")]
    public void Rejects_a_frame_ack_that_is_not_exactly_a_sequence_number(string json)
    {
        AssertInvalid(json, null, EnvelopeError.InvalidEnvelope);
    }

    private static void AssertInvalid(string json, string? echoedId, EnvelopeError reason)
    {
        var result = ClientMessageParser.Parse(json, Role.Operator);

        Assert.Equal(new InvalidMessage(new ErrorMessage(echoedId, reason)), result);
        _ = JsonDocument.Parse(ProtocolJson.Serialize(((InvalidMessage)result).Reply));
    }
}
