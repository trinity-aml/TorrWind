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
    {
        _server = server;
        _httpClient = new HttpClient(CreateHandler(server))
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

    public async Task<string> GetEchoAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetStringAsync("echo", cancellationToken).ConfigureAwait(false);
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
        sets["UseDisk"] = settings.CacheMode == CacheMode.Disk;
        sets["TorrentsSavePath"] = settings.CacheMode == CacheMode.Disk
            ? ResolveDiskCachePath(settings)
            : string.Empty;
        sets["DownloadRateLimit"] = Math.Max(0, settings.DownloadSpeedLimitKb);
        sets["UploadRateLimit"] = Math.Max(0, settings.UploadSpeedLimitKb);
        sets["EnableDLNA"] = settings.EnableDlna;
        sets["SslPort"] = settings.SslPort;
        sets["SslCert"] = settings.CertificatePath;
        sets["SslKey"] = settings.CertificateKeyPath;

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

    private async Task<IReadOnlyList<SearchResult>> SearchServerJsonAsync(
        string path,
        string providerName,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return root
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => SearchResult.FromTorrServerJson(item, providerName))
            .Where(result => !string.IsNullOrWhiteSpace(result.Title))
            .ToArray();
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
