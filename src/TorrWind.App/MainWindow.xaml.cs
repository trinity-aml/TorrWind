using System.ComponentModel;
using System.IO;
using System.Windows;
using TorrWind.App.ViewModels;

namespace TorrWind.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    public bool AllowClose { get; set; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshAsync().ConfigureAwait(true);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (AllowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private async void OnAddTorrentFile(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Torrent files (*.torrent)|*.torrent|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.AddTorrentFileAsync(dialog.FileName).ConfigureAwait(true);
        }
    }

    private async void OnExportSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".json",
            FileName = "TorrWind-settings.json",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.ExportSettingsAsync(dialog.FileName).ConfigureAwait(true);
        }
    }

    private async void OnImportSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            var result = System.Windows.MessageBox.Show(
                this,
                _viewModel.L["ConfirmImportSettings"],
                _viewModel.L["ConfirmImportSettingsTitle"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                _viewModel.StatusMessage = _viewModel.L["StatusSettingsImportCancelled"];
                return;
            }

            await _viewModel.ImportSettingsAsync(dialog.FileName).ConfigureAwait(true);
        }
    }

    private async void OnRestoreSettingsBackup(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_viewModel.SettingsBackupsDirectory);
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = _viewModel.SettingsBackupsDirectory,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            var result = System.Windows.MessageBox.Show(
                this,
                _viewModel.L["ConfirmRestoreSettingsBackup"],
                _viewModel.L["ConfirmRestoreSettingsBackupTitle"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                _viewModel.StatusMessage = _viewModel.L["StatusSettingsRestoreCancelled"];
                return;
            }

            await _viewModel.RestoreSettingsBackupAsync(dialog.FileName).ConfigureAwait(true);
        }
    }

    private void OnBrowseTorrServerExecutable(object sender, RoutedEventArgs e)
    {
        var path = BrowseFile(
            "TorrServer executable (TorrServer*.exe)|TorrServer*.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            _viewModel.LocalServer.ExecutablePath);

        if (path is not null)
        {
            _viewModel.SetLocalServerExecutablePath(path);
        }
    }

    private void OnBrowseDataDirectory(object sender, RoutedEventArgs e)
    {
        var path = BrowseFolder(_viewModel.LocalServer.DataDirectory, "Select TorrServer data folder");
        if (path is not null)
        {
            _viewModel.SetLocalServerDataDirectory(path);
        }
    }

    private void OnBrowseCacheDirectory(object sender, RoutedEventArgs e)
    {
        var path = BrowseFolder(_viewModel.LocalServer.TemporaryDataPath, "Select TorrServer cache folder");
        if (path is not null)
        {
            _viewModel.SetLocalServerTemporaryDataPath(path);
        }
    }

    private void OnBrowseSslCertificate(object sender, RoutedEventArgs e)
    {
        var path = BrowseFile(
            "Certificate files (*.crt;*.cer;*.pem)|*.crt;*.cer;*.pem|All files (*.*)|*.*",
            _viewModel.LocalServer.CertificatePath);

        if (path is not null)
        {
            _viewModel.SetLocalServerCertificatePath(path);
        }
    }

    private void OnBrowseSslKey(object sender, RoutedEventArgs e)
    {
        var path = BrowseFile(
            "Key files (*.key;*.pem)|*.key;*.pem|All files (*.*)|*.*",
            _viewModel.LocalServer.CertificateKeyPath);

        if (path is not null)
        {
            _viewModel.SetLocalServerCertificateKeyPath(path);
        }
    }

    private void OnBrowseCustomPlayer(object sender, RoutedEventArgs e)
    {
        var path = BrowseFile(
            "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            _viewModel.Player.CustomPlayerPath);

        if (path is not null)
        {
            _viewModel.SetCustomPlayerPath(path);
        }
    }

    private void OnOpenWebUi(object sender, RoutedEventArgs e)
    {
        OpenSelectedServerWebUi();
    }

    public void OpenSelectedServerWebUi()
    {
        var uri = _viewModel.SelectedServer?.BaseUri;
        if (uri is null)
        {
            return;
        }

        RootTabs.SelectedItem = WebUiTab;
        ServerWebBrowser.Navigate(uri);
    }

    private string? BrowseFile(string filter, string currentPath)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = filter,
            Multiselect = false
        };

        var initialDirectory = ResolveExistingDirectory(currentPath);
        if (initialDirectory is not null)
        {
            dialog.InitialDirectory = initialDirectory;
        }

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            dialog.FileName = Path.GetFileName(currentPath);
        }

        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private string? BrowseFolder(string currentPath, string description)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = description,
            ShowNewFolderButton = true
        };

        var initialDirectory = ResolveExistingDirectory(currentPath);
        if (initialDirectory is not null)
        {
            dialog.SelectedPath = initialDirectory;
        }

        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    private static string? ResolveExistingDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Directory.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path);
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
            ? directory
            : null;
    }
}
