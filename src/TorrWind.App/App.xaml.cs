using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows;
using TorrWind.App.ViewModels;
using TorrWind.Core;
using TorrWind.Core.Localization;
using TorrWind.Core.Services;

namespace TorrWind.App;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = "TorrWind-C82F63B6-1D78-4D3D-8A4E-8AE73E52685E";
    private const string SingleInstancePipeName = "TorrWind-C82F63B6-1D78-4D3D-8A4E-8AE73E52685E-Args";

    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private System.Drawing.Icon? _trayIcon;
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _viewModel;
    private Mutex? _singleInstanceMutex;
    private CancellationTokenSource? _singleInstanceCts;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            AppPaths.EnsureWorkingDirectories();

            if (!ClaimSingleInstance())
            {
                await SendStartupArgumentsToPrimaryAsync(e.Args).ConfigureAwait(true);
                Shutdown(0);
                return;
            }

            FileEventLog.User.Info("GUI", "Application startup.");

            var localization = new JsonLocalizationService(AppPaths.LocalesDirectory);
            var settingsStore = new AppSettingsStore(AppPaths.UserSettingsFile);
            _viewModel = new MainWindowViewModel(settingsStore, localization);
            await _viewModel.InitializeAsync().ConfigureAwait(true);

            _mainWindow = new MainWindow(_viewModel);
            MainWindow = _mainWindow;
            CreateTrayIcon(localization);
            StartSingleInstanceListener();

            var startMinimized = ShouldStartMinimized(e.Args);
            if (!startMinimized)
            {
                _mainWindow.Show();
            }

            await _viewModel.ProcessStartupArgumentsAsync(e.Args).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            FileEventLog.User.Error("GUI", "Application startup failed.", exception);
            System.Windows.MessageBox.Show(exception.Message, "TorrWind", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceCts?.Cancel();
        _singleInstanceCts?.Dispose();
        _notifyIcon?.Dispose();
        _trayIcon?.Dispose();
        _viewModel?.Dispose();
        ReleaseSingleInstance();
        FileEventLog.User.Info("GUI", "Application exit.");
        base.OnExit(e);
    }

    private void CreateTrayIcon(JsonLocalizationService localization)
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(localization["TrayOpen"], null, (_, _) => ShowMainWindow());
        menu.Items.Add(localization["ActionOpenWeb"], null, (_, _) => OpenSelectedServerWebUi());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(localization["ActionStartLocalServer"], null, (_, _) => ExecuteViewModelCommand(_viewModel?.StartLocalServerCommand));
        menu.Items.Add(localization["ActionStopLocalServer"], null, (_, _) => ExecuteViewModelCommand(_viewModel?.StopLocalServerCommand));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(localization["TrayExit"], null, (_, _) => ExitApplication());

        _trayIcon = LoadTrayIcon();
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "TorrWind",
            Icon = _trayIcon,
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var resource = GetResourceStream(new Uri("pack://application:,,,/TorrWind.ico"));
            if (resource?.Stream is not null)
            {
                using var stream = resource.Stream;
                return new System.Drawing.Icon(stream);
            }
        }
        catch
        {
            // The tray icon should not block application startup.
        }

        return (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
    }

    private static bool ShouldStartMinimized(IEnumerable<string> args)
    {
        return args.Any(arg =>
            string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "/minimized", StringComparison.OrdinalIgnoreCase));
    }

    private bool ClaimSingleInstance()
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (createdNew)
        {
            return true;
        }

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
        return false;
    }

    private void ReleaseSingleInstance()
    {
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // The secondary instance does not own the mutex.
        }
        finally
        {
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
        }
    }

    private void StartSingleInstanceListener()
    {
        _singleInstanceCts = new CancellationTokenSource();
        _ = Task.Run(() => ListenForSecondaryInstancesAsync(_singleInstanceCts.Token));
    }

    private async Task ListenForSecondaryInstancesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    SingleInstancePipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server);
                var payload = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var args = JsonSerializer.Deserialize<string[]>(payload) ?? [];

                Dispatcher.Invoke(() => HandleSecondaryStartup(args));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                FileEventLog.User.Error("GUI", "Single-instance listener failed.", exception);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task SendStartupArgumentsToPrimaryAsync(IEnumerable<string> args)
    {
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await using var client = new NamedPipeClientStream(
                ".",
                SingleInstancePipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);

            await client.ConnectAsync(cancellation.Token).ConfigureAwait(false);
            await JsonSerializer.SerializeAsync(client, args.ToArray(), cancellationToken: cancellation.Token).ConfigureAwait(false);
            await client.FlushAsync(cancellation.Token).ConfigureAwait(false);
            FileEventLog.User.Info("GUI", "Startup arguments sent to primary instance.");
        }
        catch (Exception exception)
        {
            FileEventLog.User.Warning("GUI", "Failed to send startup arguments to primary instance.", exception.Message);
        }
    }

    private void HandleSecondaryStartup(IReadOnlyCollection<string> args)
    {
        FileEventLog.User.Info("GUI", "Secondary instance activation received.", args.Count.ToString());
        ShowMainWindow();

        if (_viewModel is not null)
        {
            _ = _viewModel.ProcessStartupArgumentsAsync(args);
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OpenSelectedServerWebUi()
    {
        ShowMainWindow();
        _mainWindow?.OpenSelectedServerWebUi();
    }

    private static void ExecuteViewModelCommand(System.Windows.Input.ICommand? command)
    {
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }

    private void ExitApplication()
    {
        if (_mainWindow is not null)
        {
            _mainWindow.AllowClose = true;
        }

        Shutdown();
    }
}
