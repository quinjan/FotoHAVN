using FotoHavn.Core;
using FotoHavn.Host.Windows.Logging;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows.Hardening;

/// <summary>The kinds of power request the wake lock makes. The values are the Win32 <c>POWER_REQUEST_TYPE</c> numbers.</summary>
internal enum PowerRequestKind
{
    DisplayRequired = 0,
    SystemRequired = 1,
}

/// <summary>The Windows power-request calls, behind a seam so <see cref="WindowsWakeLock"/>'s logic is tested without the OS.</summary>
internal interface IPowerRequestApi
{
    /// <summary>Creates a request that <c>powercfg /requests</c> shows with <paramref name="reason"/>; null when Windows refused (the error code is in <paramref name="error"/>).</summary>
    IntPtr? Create(string reason, out int error);

    /// <summary>Turns <paramref name="kind"/> on for <paramref name="request"/>; the error code is in <paramref name="error"/> when it fails.</summary>
    bool Set(IntPtr request, PowerRequestKind kind, out int error);

    bool Clear(IntPtr request, PowerRequestKind kind);

    void Close(IntPtr request);
}

/// <summary>
/// Keeps the display and the system awake while an Event is held (M10): one power request with both kinds set, cleared and closed on
/// release. Unlike <c>SetThreadExecutionState</c> a power request does not belong to a thread, so the brain's executor, the UI thread,
/// and <c>Dispose</c> may each call in; Windows drops it if the process dies. A failing call is logged once and never thrown into the
/// brain: the Event runs, and the screen may sleep.
/// </summary>
internal sealed class WindowsWakeLock(IPowerRequestApi api, ILogger logger) : IWakeLock
{
    private readonly Lock _gate = new();
    private IntPtr _request;
    private bool _held;
    private string? _reason;
    private readonly HashSet<PowerRequestKind> _set = [];
    private bool _failureLogged;

    public bool IsHeld
    {
        get
        {
            lock (_gate)
            {
                return _held;
            }
        }
    }

    public string? Reason
    {
        get
        {
            lock (_gate)
            {
                return _reason;
            }
        }
    }

    public void Acquire(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (_gate)
        {
            if (_held)
            {
                return;
            }

            if (api.Create(reason, out var createError) is not { } request)
            {
                LogFailureOnce("create", createError);
                return;
            }

            foreach (var kind in new[] { PowerRequestKind.DisplayRequired, PowerRequestKind.SystemRequired })
            {
                if (api.Set(request, kind, out var setError))
                {
                    _set.Add(kind);
                }
                else
                {
                    LogFailureOnce("set", setError);
                }
            }

            if (_set.Count == 0)
            {
                api.Close(request);
                return;
            }

            _request = request;
            _reason = reason;
            _held = true;
        }
    }

    public void Release()
    {
        lock (_gate)
        {
            if (!_held)
            {
                return;
            }

            foreach (var kind in _set)
            {
                _ = api.Clear(_request, kind);
            }

            api.Close(_request);
            _set.Clear();
            _request = IntPtr.Zero;
            _reason = null;
            _held = false;
        }
    }

    private void LogFailureOnce(string operation, int error)
    {
        if (_failureLogged)
        {
            return;
        }

        _failureLogged = true;
        HostLog.WakeLockFailed(logger, operation, error);
    }
}
