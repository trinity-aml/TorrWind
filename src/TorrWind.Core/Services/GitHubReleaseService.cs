using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TorrWind.Core.Services;

public sealed class GitHubReleaseService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/YouROK/TorrServer/releases/latest";
    private readonly HttpClient _httpClient;

    public GitHubReleaseService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TorrWind/0.1");
    }

    public async Task<TorrServerRelease> GetLatestTorrServerReleaseAsync(CancellationToken cancellationToken = default)
    {
        var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(LatestReleaseUrl, cancellationToken)
            .ConfigureAwait(false);

        if (release is null)
        {
            throw new InvalidOperationException("GitHub returned an empty release response.");
        }

        var asset = release.Assets.FirstOrDefault(asset =>
            asset.Name.Contains("windows", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.Contains("amd64", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

        if (asset is null)
        {
            throw new InvalidOperationException("No TorrServer Windows amd64 asset was found in the latest release.");
        }

        return new TorrServerRelease(release.TagName, asset.Name, asset.BrowserDownloadUrl, asset.Size);
    }

    public async Task DownloadAsync(Uri url, string destinationFile, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile) ?? ".");

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = File.Create(destinationFile);

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

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

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
    }
}

public sealed record TorrServerRelease(string Version, string AssetName, Uri DownloadUrl, long SizeBytes);
