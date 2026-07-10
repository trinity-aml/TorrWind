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
