using System.Net;

namespace TorrWind.Core.Services;

public static class M3uPlaylistParser
{
    public static bool LooksLikePlaylist(Uri uri)
    {
        var path = uri.AbsolutePath;
        return path.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ||
            QueryHasPlaylistMarker(uri.Query);
    }

    public static IReadOnlyList<M3uPlaylistEntry> Parse(string playlistText, Uri playlistUri)
    {
        var entries = new List<M3uPlaylistEntry>();
        var pendingTitle = string.Empty;

        using var reader = new StringReader(playlistText);
        while (reader.ReadLine() is { } rawLine)
        {
            var line = rawLine.Trim().TrimStart('\uFEFF');
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                var comma = FindExtInfTitleSeparator(line);
                pendingTitle = comma >= 0 && comma < line.Length - 1
                    ? line[(comma + 1)..].Trim()
                    : string.Empty;
                continue;
            }

            if (line.StartsWith('#'))
            {
                continue;
            }

            if (!Uri.TryCreate(line, UriKind.Absolute, out var itemUri) &&
                !Uri.TryCreate(playlistUri, line, out itemUri))
            {
                pendingTitle = string.Empty;
                continue;
            }

            var number = entries.Count + 1;
            var title = string.IsNullOrWhiteSpace(pendingTitle)
                ? ResolveTitleFromUri(itemUri, number)
                : pendingTitle;
            entries.Add(new M3uPlaylistEntry(number, title, itemUri));
            pendingTitle = string.Empty;
        }

        return entries;
    }

    private static int FindExtInfTitleSeparator(string line)
    {
        var inQuotes = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (character == ',' && !inQuotes)
            {
                return index;
            }
        }

        return -1;
    }

    private static string ResolveTitleFromUri(Uri uri, int number)
    {
        var fileName = WebUtility.UrlDecode(Path.GetFileName(uri.LocalPath));
        return string.IsNullOrWhiteSpace(fileName)
            ? "Episode " + number
            : fileName;
    }

    private static bool QueryHasPlaylistMarker(string query)
    {
        var trimmed = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            var key = DecodeQueryPart(separator >= 0 ? part[..separator] : part);
            var value = separator >= 0 ? DecodeQueryPart(part[(separator + 1)..]) : string.Empty;

            if (IsPlaylistMarker(key) || IsPlaylistMarker(value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPlaylistMarker(string value)
    {
        return string.Equals(value, "m3u", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "m3u8", StringComparison.OrdinalIgnoreCase);
    }

    private static string DecodeQueryPart(string value)
    {
        return WebUtility.UrlDecode(value.Replace('+', ' ')) ?? string.Empty;
    }
}

public sealed record M3uPlaylistEntry(int Number, string Title, Uri Uri);
