using System.Text.Json;
using TorrWind.Core;
using TorrWind.Core.Models;

namespace TorrWind.Core.Services;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public AppSettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            var defaultSettings = AppSettings.CreateDefault();
            await SaveAsync(defaultSettings, cancellationToken);
            return defaultSettings;
        }

        return await LoadFromFileOrBackupAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AppSettings> LoadExistingAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException("Settings file was not found.", _filePath);
        }

        return await LoadFromFileOrBackupAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = Path.Combine(
            string.IsNullOrWhiteSpace(directory) ? "." : directory,
            Path.GetFileName(_filePath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(_filePath))
            {
                File.Copy(_filePath, GetBackupPath(), overwrite: true);
            }

            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private async Task<AppSettings> LoadFromFileOrBackupAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await LoadFromFileAsync(_filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException && File.Exists(GetBackupPath()))
        {
            return await LoadFromFileAsync(GetBackupPath(), cancellationToken).ConfigureAwait(false);
        }
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
        }
    }

    private string GetBackupPath()
    {
        return _filePath + ".bak";
    }

    private static async Task<AppSettings> LoadFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return Normalize(settings);
    }

    private static AppSettings Normalize(AppSettings? settings)
    {
        settings ??= AppSettings.CreateDefault();
        settings.Servers ??= [];
        settings.SearchProviders ??= [];
        settings.SearchHistory ??= [];
        settings.LocalServer ??= new LocalServerSettings();
        settings.Player ??= new PlayerSettings();
        if (settings.SettingsSchemaVersion < AppSettings.CurrentSettingsSchemaVersion &&
            settings.Player.PreferredPlayer == ExternalPlayerKind.SystemDefault)
        {
            settings.Player.PreferredPlayer = ExternalPlayerKind.BuiltInLibVlc;
        }

        settings.SettingsSchemaVersion = AppSettings.CurrentSettingsSchemaVersion;
        settings.SettingsBackupRetentionCount = settings.SettingsBackupRetentionCount < 0
            ? 0
            : settings.SettingsBackupRetentionCount;

        if (string.IsNullOrWhiteSpace(settings.LocalServer.DataDirectory) ||
            IsLegacyTorrWindDataPath(settings.LocalServer.DataDirectory))
        {
            settings.LocalServer.DataDirectory = AppPaths.DefaultLocalServerDirectory;
        }

        if (string.IsNullOrWhiteSpace(settings.LocalServer.TemporaryDataPath) ||
            IsLegacyTorrWindDataPath(settings.LocalServer.TemporaryDataPath))
        {
            settings.LocalServer.TemporaryDataPath = Path.Combine(AppPaths.DefaultLocalServerDirectory, "cache");
        }

        if (settings.Servers.Count == 0)
        {
            var localServer = ServerProfile.CreateLocal();
            settings.Servers.Add(localServer);
            settings.ActiveServerId = localServer.Id;
        }

        if (settings.ActiveServerId is not null && settings.Servers.All(server => server.Id != settings.ActiveServerId))
        {
            settings.ActiveServerId = settings.Servers[0].Id;
        }

        return settings;
    }

    private static bool IsLegacyTorrWindDataPath(string path)
    {
        var normalized = path.Replace('/', '\\');
        return normalized.Contains("\\AppData\\Roaming\\TorrWind", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("\\ProgramData\\TorrWind", StringComparison.OrdinalIgnoreCase);
    }
}
