using System.Globalization;
using System.Net;
using System.Xml.Linq;
using TorrWind.Core.Models;

namespace TorrWind.Core.Services;

public sealed class TorznabSearchClient
{
    private readonly Func<SearchProviderSettings, HttpMessageHandler> _handlerFactory;

    public TorznabSearchClient()
        : this(CreateHandler)
    {
    }

    public TorznabSearchClient(Func<SearchProviderSettings, HttpMessageHandler> handlerFactory)
    {
        _handlerFactory = handlerFactory;
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        SearchProviderSettings provider,
        string query,
        string categories,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provider.Url))
        {
            return [];
        }

        using var httpClient = new HttpClient(_handlerFactory(provider))
        {
            Timeout = TimeSpan.FromSeconds(Math.Clamp(provider.TimeoutSeconds, 5, 180))
        };

        var requestUri = BuildSearchUri(provider, query, categories);
        using var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Parse(xml, provider.Name)
            .Take(Math.Max(1, limit))
            .ToArray();
    }

    public static IReadOnlyList<SearchResult> Parse(string xml, string providerName)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return [];
        }

        var document = XDocument.Parse(xml);
        return document
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "item", StringComparison.OrdinalIgnoreCase))
            .Select(item => new SearchResult
            {
                ProviderName = providerName,
                Title = ElementValue(item, "title"),
                Link = FirstNotEmpty(ElementValue(item, "link"), NonMagnetEnclosureValue(item), ElementValue(item, "guid")),
                Magnet = FirstNotEmpty(AttributeValue(item, "magneturl"), MagnetEnclosureValue(item)),
                SizeBytes = ParseLong(FirstNotEmpty(ElementValue(item, "size"), AttributeValue(item, "size"), EnclosureValue(item, "length"))),
                Seeders = ParseInt(AttributeValue(item, "seeders")),
                Leechers = ParseInt(FirstNotEmpty(AttributeValue(item, "leechers"), AttributeValue(item, "peers"))),
                Category = FirstNotEmpty(AttributeValue(item, "category"), ElementValue(item, "category")),
                PublishedAt = DateTimeOffset.TryParse(
                    ElementValue(item, "pubDate"),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var publishedAt)
                    ? publishedAt
                    : null
            })
            .Where(result => !string.IsNullOrWhiteSpace(result.Title))
            .ToArray();
    }

    private static HttpMessageHandler CreateHandler(SearchProviderSettings provider)
    {
        var handler = new HttpClientHandler();

        if (provider.IgnoreCertificateErrors)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        if (handler.SupportsAutomaticDecompression)
        {
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        }

        return handler;
    }

    private static Uri BuildSearchUri(SearchProviderSettings provider, string query, string categories)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["t"] = "search",
            ["q"] = query
        };

        if (!string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            parameters["apikey"] = provider.ApiKey.Trim();
        }

        var selectedCategories = FirstNotEmpty(categories, provider.Categories);
        if (!string.IsNullOrWhiteSpace(selectedCategories))
        {
            parameters["cat"] = selectedCategories.Trim();
        }

        var builder = new UriBuilder(NormalizeProviderUrl(provider.Url));
        var queryParts = SplitQuery(builder.Query);
        foreach (var (key, value) in parameters)
        {
            queryParts[key] = value;
        }

        builder.Query = string.Join("&", queryParts.Select(pair =>
            Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));

        return builder.Uri;
    }

    private static Uri NormalizeProviderUrl(string url)
    {
        var value = url.Trim();
        if (!value.Contains("://", StringComparison.Ordinal))
        {
            value = "http://" + value;
        }

        var builder = new UriBuilder(value);
        var normalizedPath = builder.Path.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(normalizedPath) || string.Equals(normalizedPath, "/settings", StringComparison.OrdinalIgnoreCase))
        {
            builder.Path = "/api";
            return builder.Uri;
        }

        if (normalizedPath.EndsWith("/torznab", StringComparison.OrdinalIgnoreCase))
        {
            builder.Path = normalizedPath + "/api";
            return builder.Uri;
        }

        return builder.Uri;
    }

    private static Dictionary<string, string> SplitQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var trimmed = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return result;
        }

        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var index = part.IndexOf('=', StringComparison.Ordinal);
            var key = index >= 0 ? part[..index] : part;
            var value = index >= 0 ? part[(index + 1)..] : string.Empty;
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value);
            }
        }

        return result;
    }

    private static string ElementValue(XElement parent, string localName)
    {
        return parent
            .Elements()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            ?.Value
            .Trim() ?? string.Empty;
    }

    private static string AttributeValue(XElement item, string attrName)
    {
        return item
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "attr", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(element => string.Equals(
                (string?)element.Attribute("name"),
                attrName,
                StringComparison.OrdinalIgnoreCase))
            ?.Attribute("value")
            ?.Value
            .Trim() ?? string.Empty;
    }

    private static string EnclosureValue(XElement item, string attrName)
    {
        return item
            .Elements()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "enclosure", StringComparison.OrdinalIgnoreCase))
            ?.Attribute(attrName)
            ?.Value
            .Trim() ?? string.Empty;
    }

    private static string MagnetEnclosureValue(XElement item)
    {
        var value = EnclosureValue(item, "url");
        return value.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) ? value : string.Empty;
    }

    private static string NonMagnetEnclosureValue(XElement item)
    {
        var value = EnclosureValue(item, "url");
        return value.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
    }

    private static string FirstNotEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    private static long ParseLong(string value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }
}
