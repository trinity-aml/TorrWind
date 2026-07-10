using System.Net;
using System.Text;
using System.Text.Json;
using TorrWind.Core.Models;
using TorrWind.Core.Services;

namespace TorrWind.Core.Tests;

public sealed class TorrServerClientSettingsTests
{
    [Fact]
    public async Task GetLocalServerSettingsAsync_ReadsRuntimeSettingsAndEndpoint()
    {
        using var client = CreateClient(
            new ServerProfile { BaseUrl = "https://media.local:9443" },
            request =>
            {
                AssertSettingsAction(request, "get");
                return Json("""
                    {
                      "CacheSize": 67108864,
                      "PreloadCache": 40,
                      "ReaderReadAHead": 85,
                      "TorrentDisconnectTimeout": "45",
                      "ConnectionsLimit": 80,
                      "PeersListenPort": 50000,
                      "RetrackersMode": 3,
                      "RemoveCacheOnDrop": true,
                      "ForceEncrypt": 1,
                      "EnableDebug": "true",
                      "DownloadRateLimit": 1024,
                      "UploadRateLimit": 512,
                      "EnableDLNA": true,
                      "FriendlyName": "TorrWind DLNA",
                      "EnableWebDAV": true,
                      "EnableIPv6": true,
                      "DisableTCP": true,
                      "DisableUTP": true,
                      "DisableUPNP": true,
                      "DisableDHT": true,
                      "DisablePEX": true,
                      "DisableUpload": true,
                      "EnableLPD": false,
                      "LPDIPv6": true,
                      "ResponsiveMode": false,
                      "ShowFSActiveTorr": false,
                      "StoreSettingsInJson": true,
                      "StoreViewedInJson": true,
                      "TrackTimecode": true,
                      "UseDisk": true,
                      "TorrentsSavePath": "D:\\Cache",
                      "SslPort": 9443,
                      "ForceHTTPS": true,
                      "SslCert": "D:\\cert.pem",
                      "SslKey": "D:\\cert.key",
                      "ReadOnlyDB": true,
                      "AllowSearchWithoutAuth": true,
                      "TMDBSettings": {
                        "APIKey": "tmdb-key",
                        "APIURL": "https://api.tmdb.test",
                        "ImageURL": "https://img.tmdb.test",
                        "ImageURLRu": "https://ru.tmdb.test"
                      }
                    }
                    """);
            });

        var settings = await client.GetLocalServerSettingsAsync(new LocalServerSettings
        {
            DataDirectory = "D:\\Data",
            TemporaryDataPath = "D:\\OldCache",
            CacheSizeMb = 16,
            CacheMode = CacheMode.Memory
        });

        Assert.True(settings.UseSsl);
        Assert.Equal("media.local", settings.ListenAddress);
        Assert.Equal(9443, settings.SslPort);
        Assert.Equal(64, settings.CacheSizeMb);
        Assert.Equal(40, settings.PreloadCachePercent);
        Assert.Equal(85, settings.ReaderReadAheadPercent);
        Assert.Equal(45, settings.TorrentDisconnectTimeoutSeconds);
        Assert.Equal(80, settings.ConnectionsLimit);
        Assert.Equal(50000, settings.PeersListenPort);
        Assert.Equal(3, settings.RetrackersMode);
        Assert.True(settings.RemoveCacheOnDrop);
        Assert.True(settings.ForceEncrypt);
        Assert.True(settings.EnableDebug);
        Assert.Equal(1024, settings.DownloadSpeedLimitKb);
        Assert.Equal(512, settings.UploadSpeedLimitKb);
        Assert.True(settings.EnableDlna);
        Assert.Equal("TorrWind DLNA", settings.FriendlyName);
        Assert.True(settings.EnableWebDav);
        Assert.True(settings.EnableIPv6);
        Assert.True(settings.DisableTcp);
        Assert.True(settings.DisableUtp);
        Assert.True(settings.DisableUpnp);
        Assert.True(settings.DisableDht);
        Assert.True(settings.DisablePex);
        Assert.True(settings.DisableUpload);
        Assert.False(settings.EnableLpd);
        Assert.True(settings.LpdIPv6);
        Assert.False(settings.ResponsiveMode);
        Assert.False(settings.ShowFsActiveTorrents);
        Assert.True(settings.StoreSettingsInJson);
        Assert.True(settings.StoreViewedInJson);
        Assert.True(settings.TrackTimecode);
        Assert.Equal(CacheMode.Disk, settings.CacheMode);
        Assert.Equal("D:\\Cache", settings.TemporaryDataPath);
        Assert.True(settings.ForceHttps);
        Assert.Equal("D:\\cert.pem", settings.CertificatePath);
        Assert.Equal("D:\\cert.key", settings.CertificateKeyPath);
        Assert.True(settings.ReadOnlyDatabase);
        Assert.True(settings.AllowSearchWithoutAuth);
        Assert.Equal("tmdb-key", settings.TmdbApiKey);
        Assert.Equal("https://api.tmdb.test", settings.TmdbApiUrl);
        Assert.Equal("https://img.tmdb.test", settings.TmdbImageUrl);
        Assert.Equal("https://ru.tmdb.test", settings.TmdbImageUrlRu);
        Assert.Equal("D:\\Data", settings.DataDirectory);
    }

