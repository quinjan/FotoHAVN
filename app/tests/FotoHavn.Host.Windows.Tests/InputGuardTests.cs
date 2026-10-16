using FotoHavn.Host.Windows.Hardening;
using FotoHavn.Host.Windows.Interop;

namespace FotoHavn.Host.Windows.Tests;

/// <summary>The Guest window's input guard (M10): which chords and window commands are refused, and when the guard is on.</summary>
public sealed class InputGuardTests
{
    private static readonly IntPtr GuestWindow = new(0x1234);
    private static readonly IntPtr OtherWindow = new(0x5678);

    private static KeyboardEvent Key(int virtualKey, bool alt = false, bool down = true) => new(virtualKey, alt, down);

    [Theory]
    [InlineData(NativeMethods.VkF4, true, false, false, true)] // Alt+F4
    [InlineData(NativeMethods.VkSpace, true, false, false, true)] // Alt+Space: the system menu with move and size
    [InlineData(NativeMethods.VkTab, true, false, false, true)] // Alt+Tab
    [InlineData(NativeMethods.VkEscape, true, false, false, true)] // Alt+Esc
    [InlineData(NativeMethods.VkEscape, false, true, false, true)] // Ctrl+Esc opens Start
    [InlineData(NativeMethods.VkLWin, false, false, false, true)] // Win
    [InlineData(NativeMethods.VkRWin, false, false, false, true)]
    [InlineData(NativeMethods.VkEscape, false, true, true, false)] // Ctrl+Shift+Esc (Task Manager) stays available
    [InlineData(NativeMethods.VkEscape, true, true, true, true)] // Ctrl+Shift+Alt+Esc is still Alt+Esc
    [InlineData(NativeMethods.VkF4, false, false, false, false)]
    [InlineData(NativeMethods.VkSpace, false, false, false, false)]
    [InlineData(NativeMethods.VkTab, false, false, false, false)]
    [InlineData(NativeMethods.VkEscape, false, false, false, false)]
    [InlineData(NativeMethods.VkReturn, true, false, false, false)] // Alt+Enter is not a window shortcut here
    [InlineData(0x43, false, true, false, false)] // Ctrl+C
    public void The_policy_blocks_the_chords_that_close_move_or_leave_the_window(int virtualKey, bool alt, bool ctrl, bool shift, bool blocked)
    {
        Assert.Equal(blocked, InputGuardPolicy.BlocksKey(Key(virtualKey, alt), ctrl, shift));
        Assert.Equal(blocked, InputGuardPolicy.BlocksKey(Key(virtualKey, alt, down: false), ctrl, shift));
    }

    [Theory]
    [InlineData(0xF000, true)] // SC_SIZE
    [InlineData(0xF012, true)] // SC_MOVE with the low bits Windows sets for a mouse drag
    [InlineData(0xF020, true)] // SC_MINIMIZE
    [InlineData(0xF030, true)] // SC_MAXIMIZE
    [InlineData(0xF040, true)] // SC_NEXTWINDOW
    [InlineData(0xF050, true)] // SC_PREVWINDOW
    [InlineData(0xF100, true)] // SC_KEYMENU
    [InlineData(0xF120, true)] // SC_RESTORE
    [InlineData(0xF060, false)] // SC_CLOSE: the closing handler refuses it
    [InlineData(0xF140, false)] // SC_SCREENSAVE
    [InlineData(0x0010, false)]
    public void The_policy_blocks_the_system_commands_that_move_size_or_switch_windows(int command, bool blocked)
    {
        Assert.Equal(blocked, InputGuardPolicy.BlocksSystemCommand(command));
    }

    private sealed class FakeHook : IKeyboardHook
    {
        public int Installs { get; private set; }

        public int Uninstalls { get; private set; }

        public bool Installed { get; private set; }

        public bool Disposed { get; private set; }

        public bool Fails { get; init; }

        public int LastError => Fails ? 1428 : 0;

        public Func<KeyboardEvent, bool>? Swallow { get; private set; }

        public bool Install(Func<KeyboardEvent, bool> swallow)
        {
            if (Fails)
            {
                return false;
            }

            Installs++;
            Installed = true;
            Swallow = swallow;
            return true;
        }

