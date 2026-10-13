using FotoHavn.Core;
using FotoHavn.Core.Displays;

namespace FotoHavn.Host.Headless.Interim;

// TEMPORARY (M2 wave C): replace with FotoHavn.Core.Fakes.FakeDisplayAdapter (same default displays), which the Core
// agent delivers in wave B, and delete this file.

/// <summary>A fixed display list that never changes: the headless host's default fake displays.</summary>
internal sealed class InterimDisplayAdapter(params DisplayInfo[] displays) : IDisplayAdapter
{
    private readonly DisplayInfo[] _displays = [.. displays];

    public event EventHandler? DisplaysChanged
    {
        add { }
        remove { }
    }

    public IReadOnlyList<DisplayInfo> GetDisplays() => _displays;
}
