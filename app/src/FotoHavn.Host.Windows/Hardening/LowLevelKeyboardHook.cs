using System.Runtime.InteropServices;
using FotoHavn.Host.Windows.Interop;

namespace FotoHavn.Host.Windows.Hardening;

/// <summary>
/// The real <c>WH_KEYBOARD_LL</c> hook. It is installed on the UI thread, which pumps messages, and Windows calls it on that thread;
/// the procedure only asks the guard, so it stays far inside Windows' time limit for a low-level hook. Events sent through
/// <c>SendInput</c> (the end-to-end helper's) reach it like a person's, so the specs can prove the swallowing. Glue only: the decisions are
/// <see cref="InputGuardPolicy"/> and <see cref="GuestInputGuard"/>, which are tested.
/// </summary>
internal sealed class LowLevelKeyboardHook : IKeyboardHook
{
    private NativeMethods.LowLevelKeyboardProcedure? _procedure;
    private Func<KeyboardEvent, bool>? _swallow;
    private IntPtr _hook;

    public int LastError { get; private set; }

    public bool Install(Func<KeyboardEvent, bool> swallow)
    {
        ArgumentNullException.ThrowIfNull(swallow);
        if (_hook != IntPtr.Zero)
        {
            return true;
        }

        _swallow = swallow;
        _procedure = Handle;
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _procedure, NativeMethods.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
        {
            LastError = Marshal.GetLastPInvokeError();
            _procedure = null;
            _swallow = null;
        }

        return _hook != IntPtr.Zero;
    }

    public void Uninstall()
    {
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        _ = NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        _procedure = null;
        _swallow = null;
    }

    public void Dispose() => Uninstall();

    private IntPtr Handle(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && _swallow is { } swallow)
        {
            var message = unchecked((int)(long)wParam);
            var data = Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(lParam);
            var isDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
            var isUp = message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;
            if ((isDown || isUp) && swallow(new KeyboardEvent((int)data.VirtualKey, (data.Flags & NativeMethods.LlkhfAltDown) != 0, isDown)))
            {
                return 1;
            }
        }

        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
    }
}
