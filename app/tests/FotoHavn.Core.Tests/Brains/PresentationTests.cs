using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace FotoHavn.Core.Tests.Brains;

public sealed class PresentationTests
{
    [Fact]
    public void A_change_is_pushed_with_the_next_revision_to_every_handshaken_connection_of_each_role()
    {
        using var harness = new BrainHarness();
        var operatorA = harness.Handshake(Role.Operator);
        var operatorB = harness.Handshake(Role.Operator);
        var guest = harness.Handshake(Role.Guest);
        var notHandshaken = harness.Connect(Role.Operator);

        harness.Brain.ShowFatal("FOTOHAVN stopped.");
        harness.Run();

        foreach (var client in new[] { operatorA, operatorB })
        {
            var presentation = client.Single<OperatorPresentationMessage>();
            Assert.Equal(2, presentation.Revision);
            Assert.Equal(OperatorSurface.Fatal, presentation.Presentation.Surface);
            Assert.Equal(new FatalView("FOTOHAVN stopped."), presentation.Presentation.Fatal);
            Assert.Null(presentation.Presentation.EventList);
        }

        var guestPresentation = guest.Single<GuestPresentationMessage>();
        Assert.Equal(2, guestPresentation.Revision);
        Assert.Equal(GuestSurface.Fatal, guestPresentation.Presentation.Surface);
        Assert.Null(guestPresentation.Presentation.Start);
        Assert.Empty(notHandshaken.Raw);
    }

    [Fact]
    public void An_unchanged_presentation_keeps_its_revision()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);

        harness.Brain.ShowFatal("FOTOHAVN stopped.");
        harness.Brain.ShowFatal("FOTOHAVN stopped.");
        client.Command("OpenNewEvent");
        harness.Run();

        Assert.Single(client.Messages.OfType<OperatorPresentationMessage>());
    }

    [Fact]
    public void A_welcome_after_a_change_carries_the_current_revision()
    {
        using var harness = new BrainHarness();
        harness.Brain.ShowFatal("One.");
        harness.Brain.ShowFatal("Two.");
        harness.Run();

        var late = harness.Connect(Role.Guest);
        late.Hello();
        harness.Run();

        var welcome = late.Single<GuestWelcomeMessage>();
        Assert.Equal(3, welcome.Revision);
        Assert.Equal("Two.", welcome.Presentation.Fatal!.Reason);
    }

    [Fact]
    public void A_command_result_is_sent_before_the_presentation_its_work_item_publishes()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);

        var id = client.Command("OpenNewEvent");
        harness.Brain.ShowFatal("Stopped.");
        harness.Run();

        Assert.IsType<CommandResultMessage>(client.Messages[0]);
        Assert.Equal(id, ((CommandResultMessage)client.Messages[0]).Id);
        Assert.IsType<OperatorPresentationMessage>(client.Messages[1]);
    }

    [Fact]
    public void A_disposed_connection_receives_nothing_more()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);

        client.Connection.Dispose();
        client.Connection.Dispose();
        client.Hello();
        harness.Brain.ShowFatal("Stopped.");
        harness.Run();

        Assert.Empty(client.Raw);
    }

    [Fact]
    public void Fatal_needs_a_reason()
    {
        using var harness = new BrainHarness();

        Assert.ThrowsAny<ArgumentException>(() => harness.Brain.ShowFatal(" "));
    }

    [Property(MaxTest = 50)]
    public Property Revisions_only_increase_per_role(NonEmptyArray<NonWhiteSpaceString> reasons, bool handshakeBetween)
    {
        using var harness = new BrainHarness();
        var operatorClient = harness.Handshake(Role.Operator);
        var guestClient = harness.Handshake(Role.Guest);

        foreach (var reason in reasons.Get)
        {
            harness.Brain.ShowFatal(reason.Get);
            if (handshakeBetween)
            {
                guestClient.Hello();
            }
        }

        harness.Run();

        var operatorRevisions = operatorClient.Messages.OfType<OperatorPresentationMessage>().Select(message => message.Revision).ToList();
        var guestRevisions = guestClient.Messages
            .Select(message => message switch
            {
                GuestPresentationMessage presentation => presentation.Revision,
                GuestWelcomeMessage welcome => welcome.Revision,
                _ => -1,
            })
            .ToList();
        var distinctChanges = reasons.Get.Select(reason => reason.Get).Aggregate(
            (Count: 0, Last: (string?)null),
            (state, reason) => reason == state.Last ? state : (state.Count + 1, reason)).Count;

        return (operatorRevisions.SequenceEqual(Enumerable.Range(2, distinctChanges).Select(revision => (long)revision))
                && guestRevisions.Zip(guestRevisions.Skip(1)).All(pair => pair.Second >= pair.First)
                && guestRevisions.All(revision => revision >= 1)
                && (guestRevisions.Count == 0 || guestRevisions[^1] == distinctChanges + 1))
            .ToProperty();
    }
}
