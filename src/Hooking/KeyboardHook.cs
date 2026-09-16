using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AltTabPlus.Native;
using AltTabPlus.Settings;

namespace AltTabPlus.Hooking;

internal sealed class KeyboardHook : IDisposable
{
    private const int CustomHotkeyId = 0xA7B1;

    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private readonly HotkeyNativeWindow _hotkeyWindow;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _altDown;
    private bool _switcherActive;
    private bool _commitOnAltRelease;

    public bool ReplaceAltTab { get; set; } = true;

    public event Action? SwitcherOpenRequested;
    public event Action<bool>? StepRequested;
    public event Action<int, int>? MoveRequested;
    public event Action? SwitcherCommitRequested;
    public event Action? BackRequested;
    public event Action? ExpandRequested;
    public event Action? CloseRequested;
    public event Action<char>? FilterCharRequested;
    public event Action? FilterBackspaceRequested;
    public event Action<bool>? SameAppCycleRequested;

    public KeyboardHook()
    {
        _proc = HookCallback;
        _hotkeyWindow = new HotkeyNativeWindow();
        _hotkeyWindow.HotkeyPressed += OnCustomHotkey;
    }

    public void Install()
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _proc,
            NativeMethods.GetModuleHandle(curModule.ModuleName),
            0);

        if (_hookId == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"SetWindowsHookEx failed (Win32 error {Marshal.GetLastWin32Error()}). " +
                "Some security software blocks global keyboard hooks — check its logs if this happens.");
        }
    }

    public bool ApplyCustomHotkey(AppSettings settings)
    {
        NativeMethods.UnregisterHotKey(_hotkeyWindow.Handle, CustomHotkeyId);
        if (!settings.HasCustomHotkey)
        {
            return true;
        }

        var mods = (uint)settings.HotkeyModifiers | NativeMethods.MOD_NOREPEAT;
        return NativeMethods.RegisterHotKey(_hotkeyWindow.Handle, CustomHotkeyId, mods, (uint)settings.HotkeyKey);
    }

    public void NotifyOpened(bool commitOnAltRelease = false)
    {
        _switcherActive = true;
        _commitOnAltRelease = commitOnAltRelease;
    }

    public void NotifyClosed()
    {
        _switcherActive = false;
        _commitOnAltRelease = false;
    }

    private void OnCustomHotkey()
    {
        if (!_switcherActive)
        {
            _switcherActive = true;
            _commitOnAltRelease = false;
            SwitcherOpenRequested?.Invoke();
        }
        else
        {
            StepRequested?.Invoke(false);
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
        var vk = (int)data.vkCode;
        var isDown = wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN;
        var isUp = wParam == NativeMethods.WM_KEYUP || wParam == NativeMethods.WM_SYSKEYUP;
        var shiftHeld = (NativeMethods.GetKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;

        if (vk is NativeMethods.VK_MENU or NativeMethods.VK_LMENU or NativeMethods.VK_RMENU)
        {
            if (isDown)
            {
                _altDown = true;
            }
            else if (isUp && _altDown)
            {
                _altDown = false;
                if (_switcherActive && _commitOnAltRelease)
                {
                    _switcherActive = false;
                    _commitOnAltRelease = false;
                    SwitcherCommitRequested?.Invoke();
                }
            }
        }
        else if (isDown && _altDown && IsSameAppKey(vk))
        {
            SameAppCycleRequested?.Invoke(shiftHeld);
            return (IntPtr)1;
        }
        else if (vk == NativeMethods.VK_ESCAPE && isDown && _switcherActive)
        {
            BackRequested?.Invoke();
            return (IntPtr)1;
        }
        else if (vk == NativeMethods.VK_RETURN && isDown && _switcherActive)
        {
            _switcherActive = false;
            _commitOnAltRelease = false;
            SwitcherCommitRequested?.Invoke();
            return (IntPtr)1;
        }
        else if (vk == NativeMethods.VK_TAB && isDown && ReplaceAltTab && _altDown)
        {
            if (!_switcherActive)
            {
                _switcherActive = true;
                _commitOnAltRelease = true;
                SwitcherOpenRequested?.Invoke();
            }
            else
            {
                StepRequested?.Invoke(shiftHeld);
            }

            return (IntPtr)1;
        }
        else if (vk == NativeMethods.VK_TAB && isDown && _switcherActive)
        {
            StepRequested?.Invoke(shiftHeld);
            return (IntPtr)1;
        }
        else if (_switcherActive && vk == NativeMethods.VK_SPACE)
        {
            if (isDown)
            {
                ExpandRequested?.Invoke();
            }

            return (IntPtr)1;
        }
        else if (_switcherActive && isDown && vk == NativeMethods.VK_DELETE)
        {
            CloseRequested?.Invoke();
            return (IntPtr)1;
        }
        else if (_switcherActive && isDown && vk == NativeMethods.VK_BACK)
        {
            FilterBackspaceRequested?.Invoke();
            return (IntPtr)1;
        }
        else if (_switcherActive && isDown && TryArrowDelta(vk, out var dx, out var dy))
        {
            MoveRequested?.Invoke(dx, dy);
            return (IntPtr)1;
        }
        else if (_switcherActive && TryFilterChar(vk, data.scanCode, out var ch))
        {
            if (isDown)
            {
                _commitOnAltRelease = false;
                FilterCharRequested?.Invoke(ch);
            }

            return (IntPtr)1;
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static bool IsSameAppKey(int vk) =>
        vk is NativeMethods.VK_OEM_3 or NativeMethods.VK_OEM_7;

    private static bool TryFilterChar(int vk, uint scanCode, out char ch)
    {
        ch = '\0';
        if (vk is NativeMethods.VK_SPACE or NativeMethods.VK_TAB or NativeMethods.VK_RETURN
            or NativeMethods.VK_ESCAPE or NativeMethods.VK_BACK or NativeMethods.VK_DELETE
            or NativeMethods.VK_SHIFT or NativeMethods.VK_CONTROL or NativeMethods.VK_MENU
            or NativeMethods.VK_LMENU or NativeMethods.VK_RMENU or NativeMethods.VK_LWIN
            or NativeMethods.VK_RWIN or NativeMethods.VK_CAPITAL
            || vk is >= 0x21 and <= 0x28
            || vk is >= 0x70 and <= 0x87)
        {
            return false;
        }

        var state = new byte[256];
        if ((NativeMethods.GetKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0)
        {
            state[NativeMethods.VK_SHIFT] = 0x80;
        }

        if ((NativeMethods.GetKeyState(NativeMethods.VK_CAPITAL) & 1) != 0)
        {
            state[NativeMethods.VK_CAPITAL] = 1;
        }

        var buffer = new StringBuilder(8);
        var result = NativeMethods.ToUnicode((uint)vk, scanCode, state, buffer, buffer.Capacity, 4);
        if (result > 0 && buffer.Length > 0 && !char.IsControl(buffer[0]))
        {
            ch = buffer[0];
            return true;
        }

        if (vk is >= 0x41 and <= 0x5A)
        {
            ch = char.ToLowerInvariant((char)vk);
            return true;
        }

        if (vk is >= 0x30 and <= 0x39)
        {
            ch = (char)vk;
            return true;
        }

        return false;
    }

    private static bool TryArrowDelta(int vk, out int dx, out int dy)
    {
        switch (vk)
        {
            case NativeMethods.VK_LEFT:
                dx = -1;
                dy = 0;
                return true;
            case NativeMethods.VK_RIGHT:
                dx = 1;
                dy = 0;
                return true;
            case NativeMethods.VK_UP:
                dx = 0;
                dy = -1;
                return true;
            case NativeMethods.VK_DOWN:
                dx = 0;
                dy = 1;
                return true;
            default:
                dx = 0;
                dy = 0;
                return false;
        }
    }

    public void Dispose()
    {
        NativeMethods.UnregisterHotKey(_hotkeyWindow.Handle, CustomHotkeyId);
        _hotkeyWindow.Dispose();
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private sealed class HotkeyNativeWindow : NativeWindow, IDisposable
    {
        public event Action? HotkeyPressed;

        public HotkeyNativeWindow()
        {
            CreateHandle(new CreateParams { Caption = "AltTabPlusHotkey" });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                HotkeyPressed?.Invoke();
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                DestroyHandle();
            }
        }
    }
}
