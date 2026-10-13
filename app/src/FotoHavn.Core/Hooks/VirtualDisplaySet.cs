using FotoHavn.Core.Displays;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Hooks;

/// <summary>
/// The virtual displays of <c>test.useVirtualDisplays</c>: the mapped list plus which of them are disconnected. Pure
/// state on the brain's executor; compiled in every build so it is unit-tested (only E2E builds can reach it).
/// </summary>
internal sealed class VirtualDisplaySet
{
    private readonly IReadOnlyList<DisplayInfo> _all;
    private readonly HashSet<string> _disconnected = new(StringComparer.Ordinal);

    private VirtualDisplaySet(IReadOnlyList<DisplayInfo> all)
    {
        _all = all;
    }

    /// <summary>The connected virtual displays, in the order given.</summary>
    public IReadOnlyList<DisplayInfo> Current => [.. _all.Where(display => !_disconnected.Contains(display.Id))];

    /// <summary>The connected virtual primary's id, the Operator window's display while displays are virtual (null when unplugged).</summary>
    public string? PrimaryId => DisplayMatching.FindPrimary(Current)?.Id;

    /// <summary>
    /// Maps <paramref name="specs"/> onto the real primary display P: each spec's bounds are relative to P's top-left
    /// corner and must fit inside P; the work area equals the bounds; a missing scale takes P's. Returns the error to
    /// answer, or null with the new set.
    /// </summary>
    public static TestHookError? TryCreate(IReadOnlyList<DisplayInfo> realDisplays, IReadOnlyList<VirtualDisplaySpec> specs, out VirtualDisplaySet set)
    {
        set = null!;
        if (DisplayMatching.FindPrimary(realDisplays) is not { } primary)
        {
            return TestHookError.HookFailed;
        }

        if (!AreValid(specs, primary))
        {
            return TestHookError.InvalidArgs;
        }

        set = new VirtualDisplaySet([.. specs.Select(spec => Map(spec, primary))]);
        return null;
    }

    /// <summary>Unplugs a connected virtual display; false when it isn't one.</summary>
    public bool Disconnect(string id) => DisplayMatching.Find(_all, id) is not null && _disconnected.Add(id);

    /// <summary>Plugs a disconnected virtual display back in; false when it isn't one.</summary>
    public bool Connect(string id) => _disconnected.Remove(id);

    public static TestDisplay ToTestDisplay(DisplayInfo display) =>
        new(display.Id, display.Name, ToPixelRect(display.Bounds), ToPixelRect(display.WorkArea), display.Scale, display.IsPrimary);

    private static bool AreValid(IReadOnlyList<VirtualDisplaySpec> specs, DisplayInfo primary)
    {
        if (specs.Count == 0 || specs.Any(spec => spec is null))
        {
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        return specs.All(spec => spec.Id.Length > 0 && ids.Add(spec.Id) && FitsInside(spec.Bounds, primary.Bounds) && IsValidScale(spec.Scale))
            && specs.Count(spec => spec.IsPrimary) == 1;
    }

    private static bool FitsInside(PixelRect bounds, DisplayRect primary) =>
        bounds.X >= 0
        && bounds.Y >= 0
        && bounds.Width > 0
        && bounds.Height > 0
        && (long)bounds.X + bounds.Width <= primary.Width
        && (long)bounds.Y + bounds.Height <= primary.Height;

    private static bool IsValidScale(double? scale) => scale is null || (double.IsFinite(scale.Value) && scale.Value > 0);

    private static DisplayInfo Map(VirtualDisplaySpec spec, DisplayInfo primary)
    {
        var bounds = new DisplayRect(primary.Bounds.X + spec.Bounds.X, primary.Bounds.Y + spec.Bounds.Y, spec.Bounds.Width, spec.Bounds.Height);
        return new DisplayInfo(spec.Id, spec.Name, bounds, bounds, spec.Scale ?? primary.Scale, spec.IsPrimary);
    }

    private static PixelRect ToPixelRect(DisplayRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
