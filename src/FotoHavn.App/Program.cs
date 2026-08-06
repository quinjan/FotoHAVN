using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace FotoHavn.App;

public static class Program
{
    private const string InstanceKey = "quinjan.FotoHAVN.Primary";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        var primary = AppInstance.FindOrRegisterForKey(InstanceKey);

        if (!primary.IsCurrent)
        {
            _ = AllowSetForegroundWindow(primary.ProcessId);
            RedirectActivation(activation, primary);
            return 0;
        }

        primary.Activated += (_, redirectedActivation) => App.ForwardActivation(redirectedActivation);
        Application.Start(initializationParameters =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        return 0;
    }

    private static void RedirectActivation(AppActivationArguments activation, AppInstance primary)
    {
        var completed = CreateEvent(IntPtr.Zero, true, false, null);

        _ = Task.Run(async () =>
        {
            try
            {
                await primary.RedirectActivationToAsync(activation);
            }
            finally
            {
                _ = SetEvent(completed);
            }
        });

        _ = CoWaitForMultipleObjects(0, uint.MaxValue, 1, [completed], out _);
        _ = CloseHandle(completed);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateEvent(
        IntPtr eventAttributes,
        bool manualReset,
        bool initialState,
        string? name);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetEvent(IntPtr eventHandle);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("ole32.dll")]
    private static extern uint CoWaitForMultipleObjects(
        uint flags,
        uint milliseconds,
        ulong handleCount,
        IntPtr[] handles,
        out uint index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
