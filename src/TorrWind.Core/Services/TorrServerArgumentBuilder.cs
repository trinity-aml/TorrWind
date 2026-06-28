using TorrWind.Core;
using TorrWind.Core.Models;

namespace TorrWind.Core.Services;

public static class TorrServerArgumentBuilder
{
    public static IReadOnlyList<string> Build(LocalServerSettings settings)
    {
        var args = new List<string>
        {
            "--port",
            settings.Port.ToString(),
            "--ip",
            settings.AllowLanAccess ? "0.0.0.0" : settings.ListenAddress,
            "--path",
            LocalTorrServerConfigurationWriter.GetDataDirectory(settings)
        };

        if (settings.UseHttpAuth)
        {
            args.Add("--httpauth");
        }

        if (settings.UseSsl)
        {
            args.Add("--ssl");

            if (settings.SslPort > 0)
            {
                args.Add("--sslport");
                args.Add(settings.SslPort.ToString());
            }

            if (!string.IsNullOrWhiteSpace(settings.CertificatePath))
            {
                args.Add("--sslcert");
                args.Add(settings.CertificatePath);
            }

            if (!string.IsNullOrWhiteSpace(settings.CertificateKeyPath))
            {
                args.Add("--sslkey");
                args.Add(settings.CertificateKeyPath);
            }

            if (settings.ForceHttps)
            {
                args.Add("--force-https");
            }
        }

        if (settings.ReadOnlyDatabase)
        {
            args.Add("--rdb");
        }

        if (settings.AllowSearchWithoutAuth)
        {
            args.Add("--searchwa");
        }

        if (settings.MaxStreamSizeMb > 0)
        {
            args.Add("--maxsize");
            args.Add(((long)settings.MaxStreamSizeMb * 1024L * 1024L).ToString());
        }

        if (!string.IsNullOrWhiteSpace(settings.ProxyUrl))
        {
            args.Add("--proxyurl");
            args.Add(settings.ProxyUrl.Trim());
        }

        if (!string.IsNullOrWhiteSpace(settings.ProxyMode))
        {
            args.Add("--proxymode");
            args.Add(settings.ProxyMode.Trim());
        }

        if (settings.EnableWebDav)
        {
            args.Add("--webdav");
        }

        return args;
    }
}
