using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace TorrWind.Core.Services;

public sealed class WindowsServiceManager
{
    public const string ServiceName = "TorrWindService";
    public const string DisplayName = "TorrWind Service";

    public Task InstallAsync(string serviceExecutablePath, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows service management is supported only on Windows.");
        }

        return RunServiceHelperElevatedAsync(serviceExecutablePath, cancellationToken, "install");
    }

    public Task UninstallAsync(string serviceExecutablePath, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows service management is supported only on Windows.");
        }

        return RunServiceHelperElevatedAsync(serviceExecutablePath, cancellationToken, "uninstall");
    }

    public Task UninstallAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows service management is supported only on Windows.");
        }

        return RunScElevatedAsync(cancellationToken, "delete", ServiceName);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return RunScAsync(cancellationToken, "start", ServiceName);
    }

    public Task StartAsync(string serviceExecutablePath, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows service management is supported only on Windows.");
        }

        return RunServiceHelperElevatedAsync(serviceExecutablePath, cancellationToken, "start");
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return RunScAsync(cancellationToken, "stop", ServiceName);
    }

    public Task StopAsync(string serviceExecutablePath, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows service management is supported only on Windows.");
        }

        return RunServiceHelperElevatedAsync(serviceExecutablePath, cancellationToken, "stop");
    }

    public async Task<WindowsServiceStatus> QueryStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new WindowsServiceStatus(false, "Unsupported", "Windows service status is available only on Windows.");
        }

        var result = await RunScCaptureAsync(cancellationToken, "query", ServiceName).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return new WindowsServiceStatus(false, "Not installed", FirstNotEmpty(result.Error, result.Output));
        }

        return new WindowsServiceStatus(true, ParseState(result.Output), result.Output.Trim());
    }

    private static async Task RunScElevatedAsync(CancellationToken cancellationToken, params string[] args)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = string.Join(" ", args.Select(QuoteCommandLineArgument)),
            Verb = "runas",
            UseShellExecute = true
        });

        if (process is null)
        {
            throw new InvalidOperationException("Failed to start sc.exe.");
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"sc.exe exited with code {process.ExitCode}.");
        }
    }

    private static async Task RunServiceHelperElevatedAsync(
        string serviceExecutablePath,
        CancellationToken cancellationToken,
        params string[] args)
    {
        if (string.IsNullOrWhiteSpace(serviceExecutablePath) || !File.Exists(serviceExecutablePath))
        {
            throw new FileNotFoundException("TorrWind service helper was not found.", serviceExecutablePath);
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = serviceExecutablePath,
            Arguments = string.Join(" ", args.Select(QuoteCommandLineArgument)),
            Verb = "runas",
            UseShellExecute = true
        });

        if (process is null)
        {
            throw new InvalidOperationException("Failed to start TorrWind service helper.");
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"TorrWind service helper exited with code {process.ExitCode}.");
        }
    }

    private static async Task RunScAsync(CancellationToken cancellationToken, params string[] args)
    {
        var result = await RunScCaptureAsync(cancellationToken, args).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(FirstNotEmpty(result.Error, result.Output, $"sc.exe exited with code {result.ExitCode}."));
        }
    }

    private static async Task<ScResult> RunScCaptureAsync(CancellationToken cancellationToken, params string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows service management is supported only on Windows.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "sc.exe",
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
            throw new InvalidOperationException("Failed to start sc.exe.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new ScResult(
            process.ExitCode,
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false));
    }

    private static string ParseState(string output)
    {
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("STATE", StringComparison.OrdinalIgnoreCase))
            {
                var markerStart = line.IndexOf('(');
                var markerEnd = line.IndexOf(')');
                if (markerStart >= 0 && markerEnd > markerStart)
                {
                    return line[(markerStart + 1)..markerEnd];
                }

                var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
                return parts.Length == 2 ? parts[1] : line;
            }
        }

        return "Unknown";
    }

    private static string FirstNotEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private static string QuoteCommandLineArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (!value.Any(char.IsWhiteSpace) && !value.Contains('"', StringComparison.Ordinal))
        {
            return value;
        }

        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
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

    private sealed record ScResult(int ExitCode, string Output, string Error);
}

public sealed record WindowsServiceStatus(bool IsInstalled, string State, string Details);
