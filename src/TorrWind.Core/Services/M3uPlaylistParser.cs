using System.Net;

namespace TorrWind.Core.Services;

public static class M3uPlaylistParser
{
    public static bool LooksLikePlaylist(Uri uri)
    {
        var path = uri.AbsolutePath;
        return path.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ||
            uri.Query.Contains("m3u", StringComparison.OrdinalIgnoreCase);
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
}

public sealed record M3uPlaylistEntry(int Number, string Title, Uri Uri);
