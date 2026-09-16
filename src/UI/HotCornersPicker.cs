using AltTabPlus.I18n;
using AltTabPlus.Settings;

namespace AltTabPlus.UI;

internal sealed class HotCornersPicker : Control
{
    private readonly Dictionary<HotCorner, Rectangle> _hits = new();
    private HotCorner? _hover;

    public HotCornersPicker()
    {
        DoubleBuffered = true;
        Height = 188;
        TabStop = false;
    }

    public HotCornerAction TopLeft { get; set; }
    public HotCornerAction TopRight { get; set; }
    public HotCornerAction BottomLeft { get; set; }
    public HotCornerAction BottomRight { get; set; }

    public event EventHandler? ActionsChanged;

    public HotCornerAction ActionFor(HotCorner corner) => corner switch
    {
        HotCorner.TopLeft => TopLeft,
        HotCorner.TopRight => TopRight,
        HotCorner.BottomLeft => BottomLeft,
        HotCorner.BottomRight => BottomRight,
        _ => HotCornerAction.None,
    };

    public void SetAction(HotCorner corner, HotCornerAction action)
    {
        switch (corner)
        {
            case HotCorner.TopLeft: TopLeft = action; break;
            case HotCorner.TopRight: TopRight = action; break;
            case HotCorner.BottomLeft: BottomLeft = action; break;
            case HotCorner.BottomRight: BottomRight = action; break;
        }

        Invalidate();
        ActionsChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var next = HitTest(e.Location);
        if (next == _hover)
        {
            return;
        }

        _hover = next;
        Cursor = next is null ? Cursors.Default : Cursors.Hand;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var corner = HitTest(e.Location);
        if (corner is null)
        {
            return;
        }

        var menu = new ContextMenuStrip();
        foreach (var action in HotCornerText.All)
        {
            AddAction(menu, corner.Value, action);
        }

        menu.Show(this, e.Location);
    }

    private void AddAction(ContextMenuStrip menu, HotCorner corner, HotCornerAction action)
    {
        var item = new ToolStripMenuItem(HotCornerText.Action(action))
        {
            Checked = ActionFor(corner) == action,
        };
        item.Click += (_, _) => SetAction(corner, action);
        menu.Items.Add(item);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        OverlayTheme.PrepareGraphics(e.Graphics);
        e.Graphics.Clear(OverlayTheme.Card);

        using var titleFont = new Font("Segoe UI Semibold", 10f);
        using var mutedFont = new Font("Segoe UI", 8.5f);
        using var title = new SolidBrush(OverlayTheme.Caption);
        using var muted = new SolidBrush(OverlayTheme.CaptionMuted);
        e.Graphics.DrawString(Loc.CornerTitle, titleFont, title, 16, 12);
        e.Graphics.DrawString(Loc.CornerHint, mutedFont, muted, 16, 34);

        var screen = new Rectangle(108, 62, Width - 216, 82);
        using (var path = OverlayTheme.RoundedRect(screen, 8))
        using (var fill = new SolidBrush(OverlayTheme.Field))
        using (var ring = new Pen(OverlayTheme.IdleRing))
        {
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(ring, path);
        }

        using var bezel = new SolidBrush(OverlayTheme.CaptionMuted);
        e.Graphics.FillRectangle(bezel, screen.Left + screen.Width / 2 - 14, screen.Bottom - 8, 28, 3);

        LayoutHits(screen);
        DrawChip(e.Graphics, HotCorner.TopLeft);
        DrawChip(e.Graphics, HotCorner.TopRight);
        DrawChip(e.Graphics, HotCorner.BottomLeft);
        DrawChip(e.Graphics, HotCorner.BottomRight);
    }

    private void LayoutHits(Rectangle screen)
    {
        const int chipW = 104;
        const int chipH = 24;
        _hits[HotCorner.TopLeft] = new Rectangle(12, 58, chipW, chipH);
        _hits[HotCorner.TopRight] = new Rectangle(Width - 12 - chipW, 58, chipW, chipH);
        _hits[HotCorner.BottomLeft] = new Rectangle(12, screen.Bottom + 10, chipW, chipH);
        _hits[HotCorner.BottomRight] = new Rectangle(Width - 12 - chipW, screen.Bottom + 10, chipW, chipH);
    }

    private void DrawChip(Graphics g, HotCorner corner)
    {
        var bounds = _hits[corner];
        var action = ActionFor(corner);
        var active = action != HotCornerAction.None;
        var hover = _hover == corner;
        var fillColor = active ? Color.FromArgb(40, 70, 88) : OverlayTheme.Field;
        if (hover)
        {
            fillColor = active ? Color.FromArgb(52, 88, 108) : Color.FromArgb(42, 42, 48);
        }

        using var path = OverlayTheme.RoundedRect(bounds, 8);
        using var fill = new SolidBrush(fillColor);
        using var ring = new Pen(active ? OverlayTheme.Accent : OverlayTheme.IdleRing);
        g.FillPath(fill, path);
        g.DrawPath(ring, path);

        using var text = new SolidBrush(active ? OverlayTheme.Caption : OverlayTheme.CaptionMuted);
        using var font = new Font("Segoe UI Semibold", 8f);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        g.DrawString(HotCornerText.Action(action), font, text, bounds, format);
    }

    private HotCorner? HitTest(Point point)
    {
        foreach (var (corner, bounds) in _hits)
        {
            if (bounds.Contains(point))
            {
                return corner;
            }
        }

        return null;
    }
}
