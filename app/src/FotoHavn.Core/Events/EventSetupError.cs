using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Events;

/// <summary>
/// Why an Event setup field or section is invalid (plan section 8, Event setup). Each member maps one to one to an
/// <see cref="EventSetupErrorCode"/> on the wire; the UI owns the message for each code.
/// </summary>
public enum EventSetupError
{
    /// <summary>A required value is empty.</summary>
    Required,

    /// <summary>A text value is longer than its limit.</summary>
    TooLong,

    /// <summary>The Date isn't a real <c>yyyy-MM-dd</c> date.</summary>
    InvalidDate,

    /// <summary>No Template is offered.</summary>
    AtLeastOneTemplate,

    /// <summary>The default Template isn't one of the offered Templates (or is missing).</summary>
    DefaultTemplateNotOffered,

    /// <summary>No Look is offered.</summary>
    AtLeastOneLook,

    /// <summary>The default Look isn't one of the offered Looks (or is missing).</summary>
    DefaultLookNotOffered,

    /// <summary>A Phase Timer is outside its allowed range or can't be off.</summary>
    TimerOutOfRange,

    /// <summary>The PIN isn't 4 to 6 digits.</summary>
    PinFormat,

    /// <summary>The PIN and its confirmation differ.</summary>
    PinMismatch,

    /// <summary>The Event has no PIN yet and none was entered.</summary>
    PinRequired,

    /// <summary>No Camera is chosen.</summary>
    CameraRequired,

    /// <summary>No guest display is chosen.</summary>
    DisplayRequired,
}

/// <summary>Maps <see cref="EventSetupError"/> to its wire code.</summary>
public static class EventSetupErrors
{
    public static EventSetupErrorCode ToProtocol(EventSetupError error) => error switch
    {
        EventSetupError.Required => EventSetupErrorCode.Required,
        EventSetupError.TooLong => EventSetupErrorCode.TooLong,
        EventSetupError.InvalidDate => EventSetupErrorCode.InvalidDate,
        EventSetupError.AtLeastOneTemplate => EventSetupErrorCode.AtLeastOneTemplate,
        EventSetupError.DefaultTemplateNotOffered => EventSetupErrorCode.DefaultTemplateNotOffered,
        EventSetupError.AtLeastOneLook => EventSetupErrorCode.AtLeastOneLook,
        EventSetupError.DefaultLookNotOffered => EventSetupErrorCode.DefaultLookNotOffered,
        EventSetupError.TimerOutOfRange => EventSetupErrorCode.TimerOutOfRange,
        EventSetupError.PinFormat => EventSetupErrorCode.PinFormat,
        EventSetupError.PinMismatch => EventSetupErrorCode.PinMismatch,
        EventSetupError.PinRequired => EventSetupErrorCode.PinRequired,
        EventSetupError.CameraRequired => EventSetupErrorCode.CameraRequired,
        EventSetupError.DisplayRequired => EventSetupErrorCode.DisplayRequired,
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, "Not an Event setup error."),
    };
}
