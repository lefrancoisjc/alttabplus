using System.Drawing;
using AltTabPlus.Diagnostics;
using AltTabPlus.Native;

namespace AltTabPlus.UI;

internal sealed class DwmThumbnail : IDisposable
{
    private readonly IntPtr _sourceHandle;
    private IntPtr _thumbnailId = IntPtr.Zero;

    public DwmThumbnail(IntPtr destinationHandle, IntPtr sourceHandle, Rectangle destRect)
    {
        _sourceHandle = sourceHandle;

        var hr = NativeMethods.DwmRegisterThumbnail(destinationHandle, sourceHandle, out _thumbnailId);
        Log.Write($"DwmThumbnail: register dest=0x{destinationHandle:X} source=0x{sourceHandle:X} hr=0x{hr:X8}");

        if (hr != 0)
        {
            _thumbnailId = IntPtr.Zero;
            return;
        }

        UpdateDestination(destRect);
    }

    public void UpdateDestination(Rectangle destRect)
    {
        if (_thumbnailId == IntPtr.Zero)
        {
            return;
        }

        var dest = ToRect(destRect);
        if (NativeMethods.DwmQueryThumbnailSourceSize(_thumbnailId, out var src) == 0
            && src.cx > 0 && src.cy > 0)
        {
            dest = Letterbox(dest, src.cx, src.cy);
        }

        var props = new NativeMethods.DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = NativeMethods.DWM_TNP_VISIBLE
                | NativeMethods.DWM_TNP_RECTDESTINATION
                | NativeMethods.DWM_TNP_OPACITY,
            fVisible = destRect.Width > 1 && destRect.Height > 1,
            opacity = 255,
            rcDestination = dest,
        };

        var hr = NativeMethods.DwmUpdateThumbnailProperties(_thumbnailId, ref props);
        if (hr != 0)
        {
            Log.Write($"DwmThumbnail: update source=0x{_sourceHandle:X} hr=0x{hr:X8} dest={destRect}");
        }
    }

    public void Dispose()
    {
        if (_thumbnailId != IntPtr.Zero)
        {
            NativeMethods.DwmUnregisterThumbnail(_thumbnailId);
            _thumbnailId = IntPtr.Zero;
        }
    }

    private static NativeMethods.RECT ToRect(Rectangle r) =>
        new(r.Left, r.Top, r.Right, r.Bottom);

    private static NativeMethods.RECT Letterbox(NativeMethods.RECT dest, int srcW, int srcH)
    {
        var destW = dest.Right - dest.Left;
        var destH = dest.Bottom - dest.Top;
        if (destW <= 0 || destH <= 0)
        {
            return dest;
        }

        var destAspect = (float)destW / destH;
        var srcAspect = (float)srcW / srcH;
        if (srcAspect > destAspect)
        {
            var h = Math.Max(1, (int)(destW / srcAspect));
            var y = dest.Top + (destH - h) / 2;
            return new NativeMethods.RECT(dest.Left, y, dest.Right, y + h);
        }

        var w = Math.Max(1, (int)(destH * srcAspect));
        var x = dest.Left + (destW - w) / 2;
        return new NativeMethods.RECT(x, dest.Top, x + w, dest.Bottom);
    }
}
