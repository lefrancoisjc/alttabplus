using System.Drawing.Drawing2D;
using AltTabPlus.I18n;
using AltTabPlus.Native;
using AltTabPlus.Switching;

namespace AltTabPlus.UI;

internal sealed class SwitcherOverlayForm : Form
{
    private const int TileWidth = 252;
    private const int TileHeight = 158;
    private const int Gap = 20;
    private const int FormPadding = 28;
    private const int CaptionHeight = 28;
    private const int CaptionGap = 8;
    private const int TileRadius = 10;
    private const int RingPad = 4;
    private const int BannerHeight = 32;
    private const int GroupHeader = 26;

    private readonly List<SwitchTarget> _root;
    private readonly List<DwmThumbnail> _thumbnails = new();
    private readonly List<Rectangle> _tileRects = new();
    private readonly List<List<Icon>> _icons = new();
    private List<SwitchTarget> _view;
    private SwitchTarget? _expanded;
    private string _filter = string.Empty;
    private int _columns;
    private int _selectedIndex;

    public event Action? TileActivated;
    public event Action? CloseRequested;

    public SwitcherOverlayForm(List<SwitchTarget> targets)
    {
        _root = new List<SwitchTarget>(targets);
        _view = _root;
        ApplyView();

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = OverlayTheme.Backdrop;
        DoubleBuffered = false;

        Rebuild(initial: true);
        MouseDown += OnOverlayMouseDown;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        OverlayTheme.ApplyWindowChrome(Handle);
    }

    public void Reload(List<SwitchTarget> targets)
    {
        var selectedHandles = Selected?.Windows.Select(w => w.Handle).ToHashSet() ?? new HashSet<IntPtr>();
        _root.Clear();
        _root.AddRange(targets);
        if (_expanded is not null)
        {
            _expanded = _root.FirstOrDefault(t =>
                t.IsGroup && t.Windows.Select(w => w.Handle).ToHashSet().SetEquals(_expanded.Windows.Select(w => w.Handle)));
        }

        ApplyView();
        if (selectedHandles.Count > 0)
        {
            var index = _view.FindIndex(t => t.Windows.Any(w => selectedHandles.Contains(w.Handle)));
            if (index >= 0)
            {
                _selectedIndex = index;
            }
        }

        Rebuild();
    }

    public bool TryBack()
    {
        if (_filter.Length > 0)
        {
            _filter = string.Empty;
            ApplyView();
            Rebuild();
            return true;
        }

        if (_expanded is not null)
        {
            _expanded = null;
            ApplyView();
            Rebuild();
            return true;
        }

        return false;
    }

    public void AppendFilter(char ch)
    {
        if (char.IsControl(ch))
        {
            return;
        }

        _filter += ch;
        ApplyView();
        Rebuild();
    }

    public void BackspaceFilter()
    {
        if (_filter.Length == 0)
        {
            return;
        }

        _filter = _filter[..^1];
        ApplyView();
        Rebuild();
    }

    public bool ExpandSelected()
    {
        if (_expanded is not null || Selected is not { IsGroup: true } group)
        {
            return false;
        }

        _expanded = group;
        ApplyView();
        Rebuild();
        return true;
    }

    public void CloseSelected()
    {
        Selected?.Close();
        CloseRequested?.Invoke();
    }

    public void StepSameApp(bool reverse)
    {
        if (Selected is null || _view.Count == 0)
        {
            return;
        }

        var pid = Selected.ProcessId;
        var same = _view
            .Select((t, i) => (t, i))
            .Where(x => x.t.ProcessId == pid)
            .Select(x => x.i)
            .ToList();
        if (same.Count < 2)
        {
            return;
        }

        var pos = same.IndexOf(_selectedIndex);
        if (pos < 0)
        {
            pos = 0;
        }

        var next = same[(pos + (reverse ? -1 : 1) + same.Count) % same.Count];
        Highlight(next);
    }

    public void Step(bool reverse)
    {
        if (_view.Count == 0)
        {
            return;
        }

        Highlight((_selectedIndex + (reverse ? -1 : 1) + _view.Count) % _view.Count);
    }

    public void MoveSelection(int dx, int dy)
    {
        if (_view.Count == 0 || _columns <= 0)
        {
            return;
        }

        var row = _selectedIndex / _columns;
        var col = _selectedIndex % _columns;

        if (dx != 0)
        {
            var rowStart = row * _columns;
            var rowCount = Math.Min(_columns, _view.Count - rowStart);
            var nextCol = (col + dx) % rowCount;
            if (nextCol < 0)
            {
                nextCol += rowCount;
            }

            Highlight(rowStart + nextCol);
            return;
        }

        if (dy > 0 && ExpandSelected())
        {
            return;
        }

        if (dy == 0)
        {
            return;
        }

        var lastRow = (_view.Count - 1) / _columns;
        var nextRow = row + dy;
        if (nextRow < 0) nextRow = lastRow;
        else if (nextRow > lastRow) nextRow = 0;

        Highlight(Math.Min(_view.Count - 1, nextRow * _columns + col));
    }

