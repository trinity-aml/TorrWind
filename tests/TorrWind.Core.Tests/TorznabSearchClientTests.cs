using System.Net;
using System.Text;
using TorrWind.Core.Models;
using TorrWind.Core.Services;

namespace TorrWind.Core.Tests;

public sealed class TorznabSearchClientTests
{
    [Fact]
    public async Task SearchAsync_NormalizesBaseProviderUrlToApiEndpointAndAddsSearchParameters()
    {
        Uri? requestedUri = null;
        var client = CreateClient(request =>
        {
            requestedUri = request.RequestUri;
            return Rss("""
                <rss><channel>
                  <item>
                    <title>Movie One</title>
                    <link>http://indexer.local/download/1</link>
                  </item>
                </channel></rss>
                """);
        });
        var provider = new SearchProviderSettings
        {
            Name = "Indexer",
            Url = "192.168.1.2:9117",
            ApiKey = " secret ",
            Categories = "2000,5000",
            TimeoutSeconds = 1
        };

        var results = await client.SearchAsync(provider, "the gentlemen", categories: "");

        var result = Assert.Single(results);
        Assert.Equal("Movie One", result.Title);
        Assert.NotNull(requestedUri);
        Assert.Equal("http://192.168.1.2:9117/api", requestedUri!.GetLeftPart(UriPartial.Path));
        AssertQuery(requestedUri, "t", "search");
        AssertQuery(requestedUri, "q", "the gentlemen");
        AssertQuery(requestedUri, "apikey", "secret");
        AssertQuery(requestedUri, "cat", "2000,5000");
    }

    [Fact]
    public async Task SearchAsync_AppendsApiToTorznabPath()
    {
        Uri? requestedUri = null;
        var client = CreateClient(request =>
        {
            requestedUri = request.RequestUri;
            return Rss("<rss><channel /></rss>");
        });

        await client.SearchAsync(
            new SearchProviderSettings { Url = "http://indexer.local/torznab" },
            "query",
            "");

        Assert.Equal("http://indexer.local/torznab/api", requestedUri?.GetLeftPart(UriPartial.Path));
    }

    [Fact]
    public async Task SearchAsync_ReplacesExistingQueryParametersAndKeepsUnknownOnes()
    {
        Uri? requestedUri = null;
        var client = CreateClient(request =>
        {
            requestedUri = request.RequestUri;
            return Rss("<rss><channel /></rss>");
        });

        await client.SearchAsync(
            new SearchProviderSettings
            {
                Url = "http://indexer.local/api?t=tvsearch&apikey=old&server=jackett",
                ApiKey = "new"
            },
            "needle",
            "1000");

        AssertQuery(requestedUri!, "t", "search");
        AssertQuery(requestedUri!, "q", "needle");
        AssertQuery(requestedUri!, "apikey", "new");
        AssertQuery(requestedUri!, "cat", "1000");
        AssertQuery(requestedUri!, "server", "jackett");
    }

    [Fact]
    public async Task SearchAsync_SelectedCategoriesOverrideProviderCategories()
    {
        Uri? requestedUri = null;
        var client = CreateClient(request =>
        {
            requestedUri = request.RequestUri;
            return Rss("<rss><channel /></rss>");
        });

        await client.SearchAsync(
            new SearchProviderSettings
            {
                Url = "http://indexer.local/api",
                Categories = "default"
            },
            "query",
            "selected");

        AssertQuery(requestedUri!, "cat", "selected");
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmptyWhenProviderUrlIsBlank()
    {
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP must not be used."));

        var results = await client.SearchAsync(new SearchProviderSettings { Url = "" }, "query", "");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_AppliesLimitWithMinimumOneResult()
    {
        var client = CreateClient(_ => Rss("""
            <rss><channel>
              <item><title>First</title><link>http://example/1</link></item>
              <item><title>Second</title><link>http://example/2</link></item>
            </channel></rss>
            """));

        var results = await client.SearchAsync(new SearchProviderSettings { Url = "http://indexer.local/api" }, "q", "", limit: 0);

        var result = Assert.Single(results);
        Assert.Equal("First", result.Title);
    }

    [Fact]
    public void Parse_ReadsTorznabAttributesEnclosureAndPublishedDate()
    {
        var results = TorznabSearchClient.Parse("""
            <rss xmlns:torznab="http://torznab.com/schemas/2015/feed">
              <channel>
                <item>
                  <title>Show S01E01</title>
                  <guid>http://indexer.local/guid/1</guid>
                  <enclosure url="magnet:?xt=urn:btih:abc" length="123456" type="application/x-bittorrent" />
                  <category>5000</category>
                  <pubDate>Fri, 03 Jul 2026 19:25:30 GMT</pubDate>
                  <torznab:attr name="seeders" value="42" />
                  <torznab:attr name="peers" value="7" />
                  <torznab:attr name="category" value="5040" />
                </item>
                <item>
                  <title></title>
                  <link>http://indexer.local/ignored</link>
                </item>
              </channel>
            </rss>
            """, "Indexer");

        var result = Assert.Single(results);
        Assert.Equal("Indexer", result.ProviderName);
        Assert.Equal("Show S01E01", result.Title);
        Assert.Equal("http://indexer.local/guid/1", result.Link);
        Assert.Equal("magnet:?xt=urn:btih:abc", result.Magnet);
        Assert.Equal(123456, result.SizeBytes);
        Assert.Equal(42, result.Seeders);
        Assert.Equal(7, result.Leechers);
        Assert.Equal("5040", result.Category);
        Assert.Equal(2026, result.PublishedAt?.Year);
    }

    [Fact]
    public void Parse_PrefersLinkAndSizeElementOverFallbacks()
    {
        var results = TorznabSearchClient.Parse("""
            <rss>
              <channel>
                <item>
                  <title>Movie</title>
                  <link>http://indexer.local/download/1</link>
                  <guid>http://indexer.local/guid/1</guid>
                  <size>987654</size>
                  <enclosure url="http://indexer.local/fallback.torrent" length="123" />
                  <torznab:attr xmlns:torznab="http://torznab.com/schemas/2015/feed" name="magneturl" value="magnet:?xt=urn:btih:def" />
                  <torznab:attr xmlns:torznab="http://torznab.com/schemas/2015/feed" name="leechers" value="5" />
                </item>
              </channel>
            </rss>
            """, "Indexer");

        var result = Assert.Single(results);
        Assert.Equal("http://indexer.local/download/1", result.Link);
        Assert.Equal("magnet:?xt=urn:btih:def", result.Magnet);
        Assert.Equal(987654, result.SizeBytes);
        Assert.Equal(5, result.Leechers);
    }

    private static TorznabSearchClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        return new TorznabSearchClient(_ => new StaticResponseHandler(responder));
    }

    private static HttpResponseMessage Rss(string xml)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(xml, Encoding.UTF8, "application/rss+xml")
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
