using System.Text.Json;
using TorrWind.Core;
using TorrWind.Core.Models;
using TorrWind.Core.Services;

namespace TorrWind.Core.Tests;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_CreatesDefaultSettingsWhenFileDoesNotExist()
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = Path.Combine(directory.Path, "settings.json");
        var store = new AppSettingsStore(filePath);

        var settings = await store.LoadAsync();

        Assert.True(File.Exists(filePath));
        Assert.Equal(AppSettings.CurrentSettingsSchemaVersion, settings.SettingsSchemaVersion);
        Assert.Equal(64, settings.LocalServer.CacheSizeMb);
        Assert.Equal(ExternalPlayerKind.BuiltInMpv, settings.Player.PreferredPlayer);
        Assert.Equal(AppPaths.DefaultLocalServerDirectory, settings.LocalServer.DataDirectory);
        Assert.Equal(Path.Combine(AppPaths.DefaultLocalServerDirectory, "cache"), settings.LocalServer.TemporaryDataPath);
        var server = Assert.Single(settings.Servers);
        Assert.True(server.IsLocal);
        Assert.Equal(server.Id, settings.ActiveServerId);
    }

    [Fact]
    public async Task LoadExistingAsync_NormalizesLegacyPathsAndSchemaDefaults()
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = Path.Combine(directory.Path, "settings.json");
        var serverId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        await File.WriteAllTextAsync(filePath, $$"""
            {
              "settingsSchemaVersion": 0,
              "activeServerId": "22222222-2222-2222-2222-222222222222",
              "servers": [
                {
                  "id": "{{serverId}}",
                  "name": "Remote",
                  "baseUrl": "http://192.168.1.2:8090"
                }
              ],
              "localServer": {
                "dataDirectory": "C:\\Users\\user\\AppData\\Roaming\\TorrWind\\Data",
                "temporaryDataPath": "C:\\ProgramData\\TorrWind\\cache"
              },
              "player": {
                "preferredPlayer": 0
              },
              "settingsBackupRetentionCount": -10
            }
            """);

        var settings = await new AppSettingsStore(filePath).LoadExistingAsync();

        Assert.Equal(AppSettings.CurrentSettingsSchemaVersion, settings.SettingsSchemaVersion);
        Assert.Equal(ExternalPlayerKind.BuiltInMpv, settings.Player.PreferredPlayer);
        Assert.Equal(AppPaths.DefaultLocalServerDirectory, settings.LocalServer.DataDirectory);
        Assert.Equal(Path.Combine(AppPaths.DefaultLocalServerDirectory, "cache"), settings.LocalServer.TemporaryDataPath);
        Assert.Equal(0, settings.SettingsBackupRetentionCount);
        Assert.Equal(serverId, settings.ActiveServerId);
    }

    [Fact]
    public async Task LoadExistingAsync_FallsBackToBackupWhenMainSettingsAreCorrupt()
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = Path.Combine(directory.Path, "settings.json");
        await File.WriteAllTextAsync(filePath, "{ not-json");
        await File.WriteAllTextAsync(
            filePath + ".bak",
            JsonSerializer.Serialize(new AppSettings { Language = "ru" }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var settings = await new AppSettingsStore(filePath).LoadExistingAsync();

        Assert.Equal("ru", settings.Language);
    }

    [Fact]
    public async Task LoadAsync_RestoresSettingsFromBackupWhenMainSettingsAreMissing()
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = Path.Combine(directory.Path, "settings.json");
        await File.WriteAllTextAsync(
            filePath + ".bak",
            JsonSerializer.Serialize(new AppSettings { Language = "ru" }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var settings = await new AppSettingsStore(filePath).LoadAsync();

        Assert.Equal("ru", settings.Language);
        Assert.True(File.Exists(filePath));
        var restored = await new AppSettingsStore(filePath).LoadExistingAsync();
        Assert.Equal("ru", restored.Language);
    }

    [Fact]
    public async Task LoadExistingAsync_NormalizesNullCollectionsAndCreatesLocalServer()
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = Path.Combine(directory.Path, "settings.json");
        await File.WriteAllTextAsync(filePath, """
            {
              "servers": null,
              "searchProviders": null,
              "searchHistory": null,
              "localServer": null,
              "player": null
            }
            """);

        var settings = await new AppSettingsStore(filePath).LoadExistingAsync();

        Assert.NotNull(settings.SearchProviders);
        Assert.NotNull(settings.SearchHistory);
        Assert.NotNull(settings.LocalServer);
        Assert.NotNull(settings.Player);
        var server = Assert.Single(settings.Servers);
        Assert.True(server.IsLocal);
        Assert.Equal(server.Id, settings.ActiveServerId);
        Assert.Equal(AppPaths.DefaultLocalServerDirectory, settings.LocalServer.DataDirectory);
        Assert.Equal(Path.Combine(AppPaths.DefaultLocalServerDirectory, "cache"), settings.LocalServer.TemporaryDataPath);
    }

    [Fact]
    public async Task LoadExistingAsync_DropsNullCollectionItemsAndNormalizesImportedValues()
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = Path.Combine(directory.Path, "settings.json");
        var duplicateServerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var duplicateProviderId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        await File.WriteAllTextAsync(filePath, $$"""
            {
              "language": " ru ",
              "theme": " dark ",
              "servers": [
                null,
                {
                  "id": "00000000-0000-0000-0000-000000000000",
                  "name": "  ",
                  "baseUrl": null,
                  "username": " user "
                },
                {
                  "id": "{{duplicateServerId}}",
                  "name": " First ",
                  "baseUrl": " 192.168.1.2:8090 "
                },
                {
                  "id": "{{duplicateServerId}}",
                  "name": " Second ",
                  "baseUrl": " http://192.168.1.3:8090 "
                }
              ],
              "searchProviders": [
                null,
                {
                  "id": "00000000-0000-0000-0000-000000000000",
                  "name": "  ",
                  "url": null,
                  "apiKey": null,
                  "categories": " 2000 ",
                  "timeoutSeconds": 0
                },
                {
                  "id": "{{duplicateProviderId}}",
                  "name": " Jackett ",
                  "url": " http://indexer.local/api ",
                  "apiKey": " key ",
                  "timeoutSeconds": 999
                },
                {
                  "id": "{{duplicateProviderId}}",
                  "name": " Prowlarr ",
                  "url": " http://prowlarr.local/api ",
                  "timeoutSeconds": 2
                }
              ],
              "searchHistory": [null, "  venom  ", "", "VENOM", " dune "],
              "localServer": {
                "listenAddress": "  ",
                "port": 70000,
                "sslPort": 0,
                "cacheSizeMb": 0,
                "preloadCachePercent": 150,
                "readerReadAheadPercent": 1,
                "torrentDisconnectTimeoutSeconds": 0,
                "connectionsLimit": 0,
                "peersListenPort": 70000,
                "retrackersMode": 9,
                "downloadSpeedLimitKb": -1,
                "uploadSpeedLimitKb": -2,
                "tmdbApiUrl": "",
                "tmdbImageUrl": null,
                "tmdbImageUrlRu": "  "
              },
              "player": {
                "preferredPlayer": 999,
                "customPlayerPath": null
              }
            }
            """);

        var settings = await new AppSettingsStore(filePath).LoadExistingAsync();

        Assert.Equal("ru", settings.Language);
        Assert.Equal("dark", settings.Theme);
        Assert.Equal(3, settings.Servers.Count);
        Assert.All(settings.Servers, server => Assert.NotEqual(Guid.Empty, server.Id));
        Assert.Equal(settings.Servers.Count, settings.Servers.Select(server => server.Id).Distinct().Count());
        Assert.Equal("TorrServer", settings.Servers[0].Name);
        Assert.Equal("http://127.0.0.1:8090", settings.Servers[0].BaseUrl);
        Assert.Equal("user", settings.Servers[0].Username);
        Assert.Equal("First", settings.Servers[1].Name);
        Assert.Equal("192.168.1.2:8090", settings.Servers[1].BaseUrl);
        Assert.Equal(settings.Servers[0].Id, settings.ActiveServerId);

        Assert.Equal(3, settings.SearchProviders.Count);
        Assert.All(settings.SearchProviders, provider => Assert.NotEqual(Guid.Empty, provider.Id));
        Assert.Equal(settings.SearchProviders.Count, settings.SearchProviders.Select(provider => provider.Id).Distinct().Count());
        Assert.Equal("Torznab", settings.SearchProviders[0].Name);
        Assert.Equal(string.Empty, settings.SearchProviders[0].Url);
        Assert.Equal(string.Empty, settings.SearchProviders[0].ApiKey);
        Assert.Equal("2000", settings.SearchProviders[0].Categories);
        Assert.Equal(30, settings.SearchProviders[0].TimeoutSeconds);
        Assert.Equal("Jackett", settings.SearchProviders[1].Name);
        Assert.Equal("http://indexer.local/api", settings.SearchProviders[1].Url);
        Assert.Equal("key", settings.SearchProviders[1].ApiKey);
        Assert.Equal(180, settings.SearchProviders[1].TimeoutSeconds);
        Assert.Equal(5, settings.SearchProviders[2].TimeoutSeconds);
        Assert.Equal(["venom", "dune"], settings.SearchHistory);

        Assert.Equal(ExternalPlayerKind.BuiltInMpv, settings.Player.PreferredPlayer);
        Assert.Equal(string.Empty, settings.Player.CustomPlayerPath);
        Assert.Equal("127.0.0.1", settings.LocalServer.ListenAddress);
        Assert.Equal(65535, settings.LocalServer.Port);
        Assert.Equal(8091, settings.LocalServer.SslPort);
        Assert.Equal(1, settings.LocalServer.CacheSizeMb);
        Assert.Equal(100, settings.LocalServer.PreloadCachePercent);
        Assert.Equal(5, settings.LocalServer.ReaderReadAheadPercent);
        Assert.Equal(1, settings.LocalServer.TorrentDisconnectTimeoutSeconds);
        Assert.Equal(1, settings.LocalServer.ConnectionsLimit);
        Assert.Equal(65535, settings.LocalServer.PeersListenPort);
        Assert.Equal(3, settings.LocalServer.RetrackersMode);
        Assert.Equal(0, settings.LocalServer.DownloadSpeedLimitKb);
        Assert.Equal(0, settings.LocalServer.UploadSpeedLimitKb);
        Assert.Equal("https://api.themoviedb.org", settings.LocalServer.TmdbApiUrl);
        Assert.Equal("https://image.tmdb.org", settings.LocalServer.TmdbImageUrl);
        Assert.Equal("https://imagetmdb.com", settings.LocalServer.TmdbImageUrlRu);
    }

    [Fact]
    public async Task LoadExistingAsync_KeepsActiveServerWhenItExists()
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = Path.Combine(directory.Path, "settings.json");
        var firstServerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondServerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        await File.WriteAllTextAsync(filePath, $$"""
            {
              "activeServerId": "{{secondServerId}}",
              "servers": [
                {
                  "id": "{{firstServerId}}",
                  "name": "First",
                  "baseUrl": "http://127.0.0.1:8090"
                },
                {
                  "id": "{{secondServerId}}",
                  "name": "Second",
                  "baseUrl": "http://192.168.1.2:8090"
                }
              ]
            }
            """);

        var settings = await new AppSettingsStore(filePath).LoadExistingAsync();

        Assert.Equal(secondServerId, settings.ActiveServerId);
        Assert.Equal(2, settings.Servers.Count);
    }

    [Fact]
    public async Task SaveAsync_CreatesBackupOfPreviousSettingsOnOverwrite()
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = Path.Combine(directory.Path, "settings.json");
        var store = new AppSettingsStore(filePath);

        await store.SaveAsync(new AppSettings { Language = "en" });
        await store.SaveAsync(new AppSettings { Language = "ru" });

        var current = await new AppSettingsStore(filePath).LoadExistingAsync();
        var backup = await new AppSettingsStore(filePath + ".bak").LoadExistingAsync();

        Assert.Equal("ru", current.Language);
        Assert.Equal("en", backup.Language);
    }

    [Fact]
    public async Task SaveAsync_DoesNotOverwriteValidBackupWhenCurrentSettingsAreCorrupt()
    {
        using var directory = TemporaryDirectory.Create();
        var filePath = Path.Combine(directory.Path, "settings.json");
        await File.WriteAllTextAsync(filePath, "{ not-json");
        await File.WriteAllTextAsync(
            filePath + ".bak",
            JsonSerializer.Serialize(new AppSettings { Language = "en" }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        await new AppSettingsStore(filePath).SaveAsync(new AppSettings { Language = "ru" });

        var current = await new AppSettingsStore(filePath).LoadExistingAsync();
        var backup = await new AppSettingsStore(filePath + ".bak").LoadExistingAsync();

        Assert.Equal("ru", current.Language);
        Assert.Equal("en", backup.Language);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "torrwind-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
