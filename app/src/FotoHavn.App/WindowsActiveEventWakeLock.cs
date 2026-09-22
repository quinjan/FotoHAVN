using System.ComponentModel;
using System.Runtime.InteropServices;
using FotoHavn.Core;

namespace FotoHavn.App;

internal sealed class WindowsActiveEventWakeLock : IActiveEventWakeLock
{
    private const uint ContextVersion = 0;
    private const uint SimpleStringContext = 0x1;
    private readonly object sync = new();
    private IntPtr requestHandle;

    public Task AcquireAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            if (requestHandle != IntPtr.Zero)
            {
                return Task.CompletedTask;
            }

            var reason = Marshal.StringToHGlobalUni("FotoHAVN Active Event");
            try
            {
                var context = new ReasonContext
                {
                    Version = ContextVersion,
                    Flags = SimpleStringContext,
                    SimpleReasonString = reason,
                };
                var handle = PowerCreateRequest(ref context);
                if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the Active Event power request.");
                }

                if (!PowerSetRequest(handle, PowerRequestType.SystemRequired))
                {
                    var error = Marshal.GetLastWin32Error();
                    CloseHandle(handle);
                    throw new Win32Exception(error, "Could not prevent system sleep for the Active Event.");
                }

                if (!PowerSetRequest(handle, PowerRequestType.DisplayRequired))
                {
                    var error = Marshal.GetLastWin32Error();
                    PowerClearRequest(handle, PowerRequestType.SystemRequired);
                    CloseHandle(handle);
                    throw new Win32Exception(error, "Could not prevent display shutoff for the Active Event.");
                }

                requestHandle = handle;
            }
            finally
            {
                Marshal.FreeHGlobal(reason);
            }
        }

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(CancellationToken cancellationToken)
    {
        lock (sync)
        {
            var handle = requestHandle;
            requestHandle = IntPtr.Zero;
            if (handle == IntPtr.Zero)
            {
                return Task.CompletedTask;
            }

            PowerClearRequest(handle, PowerRequestType.DisplayRequired);
            PowerClearRequest(handle, PowerRequestType.SystemRequired);
            CloseHandle(handle);
        }

        return Task.CompletedTask;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ReasonContext
    {
        public uint Version;
        public uint Flags;
        public IntPtr SimpleReasonString;
    }

    private enum PowerRequestType
    {
        DisplayRequired = 0,
        SystemRequired = 1,
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr PowerCreateRequest(ref ReasonContext context);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerSetRequest(IntPtr powerRequest, PowerRequestType requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerClearRequest(IntPtr powerRequest, PowerRequestType requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
