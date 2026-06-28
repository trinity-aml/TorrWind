using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TorrWind.Core;
using TorrWind.Core.Services;

namespace TorrWind.Service;

public sealed class TorrServerWorker : BackgroundService
{
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
                await RunTorrServerAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "TorrServer supervision cycle failed.");
                _eventLog.Error("ServiceWorker", "TorrServer supervision cycle failed.", exception);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        StopChildProcess();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RunTorrServerAsync(CancellationToken stoppingToken)
    {
        var settings = await new AppSettingsStore(AppPaths.ServiceSettingsFile)
            .LoadAsync(stoppingToken)
            .ConfigureAwait(false);

        var localServer = settings.LocalServer;
        if (!localServer.Enabled || string.IsNullOrWhiteSpace(localServer.ExecutablePath))
        {
            _logger.LogInformation("Local TorrServer is disabled or executable path is empty.");
            _eventLog.Warning("ServiceWorker", "Local TorrServer is disabled or executable path is empty.");
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);
            return;
        }

        if (!File.Exists(localServer.ExecutablePath))
        {
            _logger.LogWarning("TorrServer executable was not found at {ExecutablePath}.", localServer.ExecutablePath);
            _eventLog.Warning("ServiceWorker", "TorrServer executable was not found.", localServer.ExecutablePath);
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);
            return;
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
        _process = Process.Start(processStartInfo);

        if (_process is null)
        {
            throw new InvalidOperationException("Failed to start TorrServer process.");
        }

        _ = LogProcessOutputAsync(_process.StandardOutput, "TorrServer stdout", stoppingToken);
        _ = LogProcessOutputAsync(_process.StandardError, "TorrServer stderr", stoppingToken);

        await _process.WaitForExitAsync(stoppingToken).ConfigureAwait(false);
        _logger.LogWarning("TorrServer exited with code {ExitCode}.", _process.ExitCode);
        _eventLog.Warning("ServiceWorker", "TorrServer exited.", _process.ExitCode.ToString());
    }

    private void StopChildProcess()
    {
        if (_process is null || _process.HasExited)
        {
            return;
        }

        try
        {
            _process.CloseMainWindow();
            if (!_process.WaitForExit(5000))
            {
                _process.Kill(entireProcessTree: true);
                _eventLog.Warning("ServiceWorker", "TorrServer process killed after graceful stop timeout.");
            }
            else
            {
                _eventLog.Info("ServiceWorker", "TorrServer process stopped.");
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to stop TorrServer process cleanly.");
            _eventLog.Error("ServiceWorker", "Failed to stop TorrServer process cleanly.", exception);
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
