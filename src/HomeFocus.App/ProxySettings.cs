using Microsoft.Win32;
using System.Runtime.InteropServices;
using HomeFocus.Shared;

namespace HomeFocus.App;

internal static class ProxySettings
{
    const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    const int INTERNET_OPTION_REFRESH = 37;
    const string InternetKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    const string BackupKey = @"Software\HomeFocus\SavedProxySettings";
    [DllImport("wininet.dll", SetLastError = true)]
    static extern bool InternetSetOption(IntPtr hInternet, int option, IntPtr buffer, int length);

    public static void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(InternetKey);
        // Avoid overwriting a user's settings more than once. Store the original
        // proxy configuration before enabling HomeFocus.
        using var saved = Registry.CurrentUser.CreateSubKey(BackupKey);
        if (saved.GetValue("Saved") is null &&
            !string.Equals(key.GetValue("ProxyServer") as string,
                $"127.0.0.1:{HomeFocusPaths.ProxyPort}", StringComparison.OrdinalIgnoreCase))
        {
            saved.SetValue("ProxyEnable", Convert.ToInt32(key.GetValue("ProxyEnable") ?? 0), RegistryValueKind.DWord);
            saved.SetValue("ProxyServer", key.GetValue("ProxyServer") as string ?? "", RegistryValueKind.String);
            saved.SetValue("ProxyOverride", key.GetValue("ProxyOverride") as string ?? "", RegistryValueKind.String);
            saved.SetValue("Saved", 1, RegistryValueKind.DWord);
        }
        if (Convert.ToInt32(key.GetValue("ProxyEnable") ?? 0) == 1 &&
            string.Equals(key.GetValue("ProxyServer") as string,
                $"127.0.0.1:{HomeFocusPaths.ProxyPort}", StringComparison.OrdinalIgnoreCase)) return;

        key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
        key.SetValue("ProxyServer", $"127.0.0.1:{HomeFocusPaths.ProxyPort}", RegistryValueKind.String);
        key.SetValue("ProxyOverride", "<local>", RegistryValueKind.String);
        Refresh();
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(InternetKey, true);
        if (key is null || !string.Equals(key.GetValue("ProxyServer") as string,
                $"127.0.0.1:{HomeFocusPaths.ProxyPort}", StringComparison.OrdinalIgnoreCase)) return;
        using var saved = Registry.CurrentUser.OpenSubKey(BackupKey, true);
        if (saved?.GetValue("Saved") is not null)
        {
            key.SetValue("ProxyEnable", Convert.ToInt32(saved.GetValue("ProxyEnable") ?? 0), RegistryValueKind.DWord);
            key.SetValue("ProxyServer", saved.GetValue("ProxyServer") as string ?? "", RegistryValueKind.String);
            key.SetValue("ProxyOverride", saved.GetValue("ProxyOverride") as string ?? "", RegistryValueKind.String);
            saved.DeleteValue("Saved", false);
        }
        else
        {
            // Repair a proxy left behind by older HomeFocus builds (1.0.1 and earlier).
            key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
        }
        Refresh();
    }

    static void Refresh()
    {
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
    }
}
