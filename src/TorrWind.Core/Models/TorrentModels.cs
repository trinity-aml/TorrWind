using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TorrWind.Core.Models;

public sealed class TorrentItem
{
    public string Hash { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string SourceLink { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Poster { get; set; } = string.Empty;

    public string Data { get; set; } = string.Empty;

    public string TorrsHash { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public long LoadedBytes { get; set; }

    public long PreloadedBytes { get; set; }

    public double DownloadSpeed { get; set; }

    public double UploadSpeed { get; set; }

    public double Progress { get; set; }

    public int Seeders { get; set; }

    public int Peers { get; set; }

    public string Status { get; set; } = string.Empty;

    public IReadOnlyList<TorrentFile> Files { get; set; } = [];

    public string SizeText => FormatBytes(SizeBytes);

    public string LoadedText => FormatBytes(LoadedBytes);

    public string PreloadedText => FormatBytes(PreloadedBytes);

    public string DownloadSpeedText => FormatBytes((long)Math.Max(DownloadSpeed, 0)) + "/s";

    public string UploadSpeedText => FormatBytes((long)Math.Max(UploadSpeed, 0)) + "/s";

    public string ProgressText => Progress.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    public static TorrentItem FromJson(JsonElement element)
    {
        var sizeBytes = element.ReadSizeBytes("size", "Size", "sizeBytes", "SizeBytes", "length", "Length", "torrent_size", "TorrentSize", "torrentSize");
        var loadedBytes = element.ReadSizeBytes("loaded_size", "LoadedSize", "loadedSize", "loaded_bytes", "LoadedBytes", "loadedBytes", "loaded", "Loaded");
        var progress = element.ReadDouble("progress", "Progress");
        if (progress <= 0 && sizeBytes > 0 && loadedBytes > 0)
        {
            progress = Math.Clamp((double)loadedBytes / sizeBytes * 100, 0, 100);
        }

        var item = new TorrentItem
        {
            Hash = element.ReadString("hash", "Hash", "info_hash", "InfoHash", "infoHash"),
            Title = element.ReadString("title", "Title", "name", "Name"),
            SourceLink = element.ReadString("link", "Link", "sourceLink", "SourceLink", "magnet", "Magnet"),
            Category = element.ReadString("category", "Category"),
            Poster = element.ReadString("poster", "Poster"),
            Data = element.ReadString("data", "Data"),
            TorrsHash = element.ReadString("torrs_hash", "TorrsHash", "torrsHash"),
            SizeBytes = sizeBytes,
            LoadedBytes = loadedBytes,
            PreloadedBytes = element.ReadSizeBytes("preloaded_bytes", "PreloadedBytes", "preloadedBytes", "preload_size", "PreloadSize", "preloadSize"),
            DownloadSpeed = element.ReadDouble("download_speed", "DownloadSpeed", "downloadSpeed", "download_rate", "DownloadRate", "downloadRate"),
            UploadSpeed = element.ReadDouble("upload_speed", "UploadSpeed", "uploadSpeed", "upload_rate", "UploadRate", "uploadRate"),
            Progress = progress,
            Seeders = element.ReadInt32("seed", "Seed", "seeders", "Seeders", "connected_seeders", "ConnectedSeeders", "connectedSeeders"),
            Peers = element.ReadInt32("peer", "Peer", "peers", "Peers", "total_peers", "TotalPeers", "totalPeers"),
            Status = element.ReadString("status", "Status", "stat_string", "StatString", "statString", "stat", "Stat")
        };

        if (element.TryGetPropertyIgnoreCase("files", out var files))
        {
            item.Files = files.ValueKind == JsonValueKind.Array
                ? files.EnumerateArray().Select(TorrentFile.FromJson).Where(file => file.IsVideoFile).ToArray()
                : [];
        }
        else if (element.TryGetPropertyIgnoreCase("file_stats", out files) ||
            element.TryGetPropertyIgnoreCase("fileStats", out files))
        {
            item.Files = files.ValueKind == JsonValueKind.Array
                ? files.EnumerateArray().Select(TorrentFile.FromJson).Where(file => file.IsVideoFile).ToArray()
                : [];
        }

        return item;
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(bytes, 0);
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return value.ToString(unit == 0 ? "0" : "0.##", CultureInfo.InvariantCulture) + " " + units[unit];
    }
}

public sealed class TorrentFile
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".264",
        ".265",
        ".3g2",
        ".3gp",
        ".3gp2",
        ".3gpp",
        ".amv",
        ".asf",
        ".av1",
        ".avc",
        ".avi",
        ".bik",
        ".cine",
        ".dav",
        ".divx",
        ".drc",
        ".dv",
        ".dvr",
        ".dvr-ms",
        ".evo",
        ".f4p",
        ".f4v",
        ".flc",
        ".fli",
        ".flv",
        ".gvi",
        ".gxf",
        ".h261",
        ".h263",
        ".h264",
        ".h265",
        ".hdmov",
        ".hevc",
        ".iso",
        ".ismv",
        ".ivf",
        ".m1v",
        ".m2p",
        ".m2t",
        ".m2ts",
        ".m2v",
        ".m4v",
        ".mj2",
        ".mjp",
        ".mjpg",
        ".mjpeg",
        ".mk3d",
        ".mkv",
        ".mod",
        ".mov",
        ".movie",
        ".mp2v",
        ".mp4",
        ".mp4v",
        ".mpe",
        ".mpeg",
        ".mpeg1",
        ".mpeg2",
        ".mpeg4",
        ".mpg",
        ".mpv",
        ".mts",
        ".mtv",
        ".mxf",
        ".mxg",
        ".nsv",
        ".nut",
        ".obu",
        ".ogm",
        ".ogv",
        ".qt",
        ".rec",
        ".rm",
        ".rmvb",
        ".roq",
        ".rv",
        ".smk",
        ".ssif",
        ".svi",
        ".tod",
        ".tp",
        ".trp",
        ".ts",
        ".tts",
        ".vdr",
        ".viv",
        ".vivo",
        ".vob",
        ".vp6",
        ".vro",
        ".webm",
        ".wm",
        ".wmv",
        ".wtv",
        ".xesc",
        ".y4m",
        ".yuv"
    };

    private static readonly Regex[] EpisodePatterns =
    [
        new(@"(?i)(?:^|[\s._\-\[])[sс](?<season>\d{1,2})[\s._\-\]]*[eе](?<episode>\d{1,3})(?:[\s._\-\]]*[eе]\d{1,3})?", RegexOptions.Compiled),
        new(@"(?i)(?:^|[\s._\-\[])(?<season>\d{1,2})x(?<episode>\d{1,3})", RegexOptions.Compiled)
    ];

    private static readonly Regex ResolutionPattern = new(
        @"(?i)(?:^|[\s._\-\[\(])(?<resolution>(?:4320|2160|1440|1080|900|720|576|540|480|360|240)p|(?:8k|4k|uhd|qhd|fhd|hd|sd))(?:$|[\s._\-\]\)])",
        RegexOptions.Compiled);

    private static readonly Regex DimensionsPattern = new(
        @"(?i)(?:^|[\s._\-\[\(])(?<width>\d{3,5})\s*[xх]\s*(?<height>\d{3,5})(?:$|[\s._\-\]\)])",
        RegexOptions.Compiled);

    public int Id { get; set; }

    public string Path { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string MimeType { get; set; } = string.Empty;

    public string Resolution { get; set; } = string.Empty;

    public string SizeText => TorrentItem.FormatBytes(SizeBytes);

    public bool IsVideoFile => IsVideoFilePath(Path, MimeType);

    public string DisplayName => CleanFileName(Path);

    public string ResolutionText => Resolution;

    public string SeasonText => TryParseEpisode(Path, out var season, out _, out _)
        ? season.ToString(CultureInfo.InvariantCulture)
        : string.Empty;

    public string EpisodeText => TryParseEpisode(Path, out _, out var episode, out _)
        ? episode.ToString(CultureInfo.InvariantCulture)
        : string.Empty;

    public string EpisodeTitle => DisplayName;

    public static TorrentFile FromJson(JsonElement element)
    {
        var path = element.ReadString("path", "Path", "name", "Name");
        return new TorrentFile
        {
            Id = element.ReadInt32("id", "Id", "index", "Index"),
            Path = path,
            SizeBytes = element.ReadSizeBytes("size", "Size", "sizeBytes", "SizeBytes", "length", "Length"),
            MimeType = element.ReadString("mime", "Mime", "mime_type", "MimeType"),
            Resolution = ResolveResolution(
                path,
                element.ReadString("resolution", "Resolution", "video_resolution", "VideoResolution"),
                element.ReadInt32("width", "Width", "video_width", "VideoWidth"),
                element.ReadInt32("height", "Height", "video_height", "VideoHeight"))
        };
    }

    public static bool IsVideoFilePath(string path, string mimeType = "")
    {
        var mime = mimeType.Split(';', 2)[0].Trim();
        if (mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = System.IO.Path.GetExtension(path.Replace('\\', '/')).Trim();
        return !string.IsNullOrWhiteSpace(extension) && VideoExtensions.Contains(extension);
    }

    private static bool TryParseEpisode(string path, out int season, out int episode, out string title)
    {
        var fileName = CleanFileName(path);
        foreach (var pattern in EpisodePatterns)
        {
            var match = pattern.Match(fileName);
            if (!match.Success ||
                !int.TryParse(match.Groups["season"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out season) ||
                !int.TryParse(match.Groups["episode"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out episode))
            {
                continue;
            }

            title = fileName;
            return true;
        }

        season = 0;
        episode = 0;
        title = string.Empty;
        return false;
    }

    private static string CleanFileName(string path)
    {
        var normalized = path.Replace('\\', '/');
        var fileName = normalized[(normalized.LastIndexOf('/') + 1)..];
        var extension = System.IO.Path.GetExtension(fileName);
        return string.IsNullOrWhiteSpace(extension) || !VideoExtensions.Contains(extension)
            ? fileName
            : fileName[..^extension.Length];
    }

    private static string ResolveResolution(string path, string explicitResolution, int width, int height)
    {
        if (!string.IsNullOrWhiteSpace(explicitResolution))
        {
            return explicitResolution.Trim();
        }

        if (width > 0 && height > 0)
        {
            return width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture);
        }

        var fileName = CleanFileName(path);
        var dimensionsMatch = DimensionsPattern.Match(fileName);
        if (dimensionsMatch.Success)
        {
            return dimensionsMatch.Groups["width"].Value + "x" + dimensionsMatch.Groups["height"].Value;
        }

        var resolutionMatch = ResolutionPattern.Match(fileName);
        if (!resolutionMatch.Success)
        {
            return string.Empty;
        }

        return resolutionMatch.Groups["resolution"].Value.ToUpperInvariant() switch
        {
            "8K" => "4320p",
            "4K" or "UHD" => "2160p",
            "QHD" => "1440p",
            "FHD" => "1080p",
            "HD" => "720p",
            "SD" => "480p",
            var value when value.EndsWith("P", StringComparison.Ordinal) => value[..^1] + "p",
            var value => value
        };
    }
}

public sealed class SearchResult
{
    public string ProviderName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Link { get; set; } = string.Empty;

    public string Magnet { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string SizeText => TorrentItem.FormatBytes(SizeBytes);

    public int Seeders { get; set; }

    public int Leechers { get; set; }

    public string Category { get; set; } = string.Empty;

    public DateTimeOffset? PublishedAt { get; set; }

    public string PublishedAtText => PublishedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture) ?? string.Empty;

    public static SearchResult FromTorrServerJson(JsonElement element, string providerName)
    {
        var link = element.ReadString("link", "Link", "downloadUrl", "DownloadUrl", "download_url", "url", "Url");
        var magnet = element.ReadString("magnet", "Magnet", "magnetUrl", "MagnetUrl", "magnet_url");

        return new SearchResult
        {
            ProviderName = FirstNotEmpty(element.ReadString(
                "tracker",
                "Tracker",
                "provider",
                "Provider",
                "indexer",
                "Indexer",
                "source",
                "Source"), providerName),
            Title = FirstNotEmpty(element.ReadString("title", "Title"), element.ReadString("name", "Name")),
            Link = NonMagnetValue(link),
            Magnet = FirstNotEmpty(magnet, MagnetValue(link)),
            SizeBytes = ParseSizeBytes(element.ReadString("size", "Size", "sizeBytes", "SizeBytes", "length", "Length")),
            Seeders = element.ReadInt32("seed", "Seed", "seeders", "Seeders", "seeds", "Seeds"),
            Leechers = element.ReadInt32("peer", "Peer", "leechers", "Leechers", "peers", "Peers", "leeches", "Leeches"),
            Category = element.ReadString("categories", "Categories", "category", "Category", "cat", "Cat"),
            PublishedAt = ParsePublishedAt(element.ReadString(
                "createDate",
                "CreateDate",
                "pubDate",
                "PubDate",
                "publishedAt",
                "PublishedAt",
                "publishDate",
                "PublishDate",
                "date",
                "Date"))
        };
    }

    internal static DateTimeOffset? ParsePublishedAt(string value)
    {
        var trimmed = value.Trim();
        if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestamp))
        {
            try
            {
                return trimmed.Length > 10
                    ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
                    : DateTimeOffset.FromUnixTimeSeconds(timestamp);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return DateTimeOffset.TryParse(
            trimmed,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var publishedAt)
                ? publishedAt
                : null;
    }

    internal static long ParseSizeBytes(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var trimmed = value.Trim().Replace('\u00A0', ' ');
        if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes))
        {
            return bytes;
        }

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var numericText = parts.Length > 0 ? parts[0] : string.Empty;
        var unitText = parts.Length > 1 ? parts[1] : "B";

        if (parts.Length > 1)
        {
            var lastPart = parts[^1];
            if (parts.Take(parts.Length - 1).All(IsIntegerGroup) && !IsIntegerGroup(lastPart))
            {
                numericText = string.Concat(parts.Take(parts.Length - 1));
                unitText = lastPart;
            }
            else if (parts.All(IsIntegerGroup))
            {
                numericText = string.Concat(parts);
                unitText = "B";
            }
        }
        else
        {
            var unitStart = 0;
            while (unitStart < trimmed.Length &&
                (char.IsDigit(trimmed[unitStart]) ||
                 trimmed[unitStart] == '.' ||
                 trimmed[unitStart] == ','))
            {
                unitStart++;
            }

            if (unitStart > 0 && unitStart < trimmed.Length)
            {
                numericText = trimmed[..unitStart];
                unitText = trimmed[unitStart..];
            }
        }

        if (string.IsNullOrWhiteSpace(numericText) ||
            !double.TryParse(numericText.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var numericValue))
        {
            return 0;
        }

        var unit = unitText.Trim().ToUpperInvariant();
        var baseValue = unit.Contains('I', StringComparison.Ordinal) ||
            unit.Contains('\u0418', StringComparison.Ordinal) ||
            unit.Contains('C', StringComparison.Ordinal)
            ? 1024D
            : 1000D;
        var exponent = unit.Length == 0
            ? 0
            : unit[0] switch
            {
                'K' or '\u041A' => 1,
                'M' or '\u041C' => 2,
                'G' or '\u0413' => 3,
                'T' or '\u0422' => 4,
                _ => 0
            };

        return (long)Math.Round(numericValue * Math.Pow(baseValue, exponent));
    }

    private static bool IsIntegerGroup(string value)
    {
        return value.All(char.IsDigit);
    }

    private static string FirstNotEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string MagnetValue(string value)
    {
        return value.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) ? value : string.Empty;
    }

    private static string NonMagnetValue(string value)
    {
        return value.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
    }
}

internal static class JsonElementExtensions
{
    public static string ReadString(this JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetPropertyIgnoreCase(name, out var property))
            {
                return property.ValueKind switch
                {
                    JsonValueKind.String => property.GetString() ?? string.Empty,
                    JsonValueKind.Number => property.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Array => JoinPrimitiveArrayValues(property),
                    _ => string.Empty
                };
            }
        }

