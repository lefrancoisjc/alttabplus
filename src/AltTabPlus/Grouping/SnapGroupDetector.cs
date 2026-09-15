using System.Drawing;
using AltTabPlus.Native;
using AltTabPlus.Windows;

namespace AltTabPlus.Grouping;

/// <summary>
/// This is the actual point of the whole project: Windows exposes no public
/// API to ask "which windows are in the same Snap Group?" — that bookkeeping
/// lives inside the shell (twinui.pcshell.dll) and nothing outside it can
/// query it. So we infer it from geometry instead:
///
///   1. group candidate windows by the monitor they're on;
///   2. connect any two windows whose rectangles touch edge-to-edge (allowing
///      a small pixel tolerance for shadows/borders) without materially
///      overlapping;
///   3. keep a connected component only if, together, its windows cover most
///      of that monitor's work area — that's the geometric signature of a
///      deliberate Snap Layout, as opposed to two windows that just happen to
///      sit near each other.
///
/// This is a heuristic, not ground truth — it can occasionally group windows
/// a user tiled manually, or miss a snap group whose windows were resized
/// afterwards. That trade-off is inherent to working around an undocumented
/// feature; see the README for known edge cases.
/// </summary>
internal static class SnapGroupDetector
{
    private const int EdgeToleragePx = 6;
    private const double MinWorkAreaCoverage = 0.55;

    public static List<List<WindowInfo>> DetectGroups(IReadOnlyList<WindowInfo> windows)
    {
        var groups = new List<List<WindowInfo>>();

        foreach (var monitorWindows in windows.GroupBy(w => w.MonitorHandle))
        {
            var list = monitorWindows.ToList();
            var visited = new bool[list.Count];

            for (var i = 0; i < list.Count; i++)
            {
                if (visited[i])
                {
                    continue;
                }

                var component = new List<WindowInfo>();
                var stack = new Stack<int>();
                stack.Push(i);
                visited[i] = true;

                while (stack.Count > 0)
                {
                    var idx = stack.Pop();
                    component.Add(list[idx]);

                    for (var j = 0; j < list.Count; j++)
                    {
                        if (!visited[j] && AreAdjacent(list[idx].Bounds, list[j].Bounds))
                        {
                            visited[j] = true;
                            stack.Push(j);
                        }
                    }
                }

                if (component.Count >= 2 && LooksLikeSnapLayout(component, monitorWindows.Key))
                {
                    groups.Add(component);
                }
            }
        }

        return groups;
    }

    private static bool AreAdjacent(Rectangle a, Rectangle b)
    {
        if (a.IntersectsWith(b))
        {
            var intersection = Rectangle.Intersect(a, b);
            // A thin sliver of overlap is just shadow/border rendering; a
            // real overlap means these are two unrelated stacked windows.
            return intersection.Width <= EdgeToleragePx || intersection.Height <= EdgeToleragePx;
        }

        var horizontallyTouching = Math.Abs(a.Right - b.Left) <= EdgeToleragePx
            || Math.Abs(b.Right - a.Left) <= EdgeToleragePx;
        var verticalOverlap = a.Top < b.Bottom && b.Top < a.Bottom;

        var verticallyTouching = Math.Abs(a.Bottom - b.Top) <= EdgeToleragePx
            || Math.Abs(b.Bottom - a.Top) <= EdgeToleragePx;
        var horizontalOverlap = a.Left < b.Right && b.Left < a.Right;

        return (horizontallyTouching && verticalOverlap) || (verticallyTouching && horizontalOverlap);
    }

    private static bool LooksLikeSnapLayout(List<WindowInfo> component, IntPtr monitor)
    {
        var workArea = MonitorHelper.GetWorkArea(monitor);
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            return false;
        }

        var union = component.Select(w => w.Bounds).Aggregate(Rectangle.Union);
        var coverage = (double)((long)union.Width * union.Height) / ((long)workArea.Width * workArea.Height);
        return coverage >= MinWorkAreaCoverage;
    }
}
