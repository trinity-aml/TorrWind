using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TorrWind.Core.Models;

namespace TorrWind.Core.Services;

public sealed class TorrServerClient : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions IndentedSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly HttpClient _httpClient;
    private readonly ServerProfile _server;

    public TorrServerClient(ServerProfile server)
        : this(server, CreateHandler)
    {
    }

    public TorrServerClient(ServerProfile server, Func<ServerProfile, HttpMessageHandler> handlerFactory)
    {
        _server = server;
        _httpClient = new HttpClient(handlerFactory(server))
        {
            BaseAddress = server.BaseUri,
            Timeout = TimeSpan.FromSeconds(30)
        };

        if (!string.IsNullOrWhiteSpace(server.Username))
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(server.Username + ":" + (server.Password ?? string.Empty)));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        }
    }

    public Uri WebUiUri => _server.BaseUri;

    public Uri WebDavUri => new(_server.BaseUri, "dav/");

    public async Task<string> GetEchoAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetStringAsync("echo", cancellationToken).ConfigureAwait(false);
    }

    public async Task<HttpStatusCode> ProbeWebDavAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), "dav/");
        request.Headers.TryAddWithoutValidation("Depth", "0");
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.StatusCode;
    }

    public async Task<IReadOnlyList<TorrentItem>> GetTorrentsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await PostTorrentsAsync(new { action = "list" }, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseTorrentItems(json);
    }

    public async Task<TorrentItem> GetTorrentAsync(string hash, CancellationToken cancellationToken = default)
    {
        using var response = await PostTorrentsAsync(new { action = "get", hash }, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseTorrentItem(json);
    }

    public async Task<TorrentItem> AddMagnetAsync(string magnet, string? title = null, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            action = "add",
            link = magnet,
            title = title ?? string.Empty,
            save_to_db = true
        };

        using var response = await PostTorrentsAsync(payload, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseTorrentItem(json);
    }

    public async Task<IReadOnlyList<TorrentItem>> AddTorrentFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var fileStream = File.OpenRead(filePath);
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(fileStream), "file", Path.GetFileName(filePath));
        form.Add(new StringContent("true"), "save");

        using var response = await _httpClient.PostAsync("torrent/upload", form, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseTorrentItems(json);
    }

    public async Task RemoveTorrentAsync(string hash, CancellationToken cancellationToken = default)
    {
        using var response = await PostTorrentsAsync(new { action = "rem", hash }, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetTorrentMetadataAsync(
        string hash,
        string title,
        string poster,
        string category,
        string data,
        CancellationToken cancellationToken = default)
    {
        using var response = await PostTorrentsAsync(
            new
            {
                action = "set",
                hash,
                title,
                poster,
                category,
                data
            },
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task DropTorrentAsync(string hash, CancellationToken cancellationToken = default)
    {
        using var response = await PostTorrentsAsync(new { action = "drop", hash }, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task WipeTorrentsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await PostTorrentsAsync(new { action = "wipe" }, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public Uri GetPlaybackUri(string hash, int fileId)
    {
        return new Uri(_server.BaseUri, $"play/{Uri.EscapeDataString(hash)}/{fileId}");
    }

    public Uri GetPlaylistUri(string link, string playlistName, bool fromLast = false)
    {
        var fileName = EnsurePlaylistFileName(playlistName);
        var query = "link=" + Uri.EscapeDataString(link) + "&m3u";
        if (fromLast)
        {
            query += "&fromlast";
        }

        var builder = new UriBuilder(new Uri(_server.BaseUri, "stream/" + EscapeStreamPathSegment(fileName)))
        {
            Query = query
        };

        return builder.Uri;
    }

    public Uri GetStreamUri(string link, int fileIndex = 0, string fileName = "", string sessionToken = "")
    {
        var streamName = string.IsNullOrWhiteSpace(fileName)
            ? "stream"
            : Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(streamName))
        {
            streamName = "stream";
        }

        var query = $"link={Uri.EscapeDataString(link)}&index={fileIndex}&play";
        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            query += "&ss=" + Uri.EscapeDataString(sessionToken.Trim());
        }

        var builder = new UriBuilder(new Uri(_server.BaseUri, "stream/" + EscapeStreamPathSegment(streamName)))
        {
            Query = query
        };
        return builder.Uri;
    }

    private static string EnsurePlaylistFileName(string title)
    {
        var fileName = string.IsNullOrWhiteSpace(title) ? "playlist" : Path.GetFileName(title.Trim());
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "playlist";
        }

        return fileName.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : fileName + ".m3u";
    }

    private static string EscapeStreamPathSegment(string value)
    {
        return Uri.EscapeDataString(value)
            .Replace("%28", "(", StringComparison.OrdinalIgnoreCase)
            .Replace("%29", ")", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchTorznabAsync(
        string query,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return (await SearchServerTorznabAsync(query, cancellationToken: cancellationToken).ConfigureAwait(false))
            .Take(Math.Max(1, limit))
            .ToArray();
    }

    public async Task<IReadOnlyList<SearchResult>> SearchServerTorznabAsync(
        string query,
        int index = -1,
        CancellationToken cancellationToken = default)
    {
        var path = $"torznab/search?query={Uri.EscapeDataString(query)}&index={index}";
        return await SearchServerJsonAsync(path, _server.Name + " Torznab", cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchServerRutorAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var path = $"search?query={Uri.EscapeDataString(query)}";
        return await SearchServerJsonAsync(path, _server.Name + " RuTor", cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonDocument> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
                "settings",
                new { action = "get" },
                SerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GetSettingsJsonAsync(CancellationToken cancellationToken = default)
    {
        using var document = await GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var node = JsonNode.Parse(document.RootElement.GetRawText());
        return JsonSerializer.Serialize(node, IndentedSerializerOptions);
    }

    public async Task<LocalServerSettings> GetLocalServerSettingsAsync(
        LocalServerSettings fallback,
        CancellationToken cancellationToken = default)
    {
        using var document = await GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var settings = CloneLocalServerSettings(fallback);
        ApplyEndpointSettings(settings);
        ApplyRuntimeSettings(settings, document.RootElement);
        return settings;
    }

    public async Task ApplySettingsJsonAsync(string settingsJson, CancellationToken cancellationToken = default)
    {
        var settings = JsonNode.Parse(settingsJson) as JsonObject
            ?? throw new InvalidOperationException("Runtime settings JSON must be an object.");

        var payload = new JsonObject
        {
            ["action"] = "set",
            ["sets"] = settings
        };

        using var response = await _httpClient.PostAsJsonAsync("settings", payload, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task ApplyLocalServerSettingsAsync(
        LocalServerSettings settings,
        CancellationToken cancellationToken = default)
    {
        var sets = await GetSettingsNodeAsync(cancellationToken).ConfigureAwait(false);

        sets["CacheSize"] = Math.Max(1, settings.CacheSizeMb) * 1024L * 1024L;
        sets["PreloadCache"] = Math.Clamp(settings.PreloadCachePercent, 0, 100);
        sets["ReaderReadAHead"] = Math.Clamp(settings.ReaderReadAheadPercent, 5, 100);
        sets["TorrentDisconnectTimeout"] = Math.Max(1, settings.TorrentDisconnectTimeoutSeconds);
        sets["ConnectionsLimit"] = Math.Max(1, settings.ConnectionsLimit);
        sets["PeersListenPort"] = Math.Max(0, settings.PeersListenPort);
        sets["UseDisk"] = settings.CacheMode == CacheMode.Disk;
        sets["TorrentsSavePath"] = settings.CacheMode == CacheMode.Disk
            ? ResolveDiskCachePath(settings)
            : string.Empty;
        sets["RemoveCacheOnDrop"] = settings.RemoveCacheOnDrop;
        sets["ForceEncrypt"] = settings.ForceEncrypt;
        sets["RetrackersMode"] = Math.Clamp(settings.RetrackersMode, 0, 3);
        sets["EnableDebug"] = settings.EnableDebug;
        sets["DownloadRateLimit"] = Math.Max(0, settings.DownloadSpeedLimitKb);
        sets["UploadRateLimit"] = Math.Max(0, settings.UploadSpeedLimitKb);
        sets["EnableDLNA"] = settings.EnableDlna;
        sets["FriendlyName"] = settings.FriendlyName ?? string.Empty;
        sets["EnableIPv6"] = settings.EnableIPv6;
        sets["DisableTCP"] = settings.DisableTcp;
        sets["DisableUTP"] = settings.DisableUtp;
        sets["DisableUPNP"] = settings.DisableUpnp;
        sets["DisableDHT"] = settings.DisableDht;
        sets["DisablePEX"] = settings.DisablePex;
        sets["DisableUpload"] = settings.DisableUpload;
        sets["EnableLPD"] = settings.EnableLpd;
        sets["LPDIPv6"] = settings.LpdIPv6;
        sets["ResponsiveMode"] = settings.ResponsiveMode;
        sets["ShowFSActiveTorr"] = settings.ShowFsActiveTorrents;
        sets["StoreSettingsInJson"] = settings.StoreSettingsInJson;
        sets["StoreViewedInJson"] = settings.StoreViewedInJson;
        sets["TrackTimecode"] = settings.TrackTimecode;
        sets["SslPort"] = settings.SslPort;
        sets["ForceHTTPS"] = settings.ForceHttps;
        sets["SslCert"] = settings.CertificatePath;
        sets["SslKey"] = settings.CertificateKeyPath;

        if (sets["TMDBSettings"] is not JsonObject tmdbSettings)
        {
            tmdbSettings = [];
            sets["TMDBSettings"] = tmdbSettings;
        }

        tmdbSettings["APIKey"] = settings.TmdbApiKey ?? string.Empty;
        tmdbSettings["APIURL"] = FirstNotEmpty(settings.TmdbApiUrl, "https://api.themoviedb.org");
        tmdbSettings["ImageURL"] = FirstNotEmpty(settings.TmdbImageUrl, "https://image.tmdb.org");
        tmdbSettings["ImageURLRu"] = FirstNotEmpty(settings.TmdbImageUrlRu, "https://imagetmdb.com");

        var payload = new JsonObject
        {
            ["action"] = "set",
            ["sets"] = sets
        };

        using var response = await _httpClient.PostAsJsonAsync("settings", payload, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<HttpResponseMessage> PostTorrentsAsync(object payload, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("torrents", payload, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return response;
    }

    private async Task<JsonObject> GetSettingsNodeAsync(CancellationToken cancellationToken)
    {
        using var document = await GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var json = document.RootElement.GetRawText();
        return JsonNode.Parse(json) as JsonObject ?? [];
    }

    private static string ResolveDiskCachePath(LocalServerSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.TemporaryDataPath))
        {
            return settings.TemporaryDataPath;
        }

        return Path.Combine(LocalTorrServerConfigurationWriter.GetDataDirectory(settings), "cache");
    }

    private static string FirstNotEmpty(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private void ApplyEndpointSettings(LocalServerSettings settings)
    {
        var endpoint = _server.BaseUri;
        settings.UseSsl = string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        settings.ListenAddress = endpoint.Host;

        if (settings.UseSsl)
        {
            settings.SslPort = endpoint.Port > 0 ? endpoint.Port : 443;
        }
        else
        {
            settings.Port = endpoint.Port > 0 ? endpoint.Port : 80;
        }
    }

    private static LocalServerSettings CloneLocalServerSettings(LocalServerSettings source)
    {
        return new LocalServerSettings
        {
            Enabled = source.Enabled,
            RunAsWindowsService = source.RunAsWindowsService,
            ExecutablePath = source.ExecutablePath,
            InstalledVersion = source.InstalledVersion,
            PreviousExecutablePath = source.PreviousExecutablePath,
            PreviousVersion = source.PreviousVersion,
            DataDirectory = source.DataDirectory,
            TemporaryDataPath = source.TemporaryDataPath,
            ListenAddress = source.ListenAddress,
            Port = source.Port,
            UseHttpAuth = source.UseHttpAuth,
            Username = source.Username,
            Password = source.Password,
            UseSsl = source.UseSsl,
            SslPort = source.SslPort,
            ForceHttps = source.ForceHttps,
            CertificatePath = source.CertificatePath,
            CertificateKeyPath = source.CertificateKeyPath,
            ReadOnlyDatabase = source.ReadOnlyDatabase,
            AllowSearchWithoutAuth = source.AllowSearchWithoutAuth,
            WhiteList = source.WhiteList,
            BlackList = source.BlackList,
            EnableDlna = source.EnableDlna,
            FriendlyName = source.FriendlyName,
            EnableWebDav = source.EnableWebDav,
            CacheMode = source.CacheMode,
            CacheSizeMb = source.CacheSizeMb,
            PreloadCachePercent = source.PreloadCachePercent,
            ReaderReadAheadPercent = source.ReaderReadAheadPercent,
            TorrentDisconnectTimeoutSeconds = source.TorrentDisconnectTimeoutSeconds,
            ConnectionsLimit = source.ConnectionsLimit,
            PeersListenPort = source.PeersListenPort,
            RetrackersMode = source.RetrackersMode,
            RemoveCacheOnDrop = source.RemoveCacheOnDrop,
            ForceEncrypt = source.ForceEncrypt,
            EnableDebug = source.EnableDebug,
            EnableIPv6 = source.EnableIPv6,
            DisableTcp = source.DisableTcp,
            DisableUtp = source.DisableUtp,
            DisableUpnp = source.DisableUpnp,
            DisableDht = source.DisableDht,
            DisablePex = source.DisablePex,
            DisableUpload = source.DisableUpload,
            EnableLpd = source.EnableLpd,
            LpdIPv6 = source.LpdIPv6,
            ResponsiveMode = source.ResponsiveMode,
            ShowFsActiveTorrents = source.ShowFsActiveTorrents,
            StoreSettingsInJson = source.StoreSettingsInJson,
            StoreViewedInJson = source.StoreViewedInJson,
            TrackTimecode = source.TrackTimecode,
            DownloadSpeedLimitKb = source.DownloadSpeedLimitKb,
            UploadSpeedLimitKb = source.UploadSpeedLimitKb,
            AllowLanAccess = source.AllowLanAccess,
            TmdbApiKey = source.TmdbApiKey,
            TmdbApiUrl = source.TmdbApiUrl,
            TmdbImageUrl = source.TmdbImageUrl,
            TmdbImageUrlRu = source.TmdbImageUrlRu
        };
    }

    private static void ApplyRuntimeSettings(LocalServerSettings settings, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        settings.CacheSizeMb = BytesToMegabytes(ReadLong(root, settings.CacheSizeMb * 1024L * 1024L, "CacheSize"));
        settings.PreloadCachePercent = ReadInt(root, settings.PreloadCachePercent, "PreloadCache");
        settings.ReaderReadAheadPercent = ReadInt(root, settings.ReaderReadAheadPercent, "ReaderReadAHead", "ReaderReadAhead");
        settings.TorrentDisconnectTimeoutSeconds = ReadInt(root, settings.TorrentDisconnectTimeoutSeconds, "TorrentDisconnectTimeout");
        settings.ConnectionsLimit = ReadInt(root, settings.ConnectionsLimit, "ConnectionsLimit");
        settings.PeersListenPort = ReadInt(root, settings.PeersListenPort, "PeersListenPort");
        settings.RetrackersMode = Math.Clamp(ReadInt(root, settings.RetrackersMode, "RetrackersMode"), 0, 3);
        settings.RemoveCacheOnDrop = ReadBool(root, settings.RemoveCacheOnDrop, "RemoveCacheOnDrop");
        settings.ForceEncrypt = ReadBool(root, settings.ForceEncrypt, "ForceEncrypt");
        settings.EnableDebug = ReadBool(root, settings.EnableDebug, "EnableDebug");
        settings.DownloadSpeedLimitKb = ReadInt(root, settings.DownloadSpeedLimitKb, "DownloadRateLimit");
        settings.UploadSpeedLimitKb = ReadInt(root, settings.UploadSpeedLimitKb, "UploadRateLimit");
        settings.EnableDlna = ReadBool(root, settings.EnableDlna, "EnableDLNA", "EnableDlna");
        settings.FriendlyName = ReadString(root, settings.FriendlyName, "FriendlyName");
        settings.EnableWebDav = ReadBool(root, settings.EnableWebDav, "EnableWebDAV", "EnableWebDav");
        settings.EnableIPv6 = ReadBool(root, settings.EnableIPv6, "EnableIPv6");
        settings.DisableTcp = ReadBool(root, settings.DisableTcp, "DisableTCP", "DisableTcp");
        settings.DisableUtp = ReadBool(root, settings.DisableUtp, "DisableUTP", "DisableUtp");
        settings.DisableUpnp = ReadBool(root, settings.DisableUpnp, "DisableUPNP", "DisableUpnp");
        settings.DisableDht = ReadBool(root, settings.DisableDht, "DisableDHT", "DisableDht");
        settings.DisablePex = ReadBool(root, settings.DisablePex, "DisablePEX", "DisablePex");
        settings.DisableUpload = ReadBool(root, settings.DisableUpload, "DisableUpload");
        settings.EnableLpd = ReadBool(root, settings.EnableLpd, "EnableLPD", "EnableLpd");
        settings.LpdIPv6 = ReadBool(root, settings.LpdIPv6, "LPDIPv6", "LpdIPv6");
        settings.ResponsiveMode = ReadBool(root, settings.ResponsiveMode, "ResponsiveMode");
        settings.ShowFsActiveTorrents = ReadBool(root, settings.ShowFsActiveTorrents, "ShowFSActiveTorr", "ShowFsActiveTorrents");
        settings.StoreSettingsInJson = ReadBool(root, settings.StoreSettingsInJson, "StoreSettingsInJson");
        settings.StoreViewedInJson = ReadBool(root, settings.StoreViewedInJson, "StoreViewedInJson");
        settings.TrackTimecode = ReadBool(root, settings.TrackTimecode, "TrackTimecode");
        settings.SslPort = ReadInt(root, settings.SslPort, "SslPort", "SSLPort");
        settings.ForceHttps = ReadBool(root, settings.ForceHttps, "ForceHTTPS", "ForceHttps");
        settings.CertificatePath = ReadString(root, settings.CertificatePath, "SslCert", "SSLCert");
        settings.CertificateKeyPath = ReadString(root, settings.CertificateKeyPath, "SslKey", "SSLKey");
        settings.ReadOnlyDatabase = ReadBool(root, settings.ReadOnlyDatabase, "ReadOnlyDB", "ReadOnlyDatabase");
        settings.AllowSearchWithoutAuth = ReadBool(root, settings.AllowSearchWithoutAuth, "AllowSearchWithoutAuth");

        var useDisk = ReadBool(root, settings.CacheMode == CacheMode.Disk, "UseDisk");
        settings.CacheMode = useDisk ? CacheMode.Disk : CacheMode.Memory;
        settings.TemporaryDataPath = ReadString(root, settings.TemporaryDataPath, "TorrentsSavePath");

        if (root.TryGetProperty("TMDBSettings", out var tmdbSettings) && tmdbSettings.ValueKind == JsonValueKind.Object)
        {
            settings.TmdbApiKey = ReadString(tmdbSettings, settings.TmdbApiKey, "APIKey");
            settings.TmdbApiUrl = ReadString(tmdbSettings, settings.TmdbApiUrl, "APIURL", "ApiUrl");
            settings.TmdbImageUrl = ReadString(tmdbSettings, settings.TmdbImageUrl, "ImageURL", "ImageUrl");
            settings.TmdbImageUrlRu = ReadString(tmdbSettings, settings.TmdbImageUrlRu, "ImageURLRu", "ImageUrlRu");
        }
    }

    private static int BytesToMegabytes(long bytes)
    {
        return (int)Math.Max(1, bytes / 1024L / 1024L);
    }

    private static bool ReadBool(JsonElement root, bool fallback, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out var number) => number != 0,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number != 0,
            _ => fallback
        };
    }

    private static int ReadInt(JsonElement root, int fallback, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.Number when value.TryGetInt64(out var number) => number > int.MaxValue ? int.MaxValue : (int)number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => fallback
        };
    }

    private static long ReadLong(JsonElement root, long fallback, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), out var number) => number,
            _ => fallback
        };
    }

    private static string ReadString(JsonElement root, string fallback, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            _ => fallback
        };
    }

    private static bool TryGetProperty(JsonElement root, out JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private async Task<IReadOnlyList<SearchResult>> SearchServerJsonAsync(
        string path,
        string providerName,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!TryGetSearchResultArray(document.RootElement, out var results))
        {
            return [];
        }

        return results
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => SearchResult.FromTorrServerJson(item, providerName))
            .Where(result => !string.IsNullOrWhiteSpace(result.Title))
            .ToArray();
    }

    private static bool TryGetSearchResultArray(JsonElement root, out JsonElement results)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            results = root;
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "results", "Results", "items", "Items", "data", "Data", "torrents", "Torrents" })
            {
                if (root.TryGetProperty(name, out results) && results.ValueKind == JsonValueKind.Array)
                {
                    return true;
                }
            }
        }

        results = default;
        return false;
    }

    private static HttpMessageHandler CreateHandler(ServerProfile server)
    {
        var handler = new HttpClientHandler();

        if (server.IgnoreCertificateErrors)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        if (handler.SupportsAutomaticDecompression)
        {
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        }

        return handler;
    }

    private static IReadOnlyList<TorrentItem> ParseTorrentItems(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var array = root;

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("torrents", out var torrents))
            {
                array = torrents;
            }
            else if (root.TryGetProperty("Torrents", out torrents))
            {
                array = torrents;
            }
            else if (root.TryGetProperty("data", out torrents))
            {
                array = torrents;
            }
            else
            {
                var item = TorrentItem.FromJson(root);
                return HasTorrentIdentity(item) ? [item] : [];
            }
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array
            .EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.Object)
            .Select(TorrentItem.FromJson)
            .Where(HasTorrentIdentity)
            .ToArray();
    }

    private static TorrentItem ParseTorrentItem(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new TorrentItem();
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Object
            ? TorrentItem.FromJson(document.RootElement)
            : new TorrentItem();
    }

    private static bool HasTorrentIdentity(TorrentItem item)
    {
        return !string.IsNullOrWhiteSpace(item.Hash) ||
            !string.IsNullOrWhiteSpace(item.Title) ||
            item.Files.Count > 0;
    }
}
