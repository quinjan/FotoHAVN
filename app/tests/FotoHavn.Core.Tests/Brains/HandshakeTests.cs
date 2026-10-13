using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Brains;

public sealed class HandshakeTests
{
    [Fact]
    public void Connecting_sends_nothing_until_hello()
    {
        using var harness = new BrainHarness();

        var client = harness.Connect(Role.Operator);

        Assert.Empty(client.Raw);
        Assert.Equal(Role.Operator, client.Connection.Role);
    }

    [Fact]
    public void Operator_hello_is_answered_with_the_empty_event_list_at_revision_1()
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(Role.Operator);

        client.Hello();
        harness.Run();

        var welcome = client.Single<OperatorWelcomeMessage>();
        Assert.Equal(Role.Operator, welcome.Role);
        Assert.Equal(ProtocolVersion.Current, welcome.ProtocolVersion);
        Assert.Equal(1, welcome.Revision);
        Assert.Equal(OperatorSurface.EventList, welcome.Presentation.Surface);
        Assert.Empty(welcome.Presentation.EventList!.Events);
        Assert.Null(welcome.Presentation.RunningEvent);
        Assert.Equal(OperatorOverlays.None, welcome.Presentation.Overlays);
        Assert.Null(welcome.Presentation.Fatal);
    }

    [Fact]
    public void Guest_hello_is_answered_with_the_start_placeholder_at_revision_1()
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(Role.Guest);

        client.Hello();
        harness.Run();

        var welcome = client.Single<GuestWelcomeMessage>();
        Assert.Equal(Role.Guest, welcome.Role);
        Assert.Equal(1, welcome.Revision);
        Assert.Equal(GuestSurface.Start, welcome.Presentation.Surface);
        Assert.False(welcome.Presentation.Start!.GuestGalleryAvailable);
        Assert.Equal(GuestOverlays.None, welcome.Presentation.Overlays);
        Assert.Null(welcome.Presentation.Fatal);
    }

    [Fact]
    public void Welcome_is_the_wire_shape_of_the_contract()
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(Role.Guest);

        client.Hello();
        harness.Run();

        Assert.Equal(
            """{"type":"welcome","role":"guest","protocolVersion":2,"revision":1,"presentation":{"surface":"start","start":{"guestGalleryAvailable":false},"overlays":{"exitHold":null,"pinDialog":null,"phaseTimerWarning":null,"startOverConfirmation":null,"notice":null},"fatal":null}}""",
            Assert.Single(client.Raw));
    }

    [Fact]
    public void A_second_hello_on_the_same_connection_gets_a_fresh_welcome()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);

        client.Hello();
        harness.Run();

        Assert.Equal(1, client.Single<OperatorWelcomeMessage>().Revision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void A_different_protocol_version_is_answered_with_protocol_mismatch(int uiVersion)
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(Role.Operator);

        client.Hello(uiVersion);
        harness.Run();

        var mismatch = client.Single<ProtocolMismatchMessage>();
        Assert.Equal(ProtocolVersion.Current, mismatch.BrainVersion);
        Assert.Equal(uiVersion, mismatch.UiVersion);
        Assert.Contains(harness.Logger.Entries, entry => entry.Message.Contains($"protocol {uiVersion}", StringComparison.Ordinal));
    }

    [Fact]
    public void Commands_before_a_matching_hello_are_rejected_with_protocol_mismatch()
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(Role.Operator);

        var valid = client.Command("OpenNewEvent");
        var unknown = client.Command("NoSuchCommand");
        var wrongRole = client.Command("BeginGuestCycle");
        var badPayload = client.Command("EditEvent", """{"eventId":1}""");
        harness.Run();

        Assert.Equal(
            [
                CommandResultMessage.Rejected(valid, RejectReason.ProtocolMismatch),
                CommandResultMessage.Rejected(unknown, RejectReason.ProtocolMismatch),
                CommandResultMessage.Rejected(wrongRole, RejectReason.ProtocolMismatch),
                CommandResultMessage.Rejected(badPayload, RejectReason.ProtocolMismatch),
            ],
            client.Messages);
    }

    [Fact]
    public void A_mismatched_hello_after_a_handshake_ends_it()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);

        client.Hello(ProtocolVersion.Current + 1);
        var id = client.Command("OpenNewEvent");
        harness.Brain.ShowFatal("Stopped.");
        harness.Run();

        Assert.Equal(
            [new ProtocolMismatchMessage(ProtocolVersion.Current, ProtocolVersion.Current + 1), CommandResultMessage.Rejected(id, RejectReason.ProtocolMismatch)],
            client.Messages);
    }

    [Fact]
    public void A_matching_hello_after_a_mismatch_completes_the_handshake()
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(Role.Guest);

        client.Hello(0);
        client.Hello();
        harness.Run();

        Assert.IsType<ProtocolMismatchMessage>(client.Messages[0]);
        Assert.IsType<GuestWelcomeMessage>(client.Messages[1]);
    }

    [Fact]
    public void A_hello_claiming_a_role_is_an_invalid_envelope_and_changes_nothing()
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(Role.Guest);

        client.Send($$"""{"type":"hello","protocolVersion":{{ProtocolVersion.Current}},"role":"operator"}""");
        client.Hello();
        harness.Run();

        Assert.Equal(new ErrorMessage(null, EnvelopeError.InvalidEnvelope), client.Messages[0]);
        var welcome = Assert.IsType<GuestWelcomeMessage>(client.Messages[1]);
        Assert.Equal(Role.Guest, welcome.Role);
    }

    [Fact]
    public void Connect_checks_its_arguments()
    {
        using var harness = new BrainHarness();

        Assert.Throws<ArgumentNullException>(() => harness.Brain.Connect(Role.Operator, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => harness.Brain.Connect((Role)7, _ => { }));
        Assert.Throws<ArgumentNullException>(() => harness.Connect(Role.Operator).Connection.Receive(null!));
    }

    [Fact]
    public void A_disposed_brain_refuses_connections_and_ignores_queued_work()
    {
        var harness = new BrainHarness();
        var client = harness.Connect(Role.Operator);
        client.Hello();

        harness.Dispose();
        harness.Dispose();
        harness.Run();
        client.Hello();

        Assert.Empty(client.Raw);
        Assert.Equal(0, harness.Executor.PendingCount);
        Assert.Throws<ObjectDisposedException>(() => harness.Brain.Connect(Role.Operator, _ => { }));
    }
}
