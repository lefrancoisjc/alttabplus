using AltTabPlus.Native;

namespace AltTabPlus.Settings;

internal static class AppPackage
{
    private const int AppmodelErrorNoPackage = 15700;

    public static bool IsPackaged { get; } = Detect();

    public static string AliasPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft",
        "WindowsApps",
        "AltTabPlus.exe");

    private static bool Detect()
    {
        var length = 0;
        return NativeMethods.GetCurrentPackageFullName(ref length, null) != AppmodelErrorNoPackage;
    }
}
