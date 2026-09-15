using AltTabPlus.Native;

namespace AltTabPlus.UI;

/// <summary>
/// A WinForms Panel that renders a live DWM thumbnail of another window on
/// top of itself. This is the same mechanism the taskbar's own hover
/// previews use (DwmRegisterThumbnail) — it draws directly via the
/// compositor, no bitmap copying involved.
/// </summary>
internal sealed class ThumbnailPanel : Panel
{
    private IntPtr _thumbnailId = IntPtr.Zero;
    private IntPtr _sourceHandle = IntPtr.Zero;

    public void SetSource(IntPtr sourceHwnd)
    {
        _sourceHandle = sourceHwnd;
        if (IsHandleCreated)
        {
            Register();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_sourceHandle != IntPtr.Zero)
        {
            Register();
        }
    }

    private void Register()
    {
        Unregister();

        if (Handle == IntPtr.Zero || _sourceHandle == IntPtr.Zero)
        {
            return;
        }

        var hr = NativeMethods.DwmRegisterThumbnail(Handle, _sourceHandle, out _thumbnailId);
        if (hr == 0) // S_OK
        {
            ApplyProperties();
        }
        else
        {
            _thumbnailId = IntPtr.Zero;
        }
    }

    private void ApplyProperties()
    {
        if (_thumbnailId == IntPtr.Zero)
        {
            return;
        }

        var props = new NativeMethods.DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = NativeMethods.DWM_TNP_VISIBLE
                | NativeMethods.DWM_TNP_RECTDESTINATION
                | NativeMethods.DWM_TNP_OPACITY
                | NativeMethods.DWM_TNP_SOURCECLIENTAREAONLY,
            fVisible = true,
            fSourceClientAreaOnly = true,
            opacity = 255,
            rcDestination = new NativeMethods.RECT(0, 0, Math.Max(Width, 1), Math.Max(Height, 1)),
        };

        NativeMethods.DwmUpdateThumbnailProperties(_thumbnailId, ref props);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyProperties();
    }

    private void Unregister()
    {
        if (_thumbnailId != IntPtr.Zero)
        {
            NativeMethods.DwmUnregisterThumbnail(_thumbnailId);
            _thumbnailId = IntPtr.Zero;
        }
    }

    protected override void Dispose(bool disposing)
    {
        Unregister();
        base.Dispose(disposing);
    }
}
