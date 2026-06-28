using System.Windows;
using TorrWind.App.ViewModels;
using TorrWind.Core;
using TorrWind.Core.Localization;
using TorrWind.Core.Services;

namespace TorrWind.App;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _viewModel;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            AppPaths.EnsureUserDirectories();
            FileEventLog.User.Info("GUI", "Application startup.");

            var localization = new JsonLocalizationService(AppPaths.LocalesDirectory);
            var settingsStore = new AppSettingsStore(AppPaths.UserSettingsFile);
            _viewModel = new MainWindowViewModel(settingsStore, localization);
            await _viewModel.InitializeAsync().ConfigureAwait(true);

            _mainWindow = new MainWindow(_viewModel);
            MainWindow = _mainWindow;
            CreateTrayIcon(localization);

            if (!ShouldStartMinimized(e.Args))
            {
                _mainWindow.Show();
            }
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
        _notifyIcon?.Dispose();
        _viewModel?.Dispose();
        FileEventLog.User.Info("GUI", "Application exit.");
        base.OnExit(e);
    }

    private void CreateTrayIcon(JsonLocalizationService localization)
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(localization["TrayOpen"], null, (_, _) => ShowMainWindow());
        menu.Items.Add(localization["TrayExit"], null, (_, _) => ExitApplication());

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "TorrWind",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private static bool ShouldStartMinimized(IEnumerable<string> args)
    {
        return args.Any(arg =>
            string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "/minimized", StringComparison.OrdinalIgnoreCase));
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

    private void ExitApplication()
    {
        if (_mainWindow is not null)
        {
            _mainWindow.AllowClose = true;
        }

        Shutdown();
    }
}
