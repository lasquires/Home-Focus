namespace HomeFocus.Shared;

public static class HomeFocusPaths
{
    public const string PipeName = "HomeFocusAgentV1";
    public const int ProxyPort = 48190;
    public static string ProgramDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "HomeFocus");
    public static string ConfigPath => Path.Combine(ProgramDataDir, "config.json");
    public static string StatePath => Path.Combine(ProgramDataDir, "state.json");
    public static string FirewallBackupPath => Path.Combine(ProgramDataDir, "firewall-backup.json");
    public static string LogPath => Path.Combine(ProgramDataDir, "HomeFocus.log");
    public static string UpdateDir => Path.Combine(ProgramDataDir, "Updates");
    public static string InstallDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "HomeFocus");
}
