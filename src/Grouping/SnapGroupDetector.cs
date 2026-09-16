using System.Drawing;
using AltTabPlus.Diagnostics;
using AltTabPlus.Native;
using AltTabPlus.Windows;

namespace AltTabPlus.Grouping;

internal static class SnapGroupDetector
{
    private const int EdgeTolerancePx = 24;
    private const int MinSharedEdgePx = 80;
    private const double MinSharedEdgeFraction = 0.4;
    private const double MinWorkAreaCoverage = 0.55;
    private const double MinWindowWorkAreaFraction = 0.10;
    private const double SameCellIou = 0.80;

    public static List<List<WindowInfo>> DetectGroups(IReadOnlyList<WindowInfo> windows)
    {
        var groups = new List<List<WindowInfo>>();
        foreach (var bucket in windows.Where(IsGroupingCandidate)
            .GroupBy(w => (w.MonitorHandle, w.DesktopId)))
        {
            var cells = PickTopmostPerSnapCell(bucket);
            var other = bucket.Any(w => w.IsOnOtherDesktop);
            Log.Write("SnapGroup: cells desktop=" + (other ? "other" : "current")
                + $" id={bucket.Key.DesktopId:N}"
                + " — " + (cells.Count == 0
                    ? "(none)"
                    : string.Join(" | ", cells.Select(w => $"{w.ProcessName}:{w.Bounds}"))));
            groups.AddRange(FindNonOverlappingTilings(cells, bucket.Key.MonitorHandle));
        }

        return groups;
    }

    private static bool IsGroupingCandidate(WindowInfo window)
    {
        if (window.IsMinimized || window.IsMaximized)
        {
            return false;
        }

        var bounds = window.Bounds;
        if (bounds.Width < 120 || bounds.Height < 80 || bounds.Left < -10_000)
        {
            return false;
        }

        var workArea = MonitorHelper.GetWorkArea(window.MonitorHandle);
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            return false;
        }

        var minArea = (long)workArea.Width * workArea.Height * (long)(MinWindowWorkAreaFraction * 100) / 100;
        if ((long)bounds.Width * bounds.Height < minArea)
        {
            return false;
        }

