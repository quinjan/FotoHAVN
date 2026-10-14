using FotoHavn.Core.Catalog;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Events.Setup;

/// <summary>
/// The Event setup form as the operator is editing it (plan section 8, Event setup; M6 contract section 10.1). Brain state, touched
/// only on the executor, dropped when setup closes. Text is kept as typed; the rules (<see cref="EventSetupValidator"/>) trim and
/// check it. The pending Operator PIN pair lives here and nowhere else: only the save reads <see cref="PendingPin"/>, and the class
/// has no <c>ToString</c> that prints it.
/// </summary>
internal sealed class EventSetupDraft
{
    private readonly ICatalog _catalog;
    private readonly Dictionary<PhaseTimerPhase, int> _timers = [];
    private List<string> _offeredTemplateIds = [];
    private List<string> _offeredLookIds = [];
    private string _pin = string.Empty;
    private string _pinConfirmation = string.Empty;
    private string _baseline = string.Empty;

    private EventSetupDraft(ICatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>The saved Event's id, or null until the first successful save.</summary>
    public string? EventId { get; private set; }

    public bool IsNew => EventId is null;

    public string Name { get; private set; } = string.Empty;

    public string Date { get; private set; } = string.Empty;

    public string Venue { get; private set; } = string.Empty;

    public string Customer { get; private set; } = string.Empty;

    public string Notes { get; private set; } = string.Empty;

    public string? CameraDeviceId { get; private set; }

    public string? CameraName { get; private set; }

    public string? GuestDisplayId { get; private set; }

    public string? GuestDisplayName { get; private set; }

    public bool Mirror { get; private set; }

    public int CountdownSeconds { get; private set; }

    public IReadOnlyList<string> OfferedTemplateIds => _offeredTemplateIds;

    public string? DefaultTemplateId { get; private set; }

    public IReadOnlyList<string> OfferedLookIds => _offeredLookIds;

    public string? DefaultLookId { get; private set; }

    public bool GuestGalleryEnabled { get; private set; }

    /// <summary>Whether the Event already has a saved PIN.</summary>
    public bool HasSavedPin { get; private set; }

    /// <summary>True after a save attempt: the form shows its errors.</summary>
    public bool ShowErrors { get; set; }

    /// <summary>True while a save (or save and start) is in flight; the draft can't change then.</summary>
    public bool Saving { get; set; }

    /// <summary>Why the last start attempt stopped; cleared by the next change or attempt.</summary>
    public EventSetupStartFailure? StartFailure { get; set; }

    /// <summary>Whether the draft holds a new PIN (or half of one) that replaces the saved PIN on save.</summary>
    public bool HasPendingPin => _pin.Length > 0 || _pinConfirmation.Length > 0;

    /// <summary>The PIN to store at save, or null when none is pending. Only the save reads this.</summary>
    public string? PendingPin => HasPendingPin ? _pin : null;

    /// <summary>The pending PIN's confirmation entry; only <see cref="EventSetupValidator"/> reads it.</summary>
    public string PendingPinConfirmation => _pinConfirmation;

    /// <summary>Whether a PIN is saved or pending: what Start needs.</summary>
    public bool HasPin => HasSavedPin || HasPendingPin;

    /// <summary>Whether the draft differs from its starting point: the defaults for a new Event, the saved record for an edit.</summary>
    public bool IsDirty => HasPendingPin || !string.Equals(Fingerprint(), _baseline, StringComparison.Ordinal);

    public int TimerSeconds(PhaseTimerPhase phase) => _timers[phase];

    /// <summary>A new Event with its defaults (plan section 8): every Template and Look offered, mirror on, countdown 5, timers at their defaults.</summary>
    public static EventSetupDraft CreateNew(ICatalog catalog, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var draft = new EventSetupDraft(catalog)
        {
            Date = EventSetupLimits.FormatDate(today),
            Mirror = EventSetupLimits.DefaultMirror,
            CountdownSeconds = EventSetupLimits.DefaultCountdownSeconds,
            _offeredTemplateIds = [.. catalog.Templates.Select(template => template.Id)],
            DefaultTemplateId = catalog.Templates.Count > 0 ? catalog.Templates[0].Id : null,
            _offeredLookIds = [.. catalog.Looks.Select(look => look.Id)],
            DefaultLookId = catalog.DefaultLookId,
        };
        foreach (var phase in PhaseTimerRules.All)
        {
            draft._timers[phase] = PhaseTimerRules.Default(phase);
        }

        draft._baseline = draft.Fingerprint();
        return draft;
    }

    /// <summary>A draft of the saved Event <paramref name="record"/>; the bindings, PIN state, and everything else come from it.</summary>
    public static EventSetupDraft FromRecord(ICatalog catalog, EventRecord record)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(record);
        var draft = new EventSetupDraft(catalog)
        {
            EventId = record.Id,
            Name = record.Name,
            Date = EventSetupLimits.FormatDate(record.EventDate),
            Venue = record.Venue ?? string.Empty,
            Customer = record.CustomerName ?? string.Empty,
            Notes = record.Notes ?? string.Empty,
            CameraDeviceId = record.CameraDeviceId,
            CameraName = record.CameraName,
            GuestDisplayId = record.GuestDisplayId,
            GuestDisplayName = record.GuestDisplayName,
            Mirror = record.Mirror,
            CountdownSeconds = record.CountdownSeconds,
            DefaultTemplateId = record.DefaultTemplateId,
            DefaultLookId = record.DefaultLookId,
            GuestGalleryEnabled = record.GuestGalleryEnabled,
            HasSavedPin = record.HasPin,
            _offeredTemplateIds = InCatalogOrder(record.OfferedTemplateIds, catalog.Templates.Select(template => template.Id)),
            _offeredLookIds = InCatalogOrder(record.OfferedLookIds, catalog.Looks.Select(look => look.Id)),
        };
        foreach (var phase in PhaseTimerRules.All)
        {
            var saved = record.PhaseTimers.FirstOrDefault(timer => timer.Phase == phase);
            draft._timers[phase] = saved?.Seconds ?? PhaseTimerRules.Default(phase);
        }

        draft._baseline = draft.Fingerprint();
        return draft;
    }

