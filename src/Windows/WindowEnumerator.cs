using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using AltTabPlus.Native;

namespace AltTabPlus.Windows;

internal static class WindowEnumerator
{
    private static readonly HashSet<string> ShellProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "TextInputHost",
        "SearchHost",
        "StartMenuExperienceHost",
        "ShellExperienceHost",
        "LockApp",
        "CredentialUIBroker",
        "dwm",
        "sihost",
        "RuntimeBroker",
    };

    public static List<WindowInfo> GetAltTabWindows(WindowQuery? query = null)
    {
        query ??= new WindowQuery();
        var result = new List<WindowInfo>();
        var selfPid = (uint)Environment.ProcessId;
        var zOrder = 0;
        var currentDesktop = VirtualDesktop.CurrentId();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!IsAltTabWindow(hWnd, selfPid, query, currentDesktop, out var title, out var desktopId, out var onOther))
            {
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            var bounds = GetVisualBounds(hWnd);
            var monitor = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST);

            result.Add(new WindowInfo
            {
                Handle = hWnd,
                Title = title,
                ProcessName = GetProcessName(pid),
                ProcessId = pid,
                Bounds = bounds,
                MonitorHandle = monitor,
                ZOrder = zOrder++,
                IsMinimized = NativeMethods.IsIconic(hWnd),
                IsMaximized = NativeMethods.IsZoomed(hWnd),
                IsOnOtherDesktop = onOther,
                DesktopId = VirtualDesktop.Bucket(desktopId, onOther),
            });

            return true;
        }, IntPtr.Zero);

        return result;
    }

    private static bool IsAltTabWindow(
        IntPtr hWnd,
        uint selfPid,
        WindowQuery query,
        Guid currentDesktop,
        out string title,
        out Guid desktopId,
        out bool onOtherDesktop)
    {
        title = string.Empty;
        desktopId = VirtualDesktop.GetId(hWnd);
        var cloaked = VirtualDesktop.IsCloaked(hWnd, out var cloakFlags);
        onOtherDesktop = desktopId != Guid.Empty
            && currentDesktop != Guid.Empty
            && desktopId != currentDesktop;

        if (!onOtherDesktop && cloaked && query.IncludeOtherDesktops)
        {
            var shellCloak = (cloakFlags & NativeMethods.DWM_CLOAKED_SHELL) != 0;
            if (shellCloak || desktopId == Guid.Empty)
            {
                onOtherDesktop = true;
                if (desktopId == Guid.Empty)
                {
                    desktopId = VirtualDesktop.UnknownOther;
                }
            }
        }

        if (onOtherDesktop)
        {
            if (!query.IncludeOtherDesktops)
            {
                return false;
            }
        }
        else if (!NativeMethods.IsWindowVisible(hWnd) || cloaked)
        {
            return false;
        }

        if (!query.IncludeHidden && NativeMethods.IsIconic(hWnd))
        {
            return false;
        }

        if (NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero)
        {
            return false;
        }

        var exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
        if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
        if (pid == selfPid)
        {
            return false;
        }

        if (query.CurrentMonitorOnly
            && query.Monitor != IntPtr.Zero
            && NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST) != query.Monitor)
        {
            return false;
        }

        var processName = GetProcessName(pid);
        if (ShellProcesses.Contains(processName) || query.IgnoredProcesses.Contains(processName))
        {
            return false;
        }

        title = GetTitle(hWnd);
        return !string.IsNullOrWhiteSpace(title);
    }

    private static Rectangle GetVisualBounds(IntPtr hWnd)
    {
        if (NativeMethods.DwmGetWindowRectAttribute(
                hWnd,
                NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                out var rect,
                Marshal.SizeOf<NativeMethods.RECT>()) == 0)
        {
            return rect.ToRectangle();
        }

        NativeMethods.GetWindowRect(hWnd, out var fallback);
        return fallback.ToRectangle();
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

    private static string GetProcessName(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }
}
