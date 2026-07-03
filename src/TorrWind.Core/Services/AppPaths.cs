namespace TorrWind.Core;

public static class AppPaths
{
    public const string ApplicationName = "TorrWind";

    public static string WorkingDirectory =>
        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static string DataDirectory =>
        Path.Combine(WorkingDirectory, "Data");

    public static string UserDataDirectory =>
        DataDirectory;

    public static string ProgramDataDirectory =>
        DataDirectory;

    public static string DefaultLocalServerDirectory =>
        Path.Combine(DataDirectory, "TorrServer");

    public static string UserLogsDirectory =>
        Path.Combine(DataDirectory, "logs");

    public static string UserSettingsBackupsDirectory =>
        Path.Combine(DataDirectory, "backups");

    public static string ProgramDataLogsDirectory =>
        UserLogsDirectory;

    public static string PlaylistsDirectory =>
        Path.Combine(DataDirectory, "playlists");

    public static string UserLogFile =>
        Path.Combine(UserLogsDirectory, "gui.jsonl");

    public static string ServiceLogFile =>
        Path.Combine(ProgramDataLogsDirectory, "service.jsonl");

    public static string MpvPlayerLogFile =>
        Path.Combine(UserLogsDirectory, "mpv-player.log");

    public static string UserSettingsFile =>
        Path.Combine(DataDirectory, "settings.json");

    public static string ServiceSettingsFile =>
        UserSettingsFile;

    public static string LocalesDirectory =>
        Path.Combine(AppContext.BaseDirectory, "locales");

    public static void EnsureUserDirectories()
    {
        EnsureWorkingDirectories();
    }

    public static void EnsureProgramDataDirectories()
    {
        EnsureWorkingDirectories();
    }

    public static void EnsureWorkingDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(DefaultLocalServerDirectory);
        Directory.CreateDirectory(UserLogsDirectory);
        Directory.CreateDirectory(UserSettingsBackupsDirectory);
        Directory.CreateDirectory(PlaylistsDirectory);
    }
}
