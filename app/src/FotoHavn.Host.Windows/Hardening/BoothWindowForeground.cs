using System.Windows;
using FotoHavn.Host.Windows.Interop;
using FotoHavn.Host.Windows.Screens;

namespace FotoHavn.Host.Windows.Hardening;

/// <summary><see cref="IForegroundWindowApi"/> for a booth window (UI thread): the Win32 calls on its handle.</summary>
internal sealed class BoothWindowForeground(BoothWindow window) : IForegroundWindowApi
{
    public bool IsMinimized => NativeMethods.IsIconic(window.Handle);

    // SW_RESTORE puts a minimized window back in its earlier state, so a maximized Operator window comes back maximized.
    public void Restore() => _ = NativeMethods.ShowWindow(window.Handle, NativeMethods.SwRestore);

    public bool SetForeground()
    {
        var handle = window.Handle;
        return NativeMethods.SetForegroundWindow(handle) && NativeMethods.GetForegroundWindow() == handle;
    }

    public void RaiseBriefly()
    {
        window.Topmost = true;
        window.Topmost = false;
        _ = window.Activate();
    }
}