        public void Uninstall()
        {
            Uninstalls++;
            Installed = false;
            Swallow = null;
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class Situation
    {
        public bool EventActive { get; set; }

        public IntPtr Foreground { get; set; } = GuestWindow;

        public HashSet<int> KeysDown { get; } = [];

        public FakeHook Hook { get; init; } = new();

        public GuestInputGuard Guard => field ??= new GuestInputGuard(Hook, () => EventActive, () => Foreground, KeysDown.Contains);
    }

    [Fact]
    public void Nothing_is_blocked_with_no_Guest_window_or_no_Event()
    {
        var situation = new Situation { EventActive = true };
        Assert.False(situation.Guard.IsActive);
        Assert.False(situation.Guard.ShouldSwallow(Key(NativeMethods.VkF4, alt: true)));
        Assert.False(situation.Guard.BlocksSystemCommand(0xF010));

        situation.Guard.Attach(GuestWindow);
        situation.EventActive = false;

        Assert.False(situation.Guard.IsActive);
        Assert.False(situation.Guard.ShouldSwallow(Key(NativeMethods.VkF4, alt: true)));
        Assert.False(situation.Guard.BlocksSystemCommand(0xF010));
        Assert.False(situation.Guard.BlocksKey(Key(NativeMethods.VkF4, alt: true), ctrlDown: false, shiftDown: false));
    }

    [Fact]
    public void During_an_Event_the_guard_swallows_the_chords_while_the_Guest_window_has_the_foreground()
    {
        var situation = new Situation { EventActive = true };
        Assert.True(situation.Guard.Attach(GuestWindow));

        Assert.True(situation.Guard.IsActive);
        Assert.True(situation.Guard.ShouldSwallow(Key(NativeMethods.VkF4, alt: true)));
        Assert.True(situation.Guard.ShouldSwallow(Key(NativeMethods.VkLWin)));
        Assert.False(situation.Guard.ShouldSwallow(Key(0x41)));
        Assert.True(situation.Guard.BlocksSystemCommand(0xF010));
        Assert.True(situation.Guard.BlocksKey(Key(NativeMethods.VkSpace, alt: true), ctrlDown: false, shiftDown: false));
        Assert.True(situation.Hook.Swallow!(Key(NativeMethods.VkTab, alt: true)));
    }

    [Fact]
    public void The_keys_of_another_window_in_the_foreground_are_left_alone()
    {
        var situation = new Situation { EventActive = true, Foreground = OtherWindow };
        situation.Guard.Attach(GuestWindow);

        Assert.False(situation.Guard.ShouldSwallow(Key(NativeMethods.VkF4, alt: true)));
        Assert.False(situation.Hook.Swallow!(Key(NativeMethods.VkLWin)));
    }

    [Fact]
    public void Control_and_shift_come_from_the_keyboard_state()
    {
        var situation = new Situation { EventActive = true };
        situation.Guard.Attach(GuestWindow);

        situation.KeysDown.Add(NativeMethods.VkControl);
        Assert.True(situation.Guard.ShouldSwallow(Key(NativeMethods.VkEscape)));

        situation.KeysDown.Add(NativeMethods.VkShift);
        Assert.False(situation.Guard.ShouldSwallow(Key(NativeMethods.VkEscape)));
    }

    [Fact]
    public void The_hook_is_installed_when_the_window_attaches_and_removed_when_it_detaches()
    {
        var situation = new Situation { EventActive = true };

        situation.Guard.Attach(GuestWindow);
        Assert.True(situation.Hook.Installed);
        Assert.Equal(1, situation.Hook.Installs);

        situation.Guard.Attach(OtherWindow);
        Assert.Equal(2, situation.Hook.Installs);
        Assert.True(situation.Hook.Installed);

        situation.Guard.Detach();
        Assert.False(situation.Hook.Installed);
        Assert.False(situation.Guard.IsActive);
        Assert.False(situation.Guard.ShouldSwallow(Key(NativeMethods.VkF4, alt: true)));
    }

    [Fact]
    public void Disposing_removes_the_hook_and_disposes_it()
    {
        var situation = new Situation { EventActive = true };
        situation.Guard.Attach(GuestWindow);

        situation.Guard.Dispose();

        Assert.False(situation.Hook.Installed);
        Assert.True(situation.Hook.Disposed);
    }

    [Fact]
    public void A_hook_that_cannot_be_installed_is_reported_and_the_window_commands_are_still_refused()
    {
        var situation = new Situation { EventActive = true, Hook = new FakeHook { Fails = true } };

        Assert.False(situation.Guard.Attach(GuestWindow));

        Assert.Equal(1428, situation.Guard.LastError);
        Assert.True(situation.Guard.BlocksSystemCommand(0xF010));
    }
}
