namespace TorrWind.Core;

public static class AppPaths
{
    public const string ApplicationName = "TorrWind";

    public static string UserDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ApplicationName);

    public static string ProgramDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ApplicationName);

    public static string DefaultLocalServerDirectory =>
        Path.Combine(ProgramDataDirectory, "TorrServer");

    public static string UserLogsDirectory =>
        Path.Combine(UserDataDirectory, "logs");

    public static string UserSettingsBackupsDirectory =>
        Path.Combine(UserDataDirectory, "backups");

    public static string ProgramDataLogsDirectory =>
        Path.Combine(ProgramDataDirectory, "logs");

    public static string UserLogFile =>
        Path.Combine(UserLogsDirectory, "gui.jsonl");

    public static string ServiceLogFile =>
        Path.Combine(ProgramDataLogsDirectory, "service.jsonl");

    public static string UserSettingsFile =>
        Path.Combine(UserDataDirectory, "settings.json");

    public static string ServiceSettingsFile =>
        Path.Combine(ProgramDataDirectory, "settings.json");

    public static string LocalesDirectory =>
        Path.Combine(AppContext.BaseDirectory, "locales");

    public static void EnsureUserDirectories()
    {
        Directory.CreateDirectory(UserDataDirectory);
        Directory.CreateDirectory(UserLogsDirectory);
        Directory.CreateDirectory(UserSettingsBackupsDirectory);
    }

    public static void EnsureProgramDataDirectories()
    {
        Directory.CreateDirectory(ProgramDataDirectory);
        Directory.CreateDirectory(DefaultLocalServerDirectory);
        Directory.CreateDirectory(ProgramDataLogsDirectory);
    }
}