    [Fact]
    public async Task ApplyLocalServerSettingsAsync_PostsClampedRuntimeSettingsAndTmdbSettings()
    {
        JsonElement? setPayload = null;
        using var client = CreateClient(
            new ServerProfile { BaseUrl = "http://127.0.0.1:8090" },
            request =>
            {
                using var requestDocument = ReadJson(request);
                var action = requestDocument.RootElement.GetProperty("action").GetString();
                if (string.Equals(action, "get", StringComparison.Ordinal))
                {
                    return Json("""
                        {
                          "ExistingSetting": "keep",
                          "TMDBSettings": {
                            "Existing": "value"
                          }
                        }
                        """);
                }

                Assert.Equal("set", action);
                setPayload = requestDocument.RootElement.Clone();
                return Json("{}");
            });

        await client.ApplyLocalServerSettingsAsync(new LocalServerSettings
        {
            CacheSizeMb = 0,
            PreloadCachePercent = 150,
            ReaderReadAheadPercent = 1,
            TorrentDisconnectTimeoutSeconds = 0,
            ConnectionsLimit = 0,
            PeersListenPort = -1,
            CacheMode = CacheMode.Disk,
            TemporaryDataPath = "E:\\Torrents",
            RemoveCacheOnDrop = true,
            ForceEncrypt = true,
            RetrackersMode = 9,
            EnableDebug = true,
            DownloadSpeedLimitKb = -100,
            UploadSpeedLimitKb = -200,
            EnableDlna = true,
            FriendlyName = "Living Room",
            EnableIPv6 = true,
            DisableTcp = true,
            DisableUtp = true,
            DisableUpnp = true,
            DisableDht = true,
            DisablePex = true,
            DisableUpload = true,
            EnableLpd = false,
            LpdIPv6 = true,
            ResponsiveMode = false,
            ShowFsActiveTorrents = false,
            StoreSettingsInJson = true,
            StoreViewedInJson = true,
            TrackTimecode = true,
            SslPort = 9443,
            ForceHttps = true,
            CertificatePath = "E:\\ssl\\cert.pem",
            CertificateKeyPath = "E:\\ssl\\key.pem",
            TmdbApiKey = "tmdb-key",
            TmdbApiUrl = "",
            TmdbImageUrl = "https://images.example",
            TmdbImageUrlRu = ""
        });

        Assert.NotNull(setPayload);
        var sets = setPayload.Value.GetProperty("sets");
        Assert.Equal("keep", sets.GetProperty("ExistingSetting").GetString());
        Assert.Equal(1024 * 1024, sets.GetProperty("CacheSize").GetInt64());
        Assert.Equal(100, sets.GetProperty("PreloadCache").GetInt32());
        Assert.Equal(5, sets.GetProperty("ReaderReadAHead").GetInt32());
        Assert.Equal(1, sets.GetProperty("TorrentDisconnectTimeout").GetInt32());
        Assert.Equal(1, sets.GetProperty("ConnectionsLimit").GetInt32());
        Assert.Equal(0, sets.GetProperty("PeersListenPort").GetInt32());
        Assert.True(sets.GetProperty("UseDisk").GetBoolean());
        Assert.Equal("E:\\Torrents", sets.GetProperty("TorrentsSavePath").GetString());
        Assert.True(sets.GetProperty("RemoveCacheOnDrop").GetBoolean());
        Assert.True(sets.GetProperty("ForceEncrypt").GetBoolean());
        Assert.Equal(3, sets.GetProperty("RetrackersMode").GetInt32());
        Assert.True(sets.GetProperty("EnableDebug").GetBoolean());
        Assert.Equal(0, sets.GetProperty("DownloadRateLimit").GetInt32());
        Assert.Equal(0, sets.GetProperty("UploadRateLimit").GetInt32());
        Assert.True(sets.GetProperty("EnableDLNA").GetBoolean());
        Assert.Equal("Living Room", sets.GetProperty("FriendlyName").GetString());
        Assert.True(sets.GetProperty("EnableIPv6").GetBoolean());
        Assert.True(sets.GetProperty("DisableTCP").GetBoolean());
        Assert.True(sets.GetProperty("DisableUTP").GetBoolean());
        Assert.True(sets.GetProperty("DisableUPNP").GetBoolean());
        Assert.True(sets.GetProperty("DisableDHT").GetBoolean());
        Assert.True(sets.GetProperty("DisablePEX").GetBoolean());
        Assert.True(sets.GetProperty("DisableUpload").GetBoolean());
        Assert.False(sets.GetProperty("EnableLPD").GetBoolean());
        Assert.True(sets.GetProperty("LPDIPv6").GetBoolean());
        Assert.False(sets.GetProperty("ResponsiveMode").GetBoolean());
        Assert.False(sets.GetProperty("ShowFSActiveTorr").GetBoolean());
        Assert.True(sets.GetProperty("StoreSettingsInJson").GetBoolean());
        Assert.True(sets.GetProperty("StoreViewedInJson").GetBoolean());
        Assert.True(sets.GetProperty("TrackTimecode").GetBoolean());
        Assert.Equal(9443, sets.GetProperty("SslPort").GetInt32());
        Assert.True(sets.GetProperty("ForceHTTPS").GetBoolean());
        Assert.Equal("E:\\ssl\\cert.pem", sets.GetProperty("SslCert").GetString());
        Assert.Equal("E:\\ssl\\key.pem", sets.GetProperty("SslKey").GetString());

        var tmdb = sets.GetProperty("TMDBSettings");
        Assert.Equal("value", tmdb.GetProperty("Existing").GetString());
        Assert.Equal("tmdb-key", tmdb.GetProperty("APIKey").GetString());
        Assert.Equal("https://api.themoviedb.org", tmdb.GetProperty("APIURL").GetString());
        Assert.Equal("https://images.example", tmdb.GetProperty("ImageURL").GetString());
        Assert.Equal("https://imagetmdb.com", tmdb.GetProperty("ImageURLRu").GetString());
    }