    /// <summary>Sets one of the text fields as typed (the rules trim).</summary>
    public void SetField(EventField field, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        switch (field)
        {
            case EventField.Name:
                Name = value;
                break;
            case EventField.Date:
                Date = value;
                break;
            case EventField.Venue:
                Venue = value;
                break;
            case EventField.Customer:
                Customer = value;
                break;
            default:
                Notes = value;
                break;
        }
    }

    public void BindCamera(string deviceId, string name)
    {
        CameraDeviceId = deviceId;
        CameraName = name;
    }

    public void BindGuestDisplay(string displayId, string name)
    {
        GuestDisplayId = displayId;
        GuestDisplayName = name;
    }

    public void SetMirror(bool enabled) => Mirror = enabled;

    public void SetCountdown(int seconds) => CountdownSeconds = seconds;

    public void SetGuestGallery(bool enabled) => GuestGalleryEnabled = enabled;

    /// <summary>
    /// Replaces the offered Templates: unknown ids are dropped and the rest are kept in catalog order. The default stays when it is
    /// still offered and is cleared otherwise, so the draft shows <c>defaultTemplateNotOffered</c> until another is chosen.
    /// </summary>
    public void SetOfferedTemplates(IEnumerable<string> templateIds)
    {
        _offeredTemplateIds = InCatalogOrder(templateIds, _catalog.Templates.Select(template => template.Id));
        if (DefaultTemplateId is { } current && !_offeredTemplateIds.Contains(current, StringComparer.Ordinal))
        {
            DefaultTemplateId = null;
        }
    }

