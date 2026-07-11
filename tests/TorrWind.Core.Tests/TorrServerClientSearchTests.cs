using System.Net;
using System.Text;
using TorrWind.Core.Models;
using TorrWind.Core.Services;

namespace TorrWind.Core.Tests;

public sealed class TorrServerClientSearchTests
{
    [Fact]
    public async Task SearchServerTorznabAsync_RequestsServerEndpointAndParsesArrayResults()
    {
        Uri? requestedUri = null;
        using var client = CreateClient("Home", request =>
        {
            requestedUri = request.RequestUri;
            Assert.Equal(HttpMethod.Get, request.Method);
            return Json("""
                [
                  {
                    "tracker": "Custom Tracker",
                    "title": "Show S01E01",
                    "link": "http://indexer.local/download/1",
                    "magnet": "magnet:?xt=urn:btih:abc",
                    "size": "1.5 GiB",
                    "seeders": 42,
                    "peers": "7",
                    "categories": "5000",
                    "createDate": "Fri, 03 Jul 2026 19:25:30 GMT"
                  },
                  {
                    "Title": "Movie",
                    "Link": "http://indexer.local/download/2",
                    "Size": 123456,
                    "Seed": "5",
                    "Peer": 2,
                    "Category": "movie"
                  },
                  { "title": "" },
                  "ignored"
                ]
                """);
        });

        var results = await client.SearchServerTorznabAsync("the gentlemen", index: 2);

        Assert.NotNull(requestedUri);
        Assert.Equal("http://127.0.0.1:8090/torznab/search", requestedUri!.GetLeftPart(UriPartial.Path));
        AssertQuery(requestedUri, "query", "the gentlemen");
        AssertQuery(requestedUri, "index", "2");
        Assert.Equal(2, results.Count);

        var show = results[0];
        Assert.Equal("Custom Tracker", show.ProviderName);
        Assert.Equal("Show S01E01", show.Title);
        Assert.Equal("http://indexer.local/download/1", show.Link);
        Assert.Equal("magnet:?xt=urn:btih:abc", show.Magnet);
        Assert.Equal(1610612736, show.SizeBytes);
        Assert.Equal(42, show.Seeders);
        Assert.Equal(7, show.Leechers);
        Assert.Equal("5000", show.Category);
        Assert.Equal(2026, show.PublishedAt?.Year);

        var movie = results[1];
        Assert.Equal("Home Torznab", movie.ProviderName);
        Assert.Equal("Movie", movie.Title);
        Assert.Equal(123456, movie.SizeBytes);
        Assert.Equal(5, movie.Seeders);
        Assert.Equal(2, movie.Leechers);
        Assert.Equal("movie", movie.Category);
    }

    [Fact]
    public async Task SearchServerRutorAsync_RequestsSearchEndpointAndUsesProviderFallback()
    {
        Uri? requestedUri = null;
        using var client = CreateClient("Local", request =>
        {
            requestedUri = request.RequestUri;
            return Json("""
                [
                  {
                    "name": "Rutor Result",
                    "link": "http://rutor.local/torrent/1",
                    "size": "2,5\u041C\u0411",
                    "seed": 3,
                    "peer": 1
                  }
                ]
                """);
        });

        var results = await client.SearchServerRutorAsync("venom 2018");

        Assert.NotNull(requestedUri);
        Assert.Equal("http://127.0.0.1:8090/search", requestedUri!.GetLeftPart(UriPartial.Path));
        AssertQuery(requestedUri, "query", "venom 2018");
        var result = Assert.Single(results);
        Assert.Equal("Local RuTor", result.ProviderName);
        Assert.Equal("Rutor Result", result.Title);
        Assert.Equal(2500000, result.SizeBytes);
        Assert.Equal(3, result.Seeders);
        Assert.Equal(1, result.Leechers);
    }

