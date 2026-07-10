using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TorrWind.Core.Services;

public sealed class TorrWindReleaseService
{
    private const string GitHubBaseUrl = "https://github.com";
    private const string LatestReleaseUrl = "https://api.github.com/repos/trinity-aml/TorrWind/releases/latest";
    private const string LatestReleasePageUrl = "https://github.com/trinity-aml/TorrWind/releases/latest";
    private const string ExpandedAssetsPageUrl = "https://github.com/trinity-aml/TorrWind/releases/expanded_assets/";
    private const string UserAgent = "TorrWind/1.0.3";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex ReleaseTagRegex = new(
        "/trinity-aml/TorrWind/releases/tag/(?<tag>[^\"?#<]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DateTimeRegex = new(
        "<relative-time\\b[^>]*datetime=\"(?<datetime>[^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AssetRowRegex = new(
        "<li\\b(?<row>.*?)</li>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex AssetHrefRegex = new(
        "<a\\s+href=\"(?<href>/trinity-aml/TorrWind/releases/download/[^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Sha256Regex = new(
        "(?:sha256:)?(?<hash>[a-f0-9]{64})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly HttpClient _httpClient;

    public TorrWindReleaseService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        }
    }

    public async Task<TorrWindRelease> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var release = await GetGitHubApiJsonAsync<GitHubRelease>(LatestReleaseUrl, cancellationToken)
                .ConfigureAwait(false);

            return MapRelease(release);
        }
        catch (GitHubApiRateLimitException)
        {
            return await GetLatestReleaseFromGitHubPageAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            return await GetLatestReleaseFromGitHubPageAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task DownloadAsync(
        Uri url,
        string destinationFile,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile) ?? ".");
        var temporaryFile = destinationFile + ".download";

        try
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }

            using var request = CreateGitHubRequest(HttpMethod.Get, url, "application/octet-stream");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            {
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var output = File.Create(temporaryFile);

                var buffer = new byte[64 * 1024];
                long total = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    total += read;
                    progress?.Report(total);
                }
            }

            File.Move(temporaryFile, destinationFile, overwrite: true);
        }
        catch
        {
            TryDeleteFile(temporaryFile);
            throw;
        }
    }

    public async Task<string?> GetExpectedSha256Async(
        TorrWindRelease release,
        CancellationToken cancellationToken = default)
    {
        var digest = NormalizeSha256(release.Sha256);
        if (!string.IsNullOrWhiteSpace(digest))
        {
            return digest;
        }

        if (release.ChecksumDownloadUrl is null)
        {
            return null;
        }

        using var request = CreateGitHubRequest(HttpMethod.Get, release.ChecksumDownloadUrl, "text/plain");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var checksumText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return FindSha256ForAsset(checksumText, release.PackageName);
    }

    private async Task<T> GetGitHubApiJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var request = CreateGitHubRequest(HttpMethod.Get, new Uri(url), "application/vnd.github+json");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Forbidden && IsGitHubRateLimited(response))
        {
            throw new GitHubApiRateLimitException(GetGitHubRateLimitMessage(response));
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonSerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return value ?? throw new InvalidOperationException("GitHub returned an empty response.");
    }

    private async Task<TorrWindRelease> GetLatestReleaseFromGitHubPageAsync(CancellationToken cancellationToken)
    {
        var (html, finalUri) = await GetGitHubHtmlAsync(new Uri(LatestReleasePageUrl), cancellationToken)
            .ConfigureAwait(false);
        var version = TryExtractReleaseTag(finalUri) ??
            TryExtractReleaseTag(html) ??
            throw new InvalidOperationException("No TorrWind release tag was found on the GitHub latest release page.");
        var publishedAt = TryExtractPublishedAt(html);

        var assets = ParseReleaseAssets(html);
        var package = FindPackageAsset(assets);
        if (package.Asset is null)
        {
            var escapedVersion = Uri.EscapeDataString(version);
            var (assetsHtml, _) = await GetGitHubHtmlAsync(
                    new Uri($"{ExpandedAssetsPageUrl}{escapedVersion}"),
                    cancellationToken)
                .ConfigureAwait(false);
            assets = ParseReleaseAssets(assetsHtml);
            package = FindPackageAsset(assets);
        }

        if (package.Asset is null)
        {
            throw new InvalidOperationException("No TorrWind Windows x64 update asset was found in the latest release.");
        }

        var exactSize = await TryGetContentLengthAsync(package.Asset.DownloadUrl, cancellationToken).ConfigureAwait(false);
        var checksumAsset = FindChecksumAsset(assets, package.Asset);
        return new TorrWindRelease(
            version,
            package.Asset.Name,
            package.Asset.DownloadUrl,
            exactSize,
            publishedAt,
            false,
            package.Kind,
            NormalizeSha256(package.Asset.Digest),
            checksumAsset?.DownloadUrl);
    }

    private async Task<(string Html, Uri FinalUri)> GetGitHubHtmlAsync(Uri url, CancellationToken cancellationToken)
    {
        using var request = CreateGitHubRequest(HttpMethod.Get, url, "text/html");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return (
            await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false),
            response.RequestMessage?.RequestUri ?? url);
    }

    private async Task<long> TryGetContentLengthAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateGitHubRequest(HttpMethod.Head, url, "application/octet-stream");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? response.Content.Headers.ContentLength.GetValueOrDefault()
                : 0;
        }
        catch (HttpRequestException)
        {
            return 0;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
    }

    private static HttpRequestMessage CreateGitHubRequest(HttpMethod method, Uri url, string accept)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Accept.ParseAdd(accept);
        return request;
    }

    private static IReadOnlyList<ReleaseAsset> ParseReleaseAssets(string html)
    {
        var assets = new List<ReleaseAsset>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match rowMatch in AssetRowRegex.Matches(html))
        {
            var row = rowMatch.Groups["row"].Value;
            var hrefMatch = AssetHrefRegex.Match(row);
            if (!hrefMatch.Success)
            {
                continue;
            }

            var href = WebUtility.HtmlDecode(hrefMatch.Groups["href"].Value);
            var downloadUrl = new Uri(new Uri(GitHubBaseUrl), href);
            if (!seen.Add(downloadUrl.AbsoluteUri))
            {
                continue;
            }

            var name = ExtractAssetName(href);
            if (!string.IsNullOrWhiteSpace(name))
            {
                assets.Add(new ReleaseAsset(name, downloadUrl, 0));
            }
        }

        return assets;
    }

    private static TorrWindRelease MapRelease(GitHubRelease release)
    {
        var assets = release.Assets
            .Select(asset => new ReleaseAsset(asset.Name, asset.BrowserDownloadUrl, asset.Size, asset.Digest))
            .ToList();
        var package = FindPackageAsset(assets);
        var checksumAsset = package.Asset is null ? null : FindChecksumAsset(assets, package.Asset);

        return package.Asset is null
            ? throw new InvalidOperationException("No TorrWind Windows x64 update asset was found in the latest release.")
            : new TorrWindRelease(
                release.TagName,
                package.Asset.Name,
                package.Asset.DownloadUrl,
                package.Asset.SizeBytes,
                release.PublishedAt,
                release.Prerelease,
                package.Kind,
                NormalizeSha256(package.Asset.Digest),
                checksumAsset?.DownloadUrl);
    }

    private static (ReleaseAsset? Asset, string Kind) FindPackageAsset(IEnumerable<ReleaseAsset> assets)
    {
        var assetList = assets.ToList();
        var installer = assetList
            .Where(IsInstallerAsset)
            .OrderBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (installer is not null)
        {
            return (installer, "Installer");
        }

        var portable = assetList
            .Where(IsPortableAsset)
            .OrderBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return portable is null ? (null, string.Empty) : (portable, "Portable");
    }

    private static bool IsInstallerAsset(ReleaseAsset asset)
    {
        return asset.Name.StartsWith("TorrWind-", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.Contains("win-x64", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPortableAsset(ReleaseAsset asset)
    {
        return asset.Name.StartsWith("TorrWind-", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.Contains("win-x64", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.Contains("portable", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static ReleaseAsset? FindChecksumAsset(IEnumerable<ReleaseAsset> assets, ReleaseAsset selectedAsset)
    {
        return assets
            .Where(asset => !ReferenceEquals(asset, selectedAsset) &&
                IsChecksumAssetName(asset.Name) &&
                asset.Name.Contains(selectedAsset.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(asset => GetChecksumAssetPreference(asset.Name))
            .ThenBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ??
            assets
                .Where(asset => !ReferenceEquals(asset, selectedAsset) && IsChecksumAssetName(asset.Name))
                .OrderBy(asset => GetChecksumAssetPreference(asset.Name))
                .ThenBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
    }

    private static bool IsChecksumAssetName(string name)
    {
        return name.Contains("sha256", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("checksum", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".sha256sum", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".sha256sums", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetChecksumAssetPreference(string name)
    {
        if (name.Contains("sha256", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return name.Contains("checksum", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
    }

    private static string? TryExtractReleaseTag(Uri uri)
    {
        const string marker = "/trinity-aml/TorrWind/releases/tag/";
        var path = uri.AbsolutePath;
        var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? null : DecodeUrlSegment(path[(index + marker.Length)..].Trim('/'));
    }

    private static string? TryExtractReleaseTag(string html)
    {
        var match = ReleaseTagRegex.Match(html);
        return match.Success ? DecodeUrlSegment(match.Groups["tag"].Value) : null;
    }

    private static DateTimeOffset TryExtractPublishedAt(string html)
    {
        var match = DateTimeRegex.Match(html);
        return match.Success && DateTimeOffset.TryParse(
            match.Groups["datetime"].Value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var publishedAt)
            ? publishedAt
            : default;
    }

    private static string ExtractAssetName(string href)
    {
        var index = href.LastIndexOf('/');
        return index < 0 || index == href.Length - 1
            ? string.Empty
            : DecodeUrlSegment(href[(index + 1)..]);
    }

    private static string DecodeUrlSegment(string value)
    {
        return Uri.UnescapeDataString(WebUtility.HtmlDecode(value));
    }

    private static string NormalizeSha256(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var match = Sha256Regex.Match(value);
        return match.Success ? match.Groups["hash"].Value.ToLowerInvariant() : string.Empty;
    }

    private static string? FindSha256ForAsset(string checksumText, string assetName)
    {
        foreach (var line in checksumText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Contains(assetName, StringComparison.OrdinalIgnoreCase))
            {
                var lineHash = NormalizeSha256(line);
                if (!string.IsNullOrWhiteSpace(lineHash))
                {
                    return lineHash;
                }
            }
        }

        var matches = Sha256Regex.Matches(checksumText);
        return matches.Count == 1
            ? matches[0].Groups["hash"].Value.ToLowerInvariant()
            : null;
    }

    private static bool IsGitHubRateLimited(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("X-RateLimit-Remaining", out var values) &&
            values.Any(value => string.Equals(value, "0", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetGitHubRateLimitMessage(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var values) &&
            long.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var resetSeconds))
        {
            var resetAt = DateTimeOffset.FromUnixTimeSeconds(resetSeconds).ToLocalTime();
            return $"GitHub API rate limit exceeded. It resets at {resetAt:yyyy-MM-dd HH:mm:ss zzz}.";
        }

        return "GitHub API rate limit exceeded.";
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("published_at")]
        public DateTimeOffset PublishedAt { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public Uri BrowserDownloadUrl { get; set; } = new("about:blank");

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("digest")]
        public string Digest { get; set; } = string.Empty;
    }

    private sealed record ReleaseAsset(string Name, Uri DownloadUrl, long SizeBytes, string Digest = "");

    private sealed class GitHubApiRateLimitException(string message) : Exception(message);
}

public sealed record TorrWindRelease(
    string Version,
    string PackageName,
    Uri DownloadUrl,
    long SizeBytes,
    DateTimeOffset PublishedAt,
    bool IsPrerelease,
    string PackageKind,
    string Sha256 = "",
    Uri? ChecksumDownloadUrl = null);
