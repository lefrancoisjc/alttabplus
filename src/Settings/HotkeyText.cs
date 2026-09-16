using AltTabPlus.I18n;
using AltTabPlus.Native;

namespace AltTabPlus.Settings;

internal static class HotkeyText
{
    public static string Format(int modifiers, int key)
    {
        if (key == 0)
        {
            return Loc.HotkeyNone;
        }

        var parts = new List<string>();
        if ((modifiers & (int)NativeMethods.MOD_WIN) != 0) parts.Add("Win");
        if ((modifiers & (int)NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & (int)NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & (int)NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
        parts.Add(KeyName(key));
        return string.Join(" + ", parts);
    }

    private static string KeyName(int key)
    {
        var keys = (Keys)key;
        return keys switch
        {
            Keys.Space => Loc.KeySpace,
            Keys.Oemtilde => "`",
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            _ => keys.ToString(),
        };
    }
}
