# Win32 window and input helper for the end-to-end specs (see win32.ts). Windows PowerShell 5.1: one operation per run, one JSON document on stdout.
# It targets windows by handle, never moves the mouse, and sends only the keys a spec names.
param(
  [Parameter(Mandatory = $true)]
  [ValidateSet('windows', 'foregroundWindow', 'foreground', 'sendAltF4', 'postClose', 'minimize')]
  [string]$Operation,
  [long]$ProcessId = 0,
  [long]$Handle = 0
)

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public static class FhWin32
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint attach, uint attachTo, bool attached);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion u; }

    private const uint InputKeyboard = 1;
    private const uint KeyUp = 0x0002;
    private const ushort VkMenu = 0x12;
    private const ushort VkF4 = 0x73;
    private const uint WmClose = 0x0010;

    public sealed class Info
    {
        public long Handle;
        public string Title;
        public bool Visible;
        public bool Minimized;
        public bool Foreground;
    }

    public static List<Info> Windows(uint processId)
    {
        var found = new List<Info>();
        var foreground = GetForegroundWindow();
        EnumWindows((hWnd, lParam) =>
        {
            uint owner;
            GetWindowThreadProcessId(hWnd, out owner);
            if (owner != processId)
            {
                return true;
            }

            var title = new StringBuilder(256);
            GetWindowText(hWnd, title, title.Capacity);
            var visible = IsWindowVisible(hWnd);
            if (title.Length > 0 && visible)
            {
                found.Add(new Info { Handle = hWnd.ToInt64(), Title = title.ToString(), Visible = visible, Minimized = IsIconic(hWnd), Foreground = hWnd == foreground });
            }

            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static long ForegroundHandle()
    {
        return GetForegroundWindow().ToInt64();
    }

    // Brings the window to the foreground. Windows lets only the foreground process do that, so the helper first attaches to the
    // foreground thread's input queue and, if that is not enough, taps Alt (which grants the right) before asking again.
    public static bool Foreground(IntPtr window)
    {
        if (IsIconic(window))
        {
            ShowWindow(window, 9);
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var current = GetForegroundWindow();
            uint ignored;
            var foregroundThread = GetWindowThreadProcessId(current, out ignored);
            var thisThread = GetCurrentThreadId();
            var attached = foregroundThread != 0 && foregroundThread != thisThread && AttachThreadInput(thisThread, foregroundThread, true);
            try
            {
                BringWindowToTop(window);
                SetForegroundWindow(window);
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(thisThread, foregroundThread, false);
                }
            }

            for (var wait = 0; wait < 15; wait++)
            {
                if (GetForegroundWindow() == window)
                {
                    return true;
                }

                Thread.Sleep(100);
            }

            keybd_event((byte)VkMenu, 0, 0, UIntPtr.Zero);
            keybd_event((byte)VkMenu, 0, KeyUp, UIntPtr.Zero);
        }

        return GetForegroundWindow() == window;
    }

    private static INPUT Key(ushort virtualKey, bool up)
    {
        var input = new INPUT { type = InputKeyboard };
        input.u.ki = new KEYBDINPUT { wVk = virtualKey, wScan = 0, dwFlags = up ? KeyUp : 0, time = 0, dwExtraInfo = IntPtr.Zero };
        return input;
    }

    // Alt down, F4 down, F4 up, Alt up through SendInput, to the given window. Nothing is sent unless it is still the foreground window,
    // so a window that took the foreground in between (another app on this machine) never receives the keys.
    public static bool SendAltF4(IntPtr window)
    {
        if (GetForegroundWindow() != window)
        {
            return false;
        }

        var steps = new[] { Key(VkMenu, false), Key(VkF4, false), Key(VkF4, true), Key(VkMenu, true) };
        foreach (var step in steps)
        {
            if (SendInput(1, new[] { step }, Marshal.SizeOf(typeof(INPUT))) != 1)
            {
                return false;
            }

            Thread.Sleep(40);
        }

        return true;
    }

    public static bool Close(IntPtr window)
    {
        return PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
    }

    public static bool Minimize(IntPtr window)
    {
        ShowWindow(window, 6);
        return IsIconic(window);
    }
}
'@

$window = [IntPtr]::new($Handle)

switch ($Operation) {
  'windows' {
    $items = @([FhWin32]::Windows([uint32]$ProcessId) | ForEach-Object {
        [pscustomobject]@{ handle = $_.Handle; title = $_.Title; visible = $_.Visible; minimized = $_.Minimized; foreground = $_.Foreground }
      })
    ConvertTo-Json -InputObject $items -Compress
  }
  'foregroundWindow' {
    ConvertTo-Json -InputObject ([pscustomobject]@{ handle = [FhWin32]::ForegroundHandle() }) -Compress
  }
  'foreground' {
    ConvertTo-Json -InputObject ([pscustomobject]@{ foreground = [FhWin32]::Foreground($window) }) -Compress
  }
  'sendAltF4' {
    $foreground = [FhWin32]::Foreground($window)
    $sent = $false
    if ($foreground) {
      $sent = [FhWin32]::SendAltF4($window)
    }
    ConvertTo-Json -InputObject ([pscustomobject]@{ foreground = $foreground; sent = $sent }) -Compress
  }
  'postClose' {
    ConvertTo-Json -InputObject ([pscustomobject]@{ posted = [FhWin32]::Close($window) }) -Compress
  }
  'minimize' {
    ConvertTo-Json -InputObject ([pscustomobject]@{ minimized = [FhWin32]::Minimize($window) }) -Compress
  }
}
