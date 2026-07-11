using System.Net;
using System.Text.RegularExpressions;

namespace TorrWind.Core.Services;

internal static class ReleaseChecksumParser
{
    private static readonly Regex Sha256Regex = new(
        "(?:sha256:)?(?<hash>[a-f0-9]{64})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CandidateTokenRegex = new(
        "[^\\s\"'()=,;<>]+",
        RegexOptions.Compiled);

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

        var decodedLine = WebUtility.HtmlDecode(WebUtility.UrlDecode(line));
        foreach (Match match in CandidateTokenRegex.Matches(decodedLine))
        {
            var fileName = ExtractCandidateFileName(match.Value);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            if (string.Equals(fileName, assetName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ExtractCandidateFileName(string token)
    {
        var candidate = token
            .Trim()
            .Trim('*', '"', '\'', '(', ')', '[', ']', '{', '}', '<', '>', ',', ';');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        candidate = candidate.Replace('\\', '/');
        var queryIndex = candidate.IndexOfAny(['?', '#']);
        if (queryIndex >= 0)
        {
            candidate = candidate[..queryIndex];
        }

        return candidate[(candidate.LastIndexOf('/') + 1)..]
            .Trim()
            .Trim('*', '"', '\'', '(', ')', '[', ']', '{', '}', '<', '>', ',', ';');
    }
}
