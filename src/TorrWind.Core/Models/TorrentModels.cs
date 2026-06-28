using System.Globalization;
using System.Text.Json;

namespace TorrWind.Core.Models;

public sealed class TorrentItem
{
    public string Hash { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string SourceLink { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public double Progress { get; set; }

    public int Seeders { get; set; }

    public int Peers { get; set; }

    public string Status { get; set; } = string.Empty;

    public IReadOnlyList<TorrentFile> Files { get; set; } = [];

    public string SizeText => FormatBytes(SizeBytes);

    public string ProgressText => Progress.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    public static TorrentItem FromJson(JsonElement element)
    {
        var item = new TorrentItem
        {
            Hash = element.ReadString("hash", "Hash", "info_hash"),
            Title = element.ReadString("title", "Title", "name", "Name"),
            SourceLink = element.ReadString("link", "Link", "magnet", "Magnet"),
            SizeBytes = element.ReadInt64("size", "Size", "length", "Length"),
            Progress = element.ReadDouble("progress", "Progress", "loaded", "Loaded"),
            Seeders = element.ReadInt32("seed", "Seed", "seeders", "Seeders"),
            Peers = element.ReadInt32("peer", "Peer", "peers", "Peers"),
            Status = element.ReadString("status", "Status", "stat", "Stat")
        };

        if (element.TryGetProperty("files", out var files) || element.TryGetProperty("Files", out files))
        {
            item.Files = files.ValueKind == JsonValueKind.Array
                ? files.EnumerateArray().Select(TorrentFile.FromJson).ToArray()
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
    public int Id { get; set; }

    public string Path { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string MimeType { get; set; } = string.Empty;

    public string SizeText => TorrentItem.FormatBytes(SizeBytes);

    public static TorrentFile FromJson(JsonElement element)
    {
        return new TorrentFile
        {
            Id = element.ReadInt32("id", "Id", "index", "Index"),
            Path = element.ReadString("path", "Path", "name", "Name"),
            SizeBytes = element.ReadInt64("size", "Size", "length", "Length"),
            MimeType = element.ReadString("mime", "Mime", "mime_type", "MimeType")
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
}

internal static class JsonElementExtensions
{
    public static string ReadString(this JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var property))
            {
                return property.ValueKind switch
                {
                    JsonValueKind.String => property.GetString() ?? string.Empty,
                    JsonValueKind.Number => property.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => string.Empty
                };
            }
        }

        return string.Empty;
    }

    public static int ReadInt32(this JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out value))
            {
                return value;
            }
        }

        return 0;
    }

    public static long ReadInt64(this JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out value))
            {
                return value;
            }
        }

        return 0;
    }

    public static double ReadDouble(this JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
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
}
