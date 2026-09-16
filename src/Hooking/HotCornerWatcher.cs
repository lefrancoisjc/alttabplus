using AltTabPlus.Diagnostics;
using AltTabPlus.Native;
using AltTabPlus.Settings;
using AltTabPlus.Windows;

namespace AltTabPlus.Hooking;

internal sealed class HotCornerWatcher : IDisposable
{
    private const int PollMs = 30;
    private const int ZonePx = 16;

    private readonly System.Windows.Forms.Timer _timer;
    private AppSettings _settings = new();
    private HotCorner? _armed;
    private DateTime _enteredAt;
    private HotCorner? _fired;

    public Func<bool>? ShouldIgnore { get; set; }
    public event Action? SwitcherRequested;

    public HotCornerWatcher()
    {
        _timer = new System.Windows.Forms.Timer { Interval = PollMs };
        _timer.Tick += (_, _) => Tick();
    }

    public void Apply(AppSettings settings)
    {
        _settings = settings;
        var enabled = settings.HotCornerTopLeft != HotCornerAction.None
            || settings.HotCornerTopRight != HotCornerAction.None
            || settings.HotCornerBottomLeft != HotCornerAction.None
            || settings.HotCornerBottomRight != HotCornerAction.None;
        _timer.Enabled = enabled;
        if (!enabled)
        {
            Reset();
        }
    }

    private void Tick()
    {
        if (ShouldIgnore?.Invoke() == true || MouseButtonDown())
        {
            Reset();
            return;
        }

        if (!NativeMethods.GetCursorPos(out var point))
        {
            return;
        }

        var monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (Fullscreen.BlocksHotCorners())
        {
            Reset();
            return;
        }

        var hit = HitCorner(new Point(point.X, point.Y), MonitorHelper.GetBounds(monitor));
        if (hit is null)
        {
            Reset();
            return;
        }

        if (_fired == hit)
        {
            return;
        }

        if (_armed != hit)
        {
            _armed = hit;
            _enteredAt = DateTime.UtcNow;
            return;
        }

        var dwell = Math.Clamp(_settings.HotCornerDwellMs, 0, 1000);
        if ((DateTime.UtcNow - _enteredAt).TotalMilliseconds < dwell)
        {
            return;
        }

        var action = ActionFor(hit.Value);
        if (action == HotCornerAction.None)
        {
            return;
        }

        _fired = hit;
        Run(action);
    }

    private HotCornerAction ActionFor(HotCorner corner) => corner switch
    {
        HotCorner.TopLeft => _settings.HotCornerTopLeft,
        HotCorner.TopRight => _settings.HotCornerTopRight,
        HotCorner.BottomLeft => _settings.HotCornerBottomLeft,
        HotCorner.BottomRight => _settings.HotCornerBottomRight,
        _ => HotCornerAction.None,
    };

    private static HotCorner? HitCorner(Point cursor, Rectangle bounds)
    {
        var left = cursor.X <= bounds.Left + ZonePx;
        var right = cursor.X >= bounds.Right - ZonePx;
        var top = cursor.Y <= bounds.Top + ZonePx;
        var bottom = cursor.Y >= bounds.Bottom - ZonePx;

        if (left && top) return HotCorner.TopLeft;
        if (right && top) return HotCorner.TopRight;
        if (left && bottom) return HotCorner.BottomLeft;
        if (right && bottom) return HotCorner.BottomRight;
        return null;
    }

    private static bool MouseButtonDown() =>
        (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & 0x8000) != 0
        || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RBUTTON) & 0x8000) != 0
        || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MBUTTON) & 0x8000) != 0;

    private void Run(HotCornerAction action)
    {
        Log.Write($"HotCorner: {action}");
        switch (action)
        {
            case HotCornerAction.WinTab:
                KeyStroke.Chord(NativeMethods.VK_LWIN, NativeMethods.VK_TAB);
                break;
            case HotCornerAction.Switcher:
                SwitcherRequested?.Invoke();
                break;
            case HotCornerAction.Desktop:
                KeyStroke.Chord(NativeMethods.VK_LWIN, NativeMethods.VK_D);
                break;
            case HotCornerAction.NextDesktop:
                KeyStroke.Chord(NativeMethods.VK_LWIN, NativeMethods.VK_CONTROL, NativeMethods.VK_RIGHT);
                break;
            case HotCornerAction.PreviousDesktop:
                KeyStroke.Chord(NativeMethods.VK_LWIN, NativeMethods.VK_CONTROL, NativeMethods.VK_LEFT);
                break;
        }
    }

    private void Reset()
    {
        _armed = null;
        _fired = null;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
