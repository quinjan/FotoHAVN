using FotoHavn.Core.Catalog;
using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Events.Active;

/// <summary>
/// Start Event step 2 (plan section 8): whether the Event as it is saved still satisfies Event setup's field rules. Event setup
/// validates a draft before saving, so a saved Event passes unless the store was edited by hand or by an older version; the check
/// is the start's safety net, not a second form validation. Pure: the catalog is the only thing it reads.
/// </summary>
internal static class SavedEventRules
{
    /// <summary>The first rule the saved Event breaks, or null when it satisfies every rule.</summary>
    public static EventSetupError? FirstViolation(EventRecord record, ICatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(catalog);

        var textError = EventSetupLimits.CheckName(record.Name)
            ?? EventSetupLimits.CheckVenue(record.Venue)
            ?? EventSetupLimits.CheckCustomer(record.CustomerName)
            ?? EventSetupLimits.CheckNotes(record.Notes);
        if (textError is not null)
        {
            return textError;
        }

        if (string.IsNullOrEmpty(record.CameraDeviceId))
        {
            return EventSetupError.CameraRequired;
        }

        if (string.IsNullOrEmpty(record.GuestDisplayId))
        {
            return EventSetupError.DisplayRequired;
        }

        var offeringError = EventSetupLimits.CheckTemplates(record.OfferedTemplateIds, record.DefaultTemplateId)
            ?? EventSetupLimits.CheckLooks(record.OfferedLookIds, record.DefaultLookId);
        if (offeringError is not null)
        {
            return offeringError;
        }

        if (record.OfferedTemplateIds.Any(id => catalog.FindTemplate(id) is null))
        {
            return EventSetupError.AtLeastOneTemplate;
        }

        if (record.OfferedLookIds.Any(id => catalog.FindLook(id) is null))
        {
            return EventSetupError.AtLeastOneLook;
        }

        // A phase the record doesn't list runs at its default; one it lists must be in range.
        if (record.PhaseTimers.Any(timer => !PhaseTimerRules.IsValid(timer.Phase, timer.Seconds)))
        {
            return EventSetupError.TimerOutOfRange;
        }

        return string.Equals(record.Printer, EventRecord.NoPrinter, StringComparison.Ordinal) && EventSetupLimits.IsValidCountdown(record.CountdownSeconds)
            ? null
            : EventSetupError.TimerOutOfRange;
    }
}
