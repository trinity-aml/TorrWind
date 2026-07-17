using System.Globalization;
using System.Text.RegularExpressions;

namespace TorrWind.Core.Services;

public static class WindowsServiceSecurityDescriptor
{
    private const string InteractiveControlAce = "(A;;LCRPWP;;;IU)";
    private const int RequiredAccessMask = 0x0004 | 0x0010 | 0x0020;

    private static readonly Regex InteractiveAllowAceRegex = new(
        @"\(A;[^;]*;(?<rights>[^;]*);[^;]*;[^;]*;(?:IU|S-1-5-4)(?:;[^)]*)?\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string ExtractFromScOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new FormatException("sc.exe did not return a service security descriptor.");
        }

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.Contains("D:", StringComparison.OrdinalIgnoreCase) &&
                (line.StartsWith("O:", StringComparison.OrdinalIgnoreCase) ||
                 line.StartsWith("G:", StringComparison.OrdinalIgnoreCase) ||
                 line.StartsWith("D:", StringComparison.OrdinalIgnoreCase)))
            {
                return line;
            }
        }

        throw new FormatException("sc.exe output does not contain a DACL security descriptor.");
    }

    public static string GrantInteractiveStartStop(string securityDescriptor)
    {
        if (string.IsNullOrWhiteSpace(securityDescriptor))
        {
            throw new ArgumentException("Service security descriptor is empty.", nameof(securityDescriptor));
        }

        if (HasInteractiveControlAccess(securityDescriptor))
        {
            return securityDescriptor;
        }

        var daclIndex = securityDescriptor.IndexOf("D:", StringComparison.OrdinalIgnoreCase);
        if (daclIndex < 0)
        {
            throw new FormatException("Service security descriptor does not contain a DACL.");
        }

        var saclIndex = securityDescriptor.IndexOf("S:", daclIndex + 2, StringComparison.OrdinalIgnoreCase);
        var insertIndex = saclIndex >= 0 ? saclIndex : securityDescriptor.Length;
        return securityDescriptor.Insert(insertIndex, InteractiveControlAce);
    }

    private static bool HasInteractiveControlAccess(string securityDescriptor)
    {
        foreach (Match match in InteractiveAllowAceRegex.Matches(securityDescriptor))
        {
            var rights = match.Groups["rights"].Value;
            if (TryParseAccessMask(rights, out var accessMask))
            {
                if ((accessMask & RequiredAccessMask) == RequiredAccessMask)
                {
                    return true;
                }

                continue;
            }

            if (rights.Contains("LC", StringComparison.OrdinalIgnoreCase) &&
                rights.Contains("RP", StringComparison.OrdinalIgnoreCase) &&
                rights.Contains("WP", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseAccessMask(string rights, out int accessMask)
    {
        if (rights.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(
                rights.AsSpan(2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out accessMask);
        }

        accessMask = 0;
        return false;
    }
}
