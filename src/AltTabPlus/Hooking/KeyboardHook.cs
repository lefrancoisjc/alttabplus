using System.Diagnostics;
using System.Runtime.InteropServices;
using AltTabPlus.Native;

namespace AltTabPlus.Hooking;

/// <summary>
/// Installs a low-level keyboard hook and takes over Alt+Tab entirely: every
/// Tab press while Alt is held is swallowed here (CallNextHookEx is skipped
/// for it), so Windows' own switcher never sees the keystroke and never pops
/// up. This is the same mechanism tools like AltTabTerminator or GoToWindow
/// use — there is no "polite" public API for suppressing the native switcher.
///
/// Must be installed from a thread that pumps Windows messages
/// (i.e. the thread running Application.Run), because WH_KEYBOARD_LL
/// callbacks are dispatched through that thread's message queue.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _altDown;
    private bool _switcherActive;

    /// <summary>Raised on the first Tab-while-Alt-held press: build the target list and show the overlay.</summary>
    public event Action? SwitcherOpenRequested;

    /// <summary>Raised on every subsequent Tab press while the overlay is open. Argument: Shift held (reverse direction).</summary>
    public event Action<bool>? StepRequested;

    /// <summary>Raised when Alt is released: activate the current selection and close the overlay.</summary>
    public event Action? SwitcherCommitRequested;

    /// <summary>Raised on Escape while the overlay is open: close without switching.</summary>
    public event Action? SwitcherCancelRequested;

    public KeyboardHook()
    {
        // Keep a reference to the delegate for the hook's whole lifetime —
        // otherwise the GC can collect it and Windows ends up calling into
        // freed/reused memory the next time a key is pressed.
        _proc = HookCallback;
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

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            var vk = (int)data.vkCode;
            var isDown = wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN;
            var isUp = wParam == NativeMethods.WM_KEYUP || wParam == NativeMethods.WM_SYSKEYUP;

            if (vk is NativeMethods.VK_MENU or NativeMethods.VK_LMENU or NativeMethods.VK_RMENU)
            {
                if (isDown)
                {
                    _altDown = true;
                }
                else if (isUp && _altDown)
                {
                    _altDown = false;
                    if (_switcherActive)
                    {
                        _switcherActive = false;
                        SwitcherCommitRequested?.Invoke();
                    }
                }
            }
            else if (vk == NativeMethods.VK_ESCAPE && isDown && _switcherActive)
            {
                _switcherActive = false;
                SwitcherCancelRequested?.Invoke();
                return (IntPtr)1;
            }
            else if (vk == NativeMethods.VK_TAB && _altDown && isDown)
            {
                var shiftHeld = (NativeMethods.GetKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;

                if (!_switcherActive)
                {
                    _switcherActive = true;
                    SwitcherOpenRequested?.Invoke();
                }
                else
                {
                    StepRequested?.Invoke(shiftHeld);
                }

                // Swallow it: this is the line that actually stops the OS
                // switcher from ever appearing.
                return (IntPtr)1;
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }
}
