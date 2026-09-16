using AltTabPlus.Diagnostics;
using AltTabPlus.Grouping;
using AltTabPlus.Hooking;
using AltTabPlus.I18n;
using AltTabPlus.Native;
using AltTabPlus.Settings;
using AltTabPlus.Switching;
using AltTabPlus.UI;
using AltTabPlus.Windows;

namespace AltTabPlus;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new AppHost());
    }
}

internal sealed class AppHost : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly Icon _icon;
    private readonly KeyboardHook _hook;
    private readonly SettingsForm _settingsForm;
    private readonly NotifyIcon _tray;
    private readonly HotCornerWatcher _hotCorners;
    private SwitcherOverlayForm? _overlay;
    private System.Windows.Forms.Timer? _refreshTimer;

    public AppHost()
    {
        _settings = AppSettings.Load();
        _settings.LaunchOnStartup = StartupRegistration.IsEnabled();
        _icon = AppIcon.Create();

        _hook = new KeyboardHook { ReplaceAltTab = _settings.ReplaceAltTab };
        _hook.SwitcherOpenRequested += OpenSwitcher;
        _hook.StepRequested += reverse => _overlay?.Step(reverse);
        _hook.MoveRequested += (dx, dy) => _overlay?.MoveSelection(dx, dy);
        _hook.SwitcherCommitRequested += CommitSwitcher;
        _hook.BackRequested += OnBack;
        _hook.ExpandRequested += () => _overlay?.ExpandSelected();
        _hook.CloseRequested += () => _overlay?.CloseSelected();
        _hook.FilterCharRequested += ch => _overlay?.AppendFilter(ch);
        _hook.FilterBackspaceRequested += () => _overlay?.BackspaceFilter();
        _hook.SameAppCycleRequested += CycleSameApp;

        _settingsForm = new SettingsForm(_settings, _icon);
        _settingsForm.SettingsChanged += ApplySettings;

        _tray = new NotifyIcon
        {
            Icon = _icon,
            Text = Loc.TrayTooltip,
            Visible = true,
            ContextMenuStrip = BuildTrayMenu(),
        };
        _tray.DoubleClick += (_, _) => _settingsForm.Reveal();

        _hotCorners = new HotCornerWatcher
        {
            ShouldIgnore = () => _overlay is { Visible: true },
        };
        _hotCorners.SwitcherRequested += OpenFromHotCorner;

        try
        {
            _hook.Install();
            Log.Write("Program: keyboard hook installed");
        }
        catch (Exception ex)
        {
            Log.Write($"Program: hook install FAILED: {ex}");
            MessageBox.Show(
                Loc.HookInstallFailed(ex.Message),
                "AltTabPlus",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        ApplySettings();
    }

    private void ApplySettings()
    {
        _hook.ReplaceAltTab = _settings.ReplaceAltTab;
        if (!_hook.ApplyCustomHotkey(_settings) && _settings.HasCustomHotkey)
        {
            MessageBox.Show(
                Loc.HotkeyInUse,
                "AltTabPlus",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        _hotCorners.Apply(_settings);
        _settingsForm.ApplyTaskbarPresence();
    }

    private void OpenFromHotCorner()
    {
        OpenSwitcher();
        if (_overlay is not null)
        {
            _hook.NotifyOpened();
        }
    }

    private void OpenSwitcher()
    {
        var targets = BuildSwitcherTargets();
        Log.Write($"Program: open — {targets.Count} tiles");
        if (targets.Count == 0)
        {
            _hook.NotifyClosed();
            return;
        }

        _overlay?.Close();
        _overlay = new SwitcherOverlayForm(targets);
        _overlay.TileActivated += CommitSwitcher;
        _overlay.CloseRequested += ScheduleRefresh;
        _overlay.Show();
    }

    private void OnBack()
    {
        if (_overlay?.TryBack() == true)
        {
            return;
        }

        CancelSwitcher();
    }

    private void CycleSameApp(bool reverse)
    {
        if (_overlay is not null)
        {
            _overlay.StepSameApp(reverse);
            return;
        }

        var windows = WindowEnumerator.GetAltTabWindows(CreateQuery());
        if (windows.Count == 0)
        {
            return;
        }

        var foreground = NativeMethods.GetForegroundWindow();
        var current = windows.FirstOrDefault(w => w.Handle == foreground) ?? windows[0];
        var same = windows.Where(w => w.ProcessId == current.ProcessId).OrderBy(w => w.ZOrder).ToList();
        if (same.Count < 2)
        {
            return;
        }

        var index = Math.Max(0, same.FindIndex(w => w.Handle == current.Handle));
        var next = same[(index + (reverse ? -1 : 1) + same.Count) % same.Count];
        ForegroundActivator.ActivateMany(new[] { next.Handle });
    }

    private void CommitSwitcher()
    {
        var current = _overlay;
        _overlay = null;
        _hook.NotifyClosed();
        current?.Commit();
        current?.Close();
    }

    private void CancelSwitcher()
    {
        _overlay?.Close();
        _overlay = null;
        _hook.NotifyClosed();
    }

    private void ScheduleRefresh()
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 120 };
        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            RefreshOverlay();
        };
        _refreshTimer.Start();
    }

    private void RefreshOverlay()
    {
        if (_overlay is null)
        {
            return;
        }

        var targets = BuildSwitcherTargets();
        if (targets.Count == 0)
        {
            CancelSwitcher();
            return;
        }

        _overlay.Reload(targets);
    }

    private List<SwitchTarget> BuildSwitcherTargets()
    {
        var windows = WindowEnumerator.GetAltTabWindows(CreateQuery());
        var other = windows.Count(w => w.IsOnOtherDesktop);
        Log.Write($"Program: enum {windows.Count} windows ({other} other desktop, includeOther={_settings.ShowOtherDesktops})");
        var groups = SnapGroupDetector.DetectGroups(windows);
        var grouped = new HashSet<WindowInfo>(groups.SelectMany(g => g));
        var targets = new List<SwitchTarget>();
        targets.AddRange(groups.Select(g => new SwitchTarget { IsGroup = true, Windows = g }));
        targets.AddRange(windows
            .Where(w => !grouped.Contains(w))
            .Select(w => new SwitchTarget { IsGroup = false, Windows = new List<WindowInfo> { w } }));
        return targets.OrderBy(t => t.Windows.Min(w => w.ZOrder)).ToList();
    }

    private WindowQuery CreateQuery()
    {
        NativeMethods.GetCursorPos(out var point);
        return new WindowQuery
        {
            IncludeHidden = _settings.ShowHiddenWindows,
            IncludeOtherDesktops = _settings.ShowOtherDesktops,
            CurrentMonitorOnly = _settings.CurrentMonitorOnly,
            Monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST),
            IgnoredProcesses = new HashSet<string>(_settings.IgnoredProcesses, StringComparer.OrdinalIgnoreCase),
        };
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Loc.TraySettings, null, (_, _) => _settingsForm.Reveal());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Loc.TrayQuit, null, (_, _) => ExitThread());
        return menu;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer?.Dispose();
            _overlay?.Close();
            _tray.Visible = false;
            _tray.Dispose();
            _settingsForm.Dispose();
            _hotCorners.Dispose();
            _hook.Dispose();
            _icon.Dispose();
        }

        base.Dispose(disposing);
    }
}
