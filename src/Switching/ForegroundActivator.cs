using AltTabPlus.Diagnostics;
using AltTabPlus.Native;

namespace AltTabPlus.Switching;

internal static class ForegroundActivator
{
    private const uint SnapSafeZOrder =
        NativeMethods.SWP_NOMOVE
        | NativeMethods.SWP_NOSIZE
        | NativeMethods.SWP_NOACTIVATE
        | NativeMethods.SWP_NOSENDCHANGING
        | NativeMethods.SWP_NOREDRAW;

    public static bool ActivateMany(IReadOnlyList<IntPtr> handles)
    {
        var windows = handles.Where(h => h != IntPtr.Zero && NativeMethods.IsWindow(h)).ToList();
        if (windows.Count == 0)
        {
            return false;
        }

        using var attachment = InputAttachment.To(windows);
        NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);

        foreach (var hwnd in windows)
        {
            RestoreIfHidden(hwnd);
        }

        if (windows.Count > 1)
        {
            RaiseGroup(windows);
        }

        var ok = NativeMethods.SetForegroundWindow(windows[0]);
        Log.Write(
            $"ForegroundActivator: focus=0x{windows[0]:X} group=[{string.Join(",", windows.Select(h => $"0x{h:X}"))}] "
            + $"SetForegroundWindow={ok}");
        return ok;
    }

    private static void RaiseGroup(IReadOnlyList<IntPtr> windows)
    {
        var hdwp = NativeMethods.BeginDeferWindowPos(windows.Count);
        if (hdwp != IntPtr.Zero)
        {
            for (var i = windows.Count - 1; i >= 0; i--)
            {
                hdwp = NativeMethods.DeferWindowPos(
                    hdwp,
                    windows[i],
                    NativeMethods.HWND_TOP,
                    0, 0, 0, 0,
                    FlagsFor(windows[i]));

                if (hdwp == IntPtr.Zero)
                {
                    break;
                }
            }

            if (hdwp != IntPtr.Zero)
            {
                NativeMethods.EndDeferWindowPos(hdwp);
                return;
            }
        }

        for (var i = windows.Count - 1; i >= 0; i--)
        {
            NativeMethods.SetWindowPos(
                windows[i],
                NativeMethods.HWND_TOP,
                0, 0, 0, 0,
                FlagsFor(windows[i]));
        }
    }

    private static void RestoreIfHidden(IntPtr hWnd)
    {
        if (!NativeMethods.IsIconic(hWnd) && NativeMethods.IsWindowVisible(hWnd))
        {
            return;
        }

        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
        Log.Write($"ForegroundActivator: restore 0x{hWnd:X}");
    }

    private static uint FlagsFor(IntPtr hWnd)
    {
        var flags = SnapSafeZOrder;
        if (NativeMethods.IsIconic(hWnd) || !NativeMethods.IsWindowVisible(hWnd))
        {
            flags |= NativeMethods.SWP_SHOWWINDOW;
        }

        return flags;
    }

    private sealed class InputAttachment : IDisposable
    {
        private readonly uint _currentThreadId;
        private readonly List<uint> _attached = new();

        private InputAttachment(uint currentThreadId)
        {
            _currentThreadId = currentThreadId;
        }

        public static InputAttachment To(IReadOnlyList<IntPtr> handles)
        {
            var currentThreadId = NativeMethods.GetCurrentThreadId();
            var attachment = new InputAttachment(currentThreadId);
            var seen = new HashSet<uint> { currentThreadId };

            void TryAttach(IntPtr hwnd)
            {
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                var tid = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
                if (!seen.Add(tid))
                {
                    return;
                }

                if (NativeMethods.AttachThreadInput(currentThreadId, tid, true))
                {
                    attachment._attached.Add(tid);
                }
            }

            TryAttach(NativeMethods.GetForegroundWindow());
            foreach (var handle in handles)
            {
                TryAttach(handle);
            }

            return attachment;
        }

        public void Dispose()
        {
            foreach (var tid in _attached)
            {
                NativeMethods.AttachThreadInput(_currentThreadId, tid, false);
            }
        }
    }
}
