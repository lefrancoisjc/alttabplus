using System.Drawing.Drawing2D;

namespace AltTabPlus.UI;

internal static class AppIcon
{
    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(Color.FromArgb(24, 24, 27));
            using var accent = new SolidBrush(Color.FromArgb(96, 205, 255));
            using var tile = new SolidBrush(Color.FromArgb(48, 48, 54));
            g.FillRectangle(bg, 0, 0, 32, 32);
            g.FillRectangle(tile, 4, 7, 11, 18);
            g.FillRectangle(accent, 17, 7, 11, 8);
            g.FillRectangle(tile, 17, 17, 11, 8);
        }

        var handle = bitmap.GetHicon();
        using var raw = Icon.FromHandle(handle);
        return (Icon)raw.Clone();
    }
}
