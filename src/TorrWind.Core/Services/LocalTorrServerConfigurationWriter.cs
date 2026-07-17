using System.Text;
using System.Text.Json;
using TorrWind.Core.Models;

namespace TorrWind.Core.Services;

public static class LocalTorrServerConfigurationWriter
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

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

        await WriteFileAtomicallyAsync(
                authPath,
                async (stream, token) =>
                {
                    await JsonSerializer.SerializeAsync(stream, accounts, JsonOptions, token)
                        .ConfigureAwait(false);
                },
                cancellationToken)
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

        var bytes = Utf8WithoutBom.GetBytes(NormalizeLines(content));
        await WriteFileAtomicallyAsync(
                filePath,
                async (stream, token) =>
                {
                    await stream.WriteAsync(bytes.AsMemory(), token).ConfigureAwait(false);
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteFileAtomicallyAsync(
        string filePath,
        Func<Stream, CancellationToken, Task> writeAsync,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(filePath);
        var tempPath = Path.Combine(
            string.IsNullOrWhiteSpace(directory) ? "." : directory,
            Path.GetFileName(filePath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await writeAsync(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
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

    private static void TryDelete(string filePath)
    {
        try
        {
            DeleteIfExists(filePath);
        }
        catch
        {
        }
    }
}
