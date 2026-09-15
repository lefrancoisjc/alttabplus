using System.Text;
using AltTabPlus.Native;

namespace AltTabPlus.Windows;

/// <summary>
/// Enumerates the windows Windows' own Alt-Tab would show. The filtering
/// rules here mirror (reverse-engineered, since none of this is officially
/// documented) what the native switcher actually applies: visible top-level
/// windows, no owner, not a tool window, and not "cloaked" (DWMWA_CLOAKED —
/// true for windows parked on another virtual desktop or suspended UWP
/// placeholder windows).
/// </summary>
internal static class WindowEnumerator
{
    public static List<WindowInfo> GetAltTabWindows()
    {
        var result = new List<WindowInfo>();
        var selfPid = (uint)Environment.ProcessId;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!IsAltTabWindow(hWnd, selfPid, out var title))
            {
                return true; // keep enumerating
            }

            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            NativeMethods.GetWindowRect(hWnd, out var rect);
            var monitor = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST);

            result.Add(new WindowInfo
            {
                Handle = hWnd,
                Title = title,
                ProcessId = pid,
                Bounds = rect.ToRectangle(),
                MonitorHandle = monitor,
            });

            return true;
        }, IntPtr.Zero);

        return result;
    }

    private static bool IsAltTabWindow(IntPtr hWnd, uint selfPid, out string title)
    {
        title = string.Empty;

        if (!NativeMethods.IsWindowVisible(hWnd))
        {
            return false;
        }

        // Windows owned by another window (dialogs, tool palettes, ...)
        // don't get their own Alt-Tab entry.
        if (NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero)
        {
            return false;
        }

        var exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
        if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }

        // Cloaked = invisible-but-technically-a-window. Covers windows sitting
        // on a different virtual desktop and the hidden placeholder windows
        // some UWP/packaged apps keep around.
        if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0
            && cloaked != 0)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
        if (pid == selfPid)
        {
            return false; // never let the switcher list itself
        }

        title = GetTitle(hWnd);
        return !string.IsNullOrWhiteSpace(title);
    }

    private static string GetTitle(IntPtr hWnd)
    {
        var length = NativeMethods.GetWindowTextLength(hWnd);
        if (length == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
