using System.Diagnostics;
using TorrWind.Core.Services;

namespace TorrWind.Service;

public static class ServiceCommandRunner
{
    public static async Task<bool> TryRunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return false;
        }

        var command = Normalize(args[0]);
        if (command is null)
        {
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Windows service commands are supported only on Windows.");
            Environment.ExitCode = 2;
            return true;
        }

        try
        {
            FileEventLog.Service.Info("ServiceCommand", "Service command requested.", command);
            switch (command)
            {
                case "install":
                    await InstallAsync().ConfigureAwait(false);
                    FileEventLog.Service.Info("ServiceCommand", "Service installed.");
                    break;
                case "uninstall":
                    if (!await ServiceExistsAsync().ConfigureAwait(false))
                    {
                        FileEventLog.Service.Info("ServiceCommand", "Uninstall skipped; service is not installed.");
                        break;
                    }

                    await StopIfInstalledAsync().ConfigureAwait(false);
                    await RunScAsync("delete", WindowsServiceManager.ServiceName).ConfigureAwait(false);
                    FileEventLog.Service.Info("ServiceCommand", "Service uninstalled.");
                    break;
                case "start":
                    await RunScAsync("start", WindowsServiceManager.ServiceName).ConfigureAwait(false);
                    FileEventLog.Service.Info("ServiceCommand", "Service start requested.");
                    break;
                case "stop":
                    await RunScAsync("stop", WindowsServiceManager.ServiceName).ConfigureAwait(false);
                    FileEventLog.Service.Info("ServiceCommand", "Service stop requested.");
                    break;
            }
        }
        catch (Exception exception)
        {
            FileEventLog.Service.Error("ServiceCommand", "Service command failed.", exception, command);
            Console.Error.WriteLine(exception.Message);
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static string? Normalize(string command)
    {
        command = command.Trim().TrimStart('-', '/').ToLowerInvariant();
        return command is "install" or "uninstall" or "start" or "stop" ? command : null;
    }

    private static async Task InstallAsync()
    {
        var executable = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(executable))
        {
            throw new InvalidOperationException("Cannot resolve TorrWind.Service executable path.");
        }

        var command = await ServiceExistsAsync().ConfigureAwait(false) ? "config" : "create";
        await RunScAsync(
            command,
            WindowsServiceManager.ServiceName,
            "binPath=",
            Quote(executable),
            "start=",
            "auto",
            "DisplayName=",
            WindowsServiceManager.DisplayName).ConfigureAwait(false);

        await RunScAsync(
                "description",
                WindowsServiceManager.ServiceName,
                "Runs the configured local TorrServer instance for TorrWind.")
            .ConfigureAwait(false);
    }

    private static async Task StopIfInstalledAsync()
    {
        try
        {
            await RunScAsync("stop", WindowsServiceManager.ServiceName).ConfigureAwait(false);
        }
        catch
        {
            // The service can be missing or already stopped during uninstall.
        }
    }

    private static async Task<bool> ServiceExistsAsync()
    {
        try
        {
            await RunScAsync("query", WindowsServiceManager.ServiceName).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RunScAsync(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "sc.exe",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException("Failed to start sc.exe.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

}
