using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TorrWind.Core;
using TorrWind.Core.Services;

namespace TorrWind.Service;

public sealed class TorrServerWorker : BackgroundService
{
    private static readonly TimeSpan ConfigurationRetryDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SupervisionErrorDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);
    private readonly ILogger<TorrServerWorker> _logger;
    private readonly FileEventLog _eventLog = FileEventLog.Service;
    private Process? _process;

    public TorrServerWorker(ILogger<TorrServerWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = await RunTorrServerAsync(stoppingToken).ConfigureAwait(false);
                if (delay > TimeSpan.Zero && !stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "TorrServer supervision cycle failed.");
                _eventLog.Error("ServiceWorker", "TorrServer supervision cycle failed.", exception);
                await Task.Delay(SupervisionErrorDelay, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        StopChildProcess();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<TimeSpan> RunTorrServerAsync(CancellationToken stoppingToken)
    {
        var settings = await new AppSettingsStore(AppPaths.ServiceSettingsFile)
            .LoadAsync(stoppingToken)
            .ConfigureAwait(false);

        var localServer = settings.LocalServer;
        if (!localServer.Enabled || string.IsNullOrWhiteSpace(localServer.ExecutablePath))
        {
            _logger.LogInformation("Local TorrServer is disabled or executable path is empty.");
            _eventLog.Warning("ServiceWorker", "Local TorrServer is disabled or executable path is empty.");
            return ConfigurationRetryDelay;
        }

        if (!File.Exists(localServer.ExecutablePath))
        {
            _logger.LogWarning("TorrServer executable was not found at {ExecutablePath}.", localServer.ExecutablePath);
            _eventLog.Warning("ServiceWorker", "TorrServer executable was not found.", localServer.ExecutablePath);
            return ConfigurationRetryDelay;
        }

        if (LocalTorrServerEndpointProbe.IsExecutableRunning(localServer.ExecutablePath))
        {
            _logger.LogInformation("Local TorrServer process is already running; process start skipped.");
            _eventLog.Info("ServiceWorker", "Local TorrServer process is already running; process start skipped.", localServer.ExecutablePath);
            return ConfigurationRetryDelay;
        }

        if (await LocalTorrServerEndpointProbe
                .IsOnlineAsync(localServer, TimeSpan.FromSeconds(2), stoppingToken)
                .ConfigureAwait(false))
        {
            _logger.LogInformation("Local TorrServer endpoint is already online; process start skipped.");
            _eventLog.Info("ServiceWorker", "Local TorrServer endpoint is already online; process start skipped.");
            return ConfigurationRetryDelay;
        }

        await LocalTorrServerConfigurationWriter.WriteAsync(localServer, stoppingToken).ConfigureAwait(false);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = localServer.ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(localServer.ExecutablePath) ?? AppContext.BaseDirectory
        };

        foreach (var argument in TorrServerArgumentBuilder.Build(localServer))
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        _logger.LogInformation("Starting TorrServer from {ExecutablePath}.", localServer.ExecutablePath);
        _eventLog.Info("ServiceWorker", "Starting TorrServer.", localServer.ExecutablePath);
        var process = Process.Start(processStartInfo);
        _process = process;

        if (process is null)
        {
            throw new InvalidOperationException("Failed to start TorrServer process.");
        }

        _ = LogProcessOutputAsync(process.StandardOutput, "TorrServer stdout", stoppingToken);
        _ = LogProcessOutputAsync(process.StandardError, "TorrServer stderr", stoppingToken);

        try
        {
            await process.WaitForExitAsync(stoppingToken).ConfigureAwait(false);
            _logger.LogWarning("TorrServer exited with code {ExitCode}.", process.ExitCode);
            _eventLog.Warning("ServiceWorker", "TorrServer exited.", process.ExitCode.ToString());
            return RestartDelay;
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

    private void StopChildProcess()
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
                _eventLog.Info("ServiceWorker", "TorrServer process already stopped.");
                return;
            }

            var closeRequested = process.CloseMainWindow();
            if (closeRequested && process.WaitForExit((int)StopTimeout.TotalMilliseconds))
            {
                _eventLog.Info("ServiceWorker", "TorrServer process stopped.");
                return;
            }

            if (!closeRequested)
            {
                _eventLog.Warning("ServiceWorker", "TorrServer process has no main window; terminating process.");
            }
            else
            {
                _eventLog.Warning("ServiceWorker", "TorrServer process did not stop before timeout; terminating process.");
            }

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit((int)StopTimeout.TotalMilliseconds);
                _eventLog.Warning("ServiceWorker", "TorrServer process killed.");
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to stop TorrServer process cleanly.");
            _eventLog.Error("ServiceWorker", "Failed to stop TorrServer process cleanly.", exception);
        }
        finally
        {
            if (ReferenceEquals(_process, process))
            {
                _process = null;
            }
        }
    }

    private async Task LogProcessOutputAsync(StreamReader reader, string source, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _eventLog.Error(source, "Failed to read TorrServer process output.", exception);
        }
    }
}
