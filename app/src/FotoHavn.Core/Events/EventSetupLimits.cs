using System.Globalization;

namespace FotoHavn.Core.Events;

/// <summary>
/// The field rules of Event setup (plan section 8, Event setup): lengths, defaults, and the checks that need no other
/// state. The brain's Event setup draft calls these; the UI never decides a rule.
/// </summary>
public static class EventSetupLimits
{
    public const int NameMinLength = 1;
    public const int NameMaxLength = 80;
    public const int VenueMaxLength = 120;
    public const int CustomerMaxLength = 120;
    public const int NotesMaxLength = 1000;

    public const int DefaultCountdownSeconds = 5;

    /// <summary>The Mirror default: on.</summary>
    public const bool DefaultMirror = true;

    /// <summary>The only printer value in v1 (shown as "No printer").</summary>
    public const string Printer = "none";

    /// <summary>The Date's wire and storage format.</summary>
    public const string DateFormat = "yyyy-MM-dd";

    private static readonly int[] CountdownChoices = [3, 5, 10];

    /// <summary>The countdown lengths an Event may use, in seconds.</summary>
    public static IReadOnlyList<int> Countdowns => CountdownChoices;

    public static bool IsValidCountdown(int seconds) => CountdownChoices.Contains(seconds);

    /// <summary>The Name after trimming: required, 1 to 80 characters. Null when valid.</summary>
    public static EventSetupError? CheckName(string? value)
    {
        var length = (value ?? string.Empty).Trim().Length;
        return length < NameMinLength
            ? EventSetupError.Required
            : length > NameMaxLength ? EventSetupError.TooLong : null;
    }

    /// <summary>An optional text field after trimming: empty is fine, longer than <paramref name="maxLength"/> isn't. Null when valid.</summary>
    public static EventSetupError? CheckOptionalText(string? value, int maxLength) =>
        (value ?? string.Empty).Trim().Length > maxLength ? EventSetupError.TooLong : null;

    public static EventSetupError? CheckVenue(string? value) => CheckOptionalText(value, VenueMaxLength);

    public static EventSetupError? CheckCustomer(string? value) => CheckOptionalText(value, CustomerMaxLength);

    public static EventSetupError? CheckNotes(string? value) => CheckOptionalText(value, NotesMaxLength);

    /// <summary>
    /// The Date: required (after trimming) and exactly <c>yyyy-MM-dd</c> naming a real day. Null when valid;
    /// <paramref name="date"/> is the parsed day when it is.
    /// </summary>
    public static EventSetupError? CheckDate(string? value, out DateOnly date)
    {
        date = default;
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return EventSetupError.Required;
        }

        return DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            ? null
            : EventSetupError.InvalidDate;
    }

    /// <summary>Formats a Date for the wire and storage.</summary>
    public static string FormatDate(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    /// <summary>The trimmed text, or null when it is empty: how an optional field is stored.</summary>
    public static string? NormalizeOptional(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>Offered Templates: at least one, and the default must be among them. Null when valid.</summary>
    public static EventSetupError? CheckTemplates(IReadOnlyCollection<string> offered, string? defaultId) =>
        CheckOffered(offered, defaultId, EventSetupError.AtLeastOneTemplate, EventSetupError.DefaultTemplateNotOffered);

    /// <summary>Offered Looks: at least one, and the default must be among them. Null when valid.</summary>
    public static EventSetupError? CheckLooks(IReadOnlyCollection<string> offered, string? defaultId) =>
        CheckOffered(offered, defaultId, EventSetupError.AtLeastOneLook, EventSetupError.DefaultLookNotOffered);

    private static EventSetupError? CheckOffered(IReadOnlyCollection<string> offered, string? defaultId, EventSetupError none, EventSetupError notOffered)
    {
        ArgumentNullException.ThrowIfNull(offered);
        if (offered.Count == 0)
        {
            return none;
        }

        return defaultId is not null && offered.Contains(defaultId, StringComparer.Ordinal) ? null : notOffered;
    }
}