        return IsAnchoredToWorkArea(bounds, workArea);
    }

    private static List<WindowInfo> PickTopmostPerSnapCell(IEnumerable<WindowInfo> candidates)
    {
        var cells = new List<WindowInfo>();
        foreach (var window in candidates.OrderBy(w => w.ZOrder))
        {
            var occupant = cells.FirstOrDefault(c => SameSnapCell(c.Bounds, window.Bounds));
            if (occupant is not null)
            {
                Log.Write(
                    $"SnapGroup: skip occluded {window.ProcessName}:'{Truncate(window.Title, 24)}' "
                    + $"behind {occupant.ProcessName}:'{Truncate(occupant.Title, 24)}'");
                continue;
            }

            if (IsBuriedBehindTiles(window.Bounds, cells))
            {
                Log.Write(
                    $"SnapGroup: skip buried leftover {window.ProcessName}:'{Truncate(window.Title, 24)}' {window.Bounds}");
                continue;
            }

            cells.Add(window);
        }

        return cells;
    }

    private static List<List<WindowInfo>> FindNonOverlappingTilings(List<WindowInfo> cells, IntPtr monitor)
    {
        var remaining = cells.OrderBy(w => w.ZOrder).ToList();
        var used = new HashSet<WindowInfo>();
        var groups = new List<List<WindowInfo>>();

        foreach (var seed in remaining)
        {
            if (used.Contains(seed))
            {
                continue;
            }

            var tiling = GrowNonOverlapping(seed, remaining, used);
            if (tiling.Count < 2 || !LooksLikeSnapLayout(tiling, monitor))
            {
                continue;
            }

            var ordered = tiling.OrderBy(w => w.ZOrder).ToList();
            groups.Add(ordered);
            foreach (var window in tiling)
            {
                used.Add(window);
            }

            Log.Write(
                "SnapGroup: "
                + string.Join(" | ", ordered.Select(w => $"{w.ProcessName}:'{Truncate(w.Title, 32)}' {w.Bounds}")));
        }

        return groups;
    }

    private static List<WindowInfo> GrowNonOverlapping(
        WindowInfo seed,
        List<WindowInfo> cells,
        HashSet<WindowInfo> used)
    {
        var group = new List<WindowInfo> { seed };
        var grew = true;
        while (grew)
        {
            grew = false;
            foreach (var candidate in cells)
            {
                if (used.Contains(candidate) || group.Contains(candidate))
                {
                    continue;
                }

                if (group.Any(member => OverlapsMaterially(member.Bounds, candidate.Bounds)))
                {
                    continue;
                }

                if (!group.Any(member => AreAdjacent(member.Bounds, candidate.Bounds)))
                {
                    continue;
                }

                group.Add(candidate);
                grew = true;
            }
        }

        return group;
    }

    private static bool OverlapsMaterially(Rectangle a, Rectangle b)
    {
        if (!a.IntersectsWith(b))
        {
            return false;
        }

        var intersection = Rectangle.Intersect(a, b);
        var minArea = Math.Min((long)a.Width * a.Height, (long)b.Width * b.Height);
        return minArea > 0 && (long)intersection.Width * intersection.Height > minArea / 20;
    }

    private static bool IsBuriedBehindTiles(Rectangle bounds, List<WindowInfo> frontCells)
    {
        var area = (long)bounds.Width * bounds.Height;
        if (area <= 0 || frontCells.Count == 0)
        {
            return false;
        }

        long covered = 0;
        foreach (var cell in frontCells)
        {
            if (!bounds.IntersectsWith(cell.Bounds))
            {
                continue;
            }

            var intersection = Rectangle.Intersect(bounds, cell.Bounds);
            var interArea = (long)intersection.Width * intersection.Height;
            var cellArea = (long)cell.Bounds.Width * cell.Bounds.Height;
            if (cellArea > 0 && interArea * 100 >= cellArea * 80)
            {
                covered += interArea;
            }
        }

        return covered * 100 >= area * 80;
    }

    private static bool SameSnapCell(Rectangle a, Rectangle b)
    {
        if (!a.IntersectsWith(b))
        {
            return false;
        }

        var intersection = Rectangle.Intersect(a, b);
        var overlap = (long)intersection.Width * intersection.Height;
        var union = (long)a.Width * a.Height + (long)b.Width * b.Height - overlap;
        return union > 0 && overlap * 100 >= union * (long)(SameCellIou * 100);
    }

    private static bool IsAnchoredToWorkArea(Rectangle bounds, Rectangle work)
    {
        var t = Math.Max(EdgeTolerancePx, 48);
        var edges = 0;
        if (Math.Abs(bounds.Left - work.Left) <= t) edges++;
        if (Math.Abs(bounds.Right - work.Right) <= t) edges++;
        if (Math.Abs(bounds.Top - work.Top) <= t) edges++;
        if (Math.Abs(bounds.Bottom - work.Bottom) <= t) edges++;
        return edges >= 2;
    }

    private static bool AreAdjacent(Rectangle a, Rectangle b)
    {
        if (a.Width <= 0 || a.Height <= 0 || b.Width <= 0 || b.Height <= 0)
        {
            return false;
        }

        if (a.IntersectsWith(b))
        {
            var intersection = Rectangle.Intersect(a, b);
            var minArea = Math.Min((long)a.Width * a.Height, (long)b.Width * b.Height);
            if (minArea > 0 && (long)intersection.Width * intersection.Height > minArea / 20)
            {
                return false;
            }
        }

        return SharesVerticalEdge(a, b) || SharesHorizontalEdge(a, b);
    }

    private static bool SharesVerticalEdge(Rectangle a, Rectangle b)
    {
        var gap = Math.Min(Math.Abs(a.Right - b.Left), Math.Abs(b.Right - a.Left));
        if (gap > EdgeTolerancePx)
        {
            return false;
        }

        var share = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
        return share >= RequiredSharedEdge(Math.Min(a.Height, b.Height));
    }

    private static bool SharesHorizontalEdge(Rectangle a, Rectangle b)
    {
        var gap = Math.Min(Math.Abs(a.Bottom - b.Top), Math.Abs(b.Bottom - a.Top));
        if (gap > EdgeTolerancePx)
        {
            return false;
        }

        var share = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
        return share >= RequiredSharedEdge(Math.Min(a.Width, b.Width));
    }

    private static int RequiredSharedEdge(int smallerSide) =>
        Math.Max(MinSharedEdgePx, (int)(smallerSide * MinSharedEdgeFraction));

    private static bool LooksLikeSnapLayout(List<WindowInfo> component, IntPtr monitor)
    {
        var workArea = MonitorHelper.GetWorkArea(monitor);
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            return false;
        }

        var covered = component.Sum(w => (long)w.Bounds.Width * w.Bounds.Height);
        var work = (long)workArea.Width * workArea.Height;
        return work > 0 && (double)covered / work >= MinWorkAreaCoverage;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";
}
