using System.Net;
using System.Text;
using System.Text.Json;
using TorrWind.Core.Services;

namespace TorrWind.Core.Tests;

public sealed class TorrWindReleaseServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetLatestReleaseAsync_PrefersInstallerAssetOverPortablePackage()
    {
        var sha256 = new string('a', 64);
        using var httpClient = new HttpClient(new StaticResponseHandler(request =>
        {
            Assert.Equal("https://api.github.com/repos/trinity-aml/TorrWind/releases/latest", request.RequestUri?.AbsoluteUri);
            return Json(new
            {
                tag_name = "v1.0.4",
                published_at = "2026-07-10T10:15:00Z",
                prerelease = false,
                assets = new object[]
                {
                    Asset("TorrWind-1.0.4-win-x64-portable.zip", 200, new string('b', 64)),
                    Asset("TorrWind-1.0.4-win-x64.exe", 100, "sha256:" + sha256),
                    Asset("TorrWind-1.0.4-SHA256SUMS.txt", 500, "")
                }
            });
        }));

        var release = await new TorrWindReleaseService(httpClient).GetLatestReleaseAsync();

        Assert.Equal("v1.0.4", release.Version);
        Assert.Equal("TorrWind-1.0.4-win-x64.exe", release.PackageName);
        Assert.Equal("Installer", release.PackageKind);
        Assert.Equal(100, release.SizeBytes);
        Assert.Equal(sha256, release.Sha256);
        Assert.Equal("https://github.com/trinity-aml/TorrWind/releases/download/v1.0.4/TorrWind-1.0.4-SHA256SUMS.txt", release.ChecksumDownloadUrl?.AbsoluteUri);
        Assert.False(release.IsPrerelease);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_UsesPortablePackageWhenInstallerIsMissing()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(_ => Json(new
        {
            tag_name = "v1.0.4",
            published_at = "2026-07-10T10:15:00Z",
            prerelease = true,
            assets = new object[]
            {
                Asset("TorrWind-1.0.4-win-x64-portable.zip", 200, new string('c', 64)),
                Asset("TorrWind-1.0.4-linux-x64.tar.gz", 100, "")
            }
        })));

        var release = await new TorrWindReleaseService(httpClient).GetLatestReleaseAsync();

        Assert.Equal("TorrWind-1.0.4-win-x64-portable.zip", release.PackageName);
        Assert.Equal("Portable", release.PackageKind);
        Assert.True(release.IsPrerelease);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RejectsReleaseWithoutWindowsX64UpdatePackage()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(_ => Json(new
        {
            tag_name = "v1.0.4",
            published_at = "2026-07-10T10:15:00Z",
            prerelease = false,
            assets = new object[]
            {
                Asset("TorrWind-1.0.4-linux-x64.tar.gz", 100, "")
            }
        })));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new TorrWindReleaseService(httpClient).GetLatestReleaseAsync());
    }

    [Fact]
    public async Task GetExpectedSha256Async_UsesInlineDigestBeforeChecksumAsset()
    {
        var requestCount = 0;
        var expected = new string('d', 64);
        using var httpClient = new HttpClient(new StaticResponseHandler(_ =>
        {
            requestCount++;
            return Text("unexpected");
        }));
        var release = new TorrWindRelease(
            "v1.0.4",
            "TorrWind-1.0.4-win-x64.exe",
            new Uri("https://example.invalid/TorrWind-1.0.4-win-x64.exe"),
            100,
            default,
            false,
            "Installer",
            "sha256:" + expected,
            new Uri("https://example.invalid/SHA256SUMS.txt"));

        var actual = await new TorrWindReleaseService(httpClient).GetExpectedSha256Async(release);

        Assert.Equal(expected, actual);
        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task GetExpectedSha256Async_ReadsChecksumAssetForSelectedPackage()
    {
        var expected = new string('e', 64);
        using var httpClient = new HttpClient(new StaticResponseHandler(request =>
        {
            Assert.Equal("https://example.invalid/SHA256SUMS.txt", request.RequestUri?.AbsoluteUri);
            return Text($"""
                {new string('f', 64)}  TorrWind-1.0.4-win-x64-portable.zip
                {expected}  TorrWind-1.0.4-win-x64.exe
                """);
        }));
        var release = new TorrWindRelease(
            "v1.0.4",
            "TorrWind-1.0.4-win-x64.exe",
            new Uri("https://example.invalid/TorrWind-1.0.4-win-x64.exe"),
            100,
            default,
            false,
            "Installer",
            ChecksumDownloadUrl: new Uri("https://example.invalid/SHA256SUMS.txt"));

        var actual = await new TorrWindReleaseService(httpClient).GetExpectedSha256Async(release);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task DownloadAsync_WritesFileAndReportsProgress()
    {
        using var directory = TemporaryDirectory.Create();
        var destination = Path.Combine(directory.Path, "TorrWind-1.0.4-win-x64.exe");
        var progressReports = new List<long>();
        var payload = Encoding.UTF8.GetBytes("installer-bytes");
        using var httpClient = new HttpClient(new StaticResponseHandler(request =>
        {
            Assert.Equal("https://example.invalid/TorrWind-1.0.4-win-x64.exe", request.RequestUri?.AbsoluteUri);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            };
        }));

        await new TorrWindReleaseService(httpClient).DownloadAsync(
            new Uri("https://example.invalid/TorrWind-1.0.4-win-x64.exe"),
            destination,
            new Progress<long>(bytes => progressReports.Add(bytes)));

        Assert.Equal(payload, await File.ReadAllBytesAsync(destination));
        Assert.False(File.Exists(destination + ".download"));
        Assert.Contains(payload.Length, progressReports);
    }

    private static object Asset(string name, long size, string digest)
    {
        return new
        {
            name,
            browser_download_url = "https://github.com/trinity-aml/TorrWind/releases/download/v1.0.4/" + name,
            size,
            digest
        };
    }

    private static HttpResponseMessage Json(object value)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage Text(string value)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(value, Encoding.UTF8, "text/plain")
        };
    }

    private sealed class StaticResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
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
