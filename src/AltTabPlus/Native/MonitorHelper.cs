using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AltTabPlus.Native;

internal static class MonitorHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public NativeMethods.RECT rcMonitor;
        public NativeMethods.RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    /// <summary>
    /// Work area (screen minus taskbar) for the monitor a window lives on.
    /// Falls back to the primary screen if the handle is invalid, which only
    /// matters on the very unlikely path where a window's monitor disappeared
    /// between enumeration and this call (e.g. a monitor was unplugged).
    /// </summary>
    public static Rectangle GetWorkArea(IntPtr monitor)
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
        {
            return info.rcWork.ToRectangle();
        }

        return Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
    }
}