    public void Commit() => Selected?.Activate();

    private SwitchTarget? Selected =>
        _selectedIndex >= 0 && _selectedIndex < _view.Count ? _view[_selectedIndex] : null;

    private int Banner => _filter.Length > 0 || _expanded is not null ? BannerHeight : 0;

    private void ApplyView()
    {
        IEnumerable<SwitchTarget> source = _expanded is { } group
            ? group.Windows.Select(w => new SwitchTarget { IsGroup = false, Windows = new() { w } })
            : _root;
        _view = source.Where(t => t.Matches(_filter)).ToList();
        _selectedIndex = _expanded is null && _filter.Length == 0 && _view.Count > 1 ? 1 : 0;
        if (_selectedIndex >= _view.Count)
        {
            _selectedIndex = Math.Max(0, _view.Count - 1);
        }
    }

    private void Rebuild(bool initial = false)
    {
        DisposeThumbnails();
        DisposeIcons();
        _tileRects.Clear();
        _columns = ComputeColumns();
        BuildLayout();
        PositionOnActiveMonitor();
        if (!initial && IsHandleCreated)
        {
            RegisterThumbnails();
            Invalidate();
        }
    }

    private int ComputeColumns()
    {
        var count = Math.Max(_view.Count, 1);
        var work = Screen.FromPoint(Cursor.Position).WorkingArea;
        var usable = Math.Max(TileWidth, work.Width * 9 / 10 - FormPadding * 2);
        var maxCols = Math.Max(1, (usable + Gap) / (TileWidth + Gap));
        return Math.Min(count, maxCols);
    }

    private void BuildLayout()
    {
        var count = Math.Max(_view.Count, 1);
        var rows = (count + _columns - 1) / _columns;
        var rowStride = TileHeight + CaptionGap + CaptionHeight + Gap;
        var banner = Banner;

        Width = FormPadding * 2 + _columns * TileWidth + (_columns - 1) * Gap;
        Height = FormPadding * 2 + banner + rows * (TileHeight + CaptionGap + CaptionHeight) + Math.Max(0, rows - 1) * Gap;

        for (var i = 0; i < _view.Count; i++)
        {
            var target = _view[i];
            var col = i % _columns;
            var row = i / _columns;
            var left = FormPadding + col * (TileWidth + Gap);
            var top = FormPadding + banner + row * rowStride;
            var tile = new Rectangle(left, top, TileWidth, TileHeight);
            _tileRects.Add(tile);
            _icons.Add(CollectIcons(target));
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        OverlayTheme.ApplyWindowChrome(Handle);
        RegisterThumbnails();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        OverlayTheme.PrepareGraphics(e.Graphics);
        base.OnPaint(e);

        if (Banner > 0)
        {
            using var brush = new SolidBrush(OverlayTheme.CaptionMuted);
            var text = _expanded is not null
                ? (_filter.Length > 0 ? Loc.GroupBackFilter(_filter) : Loc.GroupBack)
                : Loc.FilterBanner(_filter);
            e.Graphics.DrawString(text, OverlayTheme.TitleFont, brush, FormPadding, 10);
        }

        for (var i = 0; i < _tileRects.Count; i++)
        {
            DrawTileChrome(e.Graphics, i);
            DrawCaption(e.Graphics, i);
        }
    }

    private void DrawTileChrome(Graphics g, int index)
    {
        var tile = _tileRects[index];
        var selected = index == _selectedIndex;
        var ring = Rectangle.Inflate(tile, RingPad, RingPad);

        using var path = OverlayTheme.RoundedRect(ring, TileRadius);
        using var pen = new Pen(selected ? OverlayTheme.Accent : OverlayTheme.IdleRing, selected ? 2.4f : 1.2f);
        g.DrawPath(pen, path);

        if (_view[index].IsGroup)
        {
            DrawGroupBadge(g, tile);
        }

        if (selected)
        {
            using var glow = new Pen(OverlayTheme.AccentSoft, 6f);
            glow.LineJoin = LineJoin.Round;
            using var glowPath = OverlayTheme.RoundedRect(Rectangle.Inflate(ring, 3, 3), TileRadius + 2);
            g.DrawPath(glow, glowPath);
        }
    }

    private void DrawCaption(Graphics g, int index)
    {
        var tile = _tileRects[index];
        var selected = index == _selectedIndex;
        var caption = new Rectangle(tile.Left, tile.Bottom + CaptionGap, tile.Width, CaptionHeight);
        var textLeft = caption.Left + DrawIconStack(g, caption, _icons[index]);

        using var brush = new SolidBrush(selected ? OverlayTheme.Caption : OverlayTheme.CaptionMuted);
        var textRect = new RectangleF(textLeft, caption.Top + 4, caption.Right - textLeft, caption.Height - 4);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        g.DrawString(_view[index].DisplayTitle, OverlayTheme.TitleFont, brush, textRect, format);
    }

    private static List<Icon> CollectIcons(SwitchTarget target)
    {
        var icons = new List<Icon>();
        var seen = new HashSet<uint>();
        foreach (var window in target.Windows)
        {
            if (!seen.Add(window.ProcessId))
            {
                continue;
            }

            var icon = WindowIcon.TryGet(window.Handle);
            if (icon is not null)
            {
                icons.Add(icon);
            }
        }

        return icons;
    }

    private static int DrawIconStack(Graphics g, Rectangle caption, List<Icon> icons)
    {
        if (icons.Count == 0)
        {
            return 0;
        }

        const int size = 16;
        const int step = 9;
        var shown = Math.Min(icons.Count, 4);
        using var ring = new SolidBrush(OverlayTheme.Backdrop);
        for (var i = shown - 1; i >= 0; i--)
        {
            var x = caption.Left + i * step;
            var y = caption.Top + 4;
            g.FillEllipse(ring, x - 1, y - 1, size + 2, size + 2);
            g.DrawIcon(icons[i], new Rectangle(x, y, size, size));
        }

        return size + (shown - 1) * step + 6;
    }

    private void DisposeIcons()
    {
        foreach (var stack in _icons)
        {
            foreach (var icon in stack)
            {
                icon.Dispose();
            }
        }

        _icons.Clear();
    }

    private void RegisterThumbnails()
    {
        DisposeThumbnails();
        if (!IsHandleCreated)
        {
            return;
        }

        for (var i = 0; i < _view.Count; i++)
        {
            var cells = ThumbnailCells(_tileRects[i], _view[i]);
            for (var c = 0; c < cells.Length; c++)
            {
                _thumbnails.Add(new DwmThumbnail(Handle, _view[i].Windows[c].Handle, cells[c]));
            }
        }
    }

    private static void DrawGroupBadge(Graphics g, Rectangle tile)
    {
        var badge = new Rectangle(tile.Left + 8, tile.Top + 5, tile.Width - 16, 18);
        using var fill = new SolidBrush(OverlayTheme.BadgeFill);
        using var text = new SolidBrush(OverlayTheme.BadgeText);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
        };
        g.FillRectangle(fill, badge);
        g.DrawString(Loc.ExpandHint, OverlayTheme.BadgeFont, text, badge, format);
    }

