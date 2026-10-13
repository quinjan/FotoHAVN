using System.Collections.Frozen;

namespace FotoHavn.Protocol;

/// <summary>Which roles may send a command (plan section 9). Only <c>RetryAssistance</c> is allowed from both.</summary>
public enum CommandAudience
{
    OperatorOnly,
    GuestOnly,
    Both,
}

/// <summary>One command: its wire name, its payload record, and who may send it.</summary>
public sealed record CommandDescriptor(string Name, Type PayloadType, CommandAudience Audience)
{
    public bool IsAllowedFrom(Role role) => Audience switch
    {
        CommandAudience.Both => true,
        CommandAudience.OperatorOnly => role == Role.Operator,
        _ => role == Role.Guest,
    };
}

/// <summary>
/// The single table of every command, its payload record, and its allowed roles, in plan section 9 order. The
/// parser checks names, roles, and payload shapes against it; the schema export generates the UI's command types from it.
/// </summary>
public static class CommandRegistry
{
    private const string PayloadSuffix = "Payload";

    public static IReadOnlyList<CommandDescriptor> All { get; } =
    [
        // Event list (operator)
        Operator<OpenNewEventPayload>(),
        Operator<EditEventPayload>(),
        Operator<StartEventPayload>(),
        Operator<DeleteEventPayload>(),
        Operator<ConfirmDeleteEventPayload>(),
        Operator<CancelDeleteEventPayload>(),
        Operator<OpenEventFolderPayload>(),

        // Event setup (operator)
        Operator<ChangeEventFieldPayload>(),
        Operator<SelectCameraPayload>(),
        Operator<SelectGuestDisplayPayload>(),
        Operator<IdentifyDisplaysPayload>(),
        Operator<SetMirrorPayload>(),
        Operator<SetCountdownPayload>(),
        Operator<SetOfferedTemplatesPayload>(),
        Operator<SetDefaultTemplatePayload>(),
        Operator<SetOfferedLooksPayload>(),
        Operator<SetDefaultLookPayload>(),
        Operator<SetPhaseTimerPayload>(),
        Operator<SetGuestGalleryPayload>(),
        Operator<SetPinPayload>(),
        Operator<SaveEventPayload>(),
        Operator<SaveAndStartEventPayload>(),
        Operator<LeaveEventSetupPayload>(),
        Operator<ConfirmLeavePayload>(),

        // Event Gallery (operator)
        Operator<OpenGalleryPayload>(),
        Operator<LoadMoreGuestCyclesPayload>(),
        Operator<OpenGuestCyclePayload>(),
        Operator<CloseGuestCyclePayload>(),
        Operator<OpenGuestCycleFolderPayload>(),
        Operator<DeleteGuestCyclePayload>(),
        Operator<ConfirmDeleteGuestCyclePayload>(),
        Operator<CancelDeleteGuestCyclePayload>(),
        Operator<CloseGalleryPayload>(),

        // Running Event (operator); RetryAssistance is also on the Guest Screen
        Operator<EndEventPayload>(),
        Operator<ConfirmEndEventPayload>(),
        Operator<KeepRunningPayload>(),
        Descriptor<RetryAssistancePayload>(CommandAudience.Both),

        // Guest Cycle (guest)
        Guest<BeginGuestCyclePayload>(),
        Guest<SelectTemplatePayload>(),
        Guest<GoToStepPayload>(),
        Guest<ShowCameraTabPayload>(),
        Guest<ShowArrangeTabPayload>(),
        Guest<TakePhotographsPayload>(),
        Guest<StopCapturePayload>(),
        Guest<SelectPositionPayload>(),
        Guest<BeginRetakePayload>(),
        Guest<AcceptRetakePayload>(),
        Guest<TryAgainPayload>(),
        Guest<KeepOriginalPayload>(),
        Guest<BeginMovePayload>(),
        Guest<MoveToPayload>(),
        Guest<CancelMovePayload>(),
        Guest<DragPlacePayload>(),
        Guest<SelectLookPayload>(),
        Guest<FinishStripPayload>(),
        Guest<MoreTimePayload>(),
        Guest<StartOverPayload>(),
        Guest<ConfirmStartOverPayload>(),
        Guest<KeepGoingPayload>(),

        // Guest Screen (guest)
        Guest<BeginExitHoldPayload>(),
        Guest<CompleteExitHoldPayload>(),
        Guest<SubmitPinPayload>(),
        Guest<CancelExitPayload>(),
        Guest<OpenGuestGalleryPayload>(),
        Guest<LoadMoreGuestStripsPayload>(),
        Guest<OpenGuestStripPayload>(),
        Guest<CloseGuestStripPayload>(),
        Guest<BackToStartPayload>(),
    ];

    private static readonly FrozenDictionary<string, CommandDescriptor> ByName =
        All.ToFrozenDictionary(command => command.Name, StringComparer.Ordinal);

    private static readonly FrozenDictionary<Type, CommandDescriptor> ByPayloadType =
        All.ToFrozenDictionary(command => command.PayloadType);

    /// <summary>Finds a command by its exact (case-sensitive) wire name.</summary>
    public static bool TryGet(string name, out CommandDescriptor command) =>
        ByName.TryGetValue(name, out command!);

    /// <summary>The descriptor for a payload record.</summary>
    public static CommandDescriptor For<TPayload>()
        where TPayload : ICommandPayload => ByPayloadType[typeof(TPayload)];

    /// <summary>The descriptor for a payload instance.</summary>
    public static CommandDescriptor For(ICommandPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return ByPayloadType[payload.GetType()];
    }

    private static CommandDescriptor Operator<TPayload>()
        where TPayload : ICommandPayload => Descriptor<TPayload>(CommandAudience.OperatorOnly);

    private static CommandDescriptor Guest<TPayload>()
        where TPayload : ICommandPayload => Descriptor<TPayload>(CommandAudience.GuestOnly);

    private static CommandDescriptor Descriptor<TPayload>(CommandAudience audience)
        where TPayload : ICommandPayload
    {
        var typeName = typeof(TPayload).Name;
        return new CommandDescriptor(typeName[..^PayloadSuffix.Length], typeof(TPayload), audience);
    }
}
