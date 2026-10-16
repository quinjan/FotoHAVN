using FotoHavn.Host.Windows.Interop;

namespace FotoHavn.Host.Windows.Hardening;

/// <summary>A key press as the low-level keyboard hook reports it: the virtual key, whether Alt is held for this event, and whether it is the press or the release.</summary>
internal readonly record struct KeyboardEvent(int VirtualKey, bool AltDown, bool IsKeyDown);

/// <summary>
/// Which keys and window commands the Guest window ignores while an Event runs (M10). Pure: the hook procedure and the window procedure
/// only ask. Ctrl+Alt+Del is Windows' own and never reaches an application; Ctrl+Shift+Esc (Task Manager) is left alone on purpose, so
/// the operator always has a way out that is not the app.
/// </summary>
internal static class InputGuardPolicy
{
    private const int SystemCommandMask = 0xFFF0;

    // WM_SYSCOMMAND commands that move, size, switch, or open the window's system menu.
    private static readonly int[] BlockedSystemCommands =
    [
        0xF000, // SC_SIZE
        0xF010, // SC_MOVE
        0xF020, // SC_MINIMIZE
        0xF030, // SC_MAXIMIZE
        0xF040, // SC_NEXTWINDOW
        0xF050, // SC_PREVWINDOW
        0xF100, // SC_KEYMENU (Alt+Space and Alt+letter menus)
        0xF120, // SC_RESTORE
    ];

    /// <summary>
    /// Whether a key event is swallowed: Alt+F4, Alt+Space, Alt+Tab, Alt+Esc, Ctrl+Esc, and either Windows key (which takes every Win-key
    /// shortcut with it, as far as a hook can: the Start menu, Win+D, Win+Arrow, Win+Tab).
    /// </summary>
    public static bool BlocksKey(KeyboardEvent key, bool ctrlDown, bool shiftDown)
    {
        if (key.VirtualKey is NativeMethods.VkLWin or NativeMethods.VkRWin)
        {
            return true;
        }

        if (key.AltDown && key.VirtualKey is NativeMethods.VkF4 or NativeMethods.VkSpace or NativeMethods.VkTab or NativeMethods.VkEscape)
        {
            return true;
        }

        return ctrlDown && !shiftDown && !key.AltDown && key.VirtualKey == NativeMethods.VkEscape;
    }

    /// <summary>Whether a <c>WM_SYSCOMMAND</c> (its wParam) is refused. Closing is not here: the window's closing handler refuses it.</summary>
    public static bool BlocksSystemCommand(nint command) => Array.IndexOf(BlockedSystemCommands, (int)(command & SystemCommandMask)) >= 0;
}

/// <summary>The low-level keyboard hook behind <see cref="IKeyboardHook"/>: installed while the guard has a window, removed with it.</summary>
internal interface IKeyboardHook : IDisposable
{
    /// <summary>Starts calling <paramref name="swallow"/> for every key event; a true answer keeps the key from every other application. Returns whether the hook is installed.</summary>
    bool Install(Func<KeyboardEvent, bool> swallow);

    /// <summary>The Windows error of the last failed <see cref="Install"/>.</summary>
    int LastError { get; }

    void Uninstall();
}

/// <summary>
/// The Guest window's input guard (M10, plan "window shortcuts"): while the Guest window exists and an Event is active, the keys that
/// would close, move, or leave it are swallowed when the Guest window is the foreground window. With no Event nothing is blocked. The
/// hook is installed when the Guest window opens and removed when it closes or the app exits; a hook with nothing to guard would only
/// slow every key press in Windows.
/// </summary>
internal sealed class GuestInputGuard(IKeyboardHook hook, Func<bool> eventActive, Func<IntPtr> foregroundWindow, Func<int, bool> isKeyDown) : IDisposable
{
    private IntPtr _guestWindow;

    /// <summary>The Windows error behind a failed <see cref="Attach"/>, for the log.</summary>
    public int LastError => hook.LastError;

    /// <summary>Whether the guard is blocking anything now: a Guest window is attached and an Event is active.</summary>
    public bool IsActive => _guestWindow != IntPtr.Zero && eventActive();

    /// <summary>Guards <paramref name="guestWindow"/> (the Guest window's handle). Returns whether the keyboard hook is in place; the window-command guard works without it.</summary>
    public bool Attach(IntPtr guestWindow)
    {
        _guestWindow = guestWindow;
        hook.Uninstall();
        return hook.Install(ShouldSwallow);
    }

    public void Detach()
    {
        _guestWindow = IntPtr.Zero;
        hook.Uninstall();
    }

    /// <summary>The hook's decision for one key event.</summary>
    public bool ShouldSwallow(KeyboardEvent key) =>
        IsActive
        && foregroundWindow() == _guestWindow
        && InputGuardPolicy.BlocksKey(key, isKeyDown(NativeMethods.VkControl), isKeyDown(NativeMethods.VkShift));

    /// <summary>The Guest window's own key press (WPF delivered it): whether it is one the guard refuses. No foreground check: the window has the keyboard.</summary>
    public bool BlocksKey(KeyboardEvent key, bool ctrlDown, bool shiftDown) => IsActive && InputGuardPolicy.BlocksKey(key, ctrlDown, shiftDown);

    /// <summary>Whether the Guest window refuses this <c>WM_SYSCOMMAND</c> now.</summary>
    public bool BlocksSystemCommand(nint command) => IsActive && InputGuardPolicy.BlocksSystemCommand(command);

    public void Dispose()
    {
        Detach();
        hook.Dispose();
    }
}
