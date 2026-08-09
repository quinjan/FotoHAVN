using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace FotoHavn.App;

internal static class WindowPlacement
{
    private const double DefaultDpi = 96d;

    public static RectInt32 ForWindowClientArea(
        IntPtr windowHandle,
        double effectiveClientWidth,
        double effectiveClientHeight,
        RectInt32 workArea)
    {
        if (!GetWindowRect(windowHandle, out var windowRectangle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (!GetClientRect(windowHandle, out var clientRectangle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var dpi = GetDpiForWindow(windowHandle);
        return ForClientArea(
            effectiveClientWidth,
            effectiveClientHeight,
            dpi == 0 ? checked((uint)DefaultDpi) : dpi,
            windowRectangle.Width - clientRectangle.Width,
            windowRectangle.Height - clientRectangle.Height,
            workArea);
    }

    public static RectInt32 ForClientArea(
        double effectiveClientWidth,
        double effectiveClientHeight,
        uint dpi,
        int nonClientWidth,
        int nonClientHeight,
        RectInt32 workArea)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(effectiveClientWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(effectiveClientHeight);
        ArgumentOutOfRangeException.ThrowIfZero(dpi);
        ArgumentOutOfRangeException.ThrowIfNegative(nonClientWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(nonClientHeight);

        var scale = dpi / DefaultDpi;
        var physicalClientWidth = checked((int)Math.Round(effectiveClientWidth * scale));
        var physicalClientHeight = checked((int)Math.Round(effectiveClientHeight * scale));
        var physicalWindowWidth = checked(physicalClientWidth + nonClientWidth);
        var physicalWindowHeight = checked(physicalClientHeight + nonClientHeight);
        var x = workArea.X + ((workArea.Width - physicalWindowWidth) / 2);
        var y = workArea.Y + ((workArea.Height - physicalWindowHeight) / 2);
        return new RectInt32(x, y, physicalWindowWidth, physicalWindowHeight);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRectangle rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr windowHandle, out NativeRectangle rectangle);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr windowHandle);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;

        public readonly int Height => Bottom - Top;
    }
}
