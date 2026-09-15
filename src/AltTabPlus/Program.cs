using AltTabPlus.Grouping;
using AltTabPlus.Hooking;
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

        using var hook = new KeyboardHook();
        SwitcherOverlayForm? overlay = null;

        hook.SwitcherOpenRequested += () =>
        {
            var windows = WindowEnumerator.GetAltTabWindows();
            var groups = SnapGroupDetector.DetectGroups(windows);
            var targets = BuildTargets(windows, groups);

            if (targets.Count == 0)
            {
                return;
            }

            overlay?.Close();
            overlay = new SwitcherOverlayForm(targets);
            overlay.Show();
        };

        hook.StepRequested += reverse => overlay?.Step(reverse);

        hook.SwitcherCommitRequested += () =>
        {
            overlay?.Commit();
            overlay?.Close();
            overlay = null;
        };

        hook.SwitcherCancelRequested += () =>
        {
            overlay?.Close();
            overlay = null;
        };

        hook.Install();

        using var trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "AltTabPlus — Alt+Tab avec groupes",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu(),
        };

        Application.Run();
    }

    /// <summary>
    /// One tile per detected group, plus one tile per window that isn't part
    /// of any group. Group tiles are listed first so the most "collapsed"
    /// view of the desktop is what you see stepping from the very first Tab.
    /// </summary>
    private static List<SwitchTarget> BuildTargets(List<WindowInfo> windows, List<List<WindowInfo>> groups)
    {
        var grouped = new HashSet<WindowInfo>(groups.SelectMany(g => g));
        var targets = new List<SwitchTarget>();

        targets.AddRange(groups.Select(g => new SwitchTarget { IsGroup = true, Windows = g }));
        targets.AddRange(windows
            .Where(w => !grouped.Contains(w))
            .Select(w => new SwitchTarget { IsGroup = false, Windows = new List<WindowInfo> { w } }));

        return targets;
    }

    private static ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Quitter AltTabPlus", null, (_, _) => Application.Exit());
        return menu;
    }
}
