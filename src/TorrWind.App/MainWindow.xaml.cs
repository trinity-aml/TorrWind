using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using TorrWind.App.ViewModels;

namespace TorrWind.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly DispatcherTimer _liveRefreshTimer;
    private bool _webViewEventsAttached;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _liveRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _liveRefreshTimer.Tick += OnLiveRefreshTick;
    }

    public bool AllowClose { get; set; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshAsync().ConfigureAwait(true);
        _liveRefreshTimer.Start();
    }

    private async void OnRootTabsSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, RootTabs) || !ReferenceEquals(RootTabs.SelectedItem, LibraryTab))
        {
            return;
        }

        await _viewModel.RefreshAsync().ConfigureAwait(true);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (AllowClose)
        {
            _liveRefreshTimer.Stop();
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private async void OnLiveRefreshTick(object? sender, EventArgs e)
    {
        if (!IsVisible || !ReferenceEquals(RootTabs.SelectedItem, LibraryTab))
        {
            return;
        }

        await _viewModel.RefreshSelectedTorrentLiveAsync().ConfigureAwait(true);
    }

    private void OnNavigateLibrary(object sender, RoutedEventArgs e)
    {
        RootTabs.SelectedItem = LibraryTab;
    }

    private void OnNavigateSearch(object sender, RoutedEventArgs e)
    {
        RootTabs.SelectedItem = SearchTab;
    }

    private void OnNavigateDiagnostics(object sender, RoutedEventArgs e)
    {
        RootTabs.SelectedItem = DiagnosticsTab;
    }

    private void OnNavigateSettings(object sender, RoutedEventArgs e)
    {
        RootTabs.SelectedItem = SettingsTab;
    }

    private void OnExitApplication(object sender, RoutedEventArgs e)
    {
        if (!ShowExitConfirmationDialog())
        {
            Hide();
            return;
        }

        AllowClose = true;
        System.Windows.Application.Current.Shutdown();
    }

    private void OnShowAbout(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Owner = this,
            Title = _viewModel.L["AboutTitle"],
            Width = 520,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };
        dialog.SetResourceReference(BackgroundProperty, "SurfaceBrush");

        var root = new Grid
        {
            Margin = new Thickness(20)
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock
        {
            Text = _viewModel.AppDisplayName,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 16)
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

        var fields = new Grid
        {
            Margin = new Thickness(0, 0, 0, 18)
        };
        fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(fields, 1);

        AddAboutField(fields, 0, _viewModel.L["AboutVersion"], _viewModel.AppVersion);
        AddAboutField(fields, 1, _viewModel.L["AboutRepository"], _viewModel.AppRepositoryUrl);
        AddAboutField(fields, 2, _viewModel.L["AboutLicense"], _viewModel.AppLicenseName);
        AddAboutField(fields, 3, _viewModel.L["AboutReadme"], _viewModel.AppReadmeUrl);

        var buttons = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        Grid.SetRow(buttons, 2);

        var githubButton = new System.Windows.Controls.Button
        {
            Content = _viewModel.L["AboutOpenRepository"],
            MinWidth = 120,
            Margin = new Thickness(0, 0, 8, 0)
        };
        githubButton.Click += (_, _) => OpenAboutLink(_viewModel.AppRepositoryUrl);

        var readmeButton = new System.Windows.Controls.Button
        {
            Content = _viewModel.L["AboutOpenReadme"],
            MinWidth = 120,
            Margin = new Thickness(0, 0, 8, 0)
        };
        readmeButton.Click += (_, _) => OpenAboutLink(_viewModel.AppReadmeUrl);

        var closeButton = new System.Windows.Controls.Button
        {
            Content = _viewModel.L["ActionClose"],
            IsCancel = true,
            IsDefault = true,
            MinWidth = 105,
            Style = TryFindResource("PrimaryButton") as Style
        };
        closeButton.Click += (_, _) => dialog.Close();

        buttons.Children.Add(githubButton);
        buttons.Children.Add(readmeButton);
        buttons.Children.Add(closeButton);

        root.Children.Add(title);
        root.Children.Add(fields);
        root.Children.Add(buttons);
        dialog.Content = root;
        dialog.ShowDialog();
    }

    private static void AddAboutField(Grid grid, int row, string label, string value)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var labelText = new TextBlock
        {
            Text = label,
            Margin = new Thickness(0, 0, 12, 10),
            VerticalAlignment = VerticalAlignment.Top
        };
        labelText.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        Grid.SetRow(labelText, row);
        Grid.SetColumn(labelText, 0);

        var valueText = new TextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        };
        valueText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        Grid.SetRow(valueText, row);
        Grid.SetColumn(valueText, 1);

        grid.Children.Add(labelText);
        grid.Children.Add(valueText);
    }

    private void OpenAboutLink(string url)
    {
        try
        {
            OpenExternalUri(new Uri(url));
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = exception.Message;
        }
    }

    private bool ShowExitConfirmationDialog()
    {
        var exitRequested = false;
        var dialog = new Window
        {
            Owner = this,
            Title = _viewModel.L["ConfirmExitApplicationTitle"],
            Width = 390,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };
        dialog.SetResourceReference(BackgroundProperty, "SurfaceBrush");

        var root = new Grid
        {
            Margin = new Thickness(18)
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var message = new TextBlock
        {
            Text = _viewModel.L["ConfirmExitApplication"],
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 18)
        };
        message.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

        var buttons = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        Grid.SetRow(buttons, 1);

        var exitButton = new System.Windows.Controls.Button
        {
            Content = _viewModel.L["ActionExitApplication"],
            MinWidth = 105,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var minimizeButton = new System.Windows.Controls.Button
        {
            Content = _viewModel.L["ActionMinimizeToTray"],
            IsCancel = true,
            IsDefault = true,
            MinWidth = 105,
            Style = TryFindResource("PrimaryButton") as Style
        };

        exitButton.Click += (_, _) =>
        {
            exitRequested = true;
            dialog.DialogResult = true;
        };
        minimizeButton.Click += (_, _) =>
        {
            exitRequested = false;
            dialog.DialogResult = false;
        };

        buttons.Children.Add(exitButton);
        buttons.Children.Add(minimizeButton);
        root.Children.Add(message);
        root.Children.Add(buttons);
        dialog.Content = root;

        dialog.ShowDialog();
        return exitRequested;
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

    private async void OnSaveDiagnostics(object sender, RoutedEventArgs e)
    {
        if (_viewModel.DiagnosticItems.Count == 0)
        {
            _viewModel.StatusMessage = _viewModel.L["StatusNoDiagnostics"];
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".txt",
            FileName = _viewModel.CreateDiagnosticsFileName(),
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.SaveDiagnosticsAsync(dialog.FileName).ConfigureAwait(true);
        }
    }

    private async void OnSaveSupportBundle(object sender, RoutedEventArgs e)
    {
        if (_viewModel.DiagnosticItems.Count == 0)
        {
            _viewModel.StatusMessage = _viewModel.L["StatusNoDiagnostics"];
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".zip",
            FileName = _viewModel.CreateSupportBundleFileName(),
            Filter = "Zip archives (*.zip)|*.zip|All files (*.*)|*.*",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.SaveSupportBundleAsync(dialog.FileName).ConfigureAwait(true);
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

    public async void OpenSelectedServerWebUi()
    {
        var uri = _viewModel.SelectedServer?.BaseUri;
        if (uri is null)
        {
            return;
        }

        RootTabs.SelectedItem = WebUiTab;
        try
        {
            await ServerWebView.EnsureCoreWebView2Async().ConfigureAwait(true);
            AttachWebViewEvents();
            ServerWebView.Source = uri;
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = exception.Message;
            try
            {
                OpenExternalUri(uri);
            }
            catch (Exception fallbackException)
            {
                _viewModel.StatusMessage = fallbackException.Message;
            }
        }
    }

    private void AttachWebViewEvents()
    {
        if (_webViewEventsAttached || ServerWebView.CoreWebView2 is null)
        {
            return;
        }

        ServerWebView.CoreWebView2.ServerCertificateErrorDetected += OnServerCertificateErrorDetected;
        _webViewEventsAttached = true;
    }

    private void OnServerCertificateErrorDetected(object? sender, CoreWebView2ServerCertificateErrorDetectedEventArgs e)
    {
        if (_viewModel.SelectedServer?.IgnoreCertificateErrors == true)
        {
            e.Action = CoreWebView2ServerCertificateErrorAction.AlwaysAllow;
        }
    }

    private static void OpenExternalUri(Uri uri)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = uri.ToString(),
            UseShellExecute = true
        });
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