        return string.Empty;
    }

    private static string JoinPrimitiveArrayValues(JsonElement array)
    {
        return string.Join(
            ",",
            array.EnumerateArray()
                .Select(item => item.ValueKind switch
                {
                    JsonValueKind.String => item.GetString() ?? string.Empty,
                    JsonValueKind.Number => item.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => string.Empty
                })
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    public static int ReadInt32(this JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetPropertyIgnoreCase(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var number))
            {
                return ClampToInt32(number);
            }

            if (property.ValueKind == JsonValueKind.String &&
                int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String &&
                double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                return ClampToInt32(number);
            }
        }

        return 0;
    }

    public static long ReadInt64(this JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetPropertyIgnoreCase(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var number))
            {
                return ClampToInt64(number);
            }

            if (property.ValueKind == JsonValueKind.String &&
                long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String &&
                double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                return ClampToInt64(number);
            }
        }

        return 0;
    }

    private static int ClampToInt32(double value)
    {
        if (double.IsNaN(value))
        {
            return 0;
        }

        if (value >= int.MaxValue)
        {
            return int.MaxValue;
        }

        if (value <= int.MinValue)
        {
            return int.MinValue;
        }

        return (int)value;
    }

    private static long ClampToInt64(double value)
    {
        if (double.IsNaN(value))
        {
            return 0;
        }

        if (value >= long.MaxValue)
        {
            return long.MaxValue;
        }

        if (value <= long.MinValue)
        {
            return long.MinValue;
        }

        return (long)value;
    }

    public static long ReadSizeBytes(this JsonElement element, params string[] names)
    {
        return SearchResult.ParseSizeBytes(element.ReadString(names));
    }

    public static double ReadDouble(this JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetPropertyIgnoreCase(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String &&
                double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }
        }

        return 0;
    }

    public static bool TryGetPropertyIgnoreCase(this JsonElement element, string name, out JsonElement property)
    {
        if (element.TryGetProperty(name, out property))
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}
