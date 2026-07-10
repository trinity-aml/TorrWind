using System.Text.Json;
using TorrWind.Core.Models;

namespace TorrWind.Core.Services;

public static class LocalTorrServerConfigurationWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string GetDataDirectory(LocalServerSettings settings)
    {
        return string.IsNullOrWhiteSpace(settings.DataDirectory)
            ? AppPaths.DefaultLocalServerDirectory
            : settings.DataDirectory.Trim();
    }

    public static async Task WriteAsync(LocalServerSettings settings, CancellationToken cancellationToken = default)
    {
        var dataDirectory = GetDataDirectory(settings);
        Directory.CreateDirectory(dataDirectory);

        await WriteAuthAsync(settings, dataDirectory, cancellationToken).ConfigureAwait(false);
        await WriteOptionalTextFileAsync(Path.Combine(dataDirectory, "wip.txt"), settings.WhiteList, cancellationToken)
            .ConfigureAwait(false);
        await WriteOptionalTextFileAsync(Path.Combine(dataDirectory, "bip.txt"), settings.BlackList, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteAuthAsync(
        LocalServerSettings settings,
        string dataDirectory,
        CancellationToken cancellationToken)
    {
        var authPath = Path.Combine(dataDirectory, "accs.db");
        if (!settings.UseHttpAuth ||
            string.IsNullOrWhiteSpace(settings.Username) ||
            string.IsNullOrWhiteSpace(settings.Password))
        {
            DeleteIfExists(authPath);
            return;
        }

        var accounts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [settings.Username.Trim()] = settings.Password
        };

        await using var stream = File.Create(authPath);
        await JsonSerializer.SerializeAsync(stream, accounts, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteOptionalTextFileAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            DeleteIfExists(filePath);
            return;
        }

        await File.WriteAllTextAsync(filePath, NormalizeLines(content), cancellationToken)
            .ConfigureAwait(false);
    }

    private static string NormalizeLines(string content)
    {
        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