    [Fact]
    public async Task ApplyLocalServerSettingsAsync_ClearsDiskPathWhenMemoryCacheIsSelected()
    {
        JsonElement? setPayload = null;
        using var client = CreateClient(
            new ServerProfile { BaseUrl = "http://127.0.0.1:8090" },
            request =>
            {
                using var requestDocument = ReadJson(request);
                if (requestDocument.RootElement.GetProperty("action").GetString() == "get")
                {
                    return Json("{\"TorrentsSavePath\":\"old\"}");
                }

                setPayload = requestDocument.RootElement.Clone();
                return Json("{}");
            });

        await client.ApplyLocalServerSettingsAsync(new LocalServerSettings
        {
            CacheMode = CacheMode.Memory,
            TemporaryDataPath = "E:\\ShouldNotBeSent",
            CacheSizeMb = 64
        });

        var sets = setPayload!.Value.GetProperty("sets");
        Assert.False(sets.GetProperty("UseDisk").GetBoolean());
        Assert.Equal(string.Empty, sets.GetProperty("TorrentsSavePath").GetString());
    }

    [Fact]
    public async Task ApplySettingsJsonAsync_PostsRawRuntimeSettingsAsSetsObject()
    {
        JsonElement? setPayload = null;
        using var client = CreateClient(
            new ServerProfile { BaseUrl = "http://127.0.0.1:8090" },
            request =>
            {
                setPayload = ReadJson(request).RootElement.Clone();
                return Json("{}");
            });

        await client.ApplySettingsJsonAsync("{\"CacheSize\":67108864,\"EnableDLNA\":true}");

        Assert.Equal("set", setPayload!.Value.GetProperty("action").GetString());
        var sets = setPayload.Value.GetProperty("sets");
        Assert.Equal(67108864, sets.GetProperty("CacheSize").GetInt64());
        Assert.True(sets.GetProperty("EnableDLNA").GetBoolean());
    }

    private static TorrServerClient CreateClient(
        ServerProfile server,
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        return new TorrServerClient(server, _ => new StaticResponseHandler(responder));
    }

    private static JsonDocument ReadJson(HttpRequestMessage request)
    {
        var content = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "{}";
        return JsonDocument.Parse(content);
    }

    private static void AssertSettingsAction(HttpRequestMessage request, string action)
    {
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/settings", request.RequestUri?.AbsolutePath);
        using var requestDocument = ReadJson(request);
        Assert.Equal(action, requestDocument.RootElement.GetProperty("action").GetString());
    }

    private static HttpResponseMessage Json(string value)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(value, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StaticResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
