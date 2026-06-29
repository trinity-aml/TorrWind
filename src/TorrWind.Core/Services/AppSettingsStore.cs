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

        return await LoadFromFileAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AppSettings> LoadExistingAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException("Settings file was not found.", _filePath);
        }

        return await LoadFromFileAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<AppSettings> LoadFromFileAsync(CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(_filePath);
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
        settings.SettingsBackupRetentionCount = settings.SettingsBackupRetentionCount < 0
            ? 0
            : settings.SettingsBackupRetentionCount;

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
}
