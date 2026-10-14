namespace FotoHavn.Core.Displays;

/// <summary>
/// Display identity (D33): a display is found by its <see cref="DisplayInfo.Id"/>, the monitor device path, with an
/// ordinal comparison. Matching never falls back to position, bounds, name, or index: the same monitor on another port
/// is another display, and another monitor in the same place is never taken for the bound one.
/// </summary>
public static class DisplayMatching
{
    /// <summary>The display with <paramref name="id"/>, or null when no display in <paramref name="displays"/> has it.</summary>
    public static DisplayInfo? Find(IReadOnlyList<DisplayInfo> displays, string? id)
    {
        ArgumentNullException.ThrowIfNull(displays);
        if (id is null)
        {
            return null;
        }

        foreach (var display in displays)
        {
            if (string.Equals(display.Id, id, StringComparison.Ordinal))
            {
                return display;
            }
        }

        return null;
    }

    /// <summary>The primary display, or null when the list has none.</summary>
    public static DisplayInfo? FindPrimary(IReadOnlyList<DisplayInfo> displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        foreach (var display in displays)
        {
            if (display.IsPrimary)
            {
                return display;
            }
        }

        return null;
    }
}

/// <summary>Pure display rules (plan section 11, ADR 0008).</summary>
public static class DisplayRules
{
    /// <summary>
    /// Single-screen detection (D38): the Guest Screen covers the Operator Screen when only one display exists, or when the
    /// Guest Display Binding is the display that currently contains the Operator window. The comparison is with
    /// <paramref name="operatorDisplayId"/> (<see cref="IHostServices.GetOperatorDisplayId"/>), not with the primary
    /// display, because the operator may move the Operator window; only when it is null (no Operator window yet) does the
    /// rule fall back to the primary display's id.
    /// </summary>
    /// <param name="displays">The current Available Displays.</param>
    /// <param name="guestDisplayId">The Guest Display Binding's device path.</param>
    /// <param name="operatorDisplayId">The id of the display containing the Operator window, or null when unknown.</param>
    public static bool IsSingleScreen(IReadOnlyList<DisplayInfo> displays, string guestDisplayId, string? operatorDisplayId)
    {
        ArgumentNullException.ThrowIfNull(displays);
        ArgumentNullException.ThrowIfNull(guestDisplayId);
        if (displays.Count <= 1)
        {
            return true;
        }

        var operatorId = operatorDisplayId ?? DisplayMatching.FindPrimary(displays)?.Id;
        return string.Equals(guestDisplayId, operatorId, StringComparison.Ordinal);
    }

    /// <summary>
    /// The displays in the order <c>IDENTIFY DISPLAYS</c> numbers them (1 up): left to right by <see cref="DisplayInfo.Bounds"/>,
    /// then top to bottom, then by id (ordinal), so the number is stable for a given layout and matches what the guest display
    /// picker lists. Event setup lists displays in this order and gives each its position plus one as its number.
    /// </summary>
    public static IReadOnlyList<DisplayInfo> NumberingOrder(IReadOnlyList<DisplayInfo> displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        return [.. displays
            .OrderBy(display => display.Bounds.X)
            .ThenBy(display => display.Bounds.Y)
            .ThenBy(display => display.Id, StringComparer.Ordinal)];
    }
}
