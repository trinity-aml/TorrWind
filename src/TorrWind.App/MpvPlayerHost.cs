using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using TorrWind.Core;
using TorrWind.Core.Models;
using TorrWind.Core.Services;

namespace TorrWind.App;

internal sealed class MpvPlayerHost : IDisposable
{
    private const int ConnectTimeoutMs = 7000;
    private const int CommandTimeoutMs = 5000;

    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonObject?>> _pendingRequests = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _logLock = new(1, 1);
    private readonly string _pipeName = "torrwind-mpv-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N");
    private Process? _process;
    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _readerCancellation;
    private bool _disposed;
    private long _nextRequestId;

    public event EventHandler? EndReached;

    public event EventHandler? Exited;

    public bool IsStarted => _process is { HasExited: false } && _pipe?.IsConnected == true;

    public async Task StartAsync(IntPtr videoWindowHandle, ServerProfile? server, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (videoWindowHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Video window handle is not available.");
        }

        var executablePath = ResolveMpvExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new FileNotFoundException(
                "mpv.exe was not found. Put mpv.exe into the application folder, Runtime\\mpv, mpv, tools\\mpv, or add it to PATH.");
        }

        Directory.CreateDirectory(AppPaths.UserLogsDirectory);
        StartProcess(executablePath, videoWindowHandle, server);
        await ConnectIpcAsync(cancellationToken).ConfigureAwait(false);
        _readerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => ReadLoopAsync(_readerCancellation.Token), CancellationToken.None);

        await SendCommandAsync(cancellationToken, "get_property", "mpv-version").ConfigureAwait(false);
        FileEventLog.User.Info("Player", "Built-in mpv player started.", executablePath);
    }

    public Task LoadFileAsync(Uri mediaUri, CancellationToken cancellationToken)
    {
        return SendCommandAsync(cancellationToken, "loadfile", mediaUri.ToString(), "replace");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return SendCommandIgnoringErrorsAsync(cancellationToken, "stop");
    }

    public Task SetPauseAsync(bool paused, CancellationToken cancellationToken)
    {
        return SetPropertyAsync("pause", paused, cancellationToken);
    }

    public Task SetVolumeAsync(double volume, CancellationToken cancellationToken)
    {
        return SetPropertyAsync("volume", Math.Clamp(volume, 0, 150), cancellationToken);
    }

    public Task SeekAbsoluteAsync(long targetMs, CancellationToken cancellationToken)
    {
        var seconds = Math.Max(0, targetMs) / 1000d;
        return SendCommandAsync(cancellationToken, "seek", seconds, "absolute", "exact");
    }

    public Task SetTrackAsync(string propertyName, int trackId, CancellationToken cancellationToken)
    {
        object value = trackId < 0 ? "no" : trackId;
        return SetPropertyAsync(propertyName, value, cancellationToken);
    }

    public Task SetAspectRatioAsync(string value, CancellationToken cancellationToken)
    {
        return SetPropertyAsync(
            "video-aspect-override",
            string.IsNullOrWhiteSpace(value) ? "no" : value,
            cancellationToken);
    }

    public Task SetAudioDelayAsync(double seconds, CancellationToken cancellationToken)
    {
        return SetPropertyAsync("audio-delay", seconds, cancellationToken);
    }

    public Task SetSubtitleDelayAsync(double seconds, CancellationToken cancellationToken)
    {
        return SetPropertyAsync("sub-delay", seconds, cancellationToken);
    }

    public async Task<MpvPlaybackState> GetStateAsync(CancellationToken cancellationToken)
    {
        var time = await GetDoublePropertyAsync("time-pos", cancellationToken).ConfigureAwait(false);
        var duration = await GetDoublePropertyAsync("duration", cancellationToken).ConfigureAwait(false);
        var paused = await GetBooleanPropertyAsync("pause", defaultValue: true, cancellationToken).ConfigureAwait(false);
        var idle = await GetBooleanPropertyAsync("idle-active", defaultValue: true, cancellationToken).ConfigureAwait(false);
        var eof = await GetBooleanPropertyAsync("eof-reached", defaultValue: false, cancellationToken).ConfigureAwait(false);

        return new MpvPlaybackState(
            SecondsToMilliseconds(time),
            SecondsToMilliseconds(duration),
            paused,
            idle,
            eof);
    }

    public async Task<IReadOnlyList<MpvTrack>> GetTracksAsync(CancellationToken cancellationToken)
    {
        var node = await GetPropertyAsync("track-list", cancellationToken).ConfigureAwait(false);
        if (node is not JsonArray tracks)
        {
            return [];
        }

        var result = new List<MpvTrack>();
        foreach (var trackNode in tracks.OfType<JsonObject>())
        {
            var id = GetInt(trackNode["id"]);
            var type = GetString(trackNode["type"]);
            if (id is null || string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            result.Add(new MpvTrack(
                id.Value,
                type,
                BuildTrackName(id.Value, trackNode),
                GetBoolean(trackNode["selected"])));
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var pending in _pendingRequests)
        {
            pending.Value.TrySetCanceled();
        }

        _pendingRequests.Clear();

        try
        {
            _readerCancellation?.Cancel();
        }
        catch
        {
        }

        try
        {
            if (_process is { HasExited: false })
            {
                _ = SendCommandIgnoringErrorsAsync(CancellationToken.None, "quit");
                if (!_process.WaitForExit(1000))
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
        }
        catch (Exception exception)
        {
            FileEventLog.User.Warning("Player", "Failed to stop mpv cleanly.", exception.Message);
        }

        _readerCancellation?.Dispose();
        _reader?.Dispose();
        _writer?.Dispose();
        _pipe?.Dispose();
        _process?.Dispose();
        _writeLock.Dispose();
        _logLock.Dispose();
    }

    private void StartProcess(string executablePath, IntPtr videoWindowHandle, ServerProfile? server)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            },
            EnableRaisingEvents = true
        };

        var arguments = process.StartInfo.ArgumentList;
        arguments.Add("--no-config");
        arguments.Add("--idle=yes");
        arguments.Add("--force-window=yes");
        arguments.Add("--keep-open=no");
        arguments.Add("--terminal=no");
        arguments.Add("--input-terminal=no");
        arguments.Add("--input-default-bindings=no");
        arguments.Add("--input-vo-keyboard=no");
        arguments.Add("--osc=no");
        arguments.Add("--osd-bar=no");
        arguments.Add("--volume-max=150");
        arguments.Add("--cache=yes");
        arguments.Add("--demuxer-readahead-secs=30");
        arguments.Add("--demuxer-max-bytes=256MiB");
        arguments.Add("--log-file=" + AppPaths.MpvPlayerLogFile);
        arguments.Add("--msg-level=all=info");
        arguments.Add("--wid=" + videoWindowHandle.ToInt64().ToString(CultureInfo.InvariantCulture));
        arguments.Add(@"--input-ipc-server=\\.\pipe\" + _pipeName);

        if (server?.IgnoreCertificateErrors == true)
        {
            arguments.Add("--tls-verify=no");
        }

        if (server is not null && !string.IsNullOrWhiteSpace(server.Username))
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(server.Username + ":" + (server.Password ?? string.Empty)));
            arguments.Add("--http-header-fields=Authorization: Basic " + auth);
        }

        process.Exited += (_, _) => Exited?.Invoke(this, EventArgs.Empty);

        if (!process.Start())
        {
            throw new InvalidOperationException("mpv.exe did not start.");
        }

        _process = process;
        _ = Task.Run(() => CopyProcessOutputAsync(process.StandardOutput, "stdout", CancellationToken.None), CancellationToken.None);
        _ = Task.Run(() => CopyProcessOutputAsync(process.StandardError, "stderr", CancellationToken.None), CancellationToken.None);
    }

    private async Task ConnectIpcAsync(CancellationToken cancellationToken)
    {
        if (_process is null)
        {
            throw new InvalidOperationException("mpv process is not started.");
        }

        var startedAt = Environment.TickCount64;
        Exception? lastException = null;

        while (Environment.TickCount64 - startedAt < ConnectTimeoutMs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_process.HasExited)
            {
                throw new InvalidOperationException("mpv.exe exited before IPC became available.");
            }

            var pipe = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.ConnectAsync(250, cancellationToken).ConfigureAwait(false);
                _pipe = pipe;
                _reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                _writer = new StreamWriter(pipe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
                {
                    AutoFlush = true,
                    NewLine = "\n"
                };
                return;
            }
            catch (TimeoutException exception)
            {
                lastException = exception;
                pipe.Dispose();
            }
            catch (IOException exception)
            {
                lastException = exception;
                pipe.Dispose();
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new TimeoutException("Timed out while connecting to mpv IPC.", lastException);
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _reader is not null)
            {
                var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                HandleIpcLine(line);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (!_disposed)
            {
                FileEventLog.User.Warning("Player", "mpv IPC reader stopped.", exception.Message);
            }
        }
        finally
        {
            foreach (var pending in _pendingRequests)
            {
                pending.Value.TrySetCanceled();
            }

            _pendingRequests.Clear();
        }
    }

    private void HandleIpcLine(string line)
    {
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(line) as JsonObject;
        }
        catch (Exception exception)
        {
            FileEventLog.User.Warning("Player", "Failed to parse mpv IPC message.", exception.Message);
            return;
        }

        if (root is null)
        {
            return;
        }

        if (TryGetRequestId(root, out var requestId) &&
            _pendingRequests.TryRemove(requestId, out var pendingRequest))
        {
            var error = GetString(root["error"]);
            if (string.Equals(error, "success", StringComparison.OrdinalIgnoreCase))
            {
                pendingRequest.TrySetResult(root);
            }
            else
            {
                pendingRequest.TrySetException(new InvalidOperationException(error ?? "mpv command failed."));
            }

            return;
        }

        var eventName = GetString(root["event"]);
        if (string.Equals(eventName, "end-file", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetString(root["reason"]), "eof", StringComparison.OrdinalIgnoreCase))
        {
            EndReached?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<JsonObject?> SendCommandAsync(CancellationToken cancellationToken, params object?[] command)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_writer is null)
        {
            throw new InvalidOperationException("mpv IPC is not connected.");
        }

        var requestId = Interlocked.Increment(ref _nextRequestId);
        var request = new JsonObject
        {
            ["command"] = BuildJsonArray(command),
            ["request_id"] = requestId
        };
        var requestText = request.ToJsonString();
        var completion = new TaskCompletionSource<JsonObject?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = completion;

        try
        {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _writer.WriteLineAsync(requestText).ConfigureAwait(false);
                await _writer.FlushAsync().ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }

            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(CommandTimeoutMs);
            return await completion.Task.WaitAsync(timeoutCancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    private async Task SendCommandIgnoringErrorsAsync(CancellationToken cancellationToken, params object?[] command)
    {
        try
        {
            await SendCommandAsync(cancellationToken, command).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private async Task SetPropertyAsync(string propertyName, object value, CancellationToken cancellationToken)
    {
        await SendCommandAsync(cancellationToken, "set_property", propertyName, value).ConfigureAwait(false);
    }

    private async Task<JsonNode?> GetPropertyAsync(string propertyName, CancellationToken cancellationToken)
    {
        try
        {
            var response = await SendCommandAsync(cancellationToken, "get_property", propertyName).ConfigureAwait(false);
            return response?["data"];
        }
        catch
        {
            return null;
        }
    }

    private async Task<double> GetDoublePropertyAsync(string propertyName, CancellationToken cancellationToken)
    {
        var node = await GetPropertyAsync(propertyName, cancellationToken).ConfigureAwait(false);
        return GetDouble(node) ?? 0;
    }

    private async Task<bool> GetBooleanPropertyAsync(string propertyName, bool defaultValue, CancellationToken cancellationToken)
    {
        var node = await GetPropertyAsync(propertyName, cancellationToken).ConfigureAwait(false);
        return node is null ? defaultValue : GetBoolean(node);
    }

    private async Task CopyProcessOutputAsync(StreamReader reader, string streamName, CancellationToken cancellationToken)
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

                await AppendLogLineAsync(streamName + ": " + line, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
        }
    }

    private async Task AppendLogLineAsync(string line, CancellationToken cancellationToken)
    {
        await _logLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(
                AppPaths.MpvPlayerLogFile,
                DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture) + " " + line + Environment.NewLine,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _logLock.Release();
        }
    }

    private static JsonArray BuildJsonArray(IEnumerable<object?> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(ToJsonNode(value));
        }

        return array;
    }

    private static JsonNode? ToJsonNode(object? value)
    {
        return value switch
        {
            null => null,
            string stringValue => JsonValue.Create(stringValue),
            bool boolValue => JsonValue.Create(boolValue),
            int intValue => JsonValue.Create(intValue),
            long longValue => JsonValue.Create(longValue),
            double doubleValue => JsonValue.Create(doubleValue),
            float floatValue => JsonValue.Create(floatValue),
            _ => JsonValue.Create(value.ToString())
        };
    }

    private static string ResolveMpvExecutablePath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "mpv.exe"),
            Path.Combine(baseDirectory, "mpv", "mpv.exe"),
            Path.Combine(baseDirectory, "Runtime", "mpv", "mpv.exe"),
            Path.Combine(baseDirectory, "tools", "mpv", "mpv.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), "mpv.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static string BuildTrackName(int id, JsonObject track)
    {
        var parts = new List<string>();
        AddIfPresent(parts, GetString(track["title"]));
        AddIfPresent(parts, GetString(track["lang"]));
        AddIfPresent(parts, GetString(track["codec"]));

        var details = parts.Count == 0
            ? GetString(track["type"]) ?? "track"
            : string.Join(" / ", parts);
        return id.ToString(CultureInfo.InvariantCulture) + ": " + details;
    }

    private static void AddIfPresent(List<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add(value);
        }
    }

    private static long SecondsToMilliseconds(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0)
        {
            return 0;
        }

        return (long)Math.Round(seconds * 1000);
    }

    private static bool TryGetRequestId(JsonObject root, out long requestId)
    {
        requestId = 0;
        try
        {
            var node = root["request_id"];
            if (node is null)
            {
                return false;
            }

            requestId = node.GetValue<long>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetString(JsonNode? node)
    {
        try
        {
            return node?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private static int? GetInt(JsonNode? node)
    {
        try
        {
            return node?.GetValue<int>();
        }
        catch
        {
            return null;
        }
    }

    private static double? GetDouble(JsonNode? node)
    {
        try
        {
            return node?.GetValue<double>();
        }
        catch
        {
            return null;
        }
    }

    private static bool GetBoolean(JsonNode? node)
    {
        try
        {
            return node?.GetValue<bool>() == true;
        }
        catch
        {
            return false;
        }
    }
}

internal sealed record MpvPlaybackState(
    long TimeMs,
    long DurationMs,
    bool Paused,
    bool IdleActive,
    bool EofReached);

internal sealed record MpvTrack(
    int Id,
    string Type,
    string Name,
    bool Selected);
