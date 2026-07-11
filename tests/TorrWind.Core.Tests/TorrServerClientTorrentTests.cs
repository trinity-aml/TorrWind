using System.Net;
using System.Text;
using System.Text.Json;
using TorrWind.Core.Models;
using TorrWind.Core.Services;

namespace TorrWind.Core.Tests;

public sealed class TorrServerClientTorrentTests
{
    [Fact]
    public async Task GetTorrentsAsync_PostsListActionAndParsesWrappedTorrentList()
    {
        string? requestJson = null;
        using var client = CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://127.0.0.1:8090/torrents", request.RequestUri?.AbsoluteUri);
            requestJson = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json("""
                {
                  "torrents": [
                    {
                      "hash": "abc",
                      "title": "The Gentlemen S01",
                      "link": "magnet:?xt=urn:btih:abc",
                      "category": "Series",
                      "poster": "http://image.local/poster.jpg",
                      "data": "{\"tmdb\":1}",
                      "torrs_hash": "source-hash",
                      "size": 3221225472,
                      "loaded_size": 1610612736,
                      "preloaded_bytes": 1048576,
                      "download_speed": 2048,
                      "upload_speed": 1024,
                      "seeders": 12,
                      "peers": 4,
                      "stat_string": "working",
                      "files": [
                        { "id": 0, "path": "The.Gentlemen.S01E01.1080p.mkv", "size": 1073741824 },
                        { "id": 1, "path": "readme.txt", "size": 100 },
                        { "id": 2, "path": "The.Gentlemen.S01E02", "mime": "video/mp4", "size": 1073741824, "width": 1920, "height": 1080 }
                      ]
                    },
                    {
                      "title": ""
                    }
                  ]
                }
                """);
        });

        var torrents = await client.GetTorrentsAsync();

        Assert.Contains("\"action\":\"list\"", requestJson);
        var torrent = Assert.Single(torrents);
        Assert.Equal("abc", torrent.Hash);
        Assert.Equal("The Gentlemen S01", torrent.Title);
        Assert.Equal("magnet:?xt=urn:btih:abc", torrent.SourceLink);
        Assert.Equal("Series", torrent.Category);
        Assert.Equal("http://image.local/poster.jpg", torrent.Poster);
        Assert.Equal("{\"tmdb\":1}", torrent.Data);
        Assert.Equal("source-hash", torrent.TorrsHash);
        Assert.Equal(3221225472, torrent.SizeBytes);
        Assert.Equal(1610612736, torrent.LoadedBytes);
        Assert.Equal(1048576, torrent.PreloadedBytes);
        Assert.Equal(2048, torrent.DownloadSpeed);
        Assert.Equal(1024, torrent.UploadSpeed);
        Assert.Equal(50, torrent.Progress);
        Assert.Equal(12, torrent.Seeders);
        Assert.Equal(4, torrent.Peers);
        Assert.Equal("working", torrent.Status);
        Assert.Equal(2, torrent.Files.Count);
        Assert.Equal("The.Gentlemen.S01E01.1080p", torrent.Files[0].DisplayName);
        Assert.Equal("1080p", torrent.Files[0].Resolution);
        Assert.Equal("1", torrent.Files[0].SeasonText);
        Assert.Equal("1", torrent.Files[0].EpisodeText);
        Assert.Equal("The.Gentlemen.S01E02", torrent.Files[1].DisplayName);
        Assert.Equal("1920x1080", torrent.Files[1].Resolution);
        Assert.Equal("1", torrent.Files[1].SeasonText);
        Assert.Equal("2", torrent.Files[1].EpisodeText);
    }

    [Fact]
    public async Task GetTorrentAsync_PostsGetActionAndParsesSingleTorrentObject()
    {
        string? requestJson = null;
        using var client = CreateClient(request =>
        {
            requestJson = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json("""
                {
                  "info_hash": "def",
                  "name": "Movie 2160p",
                  "torrent_size": "2147483648",
                  "loaded": "2147483648",
                  "connected_seeders": 9,
                  "total_peers": 11,
                  "stat": "loaded",
                  "Files": [
                    { "Index": 7, "Name": "Movie.2160p.mp4", "Length": 2147483648, "VideoResolution": "4K" },
                    { "Index": 8, "Name": "sample.nfo", "Length": 10 }
                  ]
                }
                """);
        });

        var torrent = await client.GetTorrentAsync("def");

        Assert.Contains("\"action\":\"get\"", requestJson);
        Assert.Contains("\"hash\":\"def\"", requestJson);
        Assert.Equal("def", torrent.Hash);
        Assert.Equal("Movie 2160p", torrent.Title);
        Assert.Equal(2147483648, torrent.SizeBytes);
        Assert.Equal(100, torrent.Progress);
        Assert.Equal(9, torrent.Seeders);
        Assert.Equal(11, torrent.Peers);
        Assert.Equal("loaded", torrent.Status);
        var file = Assert.Single(torrent.Files);
        Assert.Equal(7, file.Id);
        Assert.Equal("Movie.2160p", file.DisplayName);
        Assert.Equal("4K", file.Resolution);
    }

    [Fact]
    public async Task GetTorrentsAsync_ParsesDataWrapperAndFileStats()
    {
        using var client = CreateClient(_ => Json("""
            {
              "data": [
                {
                  "Hash": "ghi",
                  "Title": "Show",
                  "Length": 1000,
                  "Loaded": 250,
                  "Seed": 2,
                  "Peer": 3,
                  "FileStats": [
                    { "Index": 1, "Path": "Show.S02E03.1280x720.avi", "Length": 1000 },
                    { "Index": 2, "Path": "cover.jpg", "Length": 20 }
                  ]
                }
              ]
            }
            """));

        var torrents = await client.GetTorrentsAsync();

        var torrent = Assert.Single(torrents);
        Assert.Equal("ghi", torrent.Hash);
        Assert.Equal(25, torrent.Progress);
        var file = Assert.Single(torrent.Files);
        Assert.Equal("Show.S02E03.1280x720", file.DisplayName);
        Assert.Equal("1280x720", file.Resolution);
        Assert.Equal("2", file.SeasonText);
        Assert.Equal("3", file.EpisodeText);
    }

    [Fact]
    public async Task GetTorrentsAsync_ParsesWrappedTorrentListCaseInsensitively()
    {
        using var client = CreateClient(_ => Json("""
            {
              "TORRENTS": [
                {
                  "hash": "wrapped-case",
                  "title": "Wrapped Case"
                }
              ]
            }
            """));

        var torrent = Assert.Single(await client.GetTorrentsAsync());

        Assert.Equal("wrapped-case", torrent.Hash);
        Assert.Equal("Wrapped Case", torrent.Title);
    }

    [Fact]
    public async Task GetTorrentsAsync_ParsesCamelCaseTorrentFields()
    {
        using var client = CreateClient(_ => Json("""
            [
              {
                "infoHash": "camel",
                "title": "Camel Show",
                "sourceLink": "magnet:?xt=urn:btih:camel",
                "torrsHash": "source-camel",
                "torrentSize": 2000,
                "loadedBytes": 500,
                "preloadedBytes": 125,
                "downloadSpeed": "2048.5",
                "uploadRate": 1024,
                "connectedSeeders": 8,
                "totalPeers": 9,
                "statString": "buffering",
                "files": [
                  {
                    "id": 3,
                    "path": "Camel.Show.S01E03.720p.mkv",
                    "sizeBytes": 1000
                  }
                ]
              }
            ]
            """));

        var torrent = Assert.Single(await client.GetTorrentsAsync());

        Assert.Equal("camel", torrent.Hash);
        Assert.Equal("Camel Show", torrent.Title);
        Assert.Equal("magnet:?xt=urn:btih:camel", torrent.SourceLink);
        Assert.Equal("source-camel", torrent.TorrsHash);
        Assert.Equal(2000, torrent.SizeBytes);
        Assert.Equal(500, torrent.LoadedBytes);
        Assert.Equal(125, torrent.PreloadedBytes);
        Assert.Equal(2048.5, torrent.DownloadSpeed);
        Assert.Equal(1024, torrent.UploadSpeed);
        Assert.Equal(25, torrent.Progress);
        Assert.Equal(8, torrent.Seeders);
        Assert.Equal(9, torrent.Peers);
        Assert.Equal("buffering", torrent.Status);
        var file = Assert.Single(torrent.Files);
        Assert.Equal(3, file.Id);
        Assert.Equal(1000, file.SizeBytes);
    }

    [Fact]
    public async Task GetTorrentsAsync_ParsesFieldsCaseInsensitively()
    {
        using var client = CreateClient(_ => Json("""
            [
              {
                "INFO_HASH": "casehash",
                "TITLE": "Case Movie",
                "SIZEBYTES": "4096",
                "LOADEDBYTES": "2048",
                "SEEDERS": "5",
                "PEERS": "6",
                "FILES": [
                  {
                    "INDEX": 9,
                    "PATH": "Case.Movie.1080p.mkv",
                    "SIZEBYTES": "4096"
                  }
                ]
              }
            ]
            """));

        var torrent = Assert.Single(await client.GetTorrentsAsync());

        Assert.Equal("casehash", torrent.Hash);
        Assert.Equal("Case Movie", torrent.Title);
        Assert.Equal(4096, torrent.SizeBytes);
        Assert.Equal(2048, torrent.LoadedBytes);
        Assert.Equal(50, torrent.Progress);
        Assert.Equal(5, torrent.Seeders);
        Assert.Equal(6, torrent.Peers);
        var file = Assert.Single(torrent.Files);
        Assert.Equal(9, file.Id);
        Assert.Equal(4096, file.SizeBytes);
    }

    [Theory]
    [InlineData("movie.mkv", "", true)]
    [InlineData("movie", "video/x-matroska; charset=utf-8", true)]
    [InlineData("poster.jpg", "image/jpeg", false)]
    [InlineData("archive.zip", "", false)]
    public void TorrentFile_IsVideoFilePath_RecognizesExtensionsAndVideoMimeTypes(
        string path,
        string mimeType,
        bool expected)
    {
        Assert.Equal(expected, TorrentFile.IsVideoFilePath(path, mimeType));
    }

    private static TorrServerClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        return new TorrServerClient(
            new ServerProfile { BaseUrl = "http://127.0.0.1:8090" },
            _ => new StaticResponseHandler(responder));
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
