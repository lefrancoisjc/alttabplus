using AltTabPlus.I18n;
using AltTabPlus.Native;
using AltTabPlus.Windows;

namespace AltTabPlus.Switching;

internal sealed class SwitchTarget
{
    public required bool IsGroup { get; init; }
    public required List<WindowInfo> Windows { get; init; }

    public string DisplayTitle
    {
        get
        {
            var title = IsGroup
                ? $"{Truncate(Windows[0].Title, 28)}  ·  {Loc.WindowsCount(Windows.Count)}"
                : Windows[0].Title;
            if (Windows.Any(w => w.IsOnOtherDesktop))
            {
                title += $"  ·  {Loc.OtherDesktop}";
            }

            return title;
        }
    }

    public uint ProcessId => Windows[0].ProcessId;

    public bool Matches(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return Windows.Any(w =>
            w.Title.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
            || w.ProcessName.Contains(filter, StringComparison.CurrentCultureIgnoreCase));
    }

    public void Activate() =>
        ForegroundActivator.ActivateMany(Windows.Select(w => w.Handle).ToList());

    public void Close()
    {
        foreach (var window in Windows)
        {
            NativeMethods.SendMessage(window.Handle, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";
}
