using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using TorrWind.Core.Models;

namespace TorrWind.Core.Services;

public static class LocalTorrServerEndpointProbe
{
    public static bool IsExecutableRunning(string executablePath)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        var processName = Path.GetFileNameWithoutExtension(executablePath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var expectedPath = Path.GetFullPath(executablePath);
        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    var processPath = process.MainModule?.FileName;
                    if (string.Equals(processPath, expectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Some elevated/system processes do not expose MainModule to a normal user.
                }
            }
        }

        return false;
    }

    public static async Task<bool> IsOnlineAsync(
        LocalServerSettings settings,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (settings.Port <= 0)
        {
            return false;
        }

        if (!TryBuildBaseUri(settings, out var baseUri))
        {
            return false;
        }

        using var handler = new HttpClientHandler();
        if (settings.UseSsl)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        using var client = new HttpClient(handler)
        {
            BaseAddress = baseUri,
            Timeout = timeout
        };

        if (settings.UseHttpAuth && !string.IsNullOrWhiteSpace(settings.Username))
        {
            var password = settings.Password ?? string.Empty;
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.Username + ":" + password));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        }

        try
        {
            using var response = await client
                .GetAsync("echo", HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private static bool TryBuildBaseUri(LocalServerSettings settings, out Uri uri)
    {
        var useSslEndpoint = settings.UseSsl && settings.SslPort > 0;
        var scheme = useSslEndpoint ? "https" : "http";
        var port = useSslEndpoint ? settings.SslPort : settings.Port;

        if (port <= 0 || port > 65535)
        {
            uri = new Uri("http://127.0.0.1/");
            return false;
        }

        var host = NormalizeProbeHost(settings.ListenAddress);
        var uriBuilderHost = NormalizeUriBuilderHost(host);
        if (Uri.CheckHostName(uriBuilderHost) == UriHostNameType.Unknown)
        {
            uri = new Uri("http://127.0.0.1/");
            return false;
        }

        try
        {
            uri = new UriBuilder(scheme, uriBuilderHost, port).Uri;
            return true;
        }
        catch (UriFormatException)
        {
            uri = new Uri("http://127.0.0.1/");
            return false;
        }
    }

    private static string NormalizeProbeHost(string listenAddress)
    {
        var host = string.IsNullOrWhiteSpace(listenAddress) ? "127.0.0.1" : listenAddress.Trim();
        return host is "*" or "+" or "0.0.0.0" or "::" or "[::]" ? "127.0.0.1" : host;
    }

    private static string NormalizeUriBuilderHost(string host)
    {
        return host.Length > 2 && host[0] == '[' && host[^1] == ']'
            ? host[1..^1]
            : host;
    }
}
