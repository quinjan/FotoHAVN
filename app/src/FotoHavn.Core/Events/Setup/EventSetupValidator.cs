using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Events.Setup;

/// <summary>One invalid field of the draft: <see cref="Field"/> is one of <see cref="EventSetupFieldNames"/>.</summary>
internal readonly record struct SetupFieldError(string Field, EventSetupError Code)
{
    public EventSetupFieldError ToProtocol() => new(Field, EventSetupErrors.ToProtocol(Code));
}

/// <summary>
/// Every field rule of Event setup in one pure place (plan section 8, Event setup; M6 contract section 10.2), built on
/// <see cref="EventSetupLimits"/>, <see cref="PhaseTimerRules"/>, and <see cref="PinRules"/>. The brain computes the errors from the
/// draft every time it renders, so the form always knows what is wrong; the UI shows them only after a save attempt.
/// </summary>
internal static class EventSetupValidator
{
    /// <summary>The errors of <paramref name="draft"/>, in the form's order (name, date, venue, customer, notes, camera, display, Templates, Looks, timers, PIN).</summary>
    public static IReadOnlyList<SetupFieldError> Validate(EventSetupDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var errors = new List<SetupFieldError>();
        Add(errors, EventSetupFieldNames.Name, EventSetupLimits.CheckName(draft.Name));
        Add(errors, EventSetupFieldNames.Date, EventSetupLimits.CheckDate(draft.Date, out _));
        Add(errors, EventSetupFieldNames.Venue, EventSetupLimits.CheckVenue(draft.Venue));
        Add(errors, EventSetupFieldNames.Customer, EventSetupLimits.CheckCustomer(draft.Customer));
        Add(errors, EventSetupFieldNames.Notes, EventSetupLimits.CheckNotes(draft.Notes));

        // A chosen binding is enough to save, whether or not that camera or display is connected right now.
        Add(errors, EventSetupFieldNames.Camera, draft.CameraDeviceId is null ? EventSetupError.CameraRequired : null);
        Add(errors, EventSetupFieldNames.GuestDisplay, draft.GuestDisplayId is null ? EventSetupError.DisplayRequired : null);

        var templates = EventSetupLimits.CheckTemplates([.. draft.OfferedTemplateIds], draft.DefaultTemplateId);
        Add(errors, templates == EventSetupError.AtLeastOneTemplate ? EventSetupFieldNames.Templates : EventSetupFieldNames.DefaultTemplate, templates);
        var looks = EventSetupLimits.CheckLooks([.. draft.OfferedLookIds], draft.DefaultLookId);
        Add(errors, looks == EventSetupError.AtLeastOneLook ? EventSetupFieldNames.Looks : EventSetupFieldNames.DefaultLook, looks);

        foreach (var phase in PhaseTimerRules.All)
        {
            if (!PhaseTimerRules.IsValid(phase, draft.TimerSeconds(phase)))
            {
                errors.Add(new SetupFieldError(EventSetupFieldNames.Timer(PhaseTimerRules.ToProtocol(phase)), EventSetupError.TimerOutOfRange));
            }
        }

        // A pending PIN is checked for format and match; with none pending, an Event that has no saved PIN still needs one to start.
        Add(errors, EventSetupFieldNames.Pin, draft.PendingPin is { } pin
            ? PinRules.Check(pin, draft.PendingPinConfirmation)
            : draft.HasSavedPin ? null : EventSetupError.PinRequired);
        return errors;
    }

    /// <summary>Whether <c>SAVE</c> may go ahead: everything is valid except that a PIN may still be missing (a draft saves without one).</summary>
    public static bool CanSave(IReadOnlyList<SetupFieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return errors.All(error => error is { Field: EventSetupFieldNames.Pin, Code: EventSetupError.PinRequired });
    }

    /// <summary>Whether <c>SAVE AND START</c> may go ahead: no error at all, so a PIN (saved or pending) exists.</summary>
    public static bool CanStart(IReadOnlyList<SetupFieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return errors.Count == 0;
    }

    private static void Add(List<SetupFieldError> errors, string field, EventSetupError? error)
    {
        if (error is { } code)
        {
            errors.Add(new SetupFieldError(field, code));
        }
    }
}
