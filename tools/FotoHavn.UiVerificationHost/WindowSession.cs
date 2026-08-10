using System.ComponentModel;
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
        if (Process.GetProcessesByName(processName).Any(IsBlockingInstance))
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

    private static bool IsBlockingInstance(Process candidate)
    {
        try
        {
            return !candidate.HasExited && candidate.MainWindowHandle != IntPtr.Zero;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            // A terminated App SDK process can remain briefly discoverable while its
            // protected process record is being reclaimed. It cannot own a usable
            // verification window and must not poison subsequent fixture runs.
            return false;
        }
        finally
        {
            candidate.Dispose();
        }
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
        if (!SetWindowPos(Handle, HwndTopmost, 0, 0, outerWidth, outerHeight, SwpShowWindow))
        {
            throw new InvalidOperationException(
                $"Windows could not size the FotoHAVN verification window (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        Activate();
        _ = GetClientRect(Handle, out client);
        if (client.Width != physicalWidth || client.Height != physicalHeight)
        {
            throw new InvalidOperationException(
                $"Requested a {width}x{height} effective client ({physicalWidth}x{physicalHeight} physical), " +
                $"Windows produced {client.Width}x{client.Height}.");
        }
    }

    public void Activate()
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
        try
        {
            using var graphics = Graphics.FromImage(physical);
            graphics.CopyFromScreen(origin.X, origin.Y, 0, 0, physical.Size, CopyPixelOperation.SourceCopy);
        }
        catch (Win32Exception)
        {
            CopyClientWithPrintWindow(physical);
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

    private void CopyClientWithPrintWindow(Bitmap target)
    {
        using var graphics = Graphics.FromImage(target);
        var deviceContext = graphics.GetHdc();
        try
        {
            if (!PrintWindow(Handle, deviceContext, PwClientOnly | PwRenderFullContent))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Windows could not capture the FotoHAVN client window.");
            }
        }
        finally
        {
            graphics.ReleaseHdc(deviceContext);
        }
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

    private const uint SwpShowWindow = 0x0040;
    private const uint PwClientOnly = 0x00000001;
    private const uint PwRenderFullContent = 0x00000002;
    private static readonly IntPtr HwndTopmost = new(-1);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr window, out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr window, ref Point point);

    [DllImport("user32.dll", SetLastError = true)]
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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr window, IntPtr deviceContext, uint flags);

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

}