    [Theory]
    [InlineData("results")]
    [InlineData("data")]
    [InlineData("items")]
    public async Task SearchServerTorznabAsync_ParsesWrappedResultArrays(string wrapperName)
    {
        using var client = CreateClient("Home", _ => Json($$"""
            {
              "{{wrapperName}}": [
                { "title": "Wrapped Result", "size": "10 KB" }
              ]
            }
            """));

        var results = await client.SearchServerTorznabAsync("wrapped");

        var result = Assert.Single(results);
        Assert.Equal("Wrapped Result", result.Title);
        Assert.Equal(10000, result.SizeBytes);
    }

    [Fact]
    public async Task SearchServerTorznabAsync_ParsesAlternativeByteSizeFields()
    {
        using var client = CreateClient("Home", _ => Json("""
            [
              { "title": "Size Bytes", "sizeBytes": 123456789 },
              { "title": "Length Bytes", "Length": "987654321" }
            ]
            """));

        var results = await client.SearchServerTorznabAsync("sizes");

        Assert.Equal(2, results.Count);
        Assert.Equal(123456789, results[0].SizeBytes);
        Assert.Equal(987654321, results[1].SizeBytes);
    }

    [Fact]
    public async Task SearchServerTorznabAsync_ParsesAlternativeSearchResultFieldNames()
    {
        using var client = CreateClient("Home", _ => Json("""
            [
              {
                "title": "Alternative Fields",
                "downloadUrl": "http://indexer.local/download/alt",
                "magnetUrl": "magnet:?xt=urn:btih:alt",
                "seeds": "17",
                "leeches": "4",
                "cat": "5030",
                "publishedAt": "2026-07-11T10:15:00Z"
              }
            ]
            """));

        var result = Assert.Single(await client.SearchServerTorznabAsync("alt"));

        Assert.Equal("http://indexer.local/download/alt", result.Link);
        Assert.Equal("magnet:?xt=urn:btih:alt", result.Magnet);
        Assert.Equal(17, result.Seeders);
        Assert.Equal(4, result.Leechers);
        Assert.Equal("5030", result.Category);
        Assert.Equal(2026, result.PublishedAt?.Year);
    }

    [Fact]
    public async Task SearchServerTorznabAsync_TreatsMagnetLinkAsMagnet()
    {
        using var client = CreateClient("Home", _ => Json("""
            [
              {
                "title": "Magnet In Link",
                "link": "magnet:?xt=urn:btih:inlink"
              }
            ]
            """));

        var result = Assert.Single(await client.SearchServerTorznabAsync("magnet"));

        Assert.Equal(string.Empty, result.Link);
        Assert.Equal("magnet:?xt=urn:btih:inlink", result.Magnet);
    }

    [Fact]
    public async Task SearchServerTorznabAsync_ReturnsEmptyForUnsupportedJsonShape()
    {
        using var client = CreateClient("Home", _ => Json("""
            { "status": "ok" }
            """));

        var results = await client.SearchServerTorznabAsync("empty");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchTorznabAsync_AppliesLimitWithMinimumOneResult()
    {
        using var client = CreateClient("Home", _ => Json("""
            [
              { "title": "First" },
              { "title": "Second" }
            ]
            """));

        var results = await client.SearchTorznabAsync("query", limit: 0);

        var result = Assert.Single(results);
        Assert.Equal("First", result.Title);
    }

    private static TorrServerClient CreateClient(string name, Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        return new TorrServerClient(
            new ServerProfile { Name = name, BaseUrl = "http://127.0.0.1:8090" },
            _ => new StaticResponseHandler(responder));
    }

    private static HttpResponseMessage Json(string value)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(value, Encoding.UTF8, "application/json")
        };
    }

    private static void AssertQuery(Uri uri, string key, string expectedValue)
    {
        var query = ParseQuery(uri.Query);
        Assert.True(query.TryGetValue(key, out var actualValue), $"Expected query parameter {key}.");
        Assert.Equal(expectedValue, actualValue);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        return query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part =>
            {
                var index = part.IndexOf('=', StringComparison.Ordinal);
                var key = index >= 0 ? part[..index] : part;
                var value = index >= 0 ? part[(index + 1)..] : string.Empty;
                return new KeyValuePair<string, string>(Uri.UnescapeDataString(key), Uri.UnescapeDataString(value));
            })
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class StaticResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
