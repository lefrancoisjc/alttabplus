namespace AltTabPlus.Windows;

internal sealed class WindowQuery
{
    public bool IncludeHidden { get; init; } = true;
    public bool IncludeOtherDesktops { get; init; }
    public bool CurrentMonitorOnly { get; init; }
    public IntPtr Monitor { get; init; }
    public IReadOnlySet<string> IgnoredProcesses { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
