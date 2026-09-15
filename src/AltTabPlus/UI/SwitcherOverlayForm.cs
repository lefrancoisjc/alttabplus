using AltTabPlus.Switching;

namespace AltTabPlus.UI;

/// <summary>
/// Borderless, topmost, click-through-to-select overlay that replaces the
/// native Alt-Tab UI. One tile per <see cref="SwitchTarget"/>: a group
/// collapses to a single tile with a badge instead of exploding into one
/// tile per window, which is the entire point of this project.
/// </summary>
internal sealed class SwitcherOverlayForm : Form
{
    private const int TileWidth = 220;
    private const int TileHeight = 150;
    private const int Gap = 16;
    private const int Padding = 24;
    private const int LabelHeight = 26;

    private readonly List<SwitchTarget> _targets;
    private readonly List<Panel> _tileContainers = new();
    private int _selectedIndex;

    public SwitcherOverlayForm(List<SwitchTarget> targets)
    {
        _targets = targets;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(28, 28, 32);
        DoubleBuffered = true;

        BuildLayout();
        PositionOnActiveMonitor();
    }

    private void BuildLayout()
    {
        var count = Math.Max(_targets.Count, 1);
        Width = Padding * 2 + count * TileWidth + (count - 1) * Gap;
        Height = Padding * 2 + TileHeight + LabelHeight + 8;

        for (var i = 0; i < _targets.Count; i++)
        {
            var target = _targets[i];
            var left = Padding + i * (TileWidth + Gap);

            var container = new Panel
            {
                Width = TileWidth,
                Height = TileHeight,
                Left = left,
                Top = Padding,
                BackColor = Color.FromArgb(48, 48, 54),
                Padding = new Padding(3),
            };

            var thumb = new ThumbnailPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 24),
            };
            thumb.SetSource(target.PreviewHandle);
            container.Controls.Add(thumb);

            if (target.IsGroup)
            {
                var badge = new Label
                {
                    Text = $"⧉ {target.Windows.Count} fenêtres groupées",
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(180, 0, 0, 0),
                    AutoSize = true,
                    Padding = new Padding(4, 2, 4, 2),
                    Location = new Point(6, 6),
                };
                container.Controls.Add(badge);
                badge.BringToFront();
            }

            var label = new Label
            {
                Text = target.DisplayTitle,
                ForeColor = Color.White,
                Left = left,
                Top = Padding + TileHeight + 4,
                Width = TileWidth,
                Height = LabelHeight,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
            };

            Controls.Add(container);
            Controls.Add(label);
            _tileContainers.Add(container);
        }

        Highlight(0);
    }

    private void PositionOnActiveMonitor()
    {
        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        Left = workingArea.Left + (workingArea.Width - Width) / 2;
        Top = workingArea.Top + (workingArea.Height - Height) / 2;
    }

    private void Highlight(int index)
    {
        for (var i = 0; i < _tileContainers.Count; i++)
        {
            _tileContainers[i].BackColor = i == index
                ? Color.FromArgb(0, 120, 215)
                : Color.FromArgb(48, 48, 54);
        }

        _selectedIndex = index;
    }

    /// <summary>Move the selection forward (or backward, for Shift+Tab) with wraparound.</summary>
    public void Step(bool reverse)
    {
        if (_targets.Count == 0)
        {
            return;
        }

        var next = (_selectedIndex + (reverse ? -1 : 1) + _targets.Count) % _targets.Count;
        Highlight(next);
    }

    /// <summary>Activate whatever is currently selected (called when Alt is released).</summary>
    public void Commit()
    {
        if (_targets.Count == 0)
        {
            return;
        }

        _targets[_selectedIndex].Activate();
    }
}
