using TorrWind.Core.Services;

namespace TorrWind.Core.Tests;

public sealed class FileEventLogTests
{
    [Fact]
    public async Task WriteAndReadLatestAsync_RoundTripsEntriesAndLimitsFromEnd()
    {
        using var directory = TemporaryDirectory.Create();
        var logPath = Path.Combine(directory.Path, "logs", "gui.jsonl");
        var log = new FileEventLog(logPath);

        log.Info("App", "Started", "details-1");
        log.Warning("App", "Slow", "details-2");
        log.Error("App", "Failed", new InvalidOperationException("broken"), "details-3");

        var entries = await log.ReadLatestAsync(maxEntries: 2);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Warning", entries[0].Level);
        Assert.Equal("Slow", entries[0].Message);
        Assert.Equal("details-2", entries[0].Details);
        Assert.Equal(logPath, entries[0].LogFile);
        Assert.Equal("Error", entries[1].Level);
        Assert.Equal("Failed", entries[1].Message);
        Assert.Contains("broken", entries[1].Exception);
    }

    [Fact]
    public async Task ReadLatestAsync_ReturnsInvalidEntryForNonJsonLine()
    {
        using var directory = TemporaryDirectory.Create();
        var logPath = Path.Combine(directory.Path, "gui.jsonl");
        await File.WriteAllTextAsync(logPath, "plain text failure" + Environment.NewLine);
        var log = new FileEventLog(logPath);

        var entry = Assert.Single(await log.ReadLatestAsync());

        Assert.Equal("Invalid", entry.Level);
        Assert.Equal("gui.jsonl", entry.Source);
        Assert.Equal("plain text failure", entry.Message);
        Assert.Equal(logPath, entry.LogFile);
    }

    [Fact]
    public async Task ReadLatestAsync_UsesAtLeastOneEntryLimit()
    {
        using var directory = TemporaryDirectory.Create();
        var logPath = Path.Combine(directory.Path, "gui.jsonl");
        var log = new FileEventLog(logPath);
        log.Info("App", "First");
        log.Info("App", "Second");

        var entry = Assert.Single(await log.ReadLatestAsync(maxEntries: 0));

        Assert.Equal("Second", entry.Message);
    }

    [Fact]
    public async Task Clear_LeavesEmptyLogFile()
    {
        using var directory = TemporaryDirectory.Create();
        var logPath = Path.Combine(directory.Path, "gui.jsonl");
        var log = new FileEventLog(logPath);
        log.Info("App", "Started");

        log.Clear();

        Assert.True(File.Exists(logPath));
        Assert.Empty(await File.ReadAllTextAsync(logPath));
        Assert.Empty(await log.ReadLatestAsync());
    }

    [Fact]
    public async Task Write_RotatesLargeLogFileBeforeAppendingEntry()
    {
        using var directory = TemporaryDirectory.Create();
        var logPath = Path.Combine(directory.Path, "gui.jsonl");
        await File.WriteAllTextAsync(logPath, new string('x', 2 * 1024 * 1024));
        var log = new FileEventLog(logPath);

        log.Info("App", "After rotation");

        Assert.True(File.Exists(logPath + ".1"));
        Assert.Equal(2 * 1024 * 1024, new FileInfo(logPath + ".1").Length);
        var entry = Assert.Single(await log.ReadLatestAsync());
        Assert.Equal("After rotation", entry.Message);
    }

    [Fact]
    public async Task ReadLatestAsync_ReturnsEmptyWhenFileDoesNotExist()
    {
        using var directory = TemporaryDirectory.Create();
        var log = new FileEventLog(Path.Combine(directory.Path, "missing.jsonl"));

        Assert.Empty(await log.ReadLatestAsync());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "torrwind-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
