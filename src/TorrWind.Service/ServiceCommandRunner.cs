using System.Diagnostics;
using System.Globalization;
using System.Text;
using TorrWind.Core;
using TorrWind.Core.Services;

namespace TorrWind.Service;

public static class ServiceCommandRunner
{
    private const string LocalServiceAccount = @"NT AUTHORITY\LocalService";
    private const string LocalServiceSid = "*S-1-5-19";

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
            "obj=",
            LocalServiceAccount,
            "DisplayName=",
            WindowsServiceManager.DisplayName).ConfigureAwait(false);

        await RunScAsync(
                "description",
                WindowsServiceManager.ServiceName,
                "Runs the configured local TorrServer instance for TorrWind.")
            .ConfigureAwait(false);

        await GrantLocalServiceDataAccessAsync().ConfigureAwait(false);
        await GrantInteractiveServiceControlAsync().ConfigureAwait(false);
    }

    private static async Task GrantLocalServiceDataAccessAsync()
    {
        var result = await RunProcessCaptureAsync(
                "icacls.exe",
                AppPaths.DataDirectory,
                "/grant",
                LocalServiceSid + ":(OI)(CI)M",
                "/T",
                "/Q")
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw CreateCommandException("icacls.exe", result);
        }
    }

    private static async Task GrantInteractiveServiceControlAsync()
    {
        var result = await RunScCaptureAsync("sdshow", WindowsServiceManager.ServiceName).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw CreateCommandException("sc.exe", result);
        }

        var currentDescriptor = WindowsServiceSecurityDescriptor.ExtractFromScOutput(result.Output);
        var updatedDescriptor = WindowsServiceSecurityDescriptor.GrantInteractiveStartStop(currentDescriptor);
        if (!string.Equals(currentDescriptor, updatedDescriptor, StringComparison.Ordinal))
        {
            await RunScAsync("sdset", WindowsServiceManager.ServiceName, updatedDescriptor).ConfigureAwait(false);
        }
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
        var result = await RunScCaptureAsync(args).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw CreateCommandException("sc.exe", result);
        }
    }

    private static Task<CommandResult> RunScCaptureAsync(params string[] args)
    {
        return RunProcessCaptureAsync("sc.exe", args);
    }

    private static async Task<CommandResult> RunProcessCaptureAsync(string fileName, params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = GetScEncoding(),
            StandardOutputEncoding = GetScEncoding(),
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException($"Failed to start {fileName}.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);

        return new CommandResult(
            process.ExitCode,
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false));
    }

    private static InvalidOperationException CreateCommandException(string command, CommandResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
        return new InvalidOperationException(
            string.IsNullOrWhiteSpace(message)
                ? $"{command} exited with code {result.ExitCode}."
                : message.Trim());
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static Encoding GetScEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch
        {
            return Encoding.Default;
        }
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
