using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;

namespace FotoHavn.UiVerificationHost;

public sealed class WindowSession : IDisposable
{
    private readonly Process process;

    private WindowSession(Process process, IntPtr handle)
    {
        this.process = process;
        Handle = handle;
    }

    public IntPtr Handle { get; }
    public double EffectiveScale { get; private set; } = 1;
    private int effectiveWidth;
    private int effectiveHeight;

    public static WindowSession Launch(string applicationPath, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        if (!File.Exists(applicationPath))
        {
            throw new FileNotFoundException("The UI verification application was not found.", applicationPath);
        }

        var processName = Path.GetFileNameWithoutExtension(applicationPath);
        var existingProcesses = Process.GetProcessesByName(processName);
        var existingProcessIsRunning = existingProcesses.Any(IsRunning);
        foreach (var existingProcess in existingProcesses)
        {
            existingProcess.Dispose();
        }

        if (existingProcessIsRunning)
        {
            throw new InvalidOperationException(
                $"Close every existing {processName} process before producing verification evidence.");
        }

        var start = new ProcessStartInfo(applicationPath) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        var process = Process.Start(start) ?? throw new InvalidOperationException("Could not launch FotoHAVN.");
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !process.HasExited)
        {
            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                return new(process, process.MainWindowHandle);
            }

            Thread.Sleep(50);
        }

        process.Dispose();
        throw new TimeoutException("FotoHAVN did not expose a main window before the launch timeout.");
    }

    public void SetEffectiveClientSize(int width, int height)
    {
        EffectiveScale = GetDpiForWindow(Handle) / 96d;
        effectiveWidth = width;
        effectiveHeight = height;
        var physicalWidth = checked((int)Math.Round(width * EffectiveScale));
        var physicalHeight = checked((int)Math.Round(height * EffectiveScale));

        _ = GetClientRect(Handle, out var client);
        _ = GetWindowRect(Handle, out var window);
        var outerWidth = physicalWidth + (window.Width - client.Width);
        var outerHeight = physicalHeight + (window.Height - client.Height);
        if (!SetWindowPos(Handle, IntPtr.Zero, 0, 0, outerWidth, outerHeight, SwpNoZOrder | SwpShowWindow))
        {
            throw new InvalidOperationException("Windows could not size the FotoHAVN verification window.");
        }

        BringToForeground();
        _ = GetClientRect(Handle, out client);
        if (client.Width != physicalWidth || client.Height != physicalHeight)
        {
            throw new InvalidOperationException(
                $"Requested a {width}x{height} effective client ({physicalWidth}x{physicalHeight} physical), " +
                $"Windows produced {client.Width}x{client.Height}.");
        }
    }

    private static bool IsRunning(Process candidate)
    {
        try
        {
            return !candidate.HasExited;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return true;
        }
    }

    private void BringToForeground()
    {
        var foreground = GetForegroundWindow();
        var currentThread = GetCurrentThreadId();
        var foregroundThread = foreground == IntPtr.Zero
            ? 0
            : GetWindowThreadProcessId(foreground, IntPtr.Zero);
        var targetThread = GetWindowThreadProcessId(Handle, IntPtr.Zero);
        var attached = foregroundThread != 0 && foregroundThread != currentThread &&
            AttachThreadInput(currentThread, foregroundThread, true);
        var attachedToTarget = targetThread != 0 && targetThread != currentThread &&
            AttachThreadInput(currentThread, targetThread, true);
        try
        {
            for (var attempt = 0; attempt < 20 && GetForegroundWindow() != Handle; attempt++)
            {
                _ = BringWindowToTop(Handle);
                _ = SetForegroundWindow(Handle);
                Thread.Sleep(25);
            }
            _ = SetFocus(Handle);
        }
        finally
        {
            if (attached)
            {
                _ = AttachThreadInput(currentThread, foregroundThread, false);
            }
            if (attachedToTarget)
            {
                _ = AttachThreadInput(currentThread, targetThread, false);
            }
        }
    }

    public void CaptureClient(string outputPath)
    {
        _ = GetClientRect(Handle, out var client);
        var origin = new Point();
        if (!ClientToScreen(Handle, ref origin))
        {
            throw new InvalidOperationException("Windows could not locate the FotoHAVN client area.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        using var physical = new Bitmap(client.Width, client.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(physical))
        {
            var deviceContext = graphics.GetHdc();
            try
            {
                if (!PrintWindow(Handle, deviceContext, PrintWindowClientOnly | PrintWindowRenderFullContent))
                {
                    throw new InvalidOperationException("Windows could not render the FotoHAVN client area.");
                }
            }
            finally
            {
                graphics.ReleaseHdc(deviceContext);
            }
        }

        if (physical.Width == effectiveWidth && physical.Height == effectiveHeight)
        {
            physical.Save(outputPath, ImageFormat.Png);
            return;
        }

        using var effective = new Bitmap(effectiveWidth, effectiveHeight, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(effective))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(physical, new Rectangle(0, 0, effectiveWidth, effectiveHeight));
        }

        effective.Save(outputPath, ImageFormat.Png);
    }

    public (int X, int Y) GetClientOrigin()
    {
        var origin = new Point();
        if (!ClientToScreen(Handle, ref origin))
        {
            throw new InvalidOperationException("Windows could not locate the FotoHAVN client area.");
        }

        return (origin.X, origin.Y);
    }

    public void Dispose()
    {
        if (!process.HasExited)
        {
            _ = process.CloseMainWindow();
            if (!process.WaitForExit(3000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }

        process.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpShowWindow = 0x0040;
    private const uint PrintWindowClientOnly = 0x00000001;
    private const uint PrintWindowRenderFullContent = 0x00000002;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr window, out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr window, ref Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint attachThread, uint attachToThread, bool attach);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr window, IntPtr deviceContext, uint flags);
}
