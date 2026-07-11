using System.Net;
using System.Text.RegularExpressions;

namespace TorrWind.Core.Services;

public static class SensitiveValueRedactor
{
    private const string Redacted = "<redacted>";

    private static readonly Regex UrlRegex = new(
        @"(?<url>(?:https?|torrs)://[^\s""'<>\\]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AuthorizationRegex = new(
        @"(?<prefix>\bAuthorization\s*[:=]\s*)(?<scheme>Basic|Bearer)\s+[^\s,""}\]]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex QuotedAssignmentRegex = new(
        @"""(?<key>[^""]*(?:api[_-]?key|key|token|secret|pass|password|authorization|auth)[^""]*)""\s*:\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EscapedQuotedAssignmentRegex = new(
        "\\\\\\\"(?<key>[^\\\\\"]*(?:api[_-]?key|key|token|secret|pass|password|authorization|auth)[^\\\\\"]*)\\\\\\\"\\s*:\\s*\\\\\\\"(?<value>[^\\\\\"]*)\\\\\\\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PlainAssignmentRegex = new(
        @"(?<key>\b(?!authorization\b)[a-z0-9_.-]*(?:api[_-]?key|key|token|secret|pass|password|authorization|auth)[a-z0-9_.-]*)(?<separator>\s*[=:]\s*)(?<value>[^\s,;&""'}\]]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string RedactText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var redacted = WebUtility.HtmlDecode(value);
        redacted = UrlRegex.Replace(redacted, match => RedactUrl(match.Groups["url"].Value));
        redacted = AuthorizationRegex.Replace(redacted, match =>
            match.Groups["prefix"].Value + match.Groups["scheme"].Value + " " + Redacted);
        redacted = QuotedAssignmentRegex.Replace(redacted, match =>
            $"\"{match.Groups["key"].Value}\":\"{Redacted}\"");
        redacted = EscapedQuotedAssignmentRegex.Replace(redacted, match =>
            $"\\\"{match.Groups["key"].Value}\\\":\\\"{Redacted}\\\"");
        redacted = PlainAssignmentRegex.Replace(redacted, match =>
            match.Groups["key"].Value + match.Groups["separator"].Value + Redacted);

        return redacted;
    }

    public static string RedactUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var redacted = WebUtility.HtmlDecode(value);
        redacted = RedactUserInfo(redacted);
        redacted = RedactQuery(redacted);
        return redacted;
    }

    private static string RedactUserInfo(string value)
    {
        var schemeIndex = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex < 0)
        {
            return value;
        }

        var authorityStart = schemeIndex + 3;
        var authorityEnd = value.IndexOfAny(['/', '?', '#'], authorityStart);
        if (authorityEnd < 0)
        {
            authorityEnd = value.Length;
        }

        var atIndex = value.LastIndexOf('@', authorityEnd - 1, authorityEnd - authorityStart + 1);
        return atIndex < authorityStart
            ? value
            : value[..authorityStart] + Redacted + value[atIndex..];
    }

    private static string RedactQuery(string value)
    {
        var queryIndex = value.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex < 0)
        {
            return value;
        }

        var fragmentIndex = value.IndexOf('#', queryIndex + 1);
        var queryEnd = fragmentIndex >= 0 ? fragmentIndex : value.Length;
        var prefix = value[..(queryIndex + 1)];
        var query = value[(queryIndex + 1)..queryEnd];
        var fragment = fragmentIndex >= 0 ? value[fragmentIndex..] : string.Empty;

        var parts = query.Split('&');
        for (var i = 0; i < parts.Length; i++)
        {
            var separatorIndex = parts[i].IndexOf('=', StringComparison.Ordinal);
            var key = separatorIndex >= 0 ? parts[i][..separatorIndex] : parts[i];
            if (IsSensitiveName(WebUtility.UrlDecode(key)))
            {
                parts[i] = separatorIndex >= 0 ? key + "=" + Redacted : key;
            }
        }

        return prefix + string.Join("&", parts) + fragment;
    }

    private static bool IsSensitiveName(string name)
    {
        return name.Contains("key", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("token", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("pass", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("auth", StringComparison.OrdinalIgnoreCase);
    }
}
