using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Gallery;

/// <summary>
/// The gallery commands' wiring (M9 wave A): which role may send which command, and, until wave B1 builds them, that every command is answered
/// <c>rejected notAvailable</c> and changes no presentation. B1 replaces the second part with the real behavior's tests.
/// </summary>
public sealed class GalleryScaffoldTests
{
    private static readonly (string Name, string Payload)[] OperatorCommands =
    [
        ("OpenGallery", """{"eventId":"event-1"}"""),
        ("LoadMoreGuestCycles", """{"cursor":"c"}"""),
        ("OpenGuestCycle", """{"guestCycleId":"cycle-1"}"""),
        ("CloseGuestCycle", "{}"),
        ("OpenGuestCycleFolder", """{"guestCycleId":"cycle-1"}"""),
        ("DeleteGuestCycle", """{"guestCycleId":"cycle-1"}"""),
        ("ConfirmDeleteGuestCycle", "{}"),
        ("CancelDeleteGuestCycle", "{}"),
        ("CloseGallery", "{}"),
    ];

    private static readonly (string Name, string Payload)[] GuestCommands =
    [
        ("OpenGuestGallery", "{}"),
        ("LoadMoreGuestStrips", """{"cursor":"c"}"""),
        ("OpenGuestStrip", """{"guestCycleId":"cycle-1"}"""),
        ("CloseGuestStrip", "{}"),
        ("BackToStart", "{}"),
    ];

    public static TheoryData<string, string> OperatorData => [.. OperatorCommands.Select(command => (command.Name, command.Payload))];

    public static TheoryData<string, string> GuestData => [.. GuestCommands.Select(command => (command.Name, command.Payload))];

    private static CommandResultMessage Result(BrainHarness harness, TestClient client, string name, string payload)
    {
        var id = client.Command(name, payload);
        harness.Run();
        return Assert.Single(client.Messages.OfType<CommandResultMessage>(), message => message.Id == id);
    }

    [Fact]
    public void Gallery_commands_belong_to_the_role_that_shows_that_gallery()
    {
        foreach (var (name, _) in OperatorCommands)
        {
            Assert.Equal(CommandAudience.OperatorOnly, CommandRegistry.TryGet(name, out var command) ? command.Audience : throw new InvalidOperationException(name));
        }

        foreach (var (name, _) in GuestCommands)
        {
            Assert.Equal(CommandAudience.GuestOnly, CommandRegistry.TryGet(name, out var command) ? command.Audience : throw new InvalidOperationException(name));
        }
    }

    [Theory]
    [MemberData(nameof(OperatorData))]
    public void A_guest_can_not_reach_an_operator_gallery_command(string name, string payload)
    {
        using var harness = new BrainHarness();
        var guest = harness.Handshake(Role.Guest);

        Assert.Equal(RejectReason.WrongRole, Result(harness, guest, name, payload).Reason);
    }

    [Theory]
    [MemberData(nameof(GuestData))]
    public void An_operator_can_not_reach_a_guest_gallery_command(string name, string payload)
    {
        using var harness = new BrainHarness();
        var operatorClient = harness.Handshake(Role.Operator);

        Assert.Equal(RejectReason.WrongRole, Result(harness, operatorClient, name, payload).Reason);
    }

    [Theory]
    [MemberData(nameof(OperatorData))]
    public void Until_B1_builds_them_the_operator_gallery_commands_are_not_available(string name, string payload)
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);

        var result = Result(harness, client, name, payload);

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(RejectReason.NotAvailable, result.Reason);
        Assert.Empty(client.Messages.OfType<OperatorPresentationMessage>());
    }

    [Theory]
    [MemberData(nameof(GuestData))]
    public void Until_B1_builds_them_the_guest_gallery_commands_are_not_available(string name, string payload)
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Guest);

        var result = Result(harness, client, name, payload);

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(RejectReason.NotAvailable, result.Reason);
        Assert.Empty(client.Messages.OfType<GuestPresentationMessage>());
    }

    [Fact]
    public void The_idle_presentations_carry_no_gallery_views_and_no_Start_link()
    {
        using var harness = new BrainHarness();
        var operatorClient = harness.Connect(Role.Operator);
        var guest = harness.Connect(Role.Guest);
        operatorClient.Hello();
        guest.Hello();
        harness.Run();

        var operatorView = Assert.IsType<OperatorWelcomeMessage>(operatorClient.Single()).Presentation;
        var guestView = Assert.IsType<GuestWelcomeMessage>(guest.Single()).Presentation;

        Assert.Null(operatorView.EventGallery);
        Assert.Null(operatorView.GuestCycleDetail);
        Assert.Null(guestView.GuestGallery);
        Assert.Null(guestView.GuestStripDetail);
        Assert.False(guestView.Start!.GuestGalleryLinkVisible);
    }
}
