using System.Drawing.Drawing2D;
using System.Drawing.Text;
using AltTabPlus.Native;

namespace AltTabPlus.UI;

internal static class OverlayTheme
{
    public static readonly Color Backdrop = Color.FromArgb(24, 24, 27);
    public static readonly Color Caption = Color.FromArgb(236, 236, 240);
    public static readonly Color CaptionMuted = Color.FromArgb(168, 168, 176);
    public static readonly Color IdleRing = Color.FromArgb(56, 56, 62);
    public static readonly Color Accent = Color.FromArgb(96, 205, 255);
    public static readonly Color AccentSoft = Color.FromArgb(60, 96, 205, 255);
    public static readonly Color BadgeFill = Color.FromArgb(210, 18, 18, 20);
    public static readonly Color BadgeText = Color.White;
    public static readonly Color Card = Color.FromArgb(34, 34, 38);
    public static readonly Color CardLine = Color.FromArgb(48, 48, 54);
    public static readonly Color Field = Color.FromArgb(28, 28, 32);
    public static readonly Color ToggleOff = Color.FromArgb(72, 72, 80);

    public static readonly Font TitleFont = new("Segoe UI Semibold", 9f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font BadgeFont = new("Segoe UI Semibold", 7.5f, FontStyle.Regular, GraphicsUnit.Point);

    public static void ApplyWindowChrome(IntPtr hwnd)
    {
        var dark = 1;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        var round = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
    }

    public static void PrepareGraphics(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }

    public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var d = Math.Max(radius, 1) * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
