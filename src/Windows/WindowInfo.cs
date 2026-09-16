using System.Drawing;

namespace AltTabPlus.Windows;

internal sealed class WindowInfo
{
    public required IntPtr Handle { get; init; }
    public required string Title { get; init; }
    public required string ProcessName { get; init; }
    public required uint ProcessId { get; init; }
    public required Rectangle Bounds { get; init; }
    public required IntPtr MonitorHandle { get; init; }
    public required int ZOrder { get; init; }
    public required bool IsMinimized { get; init; }
    public required bool IsMaximized { get; init; }
    public bool IsOnOtherDesktop { get; init; }
    public Guid DesktopId { get; init; }
}