    /// <summary>Sets the default Template if the catalog has it (whether it is offered is checked at save). False when it doesn't.</summary>
    public bool SetDefaultTemplate(string templateId)
    {
        if (_catalog.FindTemplate(templateId) is null)
        {
            return false;
        }

        DefaultTemplateId = templateId;
        return true;
    }

    /// <summary>As <see cref="SetOfferedTemplates"/> for the Looks.</summary>
    public void SetOfferedLooks(IEnumerable<string> lookIds)
    {
        _offeredLookIds = InCatalogOrder(lookIds, _catalog.Looks.Select(look => look.Id));
        if (DefaultLookId is { } current && !_offeredLookIds.Contains(current, StringComparer.Ordinal))
        {
            DefaultLookId = null;
        }
    }

    /// <summary>Sets the default Look if the catalog has it. False when it doesn't.</summary>
    public bool SetDefaultLook(string lookId)
    {
        if (_catalog.FindLook(lookId) is null)
        {
            return false;
        }

        DefaultLookId = lookId;
        return true;
    }

    /// <summary>Sets a Phase Timer to any value: an out-of-range one stays visible and is reported as an error.</summary>
    public void SetTimer(PhaseTimerPhase phase, int seconds) => _timers[phase] = seconds;

    /// <summary>Holds the new PIN and its confirmation until save. Both empty clears a pending change.</summary>
    public void SetPin(string pin, string confirmation)
    {
        _pin = pin;
        _pinConfirmation = confirmation;
    }

    /// <summary>
    /// Makes the draft the saved Event <paramref name="eventId"/>: the next save is an update, the pending PIN is gone (it was stored),
    /// and the draft is no longer dirty.
    /// </summary>
    public void MarkSaved(string eventId, bool pinSaved)
    {
        EventId = eventId;
        HasSavedPin = pinSaved;
        _pin = string.Empty;
        _pinConfirmation = string.Empty;
        _baseline = Fingerprint();
    }

    /// <summary>Records that the Event row exists (a later step of the save failed), so the next save updates it instead of creating it again.</summary>
    public void MarkCreated(string eventId) => EventId = eventId;

    /// <summary>The Event as it would be saved with id <paramref name="eventId"/>; call it only for a draft that passed validation.</summary>
    public EventRecord ToRecord(string eventId)
    {
        _ = EventSetupLimits.CheckDate(Date, out var date);
        return new EventRecord(
            eventId,
            Name.Trim(),
            date,
            EventSetupLimits.NormalizeOptional(Venue),
            EventSetupLimits.NormalizeOptional(Customer),
            EventSetupLimits.NormalizeOptional(Notes),
            CameraDeviceId,
            CameraName,
            GuestDisplayId,
            GuestDisplayName,
            EventRecord.NoPrinter,
            Mirror,
            CountdownSeconds,
            DefaultTemplateId!,
            DefaultLookId!,
            GuestGalleryEnabled,
            [.. _offeredTemplateIds],
            [.. _offeredLookIds],
            [.. PhaseTimerRules.All.Select(phase => new PhaseTimerSetting(phase, _timers[phase]))]);
    }

    private static List<string> InCatalogOrder(IEnumerable<string> ids, IEnumerable<string> catalogIds)
    {
        var wanted = new HashSet<string>(ids, StringComparer.Ordinal);
        return [.. catalogIds.Where(wanted.Contains)];
    }

    // Everything the draft holds except the PIN pair (HasPendingPin covers that), for the dirty check.
    private string Fingerprint() => string.Join(
        '\u001f',
        Name,
        Date,
        Venue,
        Customer,
        Notes,
        CameraDeviceId ?? string.Empty,
        GuestDisplayId ?? string.Empty,
        Mirror,
        CountdownSeconds,
        string.Join(',', _offeredTemplateIds),
        DefaultTemplateId ?? string.Empty,
        string.Join(',', _offeredLookIds),
        DefaultLookId ?? string.Empty,
        GuestGalleryEnabled,
        string.Join(',', PhaseTimerRules.All.Select(phase => _timers[phase])));
}
