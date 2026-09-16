using System.Text.Json;
using AltTabPlus.Diagnostics;

namespace AltTabPlus.Settings;

internal sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public bool ReplaceAltTab { get; set; } = true;
    public bool ShowHiddenWindows { get; set; } = true;
    public bool CurrentMonitorOnly { get; set; }
    public bool ShowOtherDesktops { get; set; }
    public List<string> IgnoredProcesses { get; set; } = new();
    public int HotCornerDwellMs { get; set; } = 90;
    public bool LaunchOnStartup { get; set; }
    public bool ShowTaskbarButton { get; set; } = true;
    public int HotkeyModifiers { get; set; }
    public int HotkeyKey { get; set; }
    public HotCornerAction HotCornerTopLeft { get; set; }
    public HotCornerAction HotCornerTopRight { get; set; }
    public HotCornerAction HotCornerBottomLeft { get; set; } = HotCornerAction.WinTab;
    public HotCornerAction HotCornerBottomRight { get; set; }

    public bool HasCustomHotkey => HotkeyKey != 0 && HotkeyModifiers != 0;

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AltTabPlus",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("showHiddenWindows", out _))
                {
                    settings.ShowHiddenWindows = true;
                }

                if (!doc.RootElement.TryGetProperty("hotCornerBottomLeft", out _))
                {
                    settings.HotCornerBottomLeft = HotCornerAction.WinTab;
                }

                return settings;
            }
        }
        catch (Exception ex)
        {
            Log.Write($"AppSettings: load failed: {ex.Message}");
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Write($"AppSettings: save failed: {ex.Message}");
        }
    }
}
