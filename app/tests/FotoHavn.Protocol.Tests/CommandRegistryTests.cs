namespace FotoHavn.Protocol.Tests;

public sealed class CommandRegistryTests
{
    // Plan section 9, Commands, transcribed by area. RetryAssistance is in both an operator and a guest row.
    private static readonly string[] OperatorRows =
    [
        "OpenNewEvent", "EditEvent", "StartEvent", "DeleteEvent", "ConfirmDeleteEvent", "CancelDeleteEvent", "OpenEventFolder",
        "ChangeEventField", "SelectCamera", "SelectGuestDisplay", "IdentifyDisplays", "SetMirror", "SetCountdown",
        "SetOfferedTemplates", "SetDefaultTemplate", "SetOfferedLooks", "SetDefaultLook", "SetPhaseTimer", "SetGuestGallery",
        "SetPin", "SaveEvent", "SaveAndStartEvent", "LeaveEventSetup", "ConfirmLeave",
        "OpenGallery", "LoadMoreGuestCycles", "OpenGuestCycle", "CloseGuestCycle", "OpenGuestCycleFolder", "DeleteGuestCycle",
        "ConfirmDeleteGuestCycle", "CancelDeleteGuestCycle", "CloseGallery",
        "EndEvent", "ConfirmEndEvent", "KeepRunning", "RetryAssistance", "RetryStorage",
    ];

    private static readonly string[] GuestRows =
    [
        "BeginGuestCycle", "SelectTemplate", "GoToStep", "ShowCameraTab", "ShowArrangeTab", "TakePhotographs", "StopCapture",
        "SelectPosition", "BeginRetake", "AcceptRetake", "TryAgain", "KeepOriginal", "BeginMove", "MoveTo", "CancelMove",
        "DragPlace", "SelectLook", "FinishStrip", "MoreTime", "StartOver", "ConfirmStartOver", "KeepGoing",
        "BeginExitHold", "CompleteExitHold", "SubmitPin", "CancelExit", "RetryAssistance", "OpenGuestGallery",
        "LoadMoreGuestStrips", "OpenGuestStrip", "CloseGuestStrip", "BackToStart",
    ];

    [Fact]
    public void Every_plan_command_has_exactly_one_entry()
    {
        var planned = OperatorRows.Concat(GuestRows).Distinct().Order(StringComparer.Ordinal);
        var registered = CommandRegistry.All.Select(command => command.Name).ToList();

        Assert.Equal(registered.Count, registered.Distinct().Count());
        Assert.Equal(planned, registered.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Each_command_is_allowed_from_the_roles_of_its_plan_rows()
    {
        foreach (var command in CommandRegistry.All)
        {
            var inOperatorRow = OperatorRows.Contains(command.Name);
            var inGuestRow = GuestRows.Contains(command.Name);
            var expected = (inOperatorRow, inGuestRow) switch
            {
                (true, true) => CommandAudience.Both,
                (true, false) => CommandAudience.OperatorOnly,
                _ => CommandAudience.GuestOnly,
            };

            Assert.Equal(expected, command.Audience);
            Assert.Equal(inOperatorRow, command.IsAllowedFrom(Role.Operator));
            Assert.Equal(inGuestRow, command.IsAllowedFrom(Role.Guest));
        }
    }

    [Fact]
    public void Only_RetryAssistance_is_allowed_from_both_roles()
    {
        var both = CommandRegistry.All.Where(command => command.Audience == CommandAudience.Both).Select(command => command.Name);

        Assert.Equal(["RetryAssistance"], both);
    }

    [Fact]
    public void Every_payload_record_is_registered_once()
    {
        var payloadTypes = typeof(ICommandPayload).Assembly.GetTypes()
            .Where(type => type.IsClass && typeof(ICommandPayload).IsAssignableFrom(type))
            .Order(Comparer<Type>.Create((a, b) => string.CompareOrdinal(a.Name, b.Name)));

        Assert.Equal(
            payloadTypes,
            CommandRegistry.All.Select(command => command.PayloadType).Order(Comparer<Type>.Create((a, b) => string.CompareOrdinal(a.Name, b.Name))));
        Assert.All(CommandRegistry.All, command => Assert.Equal(command.Name + "Payload", command.PayloadType.Name));
    }

    [Fact]
    public void Lookup_by_name_is_exact()
    {
        Assert.True(CommandRegistry.TryGet("OpenNewEvent", out var command));
        Assert.Equal(typeof(OpenNewEventPayload), command.PayloadType);
        Assert.False(CommandRegistry.TryGet("openNewEvent", out _));
        Assert.False(CommandRegistry.TryGet("Unknown", out _));
    }

    [Fact]
    public void Lookup_by_payload_finds_the_descriptor()
    {
        Assert.Equal("EditEvent", CommandRegistry.For<EditEventPayload>().Name);
        Assert.Equal("SubmitPin", CommandRegistry.For(new SubmitPinPayload("1234")).Name);
        Assert.Throws<ArgumentNullException>(() => CommandRegistry.For(null!));
    }
}
