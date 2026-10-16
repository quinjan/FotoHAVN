using System.Runtime.InteropServices;
using FotoHavn.Host.Windows.Interop;

namespace FotoHavn.Host.Windows.Hardening;

/// <summary>The real power-request calls (<c>PowerCreateRequest</c>, <c>PowerSetRequest</c>, <c>PowerClearRequest</c>); thin I/O, no decisions.</summary>
internal sealed class NativePowerRequestApi : IPowerRequestApi
{
    private static readonly IntPtr InvalidHandle = new(-1);

    public IntPtr? Create(string reason, out int error)
    {
        ArgumentNullException.ThrowIfNull(reason);
        var text = Marshal.StringToHGlobalUni(reason);
        try
        {
            var context = new NativeMethods.ReasonContext
            {
                Version = NativeMethods.PowerRequestContextVersion,
                Flags = NativeMethods.PowerRequestContextSimpleString,
                SimpleReasonString = text,
            };
            var request = NativeMethods.PowerCreateRequest(ref context);
            error = Marshal.GetLastPInvokeError();
            return request == IntPtr.Zero || request == InvalidHandle ? null : request;
        }
        finally
        {
            Marshal.FreeHGlobal(text);
        }
    }

    public bool Set(IntPtr request, PowerRequestKind kind, out int error)
    {
        var done = NativeMethods.PowerSetRequest(request, (int)kind);
        error = done ? 0 : Marshal.GetLastPInvokeError();
        return done;
    }

    public bool Clear(IntPtr request, PowerRequestKind kind) => NativeMethods.PowerClearRequest(request, (int)kind);

    public void Close(IntPtr request) => _ = NativeMethods.CloseHandle(request);
}
