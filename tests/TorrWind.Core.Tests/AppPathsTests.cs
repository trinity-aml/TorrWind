using TorrWind.Core;

namespace TorrWind.Core.Tests;

public sealed class AppPathsTests
{
    [Theory]
    [MemberData(nameof(WorkingDirectoryPaths))]
    public void RuntimePaths_AreInsideWorkingDirectory(string name, string path)
    {
        var workingDirectory = Path.GetFullPath(AppPaths.WorkingDirectory);
        var fullPath = Path.GetFullPath(path);

        Assert.False(string.IsNullOrWhiteSpace(name));
        Assert.StartsWith(workingDirectory, fullPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UserAndServicePaths_UseSameWorkingDataDirectory()
    {
        Assert.Equal(AppPaths.DataDirectory, AppPaths.UserDataDirectory);
        Assert.Equal(AppPaths.DataDirectory, AppPaths.ProgramDataDirectory);
        Assert.Equal(AppPaths.UserLogsDirectory, AppPaths.ProgramDataLogsDirectory);
        Assert.Equal(AppPaths.UserSettingsFile, AppPaths.ServiceSettingsFile);
    }

    [Fact]
    public void EnsureWorkingDirectories_CreatesExpectedFolders()
    {
        AppPaths.EnsureWorkingDirectories();

        Assert.True(Directory.Exists(AppPaths.DataDirectory));
        Assert.True(Directory.Exists(AppPaths.DefaultLocalServerDirectory));
        Assert.True(Directory.Exists(AppPaths.UserLogsDirectory));
        Assert.True(Directory.Exists(AppPaths.UserSettingsBackupsDirectory));
        Assert.True(Directory.Exists(AppPaths.UpdatesDirectory));
        Assert.True(Directory.Exists(AppPaths.PlaylistsDirectory));
        Assert.True(Directory.Exists(AppPaths.WebView2DataDirectory));
    }

    public static IEnumerable<object[]> WorkingDirectoryPaths()
    {
        yield return [nameof(AppPaths.DataDirectory), AppPaths.DataDirectory];
        yield return [nameof(AppPaths.UserDataDirectory), AppPaths.UserDataDirectory];
        yield return [nameof(AppPaths.ProgramDataDirectory), AppPaths.ProgramDataDirectory];
        yield return [nameof(AppPaths.DefaultLocalServerDirectory), AppPaths.DefaultLocalServerDirectory];
        yield return [nameof(AppPaths.UserLogsDirectory), AppPaths.UserLogsDirectory];
        yield return [nameof(AppPaths.ProgramDataLogsDirectory), AppPaths.ProgramDataLogsDirectory];
        yield return [nameof(AppPaths.UserSettingsBackupsDirectory), AppPaths.UserSettingsBackupsDirectory];
        yield return [nameof(AppPaths.UpdatesDirectory), AppPaths.UpdatesDirectory];
        yield return [nameof(AppPaths.PlaylistsDirectory), AppPaths.PlaylistsDirectory];
        yield return [nameof(AppPaths.WebView2DataDirectory), AppPaths.WebView2DataDirectory];
        yield return [nameof(AppPaths.UserLogFile), AppPaths.UserLogFile];
        yield return [nameof(AppPaths.ServiceLogFile), AppPaths.ServiceLogFile];
        yield return [nameof(AppPaths.MpvPlayerLogFile), AppPaths.MpvPlayerLogFile];
        yield return [nameof(AppPaths.UserSettingsFile), AppPaths.UserSettingsFile];
        yield return [nameof(AppPaths.ServiceSettingsFile), AppPaths.ServiceSettingsFile];
        yield return [nameof(AppPaths.LocalesDirectory), AppPaths.LocalesDirectory];
    }
}
