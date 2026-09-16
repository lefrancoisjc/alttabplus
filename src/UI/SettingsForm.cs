using AltTabPlus.I18n;
using AltTabPlus.Native;
using AltTabPlus.Settings;

namespace AltTabPlus.UI;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly SettingsToggle _replaceAltTab;
    private readonly SettingsToggle _showHidden;
    private readonly SettingsToggle _currentMonitor;
    private readonly SettingsToggle _otherDesktops;
    private readonly SettingsToggle _launchOnStartup;
    private readonly SettingsToggle _showTaskbar;
    private readonly HotCornersPicker _hotCorners;
    private readonly TrackBar _dwell;
    private readonly Label _dwellValue;
    private readonly ListBox _ignored;
    private readonly TextBox _ignoreInput;
    private readonly Label _hotkeyValue;
    private readonly Button _captureButton;
    private readonly Button _clearButton;
    private bool _capturing;

    public event Action? SettingsChanged;

    public SettingsForm(AppSettings settings, Icon icon)
    {
        _settings = settings;

        Text = "AltTabPlus";
        Icon = icon;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(456, 700);
        AutoScroll = true;
        BackColor = OverlayTheme.Backdrop;
        ForeColor = OverlayTheme.Caption;
        Font = new Font("Segoe UI", 9.5f);
        KeyPreview = true;

        Controls.Add(new Label
        {
            Text = Loc.SettingsTitle,
            Font = new Font("Segoe UI Semibold", 20f),
            ForeColor = OverlayTheme.Caption,
            AutoSize = true,
            Location = new Point(24, 18),
        });
        Controls.Add(new Label
        {
            Text = Loc.SettingsSubtitle,
            Font = new Font("Segoe UI", 9f),
            ForeColor = OverlayTheme.CaptionMuted,
            AutoSize = true,
            Location = new Point(26, 54),
        });

        _replaceAltTab = MakeToggle(Loc.ReplaceAltTab, Loc.ReplaceAltTabHint, settings.ReplaceAltTab);
        _showHidden = MakeToggle(Loc.HiddenWindows, Loc.HiddenWindowsHint, settings.ShowHiddenWindows);
        _currentMonitor = MakeToggle(Loc.CurrentMonitor, Loc.CurrentMonitorHint, settings.CurrentMonitorOnly);
        _otherDesktops = MakeToggle(Loc.OtherDesktops, Loc.OtherDesktopsHint, settings.ShowOtherDesktops);
        _launchOnStartup = MakeToggle(Loc.LaunchOnStartup, Loc.LaunchOnStartupHint, settings.LaunchOnStartup);
        _showTaskbar = MakeToggle(Loc.TaskbarButton, Loc.TaskbarButtonHint, settings.ShowTaskbarButton);

        _hotCorners = new HotCornersPicker
        {
            TopLeft = settings.HotCornerTopLeft,
            TopRight = settings.HotCornerTopRight,
            BottomLeft = settings.HotCornerBottomLeft,
            BottomRight = settings.HotCornerBottomRight,
            Width = 408,
        };

        _dwell = new TrackBar
        {
            Minimum = 0,
            Maximum = 400,
            TickFrequency = 50,
            Value = Math.Clamp(settings.HotCornerDwellMs, 0, 400),
            Width = 220,
            Location = new Point(16, 196),
            BackColor = OverlayTheme.Card,
        };
        _dwellValue = new Label
        {
            AutoSize = true,
            Location = new Point(246, 206),
            ForeColor = OverlayTheme.CaptionMuted,
            BackColor = OverlayTheme.Card,
            Text = DwellText(_dwell.Value),
        };

        _ignored = new ListBox
        {
            Location = new Point(16, 16),
            Size = new Size(280, 88),
            BackColor = OverlayTheme.Field,
            ForeColor = OverlayTheme.Caption,
            BorderStyle = BorderStyle.None,
        };
        foreach (var name in settings.IgnoredProcesses)
        {
            _ignored.Items.Add(name);
        }

        _ignoreInput = new TextBox
        {
            Location = new Point(16, 112),
            Size = new Size(180, 28),
            BackColor = OverlayTheme.Field,
            ForeColor = OverlayTheme.Caption,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = Loc.IgnorePlaceholder,
        };

        var y = 80;
        y = AddSection(Loc.SectionSwitcher, y, MakeCard(_replaceAltTab, _showHidden, _currentMonitor, _otherDesktops));
        y = AddSection(Loc.SectionHotkey, y, MakeHotkeyCard(out _hotkeyValue, out _captureButton, out _clearButton));
        y = AddSection(Loc.SectionHotCorners, y, MakeCornersCard());
        y = AddSection(Loc.SectionIgnored, y, MakeIgnoreCard());
        y = AddSection(Loc.SectionSystem, y, MakeCard(_launchOnStartup, _showTaskbar));
        AutoScrollMinSize = new Size(0, y + 24);

        _replaceAltTab.CheckedChanged += (_, _) => Persist();
        _showHidden.CheckedChanged += (_, _) => Persist();
        _currentMonitor.CheckedChanged += (_, _) => Persist();
        _otherDesktops.CheckedChanged += (_, _) => Persist();
        _launchOnStartup.CheckedChanged += (_, _) => Persist();
        _showTaskbar.CheckedChanged += (_, _) => Persist();
        _hotCorners.ActionsChanged += (_, _) => Persist();
        _dwell.ValueChanged += (_, _) =>
        {
            _dwellValue.Text = DwellText(_dwell.Value);
            Persist();
        };
        KeyDown += OnKeyDown;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        OverlayTheme.ApplyWindowChrome(Handle);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            if (_settings.ShowTaskbarButton)
            {
                WindowState = FormWindowState.Minimized;
            }
            else
            {
                Hide();
            }
        }

        base.OnFormClosing(e);
    }

    public void Reveal()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    public void ApplyTaskbarPresence()
    {
        ShowInTaskbar = _settings.ShowTaskbarButton;
        if (_settings.ShowTaskbarButton)
        {
            if (!Visible)
            {
                WindowState = FormWindowState.Minimized;
                Show();
            }
        }
        else if (WindowState == FormWindowState.Minimized)
        {
            Hide();
        }
    }

    private int AddSection(string title, int top, Control card)
    {
        Controls.Add(SectionLabel(title, top));
        card.Location = new Point(24, top + 8);
        Controls.Add(card);
        return card.Bottom + 16;
    }

    private Panel MakeCard(params Control[] rows)
    {
        var card = new RoundedCard { Size = new Size(408, rows.Length * 64) };
        for (var i = 0; i < rows.Length; i++)
        {
            rows[i].Location = new Point(0, i * 64);
            rows[i].Width = card.Width;
            card.Controls.Add(rows[i]);
        }

        return card;
    }

    private Panel MakeCornersCard()
    {
        var card = new RoundedCard { Size = new Size(408, 244) };
        _hotCorners.Location = new Point(0, 0);
        _hotCorners.Width = 408;
        card.Controls.Add(_hotCorners);
        card.Controls.Add(new Label
        {
            Text = Loc.Dwell,
            Font = new Font("Segoe UI Semibold", 8.5f),
            ForeColor = OverlayTheme.CaptionMuted,
            AutoSize = true,
            Location = new Point(16, 176),
            BackColor = OverlayTheme.Card,
        });
        card.Controls.Add(_dwell);
        card.Controls.Add(_dwellValue);
        return card;
    }

    private Panel MakeIgnoreCard()
    {
        var card = new RoundedCard { Size = new Size(408, 156) };
        var add = MakeButton(Loc.Add, new Point(204, 110), 88);
        var remove = MakeButton(Loc.Remove, new Point(300, 110), 88);
        add.Click += (_, _) =>
        {
            var name = _ignoreInput.Text.Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^4];
            }
            if (name.Length == 0 || _ignored.Items.Contains(name))
            {
                return;
            }

            _ignored.Items.Add(name);
            _ignoreInput.Clear();
            Persist();
        };
        remove.Click += (_, _) =>
        {
            if (_ignored.SelectedItem is string name)
            {
                _ignored.Items.Remove(name);
                Persist();
            }
        };

        card.Controls.Add(_ignored);
        card.Controls.Add(_ignoreInput);
        card.Controls.Add(add);
        card.Controls.Add(remove);
        return card;
    }

    private Panel MakeHotkeyCard(out Label hotkeyValue, out Button captureButton, out Button clearButton)
    {
        var card = new RoundedCard { Size = new Size(408, 148) };
        card.Controls.Add(new Label
        {
            Text = Loc.AlternateHotkey,
            Font = new Font("Segoe UI Semibold", 10f),
            ForeColor = OverlayTheme.Caption,
            AutoSize = true,
            Location = new Point(16, 14),
            BackColor = OverlayTheme.Card,
        });
        card.Controls.Add(new Label
        {
            Text = Loc.AlternateHotkeyHint,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = OverlayTheme.CaptionMuted,
            AutoSize = true,
            Location = new Point(16, 36),
            BackColor = OverlayTheme.Card,
        });

        hotkeyValue = new Label
        {
            Text = HotkeyText.Format(_settings.HotkeyModifiers, _settings.HotkeyKey),
            Font = new Font("Segoe UI Semibold", 10f),
            ForeColor = OverlayTheme.Caption,
            BackColor = OverlayTheme.Field,
            Location = new Point(16, 68),
            Size = new Size(220, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 12, 0),
        };

        captureButton = MakeButton(Loc.Change, new Point(246, 68), 76);
        clearButton = MakeButton(Loc.Clear, new Point(328, 68), 64);
        captureButton.Click += (_, _) => BeginCapture();
        clearButton.Click += (_, _) => ClearHotkey();

        card.Controls.Add(hotkeyValue);
        card.Controls.Add(captureButton);
        card.Controls.Add(clearButton);
        card.Controls.Add(new Label
        {
            Text = Loc.OverlayHints,
            Font = new Font("Segoe UI", 8f),
            ForeColor = OverlayTheme.CaptionMuted,
            AutoSize = true,
            Location = new Point(16, 112),
            BackColor = OverlayTheme.Card,
        });
        return card;
    }

    private static Label SectionLabel(string text, int top) => new()
    {
        Text = text.ToUpperInvariant(),
        Font = new Font("Segoe UI Semibold", 8f),
        ForeColor = OverlayTheme.CaptionMuted,
        AutoSize = true,
        Location = new Point(28, top),
    };

    private static SettingsToggle MakeToggle(string title, string description, bool isChecked) => new()
    {
        Title = title,
        Description = description,
        Checked = isChecked,
        Width = 408,
    };

    private static Button MakeButton(string text, Point location, int width) => new()
    {
        Text = text,
        Location = location,
        Size = new Size(width, 34),
        FlatStyle = FlatStyle.Flat,
        BackColor = OverlayTheme.Field,
        ForeColor = OverlayTheme.Caption,
        Font = new Font("Segoe UI Semibold", 8.5f),
        FlatAppearance = { BorderColor = OverlayTheme.IdleRing, BorderSize = 1 },
        Cursor = Cursors.Hand,
    };

    private static string DwellText(int ms) => $"{ms} ms";

    private void BeginCapture()
    {
        _capturing = true;
        _hotkeyValue.Text = Loc.PressShortcut;
        _hotkeyValue.ForeColor = OverlayTheme.Accent;
        _captureButton.Text = "…";
    }

    private void ClearHotkey()
    {
        _capturing = false;
        _settings.HotkeyModifiers = 0;
        _settings.HotkeyKey = 0;
        _hotkeyValue.Text = HotkeyText.Format(0, 0);
        _hotkeyValue.ForeColor = OverlayTheme.Caption;
        _captureButton.Text = Loc.Change;
        Persist();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_capturing)
        {
            return;
        }

        e.SuppressKeyPress = true;
        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            return;
        }

        if (e.KeyCode is Keys.Escape)
        {
            _capturing = false;
            _hotkeyValue.Text = HotkeyText.Format(_settings.HotkeyModifiers, _settings.HotkeyKey);
            _hotkeyValue.ForeColor = OverlayTheme.Caption;
            _captureButton.Text = Loc.Change;
            return;
        }

        uint mods = 0;
        if (e.Control) mods |= NativeMethods.MOD_CONTROL;
        if (e.Alt) mods |= NativeMethods.MOD_ALT;
        if (e.Shift) mods |= NativeMethods.MOD_SHIFT;
        if ((NativeMethods.GetKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0
            || (NativeMethods.GetKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0)
        {
            mods |= NativeMethods.MOD_WIN;
        }

        if (mods == 0)
        {
            _hotkeyValue.Text = Loc.NeedModifier;
            _hotkeyValue.ForeColor = OverlayTheme.Accent;
            return;
        }

        _settings.HotkeyModifiers = (int)mods;
        _settings.HotkeyKey = (int)e.KeyCode;
        _capturing = false;
        _hotkeyValue.Text = HotkeyText.Format(_settings.HotkeyModifiers, _settings.HotkeyKey);
        _hotkeyValue.ForeColor = OverlayTheme.Caption;
        _captureButton.Text = Loc.Change;
        Persist();
    }

    private void Persist()
    {
        _settings.ReplaceAltTab = _replaceAltTab.Checked;
        _settings.ShowHiddenWindows = _showHidden.Checked;
        _settings.CurrentMonitorOnly = _currentMonitor.Checked;
        _settings.ShowOtherDesktops = _otherDesktops.Checked;
        _settings.LaunchOnStartup = _launchOnStartup.Checked;
        _settings.ShowTaskbarButton = _showTaskbar.Checked;
        _settings.HotCornerTopLeft = _hotCorners.TopLeft;
        _settings.HotCornerTopRight = _hotCorners.TopRight;
        _settings.HotCornerBottomLeft = _hotCorners.BottomLeft;
        _settings.HotCornerBottomRight = _hotCorners.BottomRight;
        _settings.HotCornerDwellMs = _dwell.Value;
        _settings.IgnoredProcesses = _ignored.Items.Cast<string>().ToList();
        _settings.Save();

        try
        {
            StartupRegistration.SetEnabled(_settings.LaunchOnStartup);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                Loc.StartupFailed(ex.Message),
                "AltTabPlus",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        SettingsChanged?.Invoke();
    }

    private sealed class RoundedCard : Panel
    {
        public RoundedCard()
        {
            BackColor = OverlayTheme.Card;
            DoubleBuffered = true;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            using var path = OverlayTheme.RoundedRect(new Rectangle(0, 0, Width, Height), 10);
            Region?.Dispose();
            Region = new Region(path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            OverlayTheme.PrepareGraphics(e.Graphics);
            e.Graphics.Clear(OverlayTheme.Card);
            base.OnPaint(e);

            using var line = new Pen(OverlayTheme.CardLine);
            foreach (Control child in Controls)
            {
                if (child.Bottom < Height - 4 && child is SettingsToggle)
                {
                    e.Graphics.DrawLine(line, 16, child.Bottom - 1, Width - 16, child.Bottom - 1);
                }
            }
        }
    }
}
