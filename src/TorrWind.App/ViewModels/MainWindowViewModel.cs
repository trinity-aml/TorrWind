using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Input;
using TorrWind.Core;
using TorrWind.Core.Localization;
using TorrWind.Core.Models;
using TorrWind.Core.Services;

namespace TorrWind.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly AppSettingsStore _settingsStore;
    private readonly JsonLocalizationService _localization;
    private readonly ExternalPlayerLauncher _playerLauncher = new();
    private readonly LocalTorrServerProcessManager _localServerProcess = new();
    private readonly FileEventLog _userLog = FileEventLog.User;
    private readonly FileEventLog _serviceLog = FileEventLog.Service;
    private readonly RelayCommand _removeServerCommand;
    private readonly RelayCommand _removeSearchProviderCommand;
    private readonly AsyncRelayCommand _restoreSelectedSettingsBackupCommand;
    private readonly RelayCommand _deleteSelectedSettingsBackupCommand;
    private readonly AsyncRelayCommand _downloadSelectedTorrServerReleaseCommand;
    private AppSettings _settings = AppSettings.CreateDefault();
    private TorrServerRelease? _latestTorrServerRelease;
    private ServerProfile? _selectedServer;
    private TorrentItem? _selectedTorrent;
    private TorrentFile? _selectedTorrentFile;
    private SearchResult? _selectedSearchResult;
    private SearchProviderSettings? _selectedSearchProvider;
    private SearchProviderOption? _selectedSearchProviderOption;
    private SettingsBackupItem? _selectedSettingsBackup;
    private TorrServerReleaseItem? _selectedTorrServerRelease;
    private string? _selectedSearchHistoryItem;
    private string _selectedLanguage = "system";
    private string _newMagnet = string.Empty;
    private string _selectedTorrentTitle = string.Empty;
    private string _selectedTorrentCategory = string.Empty;
    private string _selectedTorrentPoster = string.Empty;
    private string _selectedTorrentData = string.Empty;
    private string _searchQuery = string.Empty;
    private string _searchCategories = string.Empty;
    private int _searchMinSeeders;
    private int _searchMaxSizeGb;
    private int _lastSearchFailedProviders;
    private string _statusMessage = string.Empty;
    private string _logLocationText = string.Empty;
    private string _runtimeSettingsJson = string.Empty;
    private string _serviceStatusText = string.Empty;
    private string _torrServerReleaseText = string.Empty;

    public MainWindowViewModel(AppSettingsStore settingsStore, JsonLocalizationService localization)
    {
        _settingsStore = settingsStore;
        _localization = localization;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        AddMagnetCommand = new AsyncRelayCommand(AddMagnetAsync);
        RemoveTorrentCommand = new AsyncRelayCommand(RemoveTorrentAsync);
        RefreshSelectedTorrentCommand = new AsyncRelayCommand(RefreshSelectedTorrentAsync);
        SaveTorrentMetadataCommand = new AsyncRelayCommand(SaveTorrentMetadataAsync);
        DropTorrentCommand = new AsyncRelayCommand(DropTorrentAsync);
        WipeTorrentsCommand = new AsyncRelayCommand(WipeTorrentsAsync);
        SearchCommand = new AsyncRelayCommand(SearchAsync);
        AddSelectedSearchResultCommand = new AsyncRelayCommand(AddSelectedSearchResultAsync);
        OpenSelectedCommand = new AsyncRelayCommand(OpenSelectedAsync);
        CopyPlaybackUrlCommand = new RelayCommand(CopyPlaybackUrl);
        CopyTorrentSourceCommand = new RelayCommand(CopyTorrentSource);
        CopyTorrentHashCommand = new RelayCommand(CopyTorrentHash);
        CheckServerCommand = new AsyncRelayCommand(CheckServerAsync);
        RunDiagnosticsCommand = new AsyncRelayCommand(RunDiagnosticsAsync);
        CopyDiagnosticsCommand = new RelayCommand(CopyDiagnostics);
        LoadRuntimeSettingsCommand = new AsyncRelayCommand(LoadRuntimeSettingsAsync);
        ApplyRuntimeSettingsJsonCommand = new AsyncRelayCommand(ApplyRuntimeSettingsJsonAsync);
        FormatRuntimeSettingsJsonCommand = new RelayCommand(FormatRuntimeSettingsJson);
        CopyRuntimeSettingsJsonCommand = new RelayCommand(CopyRuntimeSettingsJson);
        RefreshLogsCommand = new AsyncRelayCommand(RefreshLogsAsync);
        ClearUserLogCommand = new RelayCommand(ClearUserLog);
        CopyLogPathsCommand = new RelayCommand(CopyLogPaths);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ApplyLanguageCommand = new AsyncRelayCommand(ApplyLanguageAsync);
        CheckTorrServerUpdateCommand = new AsyncRelayCommand(CheckTorrServerUpdateAsync);
        DownloadTorrServerCommand = new AsyncRelayCommand(DownloadTorrServerAsync);
        LoadTorrServerReleasesCommand = new AsyncRelayCommand(LoadTorrServerReleasesAsync);
        _downloadSelectedTorrServerReleaseCommand = new AsyncRelayCommand(DownloadSelectedTorrServerReleaseAsync, () => SelectedTorrServerRelease is not null);
        DownloadSelectedTorrServerReleaseCommand = _downloadSelectedTorrServerReleaseCommand;
        RollbackTorrServerCommand = new AsyncRelayCommand(RollbackTorrServerAsync);
        ApplyLocalServerSettingsCommand = new AsyncRelayCommand(ApplyLocalServerSettingsAsync);
        InstallServiceCommand = new AsyncRelayCommand(InstallServiceAsync);
        UninstallServiceCommand = new AsyncRelayCommand(UninstallServiceAsync);
        StartServiceCommand = new AsyncRelayCommand(StartServiceAsync);
        StopServiceCommand = new AsyncRelayCommand(StopServiceAsync);
        QueryServiceStatusCommand = new AsyncRelayCommand(QueryServiceStatusAsync);
        StartLocalServerCommand = new AsyncRelayCommand(StartLocalServerAsync);
        StopLocalServerCommand = new RelayCommand(StopLocalServer);
        AddServerCommand = new RelayCommand(AddServer);
        _removeServerCommand = new RelayCommand(RemoveSelectedServer, () => Servers.Count > 1);
        RemoveServerCommand = _removeServerCommand;
        AddSearchProviderCommand = new RelayCommand(AddSearchProvider);
        _removeSearchProviderCommand = new RelayCommand(RemoveSelectedSearchProvider, () => SelectedSearchProvider is not null);
        RemoveSearchProviderCommand = _removeSearchProviderCommand;
        RefreshSettingsBackupsCommand = new RelayCommand(RefreshSettingsBackups);
        _restoreSelectedSettingsBackupCommand = new AsyncRelayCommand(RestoreSelectedSettingsBackupAsync, () => SelectedSettingsBackup is not null);
        RestoreSelectedSettingsBackupCommand = _restoreSelectedSettingsBackupCommand;
        _deleteSelectedSettingsBackupCommand = new RelayCommand(DeleteSelectedSettingsBackup, () => SelectedSettingsBackup is not null);
        DeleteSelectedSettingsBackupCommand = _deleteSelectedSettingsBackupCommand;
    }

    public JsonLocalizationService L => _localization;

    public ObservableCollection<TorrentItem> Torrents { get; } = [];

    public ObservableCollection<SearchResult> SearchResults { get; } = [];

    public ObservableCollection<SearchProviderSettings> SearchProviders { get; } = [];

    public ObservableCollection<SearchProviderOption> SearchProviderOptions { get; } = [];

    public ObservableCollection<string> SearchHistory { get; } = [];

    public ObservableCollection<DiagnosticItem> DiagnosticItems { get; } = [];

    public ObservableCollection<AppLogEntry> LogEntries { get; } = [];

    public ObservableCollection<ServerProfile> Servers { get; } = [];

    public ObservableCollection<SettingsBackupItem> SettingsBackups { get; } = [];

    public ObservableCollection<TorrServerReleaseItem> TorrServerReleases { get; } = [];

    public ObservableCollection<string> AvailableLanguages { get; } = [];

    public IReadOnlyList<CacheMode> CacheModes { get; } = Enum.GetValues<CacheMode>();

    public IReadOnlyList<ExternalPlayerKind> ExternalPlayerKinds { get; } = Enum.GetValues<ExternalPlayerKind>();

    public IReadOnlyList<TorrentFile> SelectedTorrentFiles => SelectedTorrent?.Files ?? Array.Empty<TorrentFile>();

    public ICommand RefreshCommand { get; }

    public ICommand AddMagnetCommand { get; }

    public ICommand RemoveTorrentCommand { get; }

    public ICommand RefreshSelectedTorrentCommand { get; }

    public ICommand SaveTorrentMetadataCommand { get; }

    public ICommand DropTorrentCommand { get; }

    public ICommand WipeTorrentsCommand { get; }

    public ICommand SearchCommand { get; }

    public ICommand AddSelectedSearchResultCommand { get; }

    public ICommand OpenSelectedCommand { get; }

    public ICommand CopyPlaybackUrlCommand { get; }

    public ICommand CopyTorrentSourceCommand { get; }

    public ICommand CopyTorrentHashCommand { get; }

    public ICommand CheckServerCommand { get; }

    public ICommand RunDiagnosticsCommand { get; }

    public ICommand CopyDiagnosticsCommand { get; }

    public ICommand LoadRuntimeSettingsCommand { get; }

    public ICommand ApplyRuntimeSettingsJsonCommand { get; }

    public ICommand FormatRuntimeSettingsJsonCommand { get; }

    public ICommand CopyRuntimeSettingsJsonCommand { get; }

    public ICommand RefreshLogsCommand { get; }

    public ICommand ClearUserLogCommand { get; }

    public ICommand CopyLogPathsCommand { get; }

    public ICommand SaveSettingsCommand { get; }

    public ICommand ApplyLanguageCommand { get; }

    public ICommand CheckTorrServerUpdateCommand { get; }

    public ICommand DownloadTorrServerCommand { get; }

    public ICommand LoadTorrServerReleasesCommand { get; }

    public ICommand DownloadSelectedTorrServerReleaseCommand { get; }

    public ICommand RollbackTorrServerCommand { get; }

    public ICommand ApplyLocalServerSettingsCommand { get; }

    public ICommand InstallServiceCommand { get; }

    public ICommand UninstallServiceCommand { get; }

    public ICommand StartServiceCommand { get; }

    public ICommand StopServiceCommand { get; }

    public ICommand QueryServiceStatusCommand { get; }

    public ICommand StartLocalServerCommand { get; }

    public ICommand StopLocalServerCommand { get; }

    public ICommand AddServerCommand { get; }

    public ICommand RemoveServerCommand { get; }

    public ICommand AddSearchProviderCommand { get; }

    public ICommand RemoveSearchProviderCommand { get; }

    public ICommand RefreshSettingsBackupsCommand { get; }

    public ICommand RestoreSelectedSettingsBackupCommand { get; }

    public ICommand DeleteSelectedSettingsBackupCommand { get; }

    public LocalServerSettings LocalServer => _settings.LocalServer;

    public PlayerSettings Player => _settings.Player;

    public string SettingsBackupsDirectory => AppPaths.UserSettingsBackupsDirectory;

    public int SettingsBackupRetentionCount
    {
        get => Math.Max(0, _settings.SettingsBackupRetentionCount);
        set
        {
            var normalized = Math.Max(0, value);
            if (_settings.SettingsBackupRetentionCount != normalized)
            {
                _settings.SettingsBackupRetentionCount = normalized;
                OnPropertyChanged();
            }
        }
    }

    public string ActiveServerLabel => SelectedServer is null ? L["NoServerSelected"] : SelectedServer.Name;

    public ServerProfile? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (SetProperty(ref _selectedServer, value))
            {
                _settings.ActiveServerId = value?.Id;
                OnPropertyChanged(nameof(ActiveServerLabel));
            }
        }
    }

    public TorrentItem? SelectedTorrent
    {
        get => _selectedTorrent;
        set
        {
            if (SetProperty(ref _selectedTorrent, value))
            {
                OnPropertyChanged(nameof(SelectedTorrentFiles));
                SelectedTorrentFile = value?.Files.FirstOrDefault();
                LoadSelectedTorrentEditor(value);
            }
        }
    }

    public TorrentFile? SelectedTorrentFile
    {
        get => _selectedTorrentFile;
        set => SetProperty(ref _selectedTorrentFile, value);
    }

    public SearchResult? SelectedSearchResult
    {
        get => _selectedSearchResult;
        set => SetProperty(ref _selectedSearchResult, value);
    }

    public SearchProviderSettings? SelectedSearchProvider
    {
        get => _selectedSearchProvider;
        set
        {
            if (SetProperty(ref _selectedSearchProvider, value))
            {
                _removeSearchProviderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public SearchProviderOption? SelectedSearchProviderOption
    {
        get => _selectedSearchProviderOption;
        set => SetProperty(ref _selectedSearchProviderOption, value);
    }

    public SettingsBackupItem? SelectedSettingsBackup
    {
        get => _selectedSettingsBackup;
        set
        {
            if (SetProperty(ref _selectedSettingsBackup, value))
            {
                _restoreSelectedSettingsBackupCommand.RaiseCanExecuteChanged();
                _deleteSelectedSettingsBackupCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public TorrServerReleaseItem? SelectedTorrServerRelease
    {
        get => _selectedTorrServerRelease;
        set
        {
            if (SetProperty(ref _selectedTorrServerRelease, value))
            {
                _downloadSelectedTorrServerReleaseCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? SelectedSearchHistoryItem
    {
        get => _selectedSearchHistoryItem;
        set
        {
            if (SetProperty(ref _selectedSearchHistoryItem, value) && !string.IsNullOrWhiteSpace(value))
            {
                SearchQuery = value;
            }
        }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    public string NewMagnet
    {
        get => _newMagnet;
        set => SetProperty(ref _newMagnet, value);
    }

    public string SelectedTorrentTitle
    {
        get => _selectedTorrentTitle;
        set => SetProperty(ref _selectedTorrentTitle, value);
    }

    public string SelectedTorrentCategory
    {
        get => _selectedTorrentCategory;
        set => SetProperty(ref _selectedTorrentCategory, value);
    }

    public string SelectedTorrentPoster
    {
        get => _selectedTorrentPoster;
        set => SetProperty(ref _selectedTorrentPoster, value);
    }

    public string SelectedTorrentData
    {
        get => _selectedTorrentData;
        set => SetProperty(ref _selectedTorrentData, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public string SearchCategories
    {
        get => _searchCategories;
        set => SetProperty(ref _searchCategories, value);
    }

    public int SearchMinSeeders
    {
        get => _searchMinSeeders;
        set => SetProperty(ref _searchMinSeeders, Math.Max(0, value));
    }

    public int SearchMaxSizeGb
    {
        get => _searchMaxSizeGb;
        set => SetProperty(ref _searchMaxSizeGb, Math.Max(0, value));
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string LogLocationText
    {
        get => _logLocationText;
        set => SetProperty(ref _logLocationText, value);
    }

    public string RuntimeSettingsJson
    {
        get => _runtimeSettingsJson;
        set => SetProperty(ref _runtimeSettingsJson, value);
    }

    public string ServiceStatusText
    {
        get => _serviceStatusText;
        set => SetProperty(ref _serviceStatusText, value);
    }

    public string TorrServerReleaseText
    {
        get => _torrServerReleaseText;
        set => SetProperty(ref _torrServerReleaseText, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(true);

        await ApplySettingsToViewAsync(cancellationToken).ConfigureAwait(true);
        StatusMessage = L["StatusReady"];
        await StartLocalServerIfConfiguredAsync().ConfigureAwait(true);
        LogInfo("Application", "ViewModel initialized.");
    }

    public async Task ExportSettingsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            SyncSettingsFromView();
            await new AppSettingsStore(filePath).SaveAsync(_settings, cancellationToken).ConfigureAwait(true);
            StatusMessage = string.Format(L["StatusSettingsExported"], filePath);
            LogInfo("Settings", "Settings exported.", filePath);
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(L["StatusSettingsExportFailed"], exception.Message);
            LogError("Settings", "Settings export failed.", exception, filePath);
        }
    }

    public async Task ImportSettingsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await ImportSettingsFileAsync(
            filePath,
            "StatusSettingsImportedWithBackup",
            "StatusSettingsImportFailed",
            "Settings imported",
            cancellationToken).ConfigureAwait(true);
    }

    public async Task RestoreSettingsBackupAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await ImportSettingsFileAsync(
            filePath,
            "StatusSettingsRestoredWithBackup",
            "StatusSettingsRestoreFailed",
            "Settings backup restored",
            cancellationToken).ConfigureAwait(true);
    }

    public void RefreshSettingsBackups()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.UserSettingsBackupsDirectory);
            var selectedPath = SelectedSettingsBackup?.FilePath;
            var backups = Directory
                .EnumerateFiles(AppPaths.UserSettingsBackupsDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .Where(file => file.Exists)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Select(file => new SettingsBackupItem(file.FullName, new DateTimeOffset(file.LastWriteTime), file.Length))
                .ToList();

            SettingsBackups.Clear();
            foreach (var backup in backups)
            {
                SettingsBackups.Add(backup);
            }

            SelectedSettingsBackup = SettingsBackups.FirstOrDefault(backup =>
                string.Equals(backup.FilePath, selectedPath, StringComparison.OrdinalIgnoreCase)) ??
                SettingsBackups.FirstOrDefault();
            StatusMessage = string.Format(L["StatusSettingsBackupsLoaded"], SettingsBackups.Count);
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(L["StatusSettingsBackupsLoadFailed"], exception.Message);
            LogError("Settings", "Settings backup list refresh failed.", exception);
        }
    }

    private async Task RestoreSelectedSettingsBackupAsync()
    {
        if (SelectedSettingsBackup is null)
        {
            StatusMessage = L["StatusNoSettingsBackupSelected"];
            return;
        }

        var result = System.Windows.MessageBox.Show(
            string.Format(L["ConfirmRestoreSelectedSettingsBackup"], SelectedSettingsBackup.FileName),
            L["ConfirmRestoreSettingsBackupTitle"],
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            StatusMessage = L["StatusSettingsRestoreCancelled"];
            return;
        }

        await RestoreSettingsBackupAsync(SelectedSettingsBackup.FilePath).ConfigureAwait(true);
    }

    private void DeleteSelectedSettingsBackup()
    {
        if (SelectedSettingsBackup is null)
        {
            StatusMessage = L["StatusNoSettingsBackupSelected"];
            return;
        }

        var backup = SelectedSettingsBackup;
        var result = System.Windows.MessageBox.Show(
            string.Format(L["ConfirmDeleteSettingsBackup"], backup.FileName),
            L["ConfirmDeleteSettingsBackupTitle"],
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            File.Delete(backup.FilePath);
            RefreshSettingsBackups();
            StatusMessage = string.Format(L["StatusSettingsBackupDeleted"], backup.FilePath);
            LogInfo("Settings", "Settings backup deleted.", backup.FilePath);
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(L["StatusSettingsBackupDeleteFailed"], exception.Message);
            LogError("Settings", "Settings backup delete failed.", exception, backup.FilePath);
        }
    }

    private async Task ImportSettingsFileAsync(
        string filePath,
        string successStatusKey,
        string failureStatusKey,
        string logMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var backupPath = await BackupSettingsBeforeImportAsync(cancellationToken).ConfigureAwait(true);
            _settings = await new AppSettingsStore(filePath).LoadExistingAsync(cancellationToken).ConfigureAwait(true);
            await ApplySettingsToViewAsync(cancellationToken).ConfigureAwait(true);
            await SaveSettingsAsync().ConfigureAwait(true);
            RefreshSettingsBackups();

            StatusMessage = string.Format(L[successStatusKey], filePath, backupPath);
            LogInfo("Settings", logMessage + ".", $"Import: {filePath}; backup: {backupPath}");
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(L[failureStatusKey], exception.Message);
            LogError("Settings", logMessage + " failed.", exception, filePath);
        }
    }

    public void SetLocalServerExecutablePath(string path)
    {
        SetLocalServerPath(path, value => LocalServer.ExecutablePath = value);
    }

    public void SetLocalServerDataDirectory(string path)
    {
        SetLocalServerPath(path, value => LocalServer.DataDirectory = value);
    }

    public void SetLocalServerTemporaryDataPath(string path)
    {
        SetLocalServerPath(path, value => LocalServer.TemporaryDataPath = value);
    }

    public void SetLocalServerCertificatePath(string path)
    {
        SetLocalServerPath(path, value => LocalServer.CertificatePath = value);
    }

    public void SetLocalServerCertificateKeyPath(string path)
    {
        SetLocalServerPath(path, value => LocalServer.CertificateKeyPath = value);
    }

    public void SetCustomPlayerPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Player.CustomPlayerPath = path;
        OnPropertyChanged(nameof(Player));
        StatusMessage = L["StatusPathSelected"];
    }

    private async Task ApplySettingsToViewAsync(CancellationToken cancellationToken)
    {
        AvailableLanguages.Clear();
        AvailableLanguages.Add("system");
        foreach (var language in _localization.GetAvailableLanguages())
        {
            AvailableLanguages.Add(language);
        }

        SelectedLanguage = _settings.Language;
        await _localization.LoadAsync(_settings.Language, cancellationToken).ConfigureAwait(true);

        Servers.Clear();
        foreach (var server in _settings.Servers)
        {
            Servers.Add(server);
        }

        SearchProviders.Clear();
        foreach (var provider in _settings.SearchProviders)
        {
            SearchProviders.Add(provider);
        }

        SearchHistory.Clear();
        foreach (var query in _settings.SearchHistory.Where(query => !string.IsNullOrWhiteSpace(query)).Take(20))
        {
            SearchHistory.Add(query);
        }

        SelectedServer = Servers.FirstOrDefault(server => server.Id == _settings.ActiveServerId) ?? Servers.FirstOrDefault();
        SelectedSearchProvider = SearchProviders.FirstOrDefault();
        RebuildSearchProviderOptions();
        Torrents.Clear();
        SearchResults.Clear();
        DiagnosticItems.Clear();
        SelectedTorrent = null;
        SelectedTorrentFile = null;
        SelectedSearchResult = null;
        RuntimeSettingsJson = string.Empty;
        LogLocationText = string.Format(L["LogLocations"], AppPaths.UserLogFile, AppPaths.ServiceLogFile);
        UpdateTorrServerReleaseText();
        RefreshSettingsBackups();
        await RefreshLogsAsync().ConfigureAwait(true);
        await UpdateServiceStatusAsync(updateStatusMessage: false).ConfigureAwait(true);
        OnPropertyChanged(nameof(LocalServer));
        OnPropertyChanged(nameof(Player));
        OnPropertyChanged(nameof(SettingsBackupRetentionCount));
        OnPropertyChanged(nameof(ActiveServerLabel));
        _removeServerCommand.RaiseCanExecuteChanged();
        _removeSearchProviderCommand.RaiseCanExecuteChanged();
    }

    private async Task<string> BackupSettingsBeforeImportAsync(CancellationToken cancellationToken)
    {
        SyncSettingsFromView();
        AppPaths.EnsureUserDirectories();

        var fileName = $"settings-before-import-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.json";
        var backupPath = Path.Combine(AppPaths.UserSettingsBackupsDirectory, fileName);
        await new AppSettingsStore(backupPath).SaveAsync(_settings, cancellationToken).ConfigureAwait(true);
        PruneSettingsBackups();
        RefreshSettingsBackups();
        LogInfo("Settings", "Settings backup created before import.", backupPath);
        return backupPath;
    }

    private void PruneSettingsBackups()
    {
        var retentionCount = SettingsBackupRetentionCount;
        if (retentionCount <= 0)
        {
            return;
        }

        try
        {
            var backups = Directory
                .EnumerateFiles(AppPaths.UserSettingsBackupsDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .Where(file => file.Exists)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Skip(retentionCount)
                .ToList();

            foreach (var backup in backups)
            {
                backup.Delete();
            }

            if (backups.Count > 0)
            {
                LogInfo("Settings", "Old settings backups pruned.", backups.Count.ToString());
            }
        }
        catch (Exception exception)
        {
            LogWarning("Settings", "Failed to prune old settings backups.", exception.Message);
        }
    }

    private void SetLocalServerPath(string path, Action<string> apply)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        apply(path);
        OnPropertyChanged(nameof(LocalServer));
        StatusMessage = L["StatusPathSelected"];
    }

    public async Task RefreshAsync()
    {
        if (SelectedServer is null)
        {
            StatusMessage = L["NoServerSelected"];
            return;
        }

        try
        {
            using var client = new TorrServerClient(SelectedServer);
            var torrents = await client.GetTorrentsAsync().ConfigureAwait(true);

            Torrents.Clear();
            foreach (var torrent in torrents)
            {
                Torrents.Add(torrent);
            }

            if (SelectedTorrent is not null && Torrents.All(torrent => torrent.Hash != SelectedTorrent.Hash))
            {
                SelectedTorrent = null;
            }

            StatusMessage = string.Format(L["StatusTorrentsLoaded"], Torrents.Count);
            LogInfo("Library", "Torrent list refreshed.", $"{SelectedServer.Name}: {Torrents.Count}");
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Library", "Failed to refresh torrent list.", exception, SelectedServer.Name);
        }
    }

    public async Task<bool> AddTorrentFileAsync(string filePath)
    {
        if (SelectedServer is null || SelectedServer.ReadOnly)
        {
            StatusMessage = L["StatusReadOnly"];
            return false;
        }

        try
        {
            using var client = new TorrServerClient(SelectedServer);
            await client.AddTorrentFileAsync(filePath).ConfigureAwait(true);
            StatusMessage = L["StatusTorrentAdded"];
            LogInfo("Library", "Torrent file added.", Path.GetFileName(filePath));
            await RefreshAsync().ConfigureAwait(true);
            return true;
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Library", "Failed to add torrent file.", exception, Path.GetFileName(filePath));
            return false;
        }
    }

    public async Task ProcessStartupArgumentsAsync(IEnumerable<string> args)
    {
        var handled = 0;
        foreach (var arg in args.Where(arg => !IsControlArgument(arg)))
        {
            var value = arg.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (IsTorrentFile(value))
            {
                if (await AddTorrentFileAsync(value).ConfigureAwait(true))
                {
                    handled++;
                }
            }
            else if (IsTorrentLink(value))
            {
                if (await AddTorrentLinkAsync(value).ConfigureAwait(true))
                {
                    handled++;
                }
            }
        }

        if (handled > 0)
        {
            StatusMessage = string.Format(L["StatusStartupArgumentsHandled"], handled);
            LogInfo("Startup", "Startup torrent arguments handled.", handled.ToString());
        }
    }

    private async Task AddMagnetAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMagnet))
        {
            return;
        }

        if (await AddTorrentLinkAsync(NewMagnet.Trim()).ConfigureAwait(true))
        {
            NewMagnet = string.Empty;
        }
    }

    private async Task<bool> AddTorrentLinkAsync(string link)
    {
        if (SelectedServer is null || SelectedServer.ReadOnly)
        {
            StatusMessage = L["StatusReadOnly"];
            return false;
        }

        if (string.IsNullOrWhiteSpace(link))
        {
            return false;
        }

        try
        {
            using var client = new TorrServerClient(SelectedServer);
            await client.AddMagnetAsync(link.Trim()).ConfigureAwait(true);
            StatusMessage = L["StatusTorrentAdded"];
            LogInfo("Library", "Torrent link added.", SelectedServer.Name);
            await RefreshAsync().ConfigureAwait(true);
            return true;
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Library", "Failed to add torrent link.", exception, SelectedServer.Name);
            return false;
        }
    }

    private async Task RemoveTorrentAsync()
    {
        if (SelectedServer is null || SelectedServer.ReadOnly)
        {
            StatusMessage = L["StatusReadOnly"];
            return;
        }

        if (SelectedTorrent is null || string.IsNullOrWhiteSpace(SelectedTorrent.Hash))
        {
            StatusMessage = L["StatusNoTorrentSelected"];
            return;
        }

        try
        {
            using var client = new TorrServerClient(SelectedServer);
            await client.RemoveTorrentAsync(SelectedTorrent.Hash).ConfigureAwait(true);
            StatusMessage = L["StatusTorrentRemoved"];
            LogInfo("Library", "Torrent removed.", SelectedTorrent.Title);
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Library", "Failed to remove torrent.", exception, SelectedTorrent?.Title ?? string.Empty);
        }
    }

    private async Task RefreshSelectedTorrentAsync()
    {
        if (SelectedServer is null || SelectedTorrent is null || string.IsNullOrWhiteSpace(SelectedTorrent.Hash))
        {
            StatusMessage = L["StatusNoTorrentSelected"];
            return;
        }

        try
        {
            using var client = new TorrServerClient(SelectedServer);
            var updated = await client.GetTorrentAsync(SelectedTorrent.Hash).ConfigureAwait(true);
            ReplaceSelectedTorrent(updated);
            StatusMessage = L["StatusTorrentDetailsLoaded"];
            LogInfo("Library", "Torrent details refreshed.", updated.Title);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Library", "Failed to refresh torrent details.", exception, SelectedTorrent.Title);
        }
    }

    private async Task SaveTorrentMetadataAsync()
    {
        if (SelectedServer is null || SelectedServer.ReadOnly)
        {
            StatusMessage = L["StatusReadOnly"];
            return;
        }

        if (SelectedTorrent is null || string.IsNullOrWhiteSpace(SelectedTorrent.Hash))
        {
            StatusMessage = L["StatusNoTorrentSelected"];
            return;
        }

        try
        {
            using var client = new TorrServerClient(SelectedServer);
            await client.SetTorrentMetadataAsync(
                SelectedTorrent.Hash,
                SelectedTorrentTitle.Trim(),
                SelectedTorrentPoster.Trim(),
                SelectedTorrentCategory.Trim(),
                SelectedTorrentData).ConfigureAwait(true);

            var updated = await client.GetTorrentAsync(SelectedTorrent.Hash).ConfigureAwait(true);
            ReplaceSelectedTorrent(updated);
            StatusMessage = L["StatusTorrentMetadataSaved"];
            LogInfo("Library", "Torrent metadata saved.", updated.Title);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Library", "Failed to save torrent metadata.", exception, SelectedTorrent.Title);
        }
    }

    private async Task DropTorrentAsync()
    {
        if (SelectedServer is null || SelectedServer.ReadOnly)
        {
            StatusMessage = L["StatusReadOnly"];
            return;
        }

        if (SelectedTorrent is null || string.IsNullOrWhiteSpace(SelectedTorrent.Hash))
        {
            StatusMessage = L["StatusNoTorrentSelected"];
            return;
        }

        try
        {
            using var client = new TorrServerClient(SelectedServer);
            await client.DropTorrentAsync(SelectedTorrent.Hash).ConfigureAwait(true);
            StatusMessage = L["StatusTorrentDropped"];
            LogInfo("Library", "Torrent dropped from active cache.", SelectedTorrent.Title);
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Library", "Failed to drop torrent.", exception, SelectedTorrent.Title);
        }
    }

    private async Task WipeTorrentsAsync()
    {
        if (SelectedServer is null || SelectedServer.ReadOnly)
        {
            StatusMessage = L["StatusReadOnly"];
            return;
        }

        var result = System.Windows.MessageBox.Show(
            L["ConfirmWipeTorrents"],
            L["WindowTitle"],
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            using var client = new TorrServerClient(SelectedServer);
            await client.WipeTorrentsAsync().ConfigureAwait(true);
            Torrents.Clear();
            SelectedTorrent = null;
            StatusMessage = L["StatusTorrentsWiped"];
            LogWarning("Library", "All torrents wiped.", SelectedServer.Name);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Library", "Failed to wipe torrents.", exception, SelectedServer.Name);
        }
    }

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return;
        }

        try
        {
            var query = SearchQuery.Trim();
            var option = SelectedSearchProviderOption ?? SearchProviderOptions.FirstOrDefault();
            if (option is null)
            {
                RebuildSearchProviderOptions();
                option = SelectedSearchProviderOption ?? SearchProviderOptions.FirstOrDefault();
            }

            if (option is null)
            {
                StatusMessage = L["StatusNoSearchProvider"];
                return;
            }

            if (option.UseSelectedServer && SelectedServer is null)
            {
                StatusMessage = L["NoServerSelected"];
                return;
            }

            _lastSearchFailedProviders = 0;
            var results = option.UseSelectedServer
                ? await SearchSelectedServerAsync(query).ConfigureAwait(true)
                : await SearchConfiguredProvidersAsync(option, query).ConfigureAwait(true);

            SearchResults.Clear();
            foreach (var result in ApplySearchFilters(results))
            {
                SearchResults.Add(result);
            }

            await SaveSearchHistoryAsync(query).ConfigureAwait(true);
            StatusMessage = _lastSearchFailedProviders > 0
                ? string.Format(L["StatusSearchLoadedWithProviderErrors"], SearchResults.Count, _lastSearchFailedProviders)
                : string.Format(L["StatusSearchLoaded"], SearchResults.Count);
            LogInfo("Search", "Search completed.", $"{query}: {SearchResults.Count}");
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Search", "Search failed.", exception, SearchQuery);
        }
    }

    private async Task AddSelectedSearchResultAsync()
    {
        if (SelectedSearchResult is null || SelectedServer is null)
        {
            return;
        }

        var link = !string.IsNullOrWhiteSpace(SelectedSearchResult.Magnet)
            ? SelectedSearchResult.Magnet
            : SelectedSearchResult.Link;

        if (string.IsNullOrWhiteSpace(link))
        {
            return;
        }

        try
        {
            using var client = new TorrServerClient(SelectedServer);
            await client.AddMagnetAsync(link, SelectedSearchResult.Title).ConfigureAwait(true);
            StatusMessage = L["StatusTorrentAdded"];
            LogInfo("Search", "Search result added to library.", SelectedSearchResult.Title);
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Search", "Failed to add search result.", exception, SelectedSearchResult.Title);
        }
    }

    private Task OpenSelectedAsync()
    {
        if (SelectedServer is null || SelectedTorrent is null)
        {
            return Task.CompletedTask;
        }

        var mediaUri = GetSelectedPlaybackUri();
        if (mediaUri is null)
        {
            StatusMessage = L["StatusNoTorrentSelected"];
            return Task.CompletedTask;
        }

        _playerLauncher.Play(mediaUri, _settings.Player);
        LogInfo("Player", "Opened selected torrent in external player.", SelectedTorrent.Title);
        return Task.CompletedTask;
    }

    private void CopyPlaybackUrl()
    {
        var mediaUri = GetSelectedPlaybackUri();
        if (mediaUri is null)
        {
            StatusMessage = L["StatusNoTorrentSelected"];
            return;
        }

        System.Windows.Clipboard.SetText(mediaUri.ToString());
        StatusMessage = L["StatusPlaybackUrlCopied"];
        LogInfo("Player", "Playback URL copied.", SelectedTorrent?.Title ?? string.Empty);
    }

    private void CopyTorrentSource()
    {
        if (SelectedTorrent is null)
        {
            StatusMessage = L["StatusNoTorrentSelected"];
            return;
        }

        var value = FirstNotEmpty(SelectedTorrent.SourceLink, SelectedTorrent.TorrsHash);
        if (string.IsNullOrWhiteSpace(value))
        {
            StatusMessage = L["StatusTorrentSourceUnavailable"];
            return;
        }

        System.Windows.Clipboard.SetText(value);
        StatusMessage = L["StatusTorrentSourceCopied"];
    }

    private void CopyTorrentHash()
    {
        if (SelectedTorrent is null || string.IsNullOrWhiteSpace(SelectedTorrent.Hash))
        {
            StatusMessage = L["StatusNoTorrentSelected"];
            return;
        }

        System.Windows.Clipboard.SetText(SelectedTorrent.Hash);
        StatusMessage = L["StatusTorrentHashCopied"];
    }

    private Uri? GetSelectedPlaybackUri()
    {
        if (SelectedServer is null || SelectedTorrent is null || string.IsNullOrWhiteSpace(SelectedTorrent.Hash))
        {
            return null;
        }

        var fileId = SelectedTorrentFile?.Id ?? SelectedTorrent.Files.FirstOrDefault()?.Id ?? 0;
        using var client = new TorrServerClient(SelectedServer);
        if (_settings.Player.PreferDirectStreamUrl && !string.IsNullOrWhiteSpace(SelectedTorrent.SourceLink))
        {
            return client.GetStreamUri(SelectedTorrent.SourceLink, fileId);
        }

        return client.GetPlaybackUri(SelectedTorrent.Hash, fileId);
    }

    private void ReplaceSelectedTorrent(TorrentItem updated)
    {
        if (string.IsNullOrWhiteSpace(updated.Hash))
        {
            return;
        }

        var index = Torrents
            .Select((torrent, torrentIndex) => new { torrent, torrentIndex })
            .FirstOrDefault(item => string.Equals(item.torrent.Hash, updated.Hash, StringComparison.OrdinalIgnoreCase))
            ?.torrentIndex;

        if (index is null)
        {
            Torrents.Add(updated);
        }
        else
        {
            Torrents[index.Value] = updated;
        }

        SelectedTorrent = updated;
    }

    private void LoadSelectedTorrentEditor(TorrentItem? torrent)
    {
        SelectedTorrentTitle = torrent?.Title ?? string.Empty;
        SelectedTorrentCategory = torrent?.Category ?? string.Empty;
        SelectedTorrentPoster = torrent?.Poster ?? string.Empty;
        SelectedTorrentData = torrent?.Data ?? string.Empty;
    }

    private async Task SaveSettingsAsync()
    {
        SyncSettingsFromView();
        await _settingsStore.SaveAsync(_settings).ConfigureAwait(true);

        try
        {
            AppPaths.EnsureProgramDataDirectories();
            await new AppSettingsStore(AppPaths.ServiceSettingsFile).SaveAsync(_settings).ConfigureAwait(true);
        }
        catch
        {
            StatusMessage = L["StatusSavedUserOnly"];
            LogWarning("Settings", "Settings saved for current user only.");
            return;
        }

        StatusMessage = L["StatusSaved"];
        LogInfo("Settings", "Settings saved.");
    }

    private void SyncSettingsFromView()
    {
        _settings.Servers = Servers.ToList();
        _settings.SearchProviders = SearchProviders.ToList();
        _settings.SearchHistory = SearchHistory.ToList();
        _settings.ActiveServerId = SelectedServer?.Id;
        _settings.Language = SelectedLanguage;
        OnPropertyChanged(nameof(Player));
    }

    private void AddServer()
    {
        var server = new ServerProfile
        {
            Name = L["NewServerName"],
            BaseUrl = "http://127.0.0.1:8090"
        };

        Servers.Add(server);
        SelectedServer = server;
        _removeServerCommand.RaiseCanExecuteChanged();
        StatusMessage = L["StatusServerAdded"];
        LogInfo("Profiles", "Server profile added.", server.Name);
    }

    private void RemoveSelectedServer()
    {
        if (SelectedServer is null || Servers.Count <= 1)
        {
            return;
        }

        var index = Servers.IndexOf(SelectedServer);
        var removedName = SelectedServer.Name;
        Servers.Remove(SelectedServer);
        SelectedServer = Servers.ElementAtOrDefault(Math.Max(0, index - 1)) ?? Servers.FirstOrDefault();
        _removeServerCommand.RaiseCanExecuteChanged();
        StatusMessage = L["StatusServerRemoved"];
        LogInfo("Profiles", "Server profile removed.", removedName);
    }

    private void AddSearchProvider()
    {
        var provider = new SearchProviderSettings
        {
            Name = L["NewSearchProviderName"],
            Url = string.Empty,
            Enabled = true,
            TimeoutSeconds = 30
        };

        SearchProviders.Add(provider);
        SelectedSearchProvider = provider;
        RebuildSearchProviderOptions();
        StatusMessage = L["StatusSearchProviderAdded"];
        LogInfo("Search", "Search provider added.", provider.Name);
    }

    private void RemoveSelectedSearchProvider()
    {
        if (SelectedSearchProvider is null)
        {
            return;
        }

        var removedName = SelectedSearchProvider.Name;
        SearchProviders.Remove(SelectedSearchProvider);
        SelectedSearchProvider = SearchProviders.FirstOrDefault();
        RebuildSearchProviderOptions();
        StatusMessage = L["StatusSearchProviderRemoved"];
        LogInfo("Search", "Search provider removed.", removedName);
    }

    private async Task CheckServerAsync()
    {
        if (SelectedServer is null)
        {
            StatusMessage = L["NoServerSelected"];
            return;
        }

        try
        {
            using var client = new TorrServerClient(SelectedServer);
            var echo = await client.GetEchoAsync().ConfigureAwait(true);
            StatusMessage = string.Format(L["StatusServerOnline"], string.IsNullOrWhiteSpace(echo) ? SelectedServer.BaseUrl : echo.Trim());
            LogInfo("Diagnostics", "Server connection check succeeded.", SelectedServer.Name);
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(L["StatusServerOffline"], exception.Message);
            LogError("Diagnostics", "Server connection check failed.", exception, SelectedServer.Name);
        }
    }

    private async Task LoadRuntimeSettingsAsync()
    {
        if (SelectedServer is null)
        {
            StatusMessage = L["NoServerSelected"];
            return;
        }

        try
        {
            using var client = new TorrServerClient(SelectedServer);
            RuntimeSettingsJson = await client.GetSettingsJsonAsync().ConfigureAwait(true);
            StatusMessage = L["StatusRuntimeSettingsLoaded"];
            LogInfo("RuntimeSettings", "Runtime settings loaded.", SelectedServer.Name);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("RuntimeSettings", "Failed to load runtime settings.", exception, SelectedServer.Name);
        }
    }

    private async Task ApplyRuntimeSettingsJsonAsync()
    {
        if (SelectedServer is null)
        {
            StatusMessage = L["NoServerSelected"];
            return;
        }

        if (SelectedServer.ReadOnly)
        {
            StatusMessage = L["StatusReadOnly"];
            return;
        }

        if (string.IsNullOrWhiteSpace(RuntimeSettingsJson))
        {
            StatusMessage = L["StatusRuntimeSettingsJsonEmpty"];
            return;
        }

        try
        {
            RuntimeSettingsJson = FormatRuntimeSettingsJsonValue(RuntimeSettingsJson);
            using var client = new TorrServerClient(SelectedServer);
            await client.ApplySettingsJsonAsync(RuntimeSettingsJson).ConfigureAwait(true);
            StatusMessage = L["StatusRuntimeSettingsApplied"];
            LogInfo("RuntimeSettings", "Runtime settings JSON applied.", SelectedServer.Name);
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(L["StatusRuntimeSettingsJsonInvalid"], exception.Message);
            LogError("RuntimeSettings", "Failed to apply runtime settings JSON.", exception, SelectedServer.Name);
        }
    }

    private void FormatRuntimeSettingsJson()
    {
        if (string.IsNullOrWhiteSpace(RuntimeSettingsJson))
        {
            StatusMessage = L["StatusRuntimeSettingsJsonEmpty"];
            return;
        }

        try
        {
            RuntimeSettingsJson = FormatRuntimeSettingsJsonValue(RuntimeSettingsJson);
            StatusMessage = L["StatusRuntimeSettingsJsonFormatted"];
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(L["StatusRuntimeSettingsJsonInvalid"], exception.Message);
            LogError("RuntimeSettings", "Failed to format runtime settings JSON.", exception);
        }
    }

    private void CopyRuntimeSettingsJson()
    {
        if (string.IsNullOrWhiteSpace(RuntimeSettingsJson))
        {
            StatusMessage = L["StatusRuntimeSettingsJsonEmpty"];
            return;
        }

        System.Windows.Clipboard.SetText(RuntimeSettingsJson);
        StatusMessage = L["StatusRuntimeSettingsJsonCopied"];
    }

    private async Task RefreshLogsAsync()
    {
        var entries = new List<AppLogEntry>();
        entries.AddRange(await ReadLogSafeAsync(_userLog).ConfigureAwait(true));
        entries.AddRange(await ReadLogSafeAsync(_serviceLog).ConfigureAwait(true));

        LogEntries.Clear();
        foreach (var entry in entries
            .OrderByDescending(entry => entry.Timestamp)
            .Take(500)
            .OrderBy(entry => entry.Timestamp))
        {
            LogEntries.Add(entry);
        }
    }

    private void ClearUserLog()
    {
        _userLog.Clear();
        LogInfo("Logs", "User log cleared.");
        _ = RefreshLogsAsync();
        StatusMessage = L["StatusUserLogCleared"];
    }

    private void CopyLogPaths()
    {
        System.Windows.Clipboard.SetText(AppPaths.UserLogFile + Environment.NewLine + AppPaths.ServiceLogFile);
        StatusMessage = L["StatusLogPathsCopied"];
    }

    private static async Task<IReadOnlyList<AppLogEntry>> ReadLogSafeAsync(FileEventLog log)
    {
        try
        {
            return await log.ReadLatestAsync().ConfigureAwait(false);
        }
        catch
        {
            return [];
        }
    }

    private async Task RunDiagnosticsAsync()
    {
        DiagnosticItems.Clear();
        AddApplicationDiagnostics();

        if (SelectedServer is null)
        {
            AddDiagnostic("DiagnosticError", L["NoServerSelected"]);
            StatusMessage = L["NoServerSelected"];
            return;
        }

        AddDiagnostic("DiagnosticProfile", SelectedServer.Name);
        AddDiagnostic("DiagnosticBaseUrl", SelectedServer.BaseUrl);
        AddDiagnostic("DiagnosticProfileType", SelectedServer.IsLocal ? L["DiagnosticLocal"] : L["DiagnosticRemote"]);
        AddDiagnostic("DiagnosticReadOnly", FormatBool(SelectedServer.ReadOnly));
        AddDiagnostic("DiagnosticIgnoreCert", FormatBool(SelectedServer.IgnoreCertificateErrors));

        await AddServerDiagnosticsAsync(SelectedServer).ConfigureAwait(true);

        if (SelectedServer.IsLocal)
        {
            await AddLocalDiagnosticsAsync().ConfigureAwait(true);
        }

        StatusMessage = L["StatusDiagnosticsComplete"];
        LogInfo("Diagnostics", "Diagnostics completed.", SelectedServer.Name);
    }

    private void CopyDiagnostics()
    {
        if (DiagnosticItems.Count == 0)
        {
            StatusMessage = L["StatusNoDiagnostics"];
            return;
        }

        var text = string.Join(Environment.NewLine, DiagnosticItems.Select(item => $"{item.Name}: {item.Value}"));
        System.Windows.Clipboard.SetText(text);
        StatusMessage = L["StatusDiagnosticsCopied"];
    }

    private void AddApplicationDiagnostics()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(MainWindowViewModel).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? string.Empty;

        AddDiagnostic("DiagnosticAppVersion", version);
        AddDiagnostic("DiagnosticRuntime", RuntimeInformation.FrameworkDescription);
        AddDiagnostic("DiagnosticOS", RuntimeInformation.OSDescription);
        AddDiagnostic("DiagnosticProcessArchitecture", RuntimeInformation.ProcessArchitecture.ToString());
        AddDiagnostic("DiagnosticUserSettingsFile", AppPaths.UserSettingsFile);
    }

    private async Task AddServerDiagnosticsAsync(ServerProfile server)
    {
        try
        {
            using var client = new TorrServerClient(server);
            var echo = await client.GetEchoAsync().ConfigureAwait(true);
            AddDiagnostic("DiagnosticOnline", L["DiagnosticYes"]);
            AddDiagnostic("DiagnosticEcho", string.IsNullOrWhiteSpace(echo) ? L["DiagnosticOk"] : echo.Trim());

            var torrents = await client.GetTorrentsAsync().ConfigureAwait(true);
            AddDiagnostic("DiagnosticTorrentCount", torrents.Count.ToString());
            AddDiagnostic("DiagnosticTorrentTotalSize", TorrentItem.FormatBytes(torrents.Sum(torrent => torrent.SizeBytes)));

            await AddRuntimeSettingsDiagnosticsAsync(client).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            AddDiagnostic("DiagnosticOnline", L["DiagnosticNo"]);
            AddDiagnostic("DiagnosticError", exception.Message);
            LogError("Diagnostics", "Server diagnostics failed.", exception, server.Name);
        }
    }

    private async Task AddRuntimeSettingsDiagnosticsAsync(TorrServerClient client)
    {
        try
        {
            using var settings = await client.GetSettingsAsync().ConfigureAwait(true);
            var root = settings.RootElement;

            AddDiagnostic("DiagnosticSettingsCache", FormatCacheSize(ReadJsonValue(root, "CacheSize")));
            AddDiagnostic("DiagnosticSettingsCacheMode", FormatCacheMode(ReadJsonValue(root, "UseDisk")));
            AddDiagnostic("DiagnosticSettingsSavePath", ReadJsonValue(root, "TorrentsSavePath", "SavePath"));
            AddDiagnostic("DiagnosticSettingsDownloadLimit", ReadJsonValue(root, "DownloadRateLimit"));
            AddDiagnostic("DiagnosticSettingsUploadLimit", ReadJsonValue(root, "UploadRateLimit"));
            AddDiagnostic("DiagnosticSettingsDlna", FormatBoolText(ReadJsonValue(root, "EnableDLNA", "EnableDlna")));
        }
        catch (Exception exception)
        {
            AddDiagnostic("DiagnosticSettings", exception.Message);
            LogError("Diagnostics", "Runtime settings diagnostics failed.", exception);
        }
    }

    private async Task AddLocalDiagnosticsAsync()
    {
        AddDiagnostic("DiagnosticLocalVersion", EmptyAsNotAvailable(LocalServer.InstalledVersion));
        AddDiagnostic("DiagnosticLocalExe", EmptyAsNotAvailable(LocalServer.ExecutablePath));
        AddDiagnostic("DiagnosticLocalExeExists", FormatBool(!string.IsNullOrWhiteSpace(LocalServer.ExecutablePath) && File.Exists(LocalServer.ExecutablePath)));
        AddDiagnostic("DiagnosticLocalDataPath", EmptyAsNotAvailable(LocalServer.DataDirectory));
        AddDiagnostic("DiagnosticLocalCachePath", EmptyAsNotAvailable(LocalServer.TemporaryDataPath));
        AddDiagnostic("DiagnosticRunAsService", FormatBool(LocalServer.RunAsWindowsService));

        try
        {
            var service = await new WindowsServiceManager().QueryStatusAsync().ConfigureAwait(true);
            AddDiagnostic("DiagnosticServiceInstalled", FormatBool(service.IsInstalled));
            AddDiagnostic("DiagnosticServiceState", service.State);
        }
        catch (Exception exception)
        {
            AddDiagnostic("DiagnosticServiceState", exception.Message);
            LogError("Diagnostics", "Service diagnostics failed.", exception);
        }
    }

    private async Task ApplyLanguageAsync()
    {
        _settings.Language = SelectedLanguage;
        await _localization.LoadAsync(SelectedLanguage).ConfigureAwait(true);
        RebuildSearchProviderOptions();
        UpdateTorrServerReleaseText();
        await SaveSettingsAsync().ConfigureAwait(true);
        OnPropertyChanged(nameof(ActiveServerLabel));
    }

    private void AddDiagnostic(string nameKey, string value)
    {
        DiagnosticItems.Add(new DiagnosticItem(L[nameKey], string.IsNullOrWhiteSpace(value) ? L["DiagnosticNotAvailable"] : value));
    }

    private void LogInfo(string source, string message, string details = "")
    {
        _userLog.Info(source, message, details);
    }

    private void LogWarning(string source, string message, string details = "")
    {
        _userLog.Warning(source, message, details);
    }

    private void LogError(string source, string message, Exception exception, string details = "")
    {
        _userLog.Error(source, message, exception, details);
    }

    private string FormatBool(bool value)
    {
        return value ? L["DiagnosticYes"] : L["DiagnosticNo"];
    }

    private string EmptyAsNotAvailable(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? L["DiagnosticNotAvailable"] : value;
    }

    private string FormatCacheSize(string value)
    {
        if (long.TryParse(value, out var bytes) && bytes > 0)
        {
            return TorrentItem.FormatBytes(bytes);
        }

        return EmptyAsNotAvailable(value);
    }

    private string FormatCacheMode(string value)
    {
        if (bool.TryParse(value, out var useDisk))
        {
            return useDisk ? CacheMode.Disk.ToString() : CacheMode.Memory.ToString();
        }

        return EmptyAsNotAvailable(value);
    }

    private string FormatBoolText(string value)
    {
        if (bool.TryParse(value, out var result))
        {
            return FormatBool(result);
        }

        return EmptyAsNotAvailable(value);
    }

    private string ReadJsonValue(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryFindJsonProperty(root, name, out var value))
            {
                return FormatJsonValue(value);
            }
        }

        return L["DiagnosticNotAvailable"];
    }

    private static bool TryFindJsonProperty(JsonElement element, string name, out JsonElement value, int depth = 0)
    {
        value = default;
        if (depth > 2 || element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object &&
                TryFindJsonProperty(property.Value, name, out value, depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Null => string.Empty,
            _ => value.GetRawText()
        };
    }

    private static string FormatRuntimeSettingsJsonValue(string json)
    {
        var node = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidOperationException("JSON root must be an object.");

        return node.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static string FirstNotEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private async Task<IReadOnlyList<SearchResult>> SearchSelectedServerAsync(string query)
    {
        if (SelectedServer is null)
        {
            StatusMessage = L["NoServerSelected"];
            return [];
        }

        using var client = new TorrServerClient(SelectedServer);
        return await client.SearchTorznabAsync(query).ConfigureAwait(true);
    }

    private async Task<IReadOnlyList<SearchResult>> SearchConfiguredProvidersAsync(SearchProviderOption option, string query)
    {
        var providers = option.Provider is null
            ? SearchProviders.Where(provider => provider.Enabled).ToArray()
            : [option.Provider];

        if (providers.Length == 0)
        {
            StatusMessage = L["StatusNoSearchProvider"];
            return [];
        }

        var client = new TorznabSearchClient();
        var results = new List<SearchResult>();
        foreach (var provider in providers)
        {
            if (!provider.Enabled && option.Provider is null)
            {
                continue;
            }

            try
            {
                results.AddRange(await client.SearchAsync(provider, query, SearchCategories).ConfigureAwait(true));
            }
            catch (Exception exception)
            {
                _lastSearchFailedProviders++;
                LogError("Search", "Search provider failed.", exception, provider.Name);
            }
        }

        return results;
    }

    private IEnumerable<SearchResult> ApplySearchFilters(IEnumerable<SearchResult> results)
    {
        var maxSizeBytes = SearchMaxSizeGb > 0
            ? SearchMaxSizeGb * 1024L * 1024L * 1024L
            : 0;

        var categories = SearchCategories
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return results
            .Where(result => SearchMinSeeders <= 0 || result.Seeders >= SearchMinSeeders)
            .Where(result => maxSizeBytes <= 0 || result.SizeBytes <= 0 || result.SizeBytes <= maxSizeBytes)
            .Where(result => categories.Count == 0 ||
                string.IsNullOrWhiteSpace(result.Category) ||
                categories.Contains(result.Category))
            .OrderByDescending(result => result.Seeders)
            .ThenByDescending(result => result.PublishedAt);
    }

    private async Task SaveSearchHistoryAsync(string query)
    {
        var existing = SearchHistory.FirstOrDefault(item => string.Equals(item, query, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SearchHistory.Remove(existing);
        }

        SearchHistory.Insert(0, query);
        while (SearchHistory.Count > 20)
        {
            SearchHistory.RemoveAt(SearchHistory.Count - 1);
        }

        _settings.SearchHistory = SearchHistory.ToList();
        _settings.SearchProviders = SearchProviders.ToList();
        _settings.ActiveServerId = SelectedServer?.Id;

        try
        {
            await _settingsStore.SaveAsync(_settings).ConfigureAwait(true);
        }
            catch
            {
                // Search history is a convenience feature; failed persistence must not break search.
                LogWarning("Search", "Search history was not saved.");
            }
    }

    private void RebuildSearchProviderOptions()
    {
        var selected = SelectedSearchProviderOption;
        SearchProviderOptions.Clear();
        SearchProviderOptions.Add(SearchProviderOption.ForSelectedServer(L["SearchModeSelectedServer"]));
        SearchProviderOptions.Add(SearchProviderOption.ForAllProviders(L["SearchModeAllProviders"]));

        foreach (var provider in SearchProviders)
        {
            SearchProviderOptions.Add(SearchProviderOption.ForProvider(provider));
        }

        SelectedSearchProviderOption = SearchProviderOptions.FirstOrDefault(option =>
                selected is not null && option.Matches(selected)) ??
            SearchProviderOptions.FirstOrDefault();
    }

    private async Task<TorrServerRelease> FetchLatestTorrServerReleaseAsync(GitHubReleaseService releases)
    {
        StatusMessage = L["StatusCheckingTorrServerUpdate"];
        var release = await releases.GetLatestTorrServerReleaseAsync().ConfigureAwait(true);
        _latestTorrServerRelease = release;
        UpdateTorrServerReleaseText();
        return release;
    }

    private void UpdateTorrServerReleaseText()
    {
        var installed = EmptyAsNotAvailable(LocalServer.InstalledVersion);
        var previous = EmptyAsNotAvailable(LocalServer.PreviousVersion);

        TorrServerReleaseText = _latestTorrServerRelease is null
            ? string.Format(L["TorrServerReleaseInfoUnchecked"], installed, previous)
            : string.Format(
                L["TorrServerReleaseInfo"],
                installed,
                _latestTorrServerRelease.Version,
                _latestTorrServerRelease.AssetName,
                TorrentItem.FormatBytes(_latestTorrServerRelease.SizeBytes),
                previous);
    }

    private bool IsInstalledTorrServerVersion(TorrServerRelease release)
    {
        return string.Equals(
            NormalizeReleaseVersion(LocalServer.InstalledVersion),
            NormalizeReleaseVersion(release.Version),
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadTorrServerReleasesAsync()
    {
        try
        {
            StatusMessage = L["StatusLoadingTorrServerReleases"];
            var selectedVersion = SelectedTorrServerRelease?.Version;
            var releases = await new GitHubReleaseService().GetTorrServerReleasesAsync().ConfigureAwait(true);

            TorrServerReleases.Clear();
            foreach (var release in releases)
            {
                TorrServerReleases.Add(new TorrServerReleaseItem(release));
            }

            _latestTorrServerRelease = releases.FirstOrDefault();
            UpdateTorrServerReleaseText();
            SelectedTorrServerRelease = TorrServerReleases.FirstOrDefault(item =>
                    string.Equals(item.Version, selectedVersion, StringComparison.OrdinalIgnoreCase)) ??
                TorrServerReleases.FirstOrDefault();

            StatusMessage = TorrServerReleases.Count == 0
                ? L["StatusNoTorrServerReleases"]
                : string.Format(L["StatusTorrServerReleasesLoaded"], TorrServerReleases.Count);
            LogInfo("TorrServer", "TorrServer releases loaded.", TorrServerReleases.Count.ToString());
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("TorrServer", "Failed to load TorrServer releases.", exception);
        }
    }

    private async Task CheckTorrServerUpdateAsync()
    {
        try
        {
            var releases = new GitHubReleaseService();
            var release = await FetchLatestTorrServerReleaseAsync(releases).ConfigureAwait(true);
            StatusMessage = IsInstalledTorrServerVersion(release)
                ? string.Format(L["StatusTorrServerUpToDate"], release.Version)
                : string.Format(
                    L["StatusTorrServerUpdateAvailable"],
                    EmptyAsNotAvailable(LocalServer.InstalledVersion),
                    release.Version);
            LogInfo("TorrServer", "TorrServer update check completed.", $"{LocalServer.InstalledVersion} -> {release.Version}");
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("TorrServer", "Failed to check TorrServer update.", exception);
        }
    }

    private async Task DownloadTorrServerAsync()
    {
        try
        {
            var releases = new GitHubReleaseService();
            var release = await FetchLatestTorrServerReleaseAsync(releases).ConfigureAwait(true);
            await DownloadTorrServerReleaseAsync(releases, release).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("TorrServer", "Failed to download TorrServer.", exception);
        }
    }

    private async Task DownloadSelectedTorrServerReleaseAsync()
    {
        if (SelectedTorrServerRelease is null)
        {
            StatusMessage = L["StatusNoTorrServerReleaseSelected"];
            return;
        }

        try
        {
            await DownloadTorrServerReleaseAsync(new GitHubReleaseService(), SelectedTorrServerRelease.Release).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("TorrServer", "Failed to download selected TorrServer release.", exception);
        }
    }

    private async Task DownloadTorrServerReleaseAsync(GitHubReleaseService releases, TorrServerRelease release)
    {
        AppPaths.EnsureProgramDataDirectories();
        var destination = Path.Combine(
            AppPaths.DefaultLocalServerDirectory,
            "versions",
            SanitizePathSegment(release.Version),
            release.AssetName);

        var nextProgressReport = 0L;
        var progress = new Progress<long>(bytes =>
        {
            if (bytes < nextProgressReport && bytes < release.SizeBytes)
            {
                return;
            }

            nextProgressReport = bytes + 1024 * 1024;
            StatusMessage = string.Format(
                L["StatusTorrServerDownloadProgress"],
                release.Version,
                TorrentItem.FormatBytes(bytes),
                TorrentItem.FormatBytes(release.SizeBytes));
        });

        await releases.DownloadAsync(release.DownloadUrl, destination, progress).ConfigureAwait(true);

        if (!string.IsNullOrWhiteSpace(LocalServer.ExecutablePath) &&
            !string.Equals(LocalServer.ExecutablePath, destination, StringComparison.OrdinalIgnoreCase))
        {
            LocalServer.PreviousExecutablePath = LocalServer.ExecutablePath;
            LocalServer.PreviousVersion = LocalServer.InstalledVersion;
        }

        LocalServer.ExecutablePath = destination;
        LocalServer.InstalledVersion = release.Version;
        OnPropertyChanged(nameof(LocalServer));
        UpdateTorrServerReleaseText();
        await SaveSettingsAsync().ConfigureAwait(true);
        StatusMessage = string.Format(L["StatusTorrServerDownloaded"], release.Version);
        LogInfo("TorrServer", "TorrServer downloaded.", $"{release.Version}: {destination}");
    }

    private async Task RollbackTorrServerAsync()
    {
        if (string.IsNullOrWhiteSpace(LocalServer.PreviousExecutablePath) ||
            !File.Exists(LocalServer.PreviousExecutablePath))
        {
            StatusMessage = L["StatusNoRollbackVersion"];
            LogWarning("TorrServer", "Rollback requested but previous version is unavailable.");
            return;
        }

        (LocalServer.ExecutablePath, LocalServer.PreviousExecutablePath) =
            (LocalServer.PreviousExecutablePath, LocalServer.ExecutablePath);

        (LocalServer.InstalledVersion, LocalServer.PreviousVersion) =
            (LocalServer.PreviousVersion, LocalServer.InstalledVersion);

        OnPropertyChanged(nameof(LocalServer));
        UpdateTorrServerReleaseText();
        await SaveSettingsAsync().ConfigureAwait(true);
        StatusMessage = string.Format(L["StatusTorrServerRolledBack"], LocalServer.InstalledVersion);
        LogInfo("TorrServer", "TorrServer rolled back.", LocalServer.InstalledVersion);
    }

    private async Task ApplyLocalServerSettingsAsync()
    {
        if (SelectedServer is null)
        {
            StatusMessage = L["NoServerSelected"];
            return;
        }

        try
        {
            using var client = new TorrServerClient(SelectedServer);
            await client.ApplyLocalServerSettingsAsync(LocalServer).ConfigureAwait(true);
            StatusMessage = L["StatusRuntimeSettingsApplied"];
            LogInfo("TorrServer", "Runtime settings applied.", SelectedServer.Name);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("TorrServer", "Failed to apply runtime settings.", exception, SelectedServer?.Name ?? string.Empty);
        }
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }

    private static string NormalizeReleaseVersion(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().TrimStart('v', 'V');
    }

    private async Task StartLocalServerAsync()
    {
        try
        {
            await _localServerProcess.StartAsync(LocalServer).ConfigureAwait(true);
            StatusMessage = L["StatusLocalServerStarted"];
            LogInfo("LocalServer", "Local TorrServer started.", LocalServer.ExecutablePath);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("LocalServer", "Failed to start local TorrServer.", exception, LocalServer.ExecutablePath);
        }
    }

    private async Task StartLocalServerIfConfiguredAsync()
    {
        if (!LocalServer.Enabled ||
            LocalServer.RunAsWindowsService ||
            string.IsNullOrWhiteSpace(LocalServer.ExecutablePath))
        {
            return;
        }

        await StartLocalServerAsync().ConfigureAwait(true);
    }

    private void StopLocalServer()
    {
        try
        {
            _localServerProcess.Stop();
            StatusMessage = L["StatusLocalServerStopped"];
            LogInfo("LocalServer", "Local TorrServer stopped.");
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("LocalServer", "Failed to stop local TorrServer.", exception);
        }
    }

    private async Task InstallServiceAsync()
    {
        try
        {
            await SaveSettingsAsync().ConfigureAwait(true);
            var serviceExe = Path.Combine(AppContext.BaseDirectory, "TorrWind.Service.exe");
            await new WindowsServiceManager().InstallAsync(serviceExe).ConfigureAwait(true);
            await UpdateServiceStatusAsync(updateStatusMessage: false).ConfigureAwait(true);
            StatusMessage = L["StatusServiceInstalled"];
            LogInfo("Service", "Windows service installed.", serviceExe);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Service", "Failed to install Windows service.", exception);
        }
    }

    private async Task UninstallServiceAsync()
    {
        try
        {
            await new WindowsServiceManager().UninstallAsync().ConfigureAwait(true);
            await UpdateServiceStatusAsync(updateStatusMessage: false).ConfigureAwait(true);
            StatusMessage = L["StatusServiceUninstalled"];
            LogInfo("Service", "Windows service uninstalled.");
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Service", "Failed to uninstall Windows service.", exception);
        }
    }

    private async Task StartServiceAsync()
    {
        try
        {
            await SaveSettingsAsync().ConfigureAwait(true);
            await new WindowsServiceManager().StartAsync().ConfigureAwait(true);
            await UpdateServiceStatusAsync(updateStatusMessage: false).ConfigureAwait(true);
            StatusMessage = L["StatusServiceStarted"];
            LogInfo("Service", "Windows service start requested.");
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Service", "Failed to start Windows service.", exception);
        }
    }

    private async Task StopServiceAsync()
    {
        try
        {
            await new WindowsServiceManager().StopAsync().ConfigureAwait(true);
            await UpdateServiceStatusAsync(updateStatusMessage: false).ConfigureAwait(true);
            StatusMessage = L["StatusServiceStopped"];
            LogInfo("Service", "Windows service stop requested.");
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Service", "Failed to stop Windows service.", exception);
        }
    }

    private async Task QueryServiceStatusAsync()
    {
        await UpdateServiceStatusAsync(updateStatusMessage: true).ConfigureAwait(true);
    }

    private async Task UpdateServiceStatusAsync(bool updateStatusMessage)
    {
        try
        {
            var service = await new WindowsServiceManager().QueryStatusAsync().ConfigureAwait(true);
            ServiceStatusText = service.IsInstalled
                ? string.Format(L["ServiceStatusInstalled"], service.State)
                : L["ServiceStatusNotInstalled"];

            if (updateStatusMessage)
            {
                StatusMessage = string.Format(L["StatusServiceStatus"], ServiceStatusText);
                LogInfo("Service", "Windows service status queried.", ServiceStatusText);
            }
        }
        catch (Exception exception)
        {
            ServiceStatusText = exception.Message;
            if (updateStatusMessage)
            {
                StatusMessage = exception.Message;
                LogError("Service", "Failed to query Windows service status.", exception);
            }
        }
    }

    private static bool IsControlArgument(string arg)
    {
        return string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(arg, "/minimized", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTorrentFile(string arg)
    {
        return string.Equals(Path.GetExtension(arg), ".torrent", StringComparison.OrdinalIgnoreCase) &&
               File.Exists(arg);
    }

    private static bool IsTorrentLink(string arg)
    {
        if (arg.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) ||
            arg.StartsWith("torrs://", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(arg, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
               uri.AbsolutePath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _localServerProcess.Dispose();
    }
}

public sealed class SearchProviderOption
{
    private SearchProviderOption(string displayName, SearchProviderSettings? provider, bool useSelectedServer, bool useAllProviders)
    {
        DisplayName = displayName;
        Provider = provider;
        UseSelectedServer = useSelectedServer;
        UseAllProviders = useAllProviders;
    }

    public string DisplayName { get; }

    public SearchProviderSettings? Provider { get; }

    public bool UseSelectedServer { get; }

    public bool UseAllProviders { get; }

    public static SearchProviderOption ForSelectedServer(string displayName)
    {
        return new SearchProviderOption(displayName, null, true, false);
    }

    public static SearchProviderOption ForAllProviders(string displayName)
    {
        return new SearchProviderOption(displayName, null, false, true);
    }

    public static SearchProviderOption ForProvider(SearchProviderSettings provider)
    {
        return new SearchProviderOption(provider.Name, provider, false, false);
    }

    public bool Matches(SearchProviderOption other)
    {
        if (UseSelectedServer || other.UseSelectedServer)
        {
            return UseSelectedServer == other.UseSelectedServer;
        }

        if (UseAllProviders || other.UseAllProviders)
        {
            return UseAllProviders == other.UseAllProviders;
        }

        return Provider?.Id == other.Provider?.Id;
    }
}

public sealed class DiagnosticItem
{
    public DiagnosticItem(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public string Value { get; }
}

public sealed class SettingsBackupItem
{
    public SettingsBackupItem(string filePath, DateTimeOffset modifiedAt, long sizeBytes)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        ModifiedAt = modifiedAt;
        SizeBytes = sizeBytes;
    }

    public string FileName { get; }

    public string FilePath { get; }

    public DateTimeOffset ModifiedAt { get; }

    public long SizeBytes { get; }

    public string ModifiedAtText => ModifiedAt.ToString("yyyy-MM-dd HH:mm:ss");

    public string SizeText => FormatBytes(SizeBytes);

    private static string FormatBytes(long value)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var size = (double)Math.Max(0, value);
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{size:0} {units[unit]}"
            : $"{size:0.##} {units[unit]}";
    }
}

public sealed class TorrServerReleaseItem
{
    public TorrServerReleaseItem(TorrServerRelease release)
    {
        Release = release;
    }

    public TorrServerRelease Release { get; }

    public string Version => Release.Version;

    public string AssetName => Release.AssetName;

    public string PublishedAtText => Release.PublishedAt == default
        ? string.Empty
        : Release.PublishedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public string SizeText => TorrentItem.FormatBytes(Release.SizeBytes);
}
