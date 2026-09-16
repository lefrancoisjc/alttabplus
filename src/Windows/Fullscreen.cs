using AltTabPlus.Native;

namespace AltTabPlus.Windows;

internal static class Fullscreen
{
    public static bool BlocksHotCorners()
    {
        return NativeMethods.SHQueryUserNotificationState(out var state) == 0
            && state == NativeMethods.QUNS_RUNNING_D3D_FULL_SCREEN;
    }
}
