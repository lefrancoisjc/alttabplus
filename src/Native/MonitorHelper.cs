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

    public static Rectangle GetWorkArea(IntPtr monitor)
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
        {
            return info.rcWork.ToRectangle();
        }

        return Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
    }

    public static Rectangle GetBounds(IntPtr monitor)
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
        {
            return info.rcMonitor.ToRectangle();
        }

        return Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
    }
}
