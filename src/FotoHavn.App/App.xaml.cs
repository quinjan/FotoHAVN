using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using FotoHavn.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace FotoHavn.App;

public partial class App : Application
{
    private static readonly ConcurrentQueue<AppActivationArguments> PendingActivations = new();
    private static App? currentApp;
    private static int windowReady;
    private MainWindow? window;
    private CameraBoundary? camera;
    private EventGuestCycleOrchestrator? orchestrator;
    private Windows.UI.ViewManagement.UISettings? uiSettings;

    public App()
    {
        currentApp = this;
        InitializeComponent();
    }

    internal static void ForwardActivation(AppActivationArguments args)
    {
        PendingActivations.Enqueue(args);
        if (Volatile.Read(ref windowReady) == 1)
        {
            currentApp?.ScheduleActivationDrain();
        }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        camera = new CameraBoundary();
        orchestrator = new EventGuestCycleOrchestrator(
            new ExecutableRelativeEventFileSystem(),
            camera,
            new PhotoStripCompositor(),
            new SystemClock(),
            wakeLock: new WindowsActiveEventWakeLock());

        window = new MainWindow(orchestrator, camera);
        window.Closed += WindowClosed;
        await window.LoadPresentationAsync();
        window.ShowCentered();
        RefreshMotionResources();
        Volatile.Write(ref windowReady, 1);
        DrainPendingActivations();
    }

    private async void WindowClosed(object sender, WindowEventArgs args)
    {
        try
        {
            if (orchestrator is not null)
            {
                await orchestrator.ExecuteAsync(new ShutdownApplication());
                orchestrator = null;
            }
        }
        finally
        {
            if (camera is not null)
            {
                await camera.DisposeAsync();
                camera = null;
            }
        }
    }

    private void ScheduleActivationDrain()
    {
        window?.DispatcherQueue.TryEnqueue(DrainPendingActivations);
    }

    private void DrainPendingActivations()
    {
        while (PendingActivations.TryDequeue(out _))
        {
            RefreshMotionResources();
            if (window is not null)
            {
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                if (IsIconic(windowHandle))
                {
                    _ = ShowWindow(windowHandle, RestoreWindow);
                }

                window.Activate();
                _ = SetForegroundWindow(windowHandle);
            }
        }
    }

    private void RefreshMotionResources()
    {
        uiSettings ??= new Windows.UI.ViewManagement.UISettings();
        var duration = MotionPolicy.ResolveDuration(
            uiSettings.AnimationsEnabled,
            TimeSpan.FromMilliseconds(180));
        Resources["FotoHavnStandardMotionDuration"] = new Duration(duration);
        Resources["FotoHavnPreviewFadeDuration"] = new Duration(MotionPolicy.ResolveDuration(
            uiSettings.AnimationsEnabled,
            TimeSpan.FromMilliseconds(450)));
    }

    private const int RestoreWindow = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);
}
