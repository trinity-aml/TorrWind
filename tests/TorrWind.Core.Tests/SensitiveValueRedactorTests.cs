using TorrWind.Core.Services;

namespace TorrWind.Core.Tests;

public sealed class SensitiveValueRedactorTests
{
    [Fact]
    public void RedactUrl_RedactsSensitiveQueryParametersAndKeepsOrdinaryParameters()
    {
        var redacted = SensitiveValueRedactor.RedactUrl(
            "http://indexer.local/api?t=search&apikey=secret-api-key&q=venom&password=secret-password#fragment");

        Assert.Equal(
            "http://indexer.local/api?t=search&apikey=<redacted>&q=venom&password=<redacted>#fragment",
            redacted);
        Assert.DoesNotContain("secret-api-key", redacted);
        Assert.DoesNotContain("secret-password", redacted);
    }

    [Fact]
    public void RedactUrl_RedactsUserInfo()
    {
        var redacted = SensitiveValueRedactor.RedactUrl("https://user:secret-password@example.local/api?cat=5000");

        Assert.Equal("https://<redacted>@example.local/api?cat=5000", redacted);
        Assert.DoesNotContain("secret-password", redacted);
    }

    [Fact]
    public void RedactText_RedactsUrlsAuthorizationHeadersAndAssignments()
    {
        var redacted = SensitiveValueRedactor.RedactText("""
            GET http://indexer.local/api?t=search&apikey=secret-api-key&q=movie
            Authorization: Bearer secret-token
            password=secret-password
            "tmdbApiKey":"secret-tmdb-key"
            """);

        Assert.DoesNotContain("secret-api-key", redacted);
        Assert.DoesNotContain("secret-token", redacted);
        Assert.DoesNotContain("secret-password", redacted);
        Assert.DoesNotContain("secret-tmdb-key", redacted);
        Assert.Contains("apikey=<redacted>", redacted);
        Assert.Contains("Authorization: Bearer <redacted>", redacted);
        Assert.Contains("password=<redacted>", redacted);
        Assert.Contains("\"tmdbApiKey\":\"<redacted>\"", redacted);
    }

    [Fact]
    public void RedactText_RedactsHtmlEscapedQuerySeparators()
    {
        var redacted = SensitiveValueRedactor.RedactText(
            "http://indexer.local/api?t=search&amp;apikey=secret-api-key&amp;q=movie");

        Assert.Equal("http://indexer.local/api?t=search&apikey=<redacted>&q=movie", redacted);
        Assert.DoesNotContain("secret-api-key", redacted);
    }

    [Fact]
    public void RedactText_RedactsEscapedJsonAssignments()
    {
        var redacted = SensitiveValueRedactor.RedactText(
            """{"details":"{\"tmdbApiKey\":\"secret-tmdb-key\",\"name\":\"Movie\"}"}""");

        Assert.Contains("\\\"tmdbApiKey\\\":\\\"<redacted>\\\"", redacted);
        Assert.Contains("\\\"name\\\":\\\"Movie\\\"", redacted);
        Assert.DoesNotContain("secret-tmdb-key", redacted);
    }
}
