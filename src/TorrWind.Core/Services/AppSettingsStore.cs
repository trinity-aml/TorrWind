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
        settings.Language = string.IsNullOrWhiteSpace(settings.Language) ? "system" : settings.Language.Trim();
        settings.Theme = string.IsNullOrWhiteSpace(settings.Theme) ? "system" : settings.Theme.Trim();
        settings.Servers ??= [];
        settings.SearchProviders ??= [];
        settings.SearchHistory ??= [];
        settings.LocalServer ??= new LocalServerSettings();
        settings.Player ??= new PlayerSettings();
        if (settings.SettingsSchemaVersion < AppSettings.CurrentSettingsSchemaVersion &&
            settings.Player.PreferredPlayer == ExternalPlayerKind.SystemDefault)
        {
            settings.Player.PreferredPlayer = ExternalPlayerKind.BuiltInMpv;
        }

        settings.SettingsSchemaVersion = AppSettings.CurrentSettingsSchemaVersion;
        settings.SettingsBackupRetentionCount = settings.SettingsBackupRetentionCount < 0
            ? 0
            : settings.SettingsBackupRetentionCount;
        NormalizePlayerSettings(settings.Player);
        NormalizeLocalServerSettings(settings.LocalServer);
        settings.Servers = NormalizeServers(settings.Servers);
        settings.SearchProviders = NormalizeSearchProviders(settings.SearchProviders);
        settings.SearchHistory = NormalizeSearchHistory(settings.SearchHistory);

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

        if (settings.ActiveServerId is null ||
            settings.Servers.All(server => server.Id != settings.ActiveServerId))
        {
            settings.ActiveServerId = settings.Servers[0].Id;
        }

        return settings;
    }

    private static void NormalizePlayerSettings(PlayerSettings settings)
    {
        settings.CustomPlayerPath ??= string.Empty;
        if (!Enum.IsDefined(settings.PreferredPlayer))
        {
            settings.PreferredPlayer = ExternalPlayerKind.BuiltInMpv;
        }
    }

    private static void NormalizeLocalServerSettings(LocalServerSettings settings)
    {
        settings.ExecutablePath ??= string.Empty;
        settings.InstalledVersion ??= string.Empty;
        settings.PreviousExecutablePath ??= string.Empty;
        settings.PreviousVersion ??= string.Empty;
        settings.DataDirectory ??= string.Empty;
        settings.TemporaryDataPath ??= string.Empty;
        settings.ListenAddress = string.IsNullOrWhiteSpace(settings.ListenAddress)
            ? "127.0.0.1"
            : settings.ListenAddress.Trim();
        settings.Username ??= string.Empty;
        settings.Password ??= string.Empty;
        settings.CertificatePath ??= string.Empty;
        settings.CertificateKeyPath ??= string.Empty;
        settings.WhiteList ??= string.Empty;
        settings.BlackList ??= string.Empty;
        settings.FriendlyName ??= string.Empty;
        settings.TmdbApiKey ??= string.Empty;
        settings.TmdbApiUrl = string.IsNullOrWhiteSpace(settings.TmdbApiUrl)
            ? "https://api.themoviedb.org"
            : settings.TmdbApiUrl.Trim();
        settings.TmdbImageUrl = string.IsNullOrWhiteSpace(settings.TmdbImageUrl)
            ? "https://image.tmdb.org"
            : settings.TmdbImageUrl.Trim();
        settings.TmdbImageUrlRu = string.IsNullOrWhiteSpace(settings.TmdbImageUrlRu)
            ? "https://imagetmdb.com"
            : settings.TmdbImageUrlRu.Trim();
        settings.Port = settings.Port <= 0 ? 8090 : Math.Clamp(settings.Port, 1, 65535);
        settings.SslPort = settings.SslPort <= 0 ? 8091 : Math.Clamp(settings.SslPort, 1, 65535);
        settings.CacheSizeMb = Math.Max(1, settings.CacheSizeMb);
        settings.PreloadCachePercent = Math.Clamp(settings.PreloadCachePercent, 0, 100);
        settings.ReaderReadAheadPercent = Math.Clamp(settings.ReaderReadAheadPercent, 5, 100);
        settings.TorrentDisconnectTimeoutSeconds = Math.Max(1, settings.TorrentDisconnectTimeoutSeconds);
        settings.ConnectionsLimit = Math.Max(1, settings.ConnectionsLimit);
        settings.PeersListenPort = Math.Clamp(settings.PeersListenPort, 0, 65535);
        settings.RetrackersMode = Math.Clamp(settings.RetrackersMode, 0, 3);
        settings.DownloadSpeedLimitKb = Math.Max(0, settings.DownloadSpeedLimitKb);
        settings.UploadSpeedLimitKb = Math.Max(0, settings.UploadSpeedLimitKb);
    }

    private static List<ServerProfile> NormalizeServers(IEnumerable<ServerProfile?> servers)
    {
        var result = new List<ServerProfile>();
        var seenIds = new HashSet<Guid>();
        foreach (var server in servers)
        {
            if (server is null)
            {
                continue;
            }

            if (server.Id == Guid.Empty || !seenIds.Add(server.Id))
            {
                server.Id = Guid.NewGuid();
                seenIds.Add(server.Id);
            }

            server.Name = string.IsNullOrWhiteSpace(server.Name) ? "TorrServer" : server.Name.Trim();
            server.BaseUrl = string.IsNullOrWhiteSpace(server.BaseUrl)
                ? "http://127.0.0.1:8090"
                : server.BaseUrl.Trim();
            server.Username = string.IsNullOrWhiteSpace(server.Username) ? null : server.Username.Trim();
            server.Password ??= string.Empty;
            result.Add(server);
        }

        return result;
    }

    private static List<SearchProviderSettings> NormalizeSearchProviders(IEnumerable<SearchProviderSettings?> providers)
    {
        var result = new List<SearchProviderSettings>();
        var seenIds = new HashSet<Guid>();
        foreach (var provider in providers)
        {
            if (provider is null)
            {
                continue;
            }

            if (provider.Id == Guid.Empty || !seenIds.Add(provider.Id))
            {
                provider.Id = Guid.NewGuid();
                seenIds.Add(provider.Id);
            }

            provider.Name = string.IsNullOrWhiteSpace(provider.Name) ? "Torznab" : provider.Name.Trim();
            provider.Url = provider.Url?.Trim() ?? string.Empty;
            provider.ApiKey = provider.ApiKey?.Trim() ?? string.Empty;
            provider.Categories = provider.Categories?.Trim() ?? string.Empty;
            provider.TimeoutSeconds = provider.TimeoutSeconds <= 0
                ? 30
                : Math.Clamp(provider.TimeoutSeconds, 5, 180);
            result.Add(provider);
        }

        return result;
    }

    private static List<string> NormalizeSearchHistory(IEnumerable<string?> searchHistory)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in searchHistory)
        {
            var query = item?.Trim();
            if (string.IsNullOrWhiteSpace(query) || !seen.Add(query))
            {
                continue;
            }

            result.Add(query);
            if (result.Count >= 20)
            {
                break;
            }
        }

        return result;
    }

    private static bool IsLegacyTorrWindDataPath(string path)
    {
        var normalized = path.Replace('/', '\\');
        return normalized.Contains("\\AppData\\Roaming\\TorrWind", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("\\ProgramData\\TorrWind", StringComparison.OrdinalIgnoreCase);
    }
}
