using System.Runtime.InteropServices;
using AltTabPlus.Native;

namespace AltTabPlus.Windows;

internal static class VirtualDesktop
{
    public static readonly Guid UnknownOther = new("ffffffff-ffff-ffff-ffff-ffffffffffff");

    private static readonly IVirtualDesktopManager? Manager = Create();

    public static Guid CurrentId()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        var id = GetId(foreground);
        if (id != Guid.Empty)
        {
            return id;
        }

        return Guid.Empty;
    }

    public static Guid GetId(IntPtr hwnd)
    {
        if (Manager is null || hwnd == IntPtr.Zero)
        {
            return Guid.Empty;
        }

        try
        {
            var hr = Manager.GetWindowDesktopId(hwnd, out var id);
            return hr == 0 ? id : Guid.Empty;
        }
        catch
        {
            return Guid.Empty;
        }
    }

    public static bool IsCloaked(IntPtr hwnd, out int flags)
    {
        flags = 0;
        return NativeMethods.DwmGetWindowAttribute(
                hwnd, NativeMethods.DWMWA_CLOAKED, out flags, sizeof(int)) == 0
            && flags != 0;
    }

    public static Guid Bucket(Guid desktopId, bool onOtherDesktop)
    {
        if (desktopId != Guid.Empty)
        {
            return desktopId;
        }

        return onOtherDesktop ? UnknownOther : Guid.Empty;
    }

    private static IVirtualDesktopManager? Create()
    {
        try
        {
            return (IVirtualDesktopManager)new CVirtualDesktopManager();
        }
        catch
        {
            return null;
        }
    }

    [ComImport]
    [Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
    private class CVirtualDesktopManager
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
    private interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out int onCurrentDesktop);

        [PreserveSig]
        int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

        [PreserveSig]
        int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }
}
