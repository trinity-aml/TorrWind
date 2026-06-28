namespace TorrWind.Core.Models;

public sealed class AppLogEntry
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

    public string Level { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public string Exception { get; set; } = string.Empty;

    public string LogFile { get; set; } = string.Empty;

    public string TimestampText => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
}
