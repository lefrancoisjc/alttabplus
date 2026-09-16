using AltTabPlus.Native;

namespace AltTabPlus.UI;

internal static class WindowIcon
{
    public static Icon? TryGet(IntPtr hwnd)
    {
        var handle = NativeMethods.SendMessage(hwnd, NativeMethods.WM_GETICON, (IntPtr)NativeMethods.ICON_SMALL2, IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            handle = NativeMethods.SendMessage(hwnd, NativeMethods.WM_GETICON, (IntPtr)NativeMethods.ICON_SMALL, IntPtr.Zero);
        }

        if (handle == IntPtr.Zero)
        {
            handle = NativeMethods.GetClassLongPtr(hwnd, NativeMethods.GCLP_HICONSM);
        }

        if (handle == IntPtr.Zero)
        {
            handle = NativeMethods.GetClassLongPtr(hwnd, NativeMethods.GCLP_HICON);
        }

        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        catch
        {
            return null;
        }
    }
}
