using System.Diagnostics;
using TorrWind.Core.Models;

namespace TorrWind.Core.Services;

public sealed class LocalTorrServerProcessManager : IDisposable
{
    private readonly FileEventLog _eventLog = FileEventLog.User;
    private Process? _process;

    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync(LocalServerSettings settings, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.ExecutablePath) || !File.Exists(settings.ExecutablePath))
        {
            throw new FileNotFoundException("TorrServer executable was not found.", settings.ExecutablePath);
        }

        await LocalTorrServerConfigurationWriter.WriteAsync(settings, cancellationToken).ConfigureAwait(false);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = settings.ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(settings.ExecutablePath) ?? AppContext.BaseDirectory
        };

        foreach (var argument in TorrServerArgumentBuilder.Build(settings))
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        _process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Failed to start TorrServer.");

        _eventLog.Info("LocalServer", "TorrServer process started.", settings.ExecutablePath);
        _ = LogProcessOutputAsync(_process.StandardOutput, "TorrServer stdout");
        _ = LogProcessOutputAsync(_process.StandardError, "TorrServer stderr");
    }

    public void Stop()
    {
        if (!IsRunning || _process is null)
        {
            return;
        }

        _process.CloseMainWindow();
        if (!_process.WaitForExit(5000))
        {
            _process.Kill(entireProcessTree: true);
            _eventLog.Warning("LocalServer", "TorrServer process killed after graceful stop timeout.");
        }
        else
        {
            _eventLog.Info("LocalServer", "TorrServer process stopped.");
        }
    }

    private async Task LogProcessOutputAsync(StreamReader reader, string source)
    {
        try
        {
            while (true)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    _eventLog.Info(source, line.Trim());
                }
            }
        }
        catch (Exception exception)
        {
            _eventLog.Error(source, "Failed to read TorrServer process output.", exception);
        }
    }

    public void Dispose()
    {
        Stop();
        _process?.Dispose();
    }
}
