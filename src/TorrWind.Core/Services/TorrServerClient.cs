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

    public async Task AddMagnetAsync(string magnet, string? title = null, CancellationToken cancellationToken = default)
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
    }

    public async Task AddTorrentFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var fileStream = File.OpenRead(filePath);
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(fileStream), "file", Path.GetFileName(filePath));
        form.Add(new StringContent("true"), "save");

        using var response = await _httpClient.PostAsync("torrent/upload", form, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveTorrentAsync(string hash, CancellationToken cancellationToken = default)
    {
        using var response = await PostTorrentsAsync(new { action = "rem", hash }, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public Uri GetPlaybackUri(string hash, int fileId)
    {
        return new Uri(_server.BaseUri, $"play/{Uri.EscapeDataString(hash)}/{fileId}");
    }

    public Uri GetStreamUri(string link, int fileIndex = 0)
    {
        var builder = new UriBuilder(new Uri(_server.BaseUri, "stream"));
        builder.Query = $"link={Uri.EscapeDataString(link)}&index={fileIndex}&play=1";
        return builder.Uri;
    }

    public async Task<IReadOnlyList<SearchResult>> SearchTorznabAsync(
        string query,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var path = $"torznab/search?query={Uri.EscapeDataString(query)}&limit={limit}";
        using var response = await _httpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return TorznabSearchClient.Parse(xml, _server.Name);
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

    public async Task ApplyLocalServerSettingsAsync(
        LocalServerSettings settings,
        CancellationToken cancellationToken = default)
    {
        var sets = await GetSettingsNodeAsync(cancellationToken).ConfigureAwait(false);

        sets["CacheSize"] = Math.Max(1, settings.CacheSizeMb) * 1024L * 1024L;
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
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray().Select(TorrentItem.FromJson).ToArray();
    }

}
