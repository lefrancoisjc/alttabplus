using AltTabPlus.Native;

namespace AltTabPlus.I18n;

internal static class Loc
{
    private static readonly bool Fr = (NativeMethods.GetUserDefaultUILanguage() & 0x3FF) == 0x0C;

    private static string L(string en, string fr) => Fr ? fr : en;

    public static string TrayTooltip => L(
        "AltTabPlus — switcher with groups",
        "AltTabPlus — switcher avec groupes");

    public static string TraySettings => L("Settings…", "Réglages…");
    public static string TrayQuit => L("Quit AltTabPlus", "Quitter AltTabPlus");

    public static string HookInstallFailed(string detail) => L(
        $"Could not install the keyboard hook:\n{detail}\n\nTry running AltTabPlus as administrator.",
        $"Impossible d'installer le hook clavier :\n{detail}\n\nEssaie de relancer AltTabPlus en tant qu'administrateur.");

    public static string HotkeyInUse => L(
        "This shortcut is already used by Windows or another app.",
        "Ce raccourci est déjà utilisé par Windows ou une autre application.");

    public static string StartupFailed(string detail) => L(
        $"Could not change Windows startup:\n{detail}",
        $"Impossible de modifier le démarrage Windows :\n{detail}");

    public static string SettingsTitle => L("Settings", "Réglages");
    public static string SettingsSubtitle => L(
        "Switcher, hot corners, and startup",
        "Switcher, coins chauds et démarrage");

    public static string SectionSwitcher => L("Switcher", "Switcher");
    public static string SectionHotkey => L("Hotkey", "Raccourci");
    public static string SectionHotCorners => L("Hot corners", "Coins chauds");
    public static string SectionIgnored => L("Ignored apps", "Apps ignorées");
    public static string SectionSystem => L("System", "Système");

    public static string ReplaceAltTab => L("Replace Alt+Tab", "Remplacer Alt+Tab");
    public static string ReplaceAltTabHint => L(
        "Hide the Windows switcher and open AltTabPlus",
        "Masque le switcher Windows et ouvre AltTabPlus");

    public static string HiddenWindows => L("Hidden windows", "Fenêtres cachées");
    public static string HiddenWindowsHint => L(
        "Include minimized windows in the switcher",
        "Inclure les fenêtres réduites dans le switcher");

    public static string CurrentMonitor => L("Current monitor", "Moniteur actuel");
    public static string CurrentMonitorHint => L(
        "Only show windows on the screen under the mouse",
        "N'afficher que les fenêtres de l'écran sous la souris");

    public static string OtherDesktops => L("Other desktops", "Autres bureaux");
    public static string OtherDesktopsHint => L(
        "Include windows from other virtual desktops",
        "Inclure les fenêtres des autres bureaux virtuels");

    public static string LaunchOnStartup => L("Launch at startup", "Lancer au démarrage");
    public static string LaunchOnStartupHint => L(
        "Open AltTabPlus when you sign in to Windows",
        "Ouvre AltTabPlus à la connexion Windows");

    public static string TaskbarButton => L("Taskbar button", "Barre des tâches");
    public static string TaskbarButtonHint => L(
        "Keep a button to reopen these settings",
        "Garder un bouton pour rouvrir ces réglages");

    public static string Dwell => L("Delay", "Délai");
    public static string IgnorePlaceholder => L("e.g. spotify", "ex. spotify");
    public static string Add => L("Add", "Ajouter");
    public static string Remove => L("Remove", "Retirer");
    public static string Change => L("Change", "Modifier");
    public static string Clear => L("Clear", "Effacer");

    public static string AlternateHotkey => L("Alternate shortcut", "Raccourci alternatif");
    public static string AlternateHotkeyHint => L(
        "Opens the switcher without Alt. Space expands a group.",
        "Ouvre le switcher sans Alt. Espace déplie un groupe.");

    public static string OverlayHints => L(
        "Type to filter · Delete closes · Alt+` cycles the app",
        "Tape pour filtrer · Suppr ferme · Alt+² cycle l'app");

    public static string PressShortcut => L("Press a shortcut…", "Appuie sur un raccourci…");
    public static string NeedModifier => L(
        "Add Ctrl, Alt, Shift, or Win",
        "Ajoute Ctrl, Alt, Shift ou Win");

    public static string HotkeyNone => L("None", "Aucun");
    public static string KeySpace => L("Space", "Espace");

    public static string CornerTitle => L("Assign an action to a corner", "Attribuer une action à un coin");
    public static string CornerHint => L(
        "Like macOS — push the mouse into the corner.",
        "Comme sur macOS — pousse la souris dans le coin.");

    public static string HotCornerNone => L("None", "Aucune");
    public static string HotCornerDesktop => L("Desktop", "Bureau");
    public static string HotCornerNextDesktop => L("Desktop →", "Bureau →");
    public static string HotCornerPreviousDesktop => L("← Desktop", "← Bureau");

    public static string WindowsCount(int count) => L($"{count} windows", $"{count} fenêtres");
    public static string OtherDesktop => L("other desktop", "autre bureau");

    public static string GroupBack => L("Group  ·  Esc to go back", "Groupe  ·  Échap pour revenir");
    public static string GroupBackFilter(string filter) =>
        L($"Group  ·  {filter}  ·  Esc to go back", $"Groupe  ·  {filter}  ·  Échap pour revenir");
    public static string FilterBanner(string filter) => L($"Filter  ·  {filter}", $"Filtre  ·  {filter}");
    public static string ExpandHint => L("Expand  ·  Space or ↓", "Déplier  ·  Espace ou ↓");
}
