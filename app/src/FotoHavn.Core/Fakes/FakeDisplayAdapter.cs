using FotoHavn.Core.Displays;

namespace FotoHavn.Core.Fakes;

/// <summary>
/// A display adapter with configurable displays, for tests and the headless host. Every change raises
/// <see cref="DisplaysChanged"/> synchronously on the caller's thread; <see cref="GetDisplays"/> is thread-safe.
/// </summary>
public sealed class FakeDisplayAdapter : IDisplayAdapter
{
    private readonly object _gate = new();
    private IReadOnlyList<DisplayInfo> _displays;

    public FakeDisplayAdapter(params DisplayInfo[] displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        _displays = [.. displays];
    }

    public event EventHandler? DisplaysChanged;

    /// <summary>The headless host's default primary display: 1920×1080 at 100% scaling.</summary>
    public static DisplayInfo DefaultPrimary { get; } = Create("fake-primary", "Fake primary", 0, 0, 1920, 1080, 1.0, isPrimary: true);

    /// <summary>The headless host's default second display: 1920×1080 at 100% scaling, to the right of the primary.</summary>
    public static DisplayInfo DefaultGuest { get; } = Create("fake-guest", "Fake guest", 1920, 0, 1920, 1080, 1.0, isPrimary: false);

    /// <summary>The current primary display's id, or null when none is primary (the headless host's Operator window display).</summary>
    public string? PrimaryDisplayId => DisplayMatching.FindPrimary(GetDisplays())?.Id;

    /// <summary>The <see cref="DefaultPrimary"/> and <see cref="DefaultGuest"/> displays.</summary>
    public static FakeDisplayAdapter CreateDefault() => new(DefaultPrimary, DefaultGuest);

    /// <summary>A display whose work area equals its bounds.</summary>
    public static DisplayInfo Create(string id, string name, int x, int y, int width, int height, double scale = 1.0, bool isPrimary = false)
    {
        var bounds = new DisplayRect(x, y, width, height);
        return new DisplayInfo(id, name, bounds, bounds, scale, isPrimary);
    }

    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        lock (_gate)
        {
            return _displays;
        }
    }

    /// <summary>Replaces every display.</summary>
    public void SetDisplays(IEnumerable<DisplayInfo> displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        DisplayInfo[] next = [.. displays];
        Change(_ => next);
    }

    /// <summary>Plugs in <paramref name="display"/>, replacing a display with the same id in place.</summary>
    public void Connect(DisplayInfo display)
    {
        ArgumentNullException.ThrowIfNull(display);
        Change(current =>
        {
            var index = IndexOf(current, display.Id);
            if (index < 0)
            {
                return [.. current, display];
            }

            var next = current.ToArray();
            next[index] = display;
            return next;
        });
    }

    /// <summary>Unplugs the display with <paramref name="id"/>. Returns false (and raises nothing) when no display has it.</summary>
    public bool Disconnect(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_gate)
        {
            if (IndexOf(_displays, id) < 0)
            {
                return false;
            }

            _displays = [.. _displays.Where(display => !string.Equals(display.Id, id, StringComparison.Ordinal))];
        }

        DisplaysChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void Change(Func<IReadOnlyList<DisplayInfo>, IReadOnlyList<DisplayInfo>> change)
    {
        lock (_gate)
        {
            _displays = change(_displays);
        }

        DisplaysChanged?.Invoke(this, EventArgs.Empty);
    }

    private static int IndexOf(IReadOnlyList<DisplayInfo> displays, string id)
    {
        for (var i = 0; i < displays.Count; i++)
        {
            if (string.Equals(displays[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
