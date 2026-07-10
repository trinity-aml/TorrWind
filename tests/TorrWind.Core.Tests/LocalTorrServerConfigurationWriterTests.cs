using System.Text.Json;
using TorrWind.Core;
using TorrWind.Core.Models;
using TorrWind.Core.Services;

namespace TorrWind.Core.Tests;

public sealed class LocalTorrServerConfigurationWriterTests
{
    [Fact]
    public void GetDataDirectory_UsesDefaultWhenSettingIsBlank()
    {
        var settings = new LocalServerSettings
        {
            DataDirectory = " "
        };

        var dataDirectory = LocalTorrServerConfigurationWriter.GetDataDirectory(settings);

        Assert.Equal(AppPaths.DefaultLocalServerDirectory, dataDirectory);
    }

    [Fact]
    public void GetDataDirectory_UsesConfiguredPathWhenPresent()
    {
        var settings = new LocalServerSettings
        {
            DataDirectory = @"D:\TorrWind\Data\TorrServer"
        };

        var dataDirectory = LocalTorrServerConfigurationWriter.GetDataDirectory(settings);

        Assert.Equal(@"D:\TorrWind\Data\TorrServer", dataDirectory);
    }

    [Fact]
    public async Task WriteAsync_WritesAuthAndIpListsIntoDataDirectory()
    {
        using var directory = TemporaryDirectory.Create();
        var settings = new LocalServerSettings
        {
            DataDirectory = directory.Path,
            UseHttpAuth = true,
            Username = " user ",
            Password = " pass with spaces ",
            WhiteList = " 127.0.0.1\r\n\n 192.168.1.0/24 ",
            BlackList = "10.0.0.1\r10.0.0.2\n"
        };

        await LocalTorrServerConfigurationWriter.WriteAsync(settings);

        var authPath = Path.Combine(directory.Path, "accs.db");
        Assert.True(File.Exists(authPath));
        using (var authDocument = JsonDocument.Parse(await File.ReadAllTextAsync(authPath)))
        {
            Assert.Equal(" pass with spaces ", authDocument.RootElement.GetProperty("user").GetString());
        }

        Assert.Equal(
            "127.0.0.1" + Environment.NewLine + "192.168.1.0/24" + Environment.NewLine,
            await File.ReadAllTextAsync(Path.Combine(directory.Path, "wip.txt")));
        Assert.Equal(
            "10.0.0.1" + Environment.NewLine + "10.0.0.2" + Environment.NewLine,
            await File.ReadAllTextAsync(Path.Combine(directory.Path, "bip.txt")));
    }

    [Theory]
    [InlineData(false, "user", "password")]
    [InlineData(true, "", "password")]
    [InlineData(true, "user", "")]
    public async Task WriteAsync_DeletesAuthFileWhenAuthSettingsAreIncomplete(
        bool useHttpAuth,
        string username,
        string password)
    {
        using var directory = TemporaryDirectory.Create();
        var authPath = Path.Combine(directory.Path, "accs.db");
        await File.WriteAllTextAsync(authPath, "{\"old\":\"value\"}");

        await LocalTorrServerConfigurationWriter.WriteAsync(new LocalServerSettings
        {
            DataDirectory = directory.Path,
            UseHttpAuth = useHttpAuth,
            Username = username,
            Password = password
        });

        Assert.False(File.Exists(authPath));
    }

    [Fact]
    public async Task WriteAsync_DeletesIpListFilesWhenListsAreBlank()
    {
        using var directory = TemporaryDirectory.Create();
        var whiteListPath = Path.Combine(directory.Path, "wip.txt");
        var blackListPath = Path.Combine(directory.Path, "bip.txt");
        await File.WriteAllTextAsync(whiteListPath, "old");
        await File.WriteAllTextAsync(blackListPath, "old");

        await LocalTorrServerConfigurationWriter.WriteAsync(new LocalServerSettings
        {
            DataDirectory = directory.Path,
            WhiteList = " \r\n ",
            BlackList = ""
        });

        Assert.False(File.Exists(whiteListPath));
        Assert.False(File.Exists(blackListPath));
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
