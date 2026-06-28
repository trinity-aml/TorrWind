using System.ComponentModel;
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
}