    private static Rectangle[] ThumbnailCells(Rectangle tile, SwitchTarget target)
    {
        var inner = Rectangle.Inflate(tile, -2, -2);
        if (target.IsGroup)
        {
            inner.Y += GroupHeader;
            inner.Height = Math.Max(8, inner.Height - GroupHeader);
        }

        var count = target.Windows.Count;
        if (!target.IsGroup || count <= 1)
        {
            return new[] { inner };
        }

        var cols = count <= 2 ? count : 2;
        var rows = (int)Math.Ceiling(count / (double)cols);
        var cellW = inner.Width / cols;
        var cellH = inner.Height / rows;
        var cells = new Rectangle[count];

        for (var i = 0; i < count; i++)
        {
            var col = i % cols;
            var row = i / cols;
            if (count == 3 && i == 2)
            {
                cells[i] = new Rectangle(inner.Left + 2, inner.Top + cellH + 2, inner.Width - 4, cellH - 4);
            }
            else
            {
                cells[i] = new Rectangle(
                    inner.Left + col * cellW + 2,
                    inner.Top + row * cellH + 2,
                    cellW - 4,
                    cellH - 4);
            }
        }

        return cells;
    }

    private void PositionOnActiveMonitor()
    {
        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        Left = workingArea.Left + (workingArea.Width - Width) / 2;
        Top = workingArea.Top + (workingArea.Height - Height) / 2;
    }

    private void Highlight(int index)
    {
        _selectedIndex = index;
        Invalidate();
    }

    private void OnOverlayMouseDown(object? sender, MouseEventArgs e)
    {
        for (var i = 0; i < _tileRects.Count; i++)
        {
            var hit = _tileRects[i];
            hit.Height += CaptionGap + CaptionHeight;
            if (!hit.Contains(e.Location))
            {
                continue;
            }

            Highlight(i);
            if (e.Button == MouseButtons.Middle)
            {
                CloseSelected();
            }
            else if (e.Button == MouseButtons.Left)
            {
                if (!ExpandSelected())
                {
                    TileActivated?.Invoke();
                }
            }

            return;
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        DisposeThumbnails();
        DisposeIcons();
        base.OnHandleDestroyed(e);
    }

    private void DisposeThumbnails()
    {
        foreach (var thumb in _thumbnails)
        {
            thumb.Dispose();
        }

        _thumbnails.Clear();
    }
}
