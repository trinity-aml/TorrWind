using System.Net;
using System.Text;
using System.Text.Json;
using TorrWind.Core.Services;

namespace TorrWind.Core.Tests;

public sealed class GitHubReleaseServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetLatestTorrServerReleaseAsync_PrefersPlainWindowsAmd64AssetOverGstBuild()
    {
        var sha256 = new string('a', 64);
        using var httpClient = new HttpClient(new StaticResponseHandler(request =>
        {
            Assert.Equal("https://api.github.com/repos/YouROK/TorrServer/releases/latest", request.RequestUri?.AbsoluteUri);
            return Json(new
            {
                tag_name = "MatriX.142",
                published_at = "2026-07-03T19:25:30Z",
                prerelease = false,
                assets = new object[]
                {
                    Asset("TorrServer-gst-windows-amd64.exe", 200, new string('b', 64)),
                    Asset("TorrServer-windows-amd64.exe", 100, "sha256:" + sha256),
                    Asset("TorrServer-windows-arm64.exe", 90, new string('c', 64))
                }
            });
        }));

        var release = await new GitHubReleaseService(httpClient).GetLatestTorrServerReleaseAsync();

        Assert.Equal("MatriX.142", release.Version);
        Assert.Equal("TorrServer-windows-amd64.exe", release.AssetName);
        Assert.Equal(100, release.SizeBytes);
        Assert.Equal(sha256, release.Sha256);
        Assert.False(release.IsPrerelease);
    }

    [Fact]
    public async Task GetTorrServerReleasesAsync_SkipsReleasesWithoutWindowsAmd64Assets()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(_ => Json(new object[]
        {
            new
            {
                tag_name = "MatriX.142",
                published_at = "2026-07-03T19:25:30Z",
                prerelease = false,
                assets = new object[] { Asset("TorrServer-windows-amd64.exe", 100, new string('a', 64)) }
            },
            new
            {
                tag_name = "MatriX.141",
                published_at = "2026-06-30T19:25:30Z",
                prerelease = false,
                assets = new object[] { Asset("TorrServer-linux-amd64", 100, new string('b', 64)) }
            }
        })));

        var releases = await new GitHubReleaseService(httpClient).GetTorrServerReleasesAsync();

        var release = Assert.Single(releases);
        Assert.Equal("MatriX.142", release.Version);
        Assert.Equal("TorrServer-windows-amd64.exe", release.AssetName);
    }

    [Fact]
    public async Task GetLatestTorrServerReleaseAsync_FallsBackToGitHubHtmlWhenApiIsForbidden()
    {
        var requestKeys = new List<string>();
        using var httpClient = new HttpClient(new StaticResponseHandler(request =>
        {
            requestKeys.Add(request.Method + " " + request.RequestUri?.AbsoluteUri);
            if (request.RequestUri?.AbsoluteUri == "https://api.github.com/repos/YouROK/TorrServer/releases/latest")
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            if (request.Method == HttpMethod.Head &&
                request.RequestUri?.AbsoluteUri == "https://github.com/YouROK/TorrServer/releases/download/MatriX.142/TorrServer-windows-amd64.exe")
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new ByteArrayContent([]);
                response.Content.Headers.ContentLength = 12345;
                return response;
            }

            return Html("""
                <html>
                  <body>
                    <a href="/YouROK/TorrServer/releases/tag/MatriX.142">MatriX.142</a>
                    <relative-time datetime="2026-07-03T19:25:30Z"></relative-time>
                    <ul>
                      <li><a href="/YouROK/TorrServer/releases/download/MatriX.142/TorrServer-gst-windows-amd64.exe">gst</a></li>
                      <li><a href="/YouROK/TorrServer/releases/download/MatriX.142/TorrServer-windows-amd64.exe">plain</a></li>
                    </ul>
                  </body>
                </html>
                """);
        }));

        var release = await new GitHubReleaseService(httpClient).GetLatestTorrServerReleaseAsync();

        Assert.Contains("GET https://github.com/YouROK/TorrServer/releases/latest", requestKeys);
        Assert.Contains("HEAD https://github.com/YouROK/TorrServer/releases/download/MatriX.142/TorrServer-windows-amd64.exe", requestKeys);
        Assert.Equal("MatriX.142", release.Version);
        Assert.Equal("TorrServer-windows-amd64.exe", release.AssetName);
        Assert.Equal(12345, release.SizeBytes);
        Assert.Equal(2026, release.PublishedAt.Year);
        Assert.False(release.IsPrerelease);
    }

    [Fact]
    public async Task GetLatestTorrServerReleaseAsync_ParsesHtmlAssetLinksOutsideListItems()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(request =>
        {
            if (request.RequestUri?.AbsoluteUri == "https://api.github.com/repos/YouROK/TorrServer/releases/latest")
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            if (request.Method == HttpMethod.Head)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new ByteArrayContent([]);
                response.Content.Headers.ContentLength = 12345;
                return response;
            }

            return Html("""
                <html>
                  <body>
                    <a href="/YouROK/TorrServer/releases/tag/MatriX.142">MatriX.142</a>
                    <relative-time datetime="2026-07-03T19:25:30Z"></relative-time>
                    <div>
                      <a href="/YouROK/TorrServer/releases/download/MatriX.142/TorrServer-windows-amd64.exe">plain</a>
                    </div>
                  </body>
                </html>
                """);
        }));

        var release = await new GitHubReleaseService(httpClient).GetLatestTorrServerReleaseAsync();

        Assert.Equal("TorrServer-windows-amd64.exe", release.AssetName);
        Assert.Equal(12345, release.SizeBytes);
    }

    [Fact]
    public async Task GetLatestTorrServerReleaseAsync_SendsCurrentTorrWindUserAgent()
    {
        string? userAgent = null;
        using var httpClient = new HttpClient(new StaticResponseHandler(request =>
        {
            userAgent = request.Headers.UserAgent.ToString();
            return Json(new
            {
                tag_name = "MatriX.142",
                published_at = "2026-07-03T19:25:30Z",
                prerelease = false,
                assets = new object[] { Asset("TorrServer-windows-amd64.exe", 100, "") }
            });
        }));

        await new GitHubReleaseService(httpClient).GetLatestTorrServerReleaseAsync();

        Assert.Equal("TorrWind/1.0.6", userAgent);
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
        var release = new TorrServerRelease(
            "MatriX.142",
            "TorrServer-windows-amd64.exe",
            new Uri("https://example.invalid/TorrServer-windows-amd64.exe"),
            100,
            default,
            false,
            "sha256:" + expected,
            new Uri("https://example.invalid/SHA256SUMS.txt"));

        var actual = await new GitHubReleaseService(httpClient).GetExpectedSha256Async(release);

        Assert.Equal(expected, actual);
        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task GetExpectedSha256Async_ReadsChecksumAssetForSelectedFile()
    {
        var expected = new string('e', 64);
        using var httpClient = new HttpClient(new StaticResponseHandler(request =>
        {
            Assert.Equal("https://example.invalid/SHA256SUMS.txt", request.RequestUri?.AbsoluteUri);
            return Text($"""
                {new string('f', 64)}  TorrServer-linux-amd64
                {expected}  TorrServer-windows-amd64.exe
                """);
        }));
        var release = new TorrServerRelease(
            "MatriX.142",
            "TorrServer-windows-amd64.exe",
            new Uri("https://example.invalid/TorrServer-windows-amd64.exe"),
            100,
            default,
            false,
            ChecksumDownloadUrl: new Uri("https://example.invalid/SHA256SUMS.txt"));

        var actual = await new GitHubReleaseService(httpClient).GetExpectedSha256Async(release);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("*TorrServer-windows-amd64.exe")]
    [InlineData("artifacts/TorrServer-windows-amd64.exe")]
    [InlineData("downloads\\TorrServer-windows-amd64.exe")]
    [InlineData("(TorrServer-windows-amd64.exe)")]
    public async Task GetExpectedSha256Async_MatchesChecksumAssetFileNameTokens(string fileNameToken)
    {
        var expected = new string('1', 64);
        using var httpClient = new HttpClient(new StaticResponseHandler(_ => Text($"""
            {new string('2', 64)}  TorrServer-windows-amd64.exe.sig
            {expected}  {fileNameToken}
            """)));
        var release = new TorrServerRelease(
            "MatriX.142",
            "TorrServer-windows-amd64.exe",
            new Uri("https://example.invalid/TorrServer-windows-amd64.exe"),
            100,
            default,
            false,
            ChecksumDownloadUrl: new Uri("https://example.invalid/SHA256SUMS.txt"));

        var actual = await new GitHubReleaseService(httpClient).GetExpectedSha256Async(release);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("SHA256(TorrServer-windows-amd64.exe)={0}")]
    [InlineData("https://github.com/YouROK/TorrServer/releases/download/MatriX.142/TorrServer-windows-amd64.exe?download=1 {0}")]
    [InlineData("https://github.com/YouROK/TorrServer/releases/download/MatriX.142/TorrServer-windows-amd64.exe?download=1&amp;raw=1 {0}")]
    public async Task GetExpectedSha256Async_MatchesChecksumAssetInCommonUrlAndDigestFormats(string lineFormat)
    {
        var expected = new string('4', 64);
        using var httpClient = new HttpClient(new StaticResponseHandler(_ =>
            Text(string.Format(lineFormat, expected))));
        var release = new TorrServerRelease(
            "MatriX.142",
            "TorrServer-windows-amd64.exe",
            new Uri("https://example.invalid/TorrServer-windows-amd64.exe"),
            100,
            default,
            false,
            ChecksumDownloadUrl: new Uri("https://example.invalid/SHA256SUMS.txt"));

        var actual = await new GitHubReleaseService(httpClient).GetExpectedSha256Async(release);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task GetExpectedSha256Async_DoesNotMatchChecksumForLongerSimilarFileName()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(_ => Text($"""
            {new string('2', 64)}  TorrServer-windows-amd64.exe.sig
            {new string('3', 64)}  backup-TorrServer-windows-amd64.exe
            """)));
        var release = new TorrServerRelease(
            "MatriX.142",
            "TorrServer-windows-amd64.exe",
            new Uri("https://example.invalid/TorrServer-windows-amd64.exe"),
            100,
            default,
            false,
            ChecksumDownloadUrl: new Uri("https://example.invalid/SHA256SUMS.txt"));

        var actual = await new GitHubReleaseService(httpClient).GetExpectedSha256Async(release);

        Assert.Null(actual);
    }

    [Fact]
    public async Task DownloadAsync_WritesFileReportsProgressAndRemovesTemporaryFile()
    {
        using var directory = TemporaryDirectory.Create();
        var destination = Path.Combine(directory.Path, "TorrServer-windows-amd64.exe");
        var payload = Encoding.UTF8.GetBytes("torrserver-bytes");
        var progressReports = new List<long>();
        using var httpClient = new HttpClient(new StaticResponseHandler(request =>
        {
            Assert.Equal("https://example.invalid/TorrServer-windows-amd64.exe", request.RequestUri?.AbsoluteUri);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            };
        }));

        await new GitHubReleaseService(httpClient).DownloadAsync(
            new Uri("https://example.invalid/TorrServer-windows-amd64.exe"),
            destination,
            payload.Length,
            new Progress<long>(bytes => progressReports.Add(bytes)));

        Assert.Equal(payload, await File.ReadAllBytesAsync(destination));
        Assert.False(File.Exists(destination + ".download"));
        Assert.Contains(payload.Length, progressReports);
    }

    [Fact]
    public async Task DownloadAsync_RemovesTemporaryFileWhenRequestFails()
    {
        using var directory = TemporaryDirectory.Create();
        var destination = Path.Combine(directory.Path, "TorrServer-windows-amd64.exe");
        await File.WriteAllTextAsync(destination + ".download", "stale");
        using var httpClient = new HttpClient(new StaticResponseHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new GitHubReleaseService(httpClient).DownloadAsync(
                new Uri("https://example.invalid/TorrServer-windows-amd64.exe"),
                destination));

        Assert.False(File.Exists(destination));
        Assert.False(File.Exists(destination + ".download"));
    }

    [Fact]
    public async Task DownloadAsync_RemovesTemporaryFileWhenDownloadedSizeDoesNotMatchExpectedSize()
    {
        using var directory = TemporaryDirectory.Create();
        var destination = Path.Combine(directory.Path, "TorrServer-windows-amd64.exe");
        var payload = Encoding.UTF8.GetBytes("short");
        using var httpClient = new HttpClient(new StaticResponseHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload)
        }));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new GitHubReleaseService(httpClient).DownloadAsync(
                new Uri("https://example.invalid/TorrServer-windows-amd64.exe"),
                destination,
                expectedSizeBytes: payload.Length + 1));

        Assert.Contains("size mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(destination));
        Assert.False(File.Exists(destination + ".download"));
    }

    private static object Asset(string name, long size, string digest)
    {
        return new
        {
            name,
            browser_download_url = "https://github.com/YouROK/TorrServer/releases/download/MatriX.142/" + name,
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

    private static HttpResponseMessage Html(string value)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(value, Encoding.UTF8, "text/html")
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
