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

        using var handler = new HttpClientHandler();
        if (settings.UseSsl)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        using var client = new HttpClient(handler)
        {
            BaseAddress = BuildBaseUri(settings),
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

    private static Uri BuildBaseUri(LocalServerSettings settings)
    {
        var useSslEndpoint = settings.UseSsl && settings.SslPort > 0;
        var scheme = useSslEndpoint ? "https" : "http";
        var port = useSslEndpoint ? settings.SslPort : settings.Port;

        return new UriBuilder(scheme, NormalizeProbeHost(settings.ListenAddress), port).Uri;
    }

    private static string NormalizeProbeHost(string listenAddress)
    {
        var host = string.IsNullOrWhiteSpace(listenAddress) ? "127.0.0.1" : listenAddress.Trim();
        return host is "*" or "+" or "0.0.0.0" or "::" or "[::]" ? "127.0.0.1" : host;
    }
}
