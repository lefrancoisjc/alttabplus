using System.Drawing;

namespace AltTabPlus.Windows;

/// <summary>Snapshot of one Alt-Tab-eligible top-level window at enumeration time.</summary>
internal sealed class WindowInfo
{
    public required IntPtr Handle { get; init; }
    public required string Title { get; init; }
    public required uint ProcessId { get; init; }
    public required Rectangle Bounds { get; init; }
    public required IntPtr MonitorHandle { get; init; }
}
