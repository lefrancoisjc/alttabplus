using AltTabPlus.Native;
using AltTabPlus.Windows;

namespace AltTabPlus.Switching;

/// <summary>
/// One tile in the overlay: either a single ungrouped window, or a whole
/// Snap Group collapsed into one entry. This is the type that finally
/// delivers on the original ask — "let me Alt-Tab past a group as one
/// thing instead of N things".
/// </summary>
internal sealed class SwitchTarget
{
    public required bool IsGroup { get; init; }
    public required List<WindowInfo> Windows { get; init; }

    /// <summary>Window whose live thumbnail represents this tile (the group's topmost/first window).</summary>
    public IntPtr PreviewHandle => Windows[0].Handle;

    public string DisplayTitle => IsGroup
        ? string.Join("  +  ", Windows.Select(w => Truncate(w.Title, 16)))
        : Windows[0].Title;

    /// <summary>Bring every window in this tile to the front, main one last so it ends up focused.</summary>
    public void Activate()
    {
        foreach (var window in Windows)
        {
            if (NativeMethods.IsIconic(window.Handle))
            {
                NativeMethods.ShowWindow(window.Handle, NativeMethods.SW_RESTORE);
            }
        }

        foreach (var window in Windows.Skip(1))
        {
            NativeMethods.SetForegroundWindow(window.Handle);
        }

        NativeMethods.SetForegroundWindow(Windows[0].Handle);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";
}
