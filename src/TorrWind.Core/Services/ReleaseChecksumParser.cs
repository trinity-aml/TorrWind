using System.Net;
using System.Text.RegularExpressions;

namespace TorrWind.Core.Services;

internal static class ReleaseChecksumParser
{
    private static readonly Regex Sha256Regex = new(
        "(?:sha256:)?(?<hash>[a-f0-9]{64})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string NormalizeSha256(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var match = Sha256Regex.Match(value);
        return match.Success ? match.Groups["hash"].Value.ToLowerInvariant() : string.Empty;
    }

    public static string? FindSha256ForAsset(string checksumText, string assetName)
    {
        foreach (var line in checksumText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (LineReferencesAsset(line, assetName))
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

    private static bool LineReferencesAsset(string line, string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return false;
        }

        foreach (var token in line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = WebUtility.UrlDecode(token)
                .Trim()
                .Trim('*', '"', '\'', '(', ')');
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            candidate = candidate.Replace('\\', '/');
            var fileName = candidate[(candidate.LastIndexOf('/') + 1)..];
            if (string.Equals(fileName, assetName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
