using System.Diagnostics;
using TorrWind.Core.Models;

namespace TorrWind.Core.Services;

public sealed class LocalTorrServerProcessManager : IDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);
    private readonly FileEventLog _eventLog = FileEventLog.User;
    private Process? _process;

    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync(LocalServerSettings settings, CancellationToken cancellationToken = default)
    {
        if (_process is { HasExited: true } exitedProcess)
        {
            exitedProcess.Dispose();
            _process = null;
        }

        if (IsRunning)
        {
            return;
        }

        if (LocalTorrServerEndpointProbe.IsExecutableRunning(settings.ExecutablePath))
        {
            _eventLog.Info("LocalServer", "Local TorrServer process is already running; process start skipped.", settings.ExecutablePath);
            return;
        }

        if (await LocalTorrServerEndpointProbe
                .IsOnlineAsync(settings, TimeSpan.FromSeconds(2), cancellationToken)
                .ConfigureAwait(false))
        {
            _eventLog.Info("LocalServer", "Local TorrServer endpoint is already online; process start skipped.");
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
        var process = _process;
        if (process is null)
        {
            return;
        }

        try
        {
            if (process.HasExited)
            {
                _eventLog.Info("LocalServer", "TorrServer process already stopped.");
                return;
            }

            var closeRequested = process.CloseMainWindow();
            if (closeRequested && process.WaitForExit((int)StopTimeout.TotalMilliseconds))
            {
                _eventLog.Info("LocalServer", "TorrServer process stopped.");
                return;
            }

            if (!closeRequested)
            {
                _eventLog.Warning("LocalServer", "TorrServer process has no main window; terminating process.");
            }
            else
            {
                _eventLog.Warning("LocalServer", "TorrServer process did not stop before timeout; terminating process.");
            }

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit((int)StopTimeout.TotalMilliseconds);
                _eventLog.Warning("LocalServer", "TorrServer process killed.");
            }
        }
        finally
        {
            if (ReferenceEquals(_process, process))
            {
                _process = null;
            }

            process.Dispose();
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
