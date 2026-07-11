using System.Text.Json;
using TorrWind.Core.Models;

namespace TorrWind.Core.Services;

public sealed class FileEventLog
{
    private const long MaxLogBytes = 2 * 1024 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly object _sync = new();

    public FileEventLog(string logFilePath)
    {
        LogFilePath = logFilePath;
    }

    public string LogFilePath { get; }

    public static FileEventLog User => new(AppPaths.UserLogFile);

    public static FileEventLog Service => new(AppPaths.ServiceLogFile);

    public void Info(string source, string message, string details = "")
    {
        Write("Info", source, message, details);
    }

    public void Warning(string source, string message, string details = "")
    {
        Write("Warning", source, message, details);
    }

    public void Error(string source, string message, Exception? exception = null, string details = "")
    {
        Write("Error", source, message, details, exception);
    }

    public async Task<IReadOnlyList<AppLogEntry>> ReadLatestAsync(int maxEntries = 300, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(LogFilePath))
        {
            return [];
        }

        string[] lines;
        try
        {
            lines = await ReadAllLinesSharedAsync(LogFilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return lines
            .AsEnumerable()
            .Reverse()
            .Take(Math.Max(1, maxEntries))
            .Select(Parse)
            .Where(entry => entry is not null)
            .Cast<AppLogEntry>()
            .Reverse()
            .ToArray();
    }

    public void Clear()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath) ?? AppContext.BaseDirectory);
            File.WriteAllText(LogFilePath, string.Empty);
        }
        catch
        {
            // Logging must never interrupt the primary application workflow.
        }
    }

    private void Write(string level, string source, string message, string details = "", Exception? exception = null)
    {
        try
        {
            lock (_sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath) ?? AppContext.BaseDirectory);
                RotateIfNeeded();

                var entry = new AppLogEntry
                {
                    Timestamp = DateTimeOffset.Now,
                    Level = level,
                    Source = source,
                    Message = message,
                    Details = details,
                    Exception = exception?.ToString() ?? string.Empty,
                    LogFile = LogFilePath
                };

                File.AppendAllText(LogFilePath, JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never interrupt the primary application workflow.
        }
    }

    private void RotateIfNeeded()
    {
        var fileInfo = new FileInfo(LogFilePath);
        if (!fileInfo.Exists || fileInfo.Length < MaxLogBytes)
        {
            return;
        }

        var archivePath = LogFilePath + ".1";
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        File.Move(LogFilePath, archivePath);
    }

    private static async Task<string[]> ReadAllLinesSharedAsync(string filePath, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 16 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lines.Add(line);
        }

        return lines.ToArray();
    }

    private AppLogEntry? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            var entry = JsonSerializer.Deserialize<AppLogEntry>(line, SerializerOptions);
            if (entry is not null && string.IsNullOrWhiteSpace(entry.LogFile))
            {
                entry.LogFile = LogFilePath;
            }

            NormalizeEntry(entry);
            return entry;
        }
        catch
        {
            return new AppLogEntry
            {
                Timestamp = DateTimeOffset.MinValue,
                Level = "Invalid",
                Source = Path.GetFileName(LogFilePath),
                Message = line,
                LogFile = LogFilePath
            };
        }
    }

    private static void NormalizeEntry(AppLogEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        entry.Level ??= string.Empty;
        entry.Source ??= string.Empty;
        entry.Message ??= string.Empty;
        entry.Details ??= string.Empty;
        entry.Exception ??= string.Empty;
        entry.LogFile ??= string.Empty;
    }
}
