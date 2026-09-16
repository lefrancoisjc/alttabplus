using AltTabPlus.I18n;

namespace AltTabPlus.Settings;

internal enum HotCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

internal enum HotCornerAction
{
    None = 0,
    WinTab = 1,
    Switcher = 2,
    Desktop = 3,
    NextDesktop = 4,
    PreviousDesktop = 5,
}

internal static class HotCornerText
{
    public static string Action(HotCornerAction action) => action switch
    {
        HotCornerAction.WinTab => "Win+Tab",
        HotCornerAction.Switcher => "Switcher",
        HotCornerAction.Desktop => Loc.HotCornerDesktop,
        HotCornerAction.NextDesktop => Loc.HotCornerNextDesktop,
        HotCornerAction.PreviousDesktop => Loc.HotCornerPreviousDesktop,
        _ => Loc.HotCornerNone,
    };

    public static IEnumerable<HotCornerAction> All { get; } = new[]
    {
        HotCornerAction.None,
        HotCornerAction.WinTab,
        HotCornerAction.Switcher,
        HotCornerAction.Desktop,
        HotCornerAction.NextDesktop,
        HotCornerAction.PreviousDesktop,
    };
}
