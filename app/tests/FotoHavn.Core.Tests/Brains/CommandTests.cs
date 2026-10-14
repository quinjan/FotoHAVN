using System.Text.Json;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;

namespace FotoHavn.Core.Tests.Brains;

public sealed class CommandTests
{
    public static TheoryData<string> OperatorOnlyCommands => Names(CommandAudience.OperatorOnly);

    public static TheoryData<string> GuestOnlyCommands => Names(CommandAudience.GuestOnly);

    public static TheoryData<string, Role> EveryAllowedSender =>
    [
        .. CommandRegistry.All.SelectMany(command => new[] { Role.Operator, Role.Guest }
            .Where(command.IsAllowedFrom)
            .Select(role => (command.Name, role)))
            .Where(sender => sender.Name != "OpenNewEvent"),
    ];

    [Theory]
    [MemberData(nameof(OperatorOnlyCommands))]
    public void Every_operator_only_command_is_rejected_from_the_guest(string name)
    {
        using var harness = new BrainHarness();
        var guest = harness.Handshake(Role.Guest);

        var id = guest.Command(name, SamplePayload(name));
        harness.Run();

        Assert.Equal(CommandResultMessage.Rejected(id, RejectReason.WrongRole), guest.Single());
    }

    [Theory]
    [MemberData(nameof(GuestOnlyCommands))]
    public void Every_guest_only_command_is_rejected_from_the_operator(string name)
    {
        using var harness = new BrainHarness();
        var @operator = harness.Handshake(Role.Operator);

        var id = @operator.Command(name, SamplePayload(name));
        harness.Run();

        Assert.Equal(CommandResultMessage.Rejected(id, RejectReason.WrongRole), @operator.Single());
    }

    [Theory]
    [MemberData(nameof(EveryAllowedSender))]
    public void Every_valid_command_from_an_allowed_role_is_not_available_in_M2(string name, Role role)
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(role);

        var id = client.Command(name, SamplePayload(name));
        harness.Run();

        Assert.Equal(CommandResultMessage.Rejected(id, RejectReason.NotAvailable), client.Single());
    }

    [Fact]
    public void Role_is_checked_before_the_payload()
    {
        using var harness = new BrainHarness();
        var guest = harness.Handshake(Role.Guest);

        var id = guest.Command("EditEvent", """{"eventId":42}""");
        harness.Run();

        Assert.Equal(CommandResultMessage.Rejected(id, RejectReason.WrongRole), guest.Single());
    }

    [Fact]
    public void Unknown_commands_and_invalid_payloads_are_rejected()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);

        var unknown = client.Command("openNewEvent");
        var invalid = client.Command("EditEvent", """{"eventId":"event-1","role":"guest"}""");
        harness.Run();

        Assert.Equal(
            [CommandResultMessage.Rejected(unknown, RejectReason.UnknownCommand), CommandResultMessage.Rejected(invalid, RejectReason.InvalidPayload)],
            client.Messages);
    }

    [Fact]
    public void A_page_cannot_change_its_role()
    {
        using var harness = new BrainHarness();
        var guest = harness.Handshake(Role.Guest);

        guest.Send($$"""{"type":"command","id":"{{Guid.NewGuid()}}","name":"OpenNewEvent","payload":{},"role":"operator"}""");
        guest.Send($$"""{"type":"hello","protocolVersion":{{ProtocolVersion.Current}},"role":"operator"}""");
        var stillGuest = guest.Command("OpenNewEvent", """{"role":"operator"}""");
        guest.Hello();
        harness.Run();

        var messages = guest.Messages;
        Assert.Equal(EnvelopeError.InvalidEnvelope, Assert.IsType<ErrorMessage>(messages[0]).Reason);
        Assert.Equal(new ErrorMessage(null, EnvelopeError.InvalidEnvelope), messages[1]);
        Assert.Equal(CommandResultMessage.Rejected(stillGuest, RejectReason.WrongRole), messages[2]);
        Assert.Equal(Role.Guest, Assert.IsType<GuestWelcomeMessage>(messages[3]).Role);
        Assert.Equal(Role.Guest, guest.Connection.Role);
    }

    [Fact]
    public void Each_command_gets_exactly_one_result_correlated_by_id_in_order()
    {
        using var harness = new BrainHarness();
        var operatorClient = harness.Handshake(Role.Operator);
        var guestClient = harness.Handshake(Role.Guest);

        var ids = Enumerable.Range(0, 12)
            .Select(i => i % 2 == 0 ? operatorClient.Command("CloseGallery") : guestClient.Command("CloseGallery"))
            .ToList();
        harness.Run();

        var operatorResults = operatorClient.Messages.Cast<CommandResultMessage>().ToList();
        var guestResults = guestClient.Messages.Cast<CommandResultMessage>().ToList();
        Assert.Equal(ids.Where((_, i) => i % 2 == 0), operatorResults.Select(result => result.Id));
        Assert.Equal(ids.Where((_, i) => i % 2 == 1), guestResults.Select(result => result.Id));
        Assert.All(operatorResults, result => Assert.Equal(RejectReason.NotAvailable, result.Reason));
        Assert.All(guestResults, result => Assert.Equal(RejectReason.WrongRole, result.Reason));
    }

    [Theory]
    [InlineData("not json", null, EnvelopeError.InvalidJson)]
    [InlineData("""{"type":"presentation"}""", null, EnvelopeError.UnknownType)]
    [InlineData("""{"type":"command","id":"1","name":"OpenNewEvent","payload":{}}""", "1", EnvelopeError.InvalidId)]
    [InlineData("""{"type":"command","id":"6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b","payload":{}}""", "6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b", EnvelopeError.InvalidEnvelope)]
    [InlineData("[]", null, EnvelopeError.InvalidEnvelope)]
    public void Invalid_envelopes_are_answered_with_error(string json, string? id, EnvelopeError reason)
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);

        client.Send(json);
        harness.Run();

        Assert.Equal(new ErrorMessage(id, reason), client.Single());
    }

    [Fact]
    public void Oversized_messages_are_answered_with_payload_too_large()
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(Role.Operator);

        client.Send(new string(' ', ProtocolLimits.MaxMessageBytes + 1));
        harness.Run();

        Assert.Equal(new ErrorMessage(null, EnvelopeError.PayloadTooLarge), client.Single());
    }

    private static TheoryData<string> Names(CommandAudience audience) =>
        [.. CommandRegistry.All.Where(command => command.Audience == audience).Select(command => command.Name)];

    /// <summary>The payload of the command's canonical envelope in the protocol snapshots.</summary>
    private static string SamplePayload(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(SourcePaths.CommandSnapshotFolder, name + ".json")));
        return document.RootElement.GetProperty("payload").GetRawText();
    }
}
