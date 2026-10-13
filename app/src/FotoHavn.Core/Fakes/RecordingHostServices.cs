using FotoHavn.Core.Displays;

namespace FotoHavn.Core.Fakes;

/// <summary>
/// Host services that only record what the brain asked for, for tests (and hosts that don't show windows). Thread-safe.
/// </summary>
public sealed class RecordingHostServices : IHostServices
{
    private readonly object _gate = new();
    private readonly List<string> _calls = [];
    private readonly List<DisplayInfo> _openedGuestScreens = [];
    private readonly List<string> _openedFolders = [];
    private string? _operatorDisplayId;

    /// <param name="operatorDisplayId">What <see cref="GetOperatorDisplayId"/> returns; usually the primary display's id.</param>
    public RecordingHostServices(string? operatorDisplayId = null)
    {
        _operatorDisplayId = operatorDisplayId;
    }

    /// <summary>The display that contains the Operator window, as <see cref="GetOperatorDisplayId"/> reports it.</summary>
    public string? OperatorDisplayId
    {
        get
        {
            lock (_gate)
            {
                return _operatorDisplayId;
            }
        }

        set
        {
            lock (_gate)
            {
                _operatorDisplayId = value;
            }
        }
    }

    /// <summary>Every call in order: <c>OpenGuestScreen(&lt;id&gt;)</c>, <c>CloseGuestScreen</c>, <c>OpenFolder(&lt;path&gt;)</c>.</summary>
    public IReadOnlyList<string> Calls => Snapshot(_calls);

    public IReadOnlyList<DisplayInfo> OpenedGuestScreens => Snapshot(_openedGuestScreens);

    public IReadOnlyList<string> OpenedFolders => Snapshot(_openedFolders);

    public void OpenGuestScreen(DisplayInfo display)
    {
        ArgumentNullException.ThrowIfNull(display);
        lock (_gate)
        {
            _openedGuestScreens.Add(display);
            _calls.Add($"OpenGuestScreen({display.Id})");
        }
    }

    public void CloseGuestScreen()
    {
        lock (_gate)
        {
            _calls.Add("CloseGuestScreen");
        }
    }

    public void OpenFolder(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            _openedFolders.Add(path);
            _calls.Add($"OpenFolder({path})");
        }
    }

    public string? GetOperatorDisplayId() => OperatorDisplayId;

    private T[] Snapshot<T>(List<T> list)
    {
        lock (_gate)
        {
            return [.. list];
        }
    }
}
