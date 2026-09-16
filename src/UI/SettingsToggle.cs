namespace AltTabPlus.UI;

internal sealed class SettingsToggle : Control
{
    private const int TrackWidth = 40;
    private const int TrackHeight = 20;
    private const int Thumb = 16;

    private bool _checked;

    public SettingsToggle()
    {
        DoubleBuffered = true;
        Height = 64;
        Cursor = Cursors.Hand;
        TabStop = true;
        Title = string.Empty;
        Description = string.Empty;
    }

    public string Title { get; set; }
    public string Description { get; set; }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CheckedChanged;

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            e.Handled = true;
            Checked = !Checked;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        OverlayTheme.PrepareGraphics(e.Graphics);
        e.Graphics.Clear(OverlayTheme.Card);

        using var titleBrush = new SolidBrush(OverlayTheme.Caption);
        using var descBrush = new SolidBrush(OverlayTheme.CaptionMuted);
        using var titleFont = new Font("Segoe UI Semibold", 10f);
        using var descFont = new Font("Segoe UI", 8.5f);

        var textWidth = Math.Max(40, Width - TrackWidth - 40);
        e.Graphics.DrawString(Title, titleFont, titleBrush, new RectangleF(16, 12, textWidth, 22));
        e.Graphics.DrawString(Description, descFont, descBrush, new RectangleF(16, 34, textWidth, 22));

        var track = new Rectangle(Width - 16 - TrackWidth, (Height - TrackHeight) / 2, TrackWidth, TrackHeight);
        using (var path = OverlayTheme.RoundedRect(track, TrackHeight / 2))
        using (var fill = new SolidBrush(Checked ? OverlayTheme.Accent : OverlayTheme.ToggleOff))
        {
            e.Graphics.FillPath(fill, path);
        }

        var thumbX = Checked ? track.Right - Thumb - 2 : track.Left + 2;
        var thumb = new Rectangle(thumbX, track.Top + 2, Thumb, Thumb);
        using (var path = OverlayTheme.RoundedRect(thumb, Thumb / 2))
        using (var fill = new SolidBrush(Color.White))
        {
            e.Graphics.FillPath(fill, path);
        }

        if (Focused)
        {
            using var focus = new Pen(OverlayTheme.AccentSoft, 1.5f);
            e.Graphics.DrawRectangle(focus, 2, 2, Width - 5, Height - 5);
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }
}
