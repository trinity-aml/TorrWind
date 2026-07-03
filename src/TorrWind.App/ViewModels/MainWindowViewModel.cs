using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
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
    private readonly AsyncRelayCommand _useSelectedInstalledTorrServerCommand;
    private readonly RelayCommand _deleteSelectedInstalledTorrServerCommand;
    private readonly RelayCommand _openSelectedInstalledTorrServerFolderCommand;
    private readonly RelayCommand _openDownloadedTorrWindUpdateCommand;
    private AppSettings _settings = AppSettings.CreateDefault();
    private TorrServerRelease? _latestTorrServerRelease;
    private TorrWindRelease? _latestTorrWindRelease;
    private ServerProfile? _selectedServer;
    private TorrentItem? _selectedTorrent;
    private TorrentFile? _selectedTorrentFile;
    private SearchResult? _selectedSearchResult;
    private SearchProviderSettings? _selectedSearchProvider;
    private SearchProviderOption? _selectedSearchProviderOption;
    private SettingsBackupItem? _selectedSettingsBackup;
    private TorrServerReleaseItem? _selectedTorrServerRelease;
    private InstalledTorrServerItem? _selectedInstalledTorrServer;
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
    private DateTime? _searchPublishedFrom;
    private DateTime? _searchPublishedTo;
    private int _lastSearchFailedProviders;
    private string _statusMessage = string.Empty;
    private string _logLocationText = string.Empty;
    private string _runtimeSettingsJson = string.Empty;
    private string _serviceStatusText = string.Empty;
    private string _torrServerReleaseText = string.Empty;
    private string _torrWindUpdateText = string.Empty;
    private string _downloadedTorrWindUpdatePath = string.Empty;
    private bool _isRefreshingLibrary;
    private bool _isRefreshingSelectedTorrentLive;
    private bool _isApplyingLanguage;
    private bool _suppressLanguageApply;
    private bool _suppressSelectedTorrentDetailsRefresh;
    private int _selectedTorrentDetailsRequestVersion;
    private const int ClipboardCannotOpen = unchecked((int)0x800401D0);
    private const uint ClipboardFormatUnicodeText = 13;
    private const uint GlobalMemoryMoveable = 0x0002;
    private const uint GlobalMemoryZeroInit = 0x0040;
    private const string AppRepositoryUrlValue = "https://github.com/trinity-aml/TorrWind";
    private const string AppReadmeUrlValue = "https://github.com/trinity-aml/TorrWind#readme";
    private const string AppLicenseNameValue = "GPL-3.0-only";
    private static readonly int[] ClipboardRetryDelaysMs = [0, 25, 50, 75, 100, 150, 250, 400, 650, 900, 1200];

    public MainWindowViewModel(AppSettingsStore settingsStore, JsonLocalizationService localization)
    {
        _settingsStore = settingsStore;
        _localization = localization;

        RefreshCommand = CreateAsyncCommand(RefreshAsync);
        AddMagnetCommand = CreateAsyncCommand(AddMagnetAsync);
        RemoveTorrentCommand = CreateAsyncCommand(RemoveTorrentAsync);
        RefreshSelectedTorrentCommand = CreateAsyncCommand(RefreshSelectedTorrentAsync);
        SaveTorrentMetadataCommand = CreateAsyncCommand(SaveTorrentMetadataAsync);
        DropTorrentCommand = CreateAsyncCommand(DropTorrentAsync);
        WipeTorrentsCommand = CreateAsyncCommand(WipeTorrentsAsync);
        SearchCommand = CreateAsyncCommand(SearchAsync);
        AddSelectedSearchResultCommand = CreateAsyncCommand(AddSelectedSearchResultAsync);
        OpenSelectedCommand = CreateAsyncCommand(OpenSelectedAsync);
        OpenContinuePlaylistCommand = CreateAsyncCommand(OpenContinuePlaylistAsync);
        OpenPlaylistFromSelectedCommand = CreateAsyncCommand(OpenPlaylistFromSelectedAsync);
        CopyPlaybackUrlCommand = CreateCommand(CopyPlaybackUrl);
        CopyTorrentSourceCommand = CreateCommand(CopyTorrentSource);
        CopyTorrentHashCommand = CreateCommand(CopyTorrentHash);
        CheckServerCommand = CreateAsyncCommand(CheckServerAsync);
        RunDiagnosticsCommand = CreateAsyncCommand(RunDiagnosticsAsync);
        CopyDiagnosticsCommand = CreateCommand(CopyDiagnostics);
        LoadRuntimeSettingsCommand = CreateAsyncCommand(LoadRuntimeSettingsAsync);
        ApplyRuntimeSettingsJsonCommand = CreateAsyncCommand(ApplyRuntimeSettingsJsonAsync);
        FormatRuntimeSettingsJsonCommand = CreateCommand(FormatRuntimeSettingsJson);
        CopyRuntimeSettingsJsonCommand = CreateCommand(CopyRuntimeSettingsJson);
        RefreshLogsCommand = CreateAsyncCommand(RefreshLogsAsync);
        ClearUserLogCommand = CreateCommand(ClearUserLog);
        CopyLogPathsCommand = CreateCommand(CopyLogPaths);
        OpenLogFoldersCommand = CreateCommand(OpenLogFolders);
        SaveSettingsCommand = CreateAsyncCommand(SaveSettingsAsync);
        ApplyLanguageCommand = CreateAsyncCommand(ApplyLanguageAsync);
        OpenTorrServerExecutableFolderCommand = CreateCommand(OpenTorrServerExecutableFolder);
        OpenLocalServerDataFolderCommand = CreateCommand(OpenLocalServerDataFolder);
        OpenLocalServerCacheFolderCommand = CreateCommand(OpenLocalServerCacheFolder);
        CheckTorrServerUpdateCommand = CreateAsyncCommand(CheckTorrServerUpdateAsync);
        DownloadTorrServerCommand = CreateAsyncCommand(DownloadTorrServerAsync);
        LoadTorrServerReleasesCommand = CreateAsyncCommand(LoadTorrServerReleasesAsync);
        _downloadSelectedTorrServerReleaseCommand = CreateAsyncCommand(DownloadSelectedTorrServerReleaseAsync, () => SelectedTorrServerRelease is not null);
        DownloadSelectedTorrServerReleaseCommand = _downloadSelectedTorrServerReleaseCommand;
        RefreshInstalledTorrServersCommand = CreateCommand(RefreshInstalledTorrServers);
        OpenTorrServerVersionsFolderCommand = CreateCommand(OpenTorrServerVersionsFolder);
        _useSelectedInstalledTorrServerCommand = CreateAsyncCommand(UseSelectedInstalledTorrServerAsync, () => SelectedInstalledTorrServer is not null);
        UseSelectedInstalledTorrServerCommand = _useSelectedInstalledTorrServerCommand;
        _deleteSelectedInstalledTorrServerCommand = CreateCommand(DeleteSelectedInstalledTorrServer, () => SelectedInstalledTorrServer is not null);
        DeleteSelectedInstalledTorrServerCommand = _deleteSelectedInstalledTorrServerCommand;
        _openSelectedInstalledTorrServerFolderCommand = CreateCommand(OpenSelectedInstalledTorrServerFolder, () => SelectedInstalledTorrServer is not null);
        OpenSelectedInstalledTorrServerFolderCommand = _openSelectedInstalledTorrServerFolderCommand;
        RollbackTorrServerCommand = CreateAsyncCommand(RollbackTorrServerAsync);
        CheckTorrWindUpdateCommand = CreateAsyncCommand(CheckTorrWindUpdateAsync);
        DownloadTorrWindUpdateCommand = CreateAsyncCommand(DownloadTorrWindUpdateAsync);
        OpenTorrWindUpdatesFolderCommand = CreateCommand(OpenTorrWindUpdatesFolder);
        _openDownloadedTorrWindUpdateCommand = CreateCommand(OpenDownloadedTorrWindUpdate, () => File.Exists(_downloadedTorrWindUpdatePath));
        OpenDownloadedTorrWindUpdateCommand = _openDownloadedTorrWindUpdateCommand;
        ApplyLocalServerSettingsCommand = CreateAsyncCommand(ApplyLocalServerSettingsAsync);
        InstallServiceCommand = CreateAsyncCommand(InstallServiceAsync);
        UninstallServiceCommand = CreateAsyncCommand(UninstallServiceAsync);
        StartServiceCommand = CreateAsyncCommand(StartServiceAsync);
        StopServiceCommand = CreateAsyncCommand(StopServiceAsync);
        QueryServiceStatusCommand = CreateAsyncCommand(QueryServiceStatusAsync);
        StartLocalServerCommand = CreateAsyncCommand(StartLocalServerAsync);
        StopLocalServerCommand = CreateCommand(StopLocalServer);
        AddServerCommand = CreateCommand(AddServer);
        _removeServerCommand = CreateCommand(RemoveSelectedServer, () => Servers.Count > 1);
        RemoveServerCommand = _removeServerCommand;
        AddSearchProviderCommand = CreateCommand(AddSearchProvider);
        _removeSearchProviderCommand = CreateCommand(RemoveSelectedSearchProvider, () => SelectedSearchProvider is not null);
        RemoveSearchProviderCommand = _removeSearchProviderCommand;
        RefreshSettingsBackupsCommand = CreateCommand(RefreshSettingsBackups);
        OpenSettingsBackupsFolderCommand = CreateCommand(OpenSettingsBackupsFolder);
        _restoreSelectedSettingsBackupCommand = CreateAsyncCommand(RestoreSelectedSettingsBackupAsync, () => SelectedSettingsBackup is not null);
        RestoreSelectedSettingsBackupCommand = _restoreSelectedSettingsBackupCommand;
        _deleteSelectedSettingsBackupCommand = CreateCommand(DeleteSelectedSettingsBackup, () => SelectedSettingsBackup is not null);
        DeleteSelectedSettingsBackupCommand = _deleteSelectedSettingsBackupCommand;
    }

    private RelayCommand CreateCommand(Action execute, Func<bool>? canExecute = null)
    {
        return new RelayCommand(execute, canExecute, HandleCommandException);
    }

    private AsyncRelayCommand CreateAsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        return new AsyncRelayCommand(execute, canExecute, HandleCommandException);
    }

    public JsonLocalizationService L => _localization;

    public event EventHandler<BuiltInPlayerRequest>? BuiltInPlayerRequested;

    public string AppVersion => ResolveAppVersion();

    public string AppVersionBadge => "v" + AppVersion;

    public string AppDisplayName => "TorrWind " + AppVersionBadge;

    public string AppRepositoryUrl => AppRepositoryUrlValue;

    public string AppReadmeUrl => AppReadmeUrlValue;

    public string AppLicenseName => AppLicenseNameValue;

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

    public ObservableCollection<InstalledTorrServerItem> InstalledTorrServers { get; } = [];

    public ObservableCollection<string> AvailableLanguages { get; } = [];

    public ObservableCollection<PlayerKindOption> PlayerKindOptions { get; } = [];

    public ObservableCollection<RetrackersModeOption> RetrackersModeOptions { get; } = [];

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

    public ICommand OpenContinuePlaylistCommand { get; }

    public ICommand OpenPlaylistFromSelectedCommand { get; }

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

    public ICommand OpenLogFoldersCommand { get; }

    public ICommand SaveSettingsCommand { get; }

    public ICommand ApplyLanguageCommand { get; }

    public ICommand OpenTorrServerExecutableFolderCommand { get; }

    public ICommand OpenLocalServerDataFolderCommand { get; }

    public ICommand OpenLocalServerCacheFolderCommand { get; }

    public ICommand CheckTorrServerUpdateCommand { get; }

    public ICommand DownloadTorrServerCommand { get; }

    public ICommand LoadTorrServerReleasesCommand { get; }

    public ICommand DownloadSelectedTorrServerReleaseCommand { get; }

    public ICommand RefreshInstalledTorrServersCommand { get; }

    public ICommand OpenTorrServerVersionsFolderCommand { get; }

    public ICommand UseSelectedInstalledTorrServerCommand { get; }

    public ICommand DeleteSelectedInstalledTorrServerCommand { get; }

    public ICommand OpenSelectedInstalledTorrServerFolderCommand { get; }

    public ICommand RollbackTorrServerCommand { get; }

    public ICommand CheckTorrWindUpdateCommand { get; }

    public ICommand DownloadTorrWindUpdateCommand { get; }

    public ICommand OpenTorrWindUpdatesFolderCommand { get; }

    public ICommand OpenDownloadedTorrWindUpdateCommand { get; }

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

    public ICommand OpenSettingsBackupsFolderCommand { get; }

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
            if (ReferenceEquals(_selectedServer, value))
            {
                return;
            }

            if (_selectedServer is not null)
            {
                _selectedServer.PropertyChanged -= OnSelectedServerPropertyChanged;
            }

            if (SetProperty(ref _selectedServer, value))
            {
                if (_selectedServer is not null)
                {
                    _selectedServer.PropertyChanged += OnSelectedServerPropertyChanged;
                }

                _settings.ActiveServerId = value?.Id;
                OnPropertyChanged(nameof(ActiveServerLabel));
            }
        }
    }

    private void OnSelectedServerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName) ||
            string.Equals(e.PropertyName, nameof(ServerProfile.Name), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(ActiveServerLabel));
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
                OnPropertyChanged(nameof(SelectedTorrentPosterImageUri));
                OnPropertyChanged(nameof(HasSelectedTorrentPoster));
                SelectedTorrentFile = value?.Files.Count == 1 ? value.Files[0] : null;
                LoadSelectedTorrentEditor(value);
                QueueSelectedTorrentDetailsRefresh(value);
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

    public InstalledTorrServerItem? SelectedInstalledTorrServer
    {
        get => _selectedInstalledTorrServer;
        set
        {
            if (SetProperty(ref _selectedInstalledTorrServer, value))
            {
                _useSelectedInstalledTorrServerCommand.RaiseCanExecuteChanged();
                _deleteSelectedInstalledTorrServerCommand.RaiseCanExecuteChanged();
                _openSelectedInstalledTorrServerFolderCommand.RaiseCanExecuteChanged();
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
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                _ = ApplySelectedLanguageAsync();
            }
        }
    }

    public string NewMagnet
    {
        get => _newMagnet;
        set => SetProperty(ref _newMagnet, value);
    }

    public string SelectedTorrentPosterImageUri => SelectedTorrent?.Poster ?? string.Empty;

    public bool HasSelectedTorrentPoster => !string.IsNullOrWhiteSpace(SelectedTorrent?.Poster);

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

    public DateTime? SearchPublishedFrom
    {
        get => _searchPublishedFrom;
        set => SetProperty(ref _searchPublishedFrom, value?.Date);
    }

    public DateTime? SearchPublishedTo
    {
        get => _searchPublishedTo;
        set => SetProperty(ref _searchPublishedTo, value?.Date);
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

    public string TorrWindUpdateText
    {
        get => _torrWindUpdateText;
        set => SetProperty(ref _torrWindUpdateText, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(true);

        await ApplySettingsToViewAsync(cancellationToken).ConfigureAwait(true);
        UpdateTorrWindUpdateText();
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

    public string CreateDiagnosticsFileName()
    {
        return "TorrWind-diagnostics-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
    }

    public string CreateSupportBundleFileName()
    {
        return "TorrWind-support-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss") + ".zip";
    }

    public async Task SaveDiagnosticsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (DiagnosticItems.Count == 0)
        {
            StatusMessage = L["StatusNoDiagnostics"];
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(filePath, BuildDiagnosticsReport(includeHeader: true), cancellationToken)
                .ConfigureAwait(true);
            StatusMessage = string.Format(L["StatusDiagnosticsSaved"], filePath);
            LogInfo("Diagnostics", "Diagnostics report saved.", filePath);
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(L["StatusDiagnosticsSaveFailed"], exception.Message);
            LogError("Diagnostics", "Failed to save diagnostics report.", exception, filePath);
        }
    }

    public async Task SaveSupportBundleAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (DiagnosticItems.Count == 0)
        {
            StatusMessage = L["StatusNoDiagnostics"];
            return;
        }

        try
        {
            SyncSettingsFromView();
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);
            await AddTextEntryAsync(archive, "diagnostics.txt", BuildDiagnosticsReport(includeHeader: true), cancellationToken)
                .ConfigureAwait(true);
            await AddTextEntryAsync(archive, "settings.sanitized.json", BuildSanitizedSettingsJson(), cancellationToken)
                .ConfigureAwait(true);
            await AddTextEntryAsync(archive, "manifest.txt", BuildSupportBundleManifest(), cancellationToken)
                .ConfigureAwait(true);
            await AddFileEntryIfExistsAsync(archive, AppPaths.UserLogFile, "logs/gui.jsonl", cancellationToken)
                .ConfigureAwait(true);
            await AddFileEntryIfExistsAsync(archive, AppPaths.UserLogFile + ".1", "logs/gui.jsonl.1", cancellationToken)
                .ConfigureAwait(true);
            await AddFileEntryIfExistsAsync(archive, AppPaths.ServiceLogFile, "logs/service.jsonl", cancellationToken)
                .ConfigureAwait(true);
            await AddFileEntryIfExistsAsync(archive, AppPaths.ServiceLogFile + ".1", "logs/service.jsonl.1", cancellationToken)
                .ConfigureAwait(true);
            await AddFileEntryIfExistsAsync(archive, AppPaths.MpvPlayerLogFile, "logs/mpv-player.log", cancellationToken)
                .ConfigureAwait(true);

            StatusMessage = string.Format(L["StatusSupportBundleSaved"], filePath);
            LogInfo("Diagnostics", "Support bundle saved.", filePath);
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(L["StatusSupportBundleSaveFailed"], exception.Message);
            LogError("Diagnostics", "Failed to save support bundle.", exception, filePath);
        }
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

        _suppressLanguageApply = true;
        try
        {
            SelectedLanguage = _settings.Language;
        }
        finally
        {
            _suppressLanguageApply = false;
        }

        await _localization.LoadAsync(_settings.Language, cancellationToken).ConfigureAwait(true);
        RebuildPlayerKindOptions();
        RebuildRetrackersModeOptions();

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
        RefreshInstalledTorrServers(updateStatusMessage: false);
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
        if (_isRefreshingLibrary)
        {
            return;
        }

        if (SelectedServer is null)
        {
            StatusMessage = L["NoServerSelected"];
            return;
        }

        _isRefreshingLibrary = true;
        try
        {
            var selectedHash = SelectedTorrent?.Hash;
            using var client = new TorrServerClient(SelectedServer);
            var torrents = await client.GetTorrentsAsync().ConfigureAwait(true);

            Torrents.Clear();
            foreach (var torrent in torrents)
            {
                Torrents.Add(torrent);
            }

            SelectedTorrent = Torrents.FirstOrDefault(torrent =>
                    !string.IsNullOrWhiteSpace(selectedHash) &&
                    string.Equals(torrent.Hash, selectedHash, StringComparison.OrdinalIgnoreCase)) ??
                Torrents.FirstOrDefault();

            try
            {
                await RefreshSelectedTorrentDetailsAsync(client, updateStatusMessage: false).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                LogError("Library", "Failed to refresh selected torrent details.", exception, SelectedTorrent?.Title ?? string.Empty);
            }

            StatusMessage = string.Format(L["StatusTorrentsLoaded"], Torrents.Count);
            LogInfo("Library", "Torrent list refreshed.", $"{SelectedServer.Name}: {Torrents.Count}");
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Library", "Failed to refresh torrent list.", exception, SelectedServer.Name);
        }
        finally
        {
            _isRefreshingLibrary = false;
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
            var previousHashes = CurrentTorrentHashes();
            using var client = new TorrServerClient(SelectedServer);
            var addedTorrents = await client.AddTorrentFileAsync(filePath).ConfigureAwait(true);
            await RefreshAfterTorrentAddedAsync(client, addedTorrents, previousHashes).ConfigureAwait(true);
            StatusMessage = L["StatusTorrentAdded"];
            LogInfo("Library", "Torrent file added.", Path.GetFileName(filePath));
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
            var previousHashes = CurrentTorrentHashes();
            using var client = new TorrServerClient(SelectedServer);
            var addedTorrent = await client.AddMagnetAsync(link.Trim()).ConfigureAwait(true);
            await RefreshAfterTorrentAddedAsync(client, [addedTorrent], previousHashes).ConfigureAwait(true);
            StatusMessage = L["StatusTorrentAdded"];
            LogInfo("Library", "Torrent link added.", SelectedServer.Name);
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
            await RefreshSelectedTorrentDetailsAsync(client, updateStatusMessage: true).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Library", "Failed to refresh torrent details.", exception, SelectedTorrent.Title);
        }
    }

    public async Task RefreshSelectedTorrentLiveAsync()
    {
        if (_isRefreshingLibrary ||
            _isRefreshingSelectedTorrentLive ||
            SelectedServer is null ||
            SelectedTorrent is null ||
            string.IsNullOrWhiteSpace(SelectedTorrent.Hash))
        {
            return;
        }

        _isRefreshingSelectedTorrentLive = true;
        try
        {
            using var client = new TorrServerClient(SelectedServer);
            await RefreshSelectedTorrentDetailsAsync(
                client,
                updateStatusMessage: false,
                logRefresh: false).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            LogWarning("Library", "Live torrent details refresh failed.", exception.Message);
        }
        finally
        {
            _isRefreshingSelectedTorrentLive = false;
        }
    }

    private async Task RefreshSelectedTorrentDetailsAsync(
        TorrServerClient client,
        bool updateStatusMessage,
        bool logRefresh = true)
    {
        if (SelectedTorrent is null || string.IsNullOrWhiteSpace(SelectedTorrent.Hash))
        {
            return;
        }

        var selectedFileId = SelectedTorrentFile?.Id;
        var updated = await client.GetTorrentAsync(SelectedTorrent.Hash).ConfigureAwait(true);
        ReplaceSelectedTorrent(updated, selectedFileId);

        if (updateStatusMessage)
        {
            StatusMessage = L["StatusTorrentDetailsLoaded"];
        }

        if (logRefresh)
        {
            LogInfo("Library", "Torrent details refreshed.", updated.Title);
        }
    }

    private void QueueSelectedTorrentDetailsRefresh(TorrentItem? torrent)
    {
        var requestVersion = ++_selectedTorrentDetailsRequestVersion;
        var server = SelectedServer;
        if (_suppressSelectedTorrentDetailsRefresh ||
            _isRefreshingLibrary ||
            _isRefreshingSelectedTorrentLive ||
            server is null ||
            torrent is null ||
            string.IsNullOrWhiteSpace(torrent.Hash))
        {
            return;
        }

        _ = RefreshSelectedTorrentDetailsFromApiAsync(server, torrent.Hash, requestVersion);
    }

    private async Task RefreshSelectedTorrentDetailsFromApiAsync(ServerProfile server, string hash, int requestVersion)
    {
        try
        {
            using var client = new TorrServerClient(server);
            var updated = await client.GetTorrentAsync(hash).ConfigureAwait(true);
            if (requestVersion != _selectedTorrentDetailsRequestVersion ||
                SelectedServer is null ||
                SelectedServer.Id != server.Id ||
                SelectedTorrent is null ||
                !string.Equals(SelectedTorrent.Hash, hash, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ReplaceSelectedTorrent(updated, SelectedTorrentFile?.Id);
            LogInfo("Library", "Torrent details loaded from API.", updated.Title);
        }
        catch (Exception exception)
        {
            LogWarning("Library", "Failed to load selected torrent details from API.", exception.Message);
        }
    }

    private async Task RefreshAfterTorrentAddedAsync(
        TorrServerClient client,
        IReadOnlyList<TorrentItem> addedTorrents,
        IReadOnlySet<string> previousHashes)
    {
        var preferredHash = addedTorrents
            .Select(torrent => torrent.Hash)
            .FirstOrDefault(hash => !string.IsNullOrWhiteSpace(hash));
        var torrents = await client.GetTorrentsAsync().ConfigureAwait(true);

        Torrents.Clear();
        foreach (var torrent in torrents)
        {
            Torrents.Add(torrent);
        }

        var selected = SelectAddedTorrent(torrents, preferredHash, previousHashes) ??
            addedTorrents.FirstOrDefault(torrent => !string.IsNullOrWhiteSpace(torrent.Hash));
        if (selected is null)
        {
            return;
        }

        ReplaceSelectedTorrent(selected);
        if (!string.IsNullOrWhiteSpace(selected.Hash))
        {
            await RefreshAddedTorrentDetailsWithRetryAsync(client, selected.Hash).ConfigureAwait(true);
        }
    }

    private async Task RefreshAddedTorrentDetailsWithRetryAsync(TorrServerClient client, string hash)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                var updated = await client.GetTorrentAsync(hash).ConfigureAwait(true);
                ReplaceSelectedTorrent(updated);
                if (HasUsefulTorrentDetails(updated))
                {
                    LogInfo("Library", "Added torrent details refreshed.", updated.Title);
                    return;
                }
            }
            catch (Exception exception)
            {
                lastException = exception;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(750)).ConfigureAwait(true);
        }

        LogWarning(
            "Library",
            "Added torrent details are not fully available yet.",
            lastException?.Message ?? hash);
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
            var previousHashes = CurrentTorrentHashes();
            using var client = new TorrServerClient(SelectedServer);
            var addedTorrent = await client.AddMagnetAsync(link, SelectedSearchResult.Title).ConfigureAwait(true);
            await RefreshAfterTorrentAddedAsync(client, [addedTorrent], previousHashes).ConfigureAwait(true);
            StatusMessage = L["StatusTorrentAdded"];
            LogInfo("Search", "Search result added to library.", SelectedSearchResult.Title);
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

        try
        {
            OpenMediaUri(mediaUri, SelectedTorrent.Title, "Opened selected torrent in player.");
        }
        catch (Exception exception)
        {
            HandlePlaybackException(exception, SelectedTorrent.Title);
        }

        return Task.CompletedTask;
    }

    private Task OpenContinuePlaylistAsync()
    {
        if (SelectedServer is null || SelectedTorrent is null || string.IsNullOrWhiteSpace(SelectedTorrent.Hash))
        {
            StatusMessage = L["StatusNoTorrentSelected"];
            return Task.CompletedTask;
        }

        using var client = new TorrServerClient(SelectedServer);
        var mediaUri = client.GetPlaylistUri(SelectedTorrent.Hash, ResolvePlaylistName(SelectedTorrent), fromLast: true);
        OpenPlayerUri(mediaUri, "Opened continue playlist in player.");
        return Task.CompletedTask;
    }

    private async Task OpenPlaylistFromSelectedAsync()
    {
        var mediaUri = await CreatePlaylistFromSelectedAsync().ConfigureAwait(true);
        if (mediaUri is null)
        {
            StatusMessage = L["StatusNoTorrentSelected"];
            return;
        }

        OpenPlayerUri(mediaUri, "Opened playlist from selected file in player.");
    }

    private void OpenPlayerUri(Uri mediaUri, string logMessage)
    {
        try
        {
            OpenMediaUri(mediaUri, SelectedTorrent?.Title ?? string.Empty, logMessage);
        }
        catch (Exception exception)
        {
            HandlePlaybackException(exception, SelectedTorrent?.Title ?? string.Empty);
        }
    }

    private void OpenMediaUri(Uri mediaUri, string title, string logMessage)
    {
        if (_settings.Player.PreferredPlayer == ExternalPlayerKind.BuiltInMpv)
        {
            if (BuiltInPlayerRequested is null)
            {
                throw new InvalidOperationException(L["StatusBuiltInPlayerUnavailable"]);
            }

            BuiltInPlayerRequested.Invoke(this, new BuiltInPlayerRequest(mediaUri, title, SelectedServer));
            StatusMessage = L["StatusBuiltInPlayerOpened"];
            LogInfo("Player", logMessage, title);
            return;
        }

        _playerLauncher.Play(mediaUri, _settings.Player);
        StatusMessage = L["StatusExternalPlayerOpened"];
        LogInfo("Player", logMessage, title);
    }

    private void HandlePlaybackException(Exception exception, string title)
    {
        StatusMessage = exception.Message;
        LogError("Player", "Failed to open media in player.", exception, title);
    }

    private async Task<Uri?> CreatePlaylistFromSelectedAsync()
    {
        if (SelectedServer is null || SelectedTorrent is null || string.IsNullOrWhiteSpace(SelectedTorrent.Hash))
        {
            return null;
        }

        if (SelectedTorrentFile is null)
        {
            using var client = new TorrServerClient(SelectedServer);
            return client.GetPlaylistUri(SelectedTorrent.Hash, ResolvePlaylistName(SelectedTorrent));
        }

        var selectedIndex = SelectedTorrent.Files
            .Select((file, index) => new { file, index })
            .FirstOrDefault(item => item.file.Id == SelectedTorrentFile.Id)
            ?.index;
        if (selectedIndex is null)
        {
            return null;
        }

        Directory.CreateDirectory(AppPaths.PlaylistsDirectory);
        using var playlistClient = new TorrServerClient(SelectedServer);
        var sessionToken = Guid.NewGuid().ToString("N")[..12];
        var lines = new List<string> { "#EXTM3U" };
        foreach (var file in SelectedTorrent.Files.Skip(selectedIndex.Value))
        {
            var name = Path.GetFileName(file.Path);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = file.Path;
            }

            lines.Add("#EXTINF:0," + name);
            lines.Add(playlistClient.GetStreamUri(SelectedTorrent.Hash, file.Id, file.Path, sessionToken).AbsoluteUri);
        }

        var playlistName = SanitizePathSegment(ResolvePlaylistName(SelectedTorrent)) + "-from-" + SelectedTorrentFile.Id + ".m3u";
        var playlistPath = Path.Combine(AppPaths.PlaylistsDirectory, playlistName);
        await File.WriteAllLinesAsync(playlistPath, lines).ConfigureAwait(true);
        return new Uri(playlistPath);
    }

    private bool TrySetClipboardText(string text, string source, string details = "")
    {
        Exception? lastOpenClipboardException = null;
        for (var attempt = 0; attempt < ClipboardRetryDelaysMs.Length; attempt++)
        {
            var delay = ClipboardRetryDelaysMs[attempt];
            if (delay > 0)
            {
                System.Threading.Thread.Sleep(delay);
            }

            try
            {
                SetClipboardTextNative(text);
                return true;
            }
            catch (Exception exception) when (IsOpenClipboardFailure(exception))
            {
                lastOpenClipboardException = exception;
                if (ClipboardContainsText(text))
                {
                    LogWarning(source, "Clipboard reported busy after text was copied.", details);
                    return true;
                }
            }
            catch
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    return true;
                }
                catch (Exception fallbackException) when (IsOpenClipboardFailure(fallbackException))
                {
                    lastOpenClipboardException = fallbackException;
                }
                catch (Exception fallbackException)
                {
                    StatusMessage = fallbackException.Message;
                    LogError(source, "Failed to copy text to clipboard.", fallbackException, details);
                    return false;
                }
            }
        }

        if (lastOpenClipboardException is not null)
        {
            if (ClipboardContainsText(text))
            {
                LogWarning(source, "Clipboard reported busy after text was copied.", details);
                return true;
            }

            StatusMessage = L["StatusClipboardBusy"];
            LogError(source, "Failed to copy text to clipboard.", lastOpenClipboardException, details);
            return false;
        }

        return false;
    }

    private static bool IsOpenClipboardFailure(Exception exception)
    {
        return exception.HResult == ClipboardCannotOpen ||
            exception.Message.Contains("OpenClipboard", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetClipboardTextNative(string text)
    {
        var bytes = Encoding.Unicode.GetBytes(text + '\0');
        var clipboardOpened = false;
        var clipboardOwnsHandle = false;
        var handle = GlobalAlloc(GlobalMemoryMoveable | GlobalMemoryZeroInit, (UIntPtr)bytes.Length);
        if (handle == IntPtr.Zero)
        {
            throw CreateNativeClipboardException("GlobalAlloc");
        }

        try
        {
            var target = GlobalLock(handle);
            if (target == IntPtr.Zero)
            {
                throw CreateNativeClipboardException("GlobalLock");
            }

            try
            {
                Marshal.Copy(bytes, 0, target, bytes.Length);
            }
            finally
            {
                GlobalUnlock(handle);
            }

            if (!OpenClipboard(IntPtr.Zero))
            {
                throw new ExternalException(
                    $"OpenClipboard failed. Win32 error: {Marshal.GetLastWin32Error()}.",
                    ClipboardCannotOpen);
            }

            clipboardOpened = true;

            if (!EmptyClipboard())
            {
                throw CreateNativeClipboardException("EmptyClipboard");
            }

            if (SetClipboardData(ClipboardFormatUnicodeText, handle) == IntPtr.Zero)
            {
                throw CreateNativeClipboardException("SetClipboardData");
            }

            clipboardOwnsHandle = true;
        }
        finally
        {
            if (clipboardOpened)
            {
                CloseClipboard();
            }

            if (!clipboardOwnsHandle)
            {
                GlobalFree(handle);
            }
        }
    }

    private static ExternalException CreateNativeClipboardException(string operation)
    {
        return new ExternalException($"{operation} failed. Win32 error: {Marshal.GetLastWin32Error()}.");
    }

    private static bool ClipboardContainsText(string text)
    {
        try
        {
            return System.Windows.Clipboard.ContainsText() &&
                string.Equals(System.Windows.Clipboard.GetText(), text, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    private void CopyPlaybackUrl()
    {
        var mediaUri = GetSelectedPlaylistUri();
        if (mediaUri is null)
        {
            StatusMessage = L["StatusNoTorrentSelected"];
            return;
        }

        if (!TrySetClipboardText(mediaUri.AbsoluteUri, "Player", SelectedTorrent?.Title ?? string.Empty))
        {
            return;
        }

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

        if (!TrySetClipboardText(value, "Library", SelectedTorrent.Title))
        {
            return;
        }

        StatusMessage = L["StatusTorrentSourceCopied"];
    }

    private void CopyTorrentHash()
    {
        if (SelectedTorrent is null || string.IsNullOrWhiteSpace(SelectedTorrent.Hash))
        {
            StatusMessage = L["StatusNoTorrentSelected"];
            return;
        }

        if (!TrySetClipboardText(SelectedTorrent.Hash, "Library", SelectedTorrent.Title))
        {
            return;
        }

        StatusMessage = L["StatusTorrentHashCopied"];
    }

    private Uri? GetSelectedPlaybackUri()
    {
        if (SelectedServer is null || SelectedTorrent is null || string.IsNullOrWhiteSpace(SelectedTorrent.Hash))
        {
            return null;
        }

        using var client = new TorrServerClient(SelectedServer);
        if (SelectedTorrentFile is not null)
        {
            return client.GetStreamUri(SelectedTorrent.Hash, SelectedTorrentFile.Id, SelectedTorrentFile.Path);
        }

        if (SelectedTorrent.Files.Count != 1)
        {
            return client.GetPlaylistUri(SelectedTorrent.Hash, ResolvePlaylistName(SelectedTorrent));
        }

        var file = SelectedTorrent.Files[0];
        var fileId = file.Id;
        if (_settings.Player.PreferDirectStreamUrl)
        {
            return client.GetStreamUri(SelectedTorrent.Hash, fileId, file.Path);
        }

        return client.GetPlaybackUri(SelectedTorrent.Hash, fileId);
    }

    private Uri? GetSelectedPlaylistUri()
    {
        if (SelectedServer is null || SelectedTorrent is null || string.IsNullOrWhiteSpace(SelectedTorrent.Hash))
        {
            return null;
        }

        using var client = new TorrServerClient(SelectedServer);
        return SelectedTorrentFile is not null
            ? client.GetPlaylistUri(SelectedTorrent.Hash, SelectedTorrentFile.Path)
            : client.GetPlaylistUri(SelectedTorrent.Hash, ResolvePlaylistName(SelectedTorrent));
    }

    private static string ResolvePlaylistName(TorrentItem torrent)
    {
        foreach (var file in torrent.Files)
        {
            var path = file.Path.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var parts = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > 1)
            {
                return parts[0];
            }
        }

        var firstFilePath = torrent.Files.FirstOrDefault()?.Path;
        if (!string.IsNullOrWhiteSpace(firstFilePath))
        {
            var fileName = Path.GetFileNameWithoutExtension(firstFilePath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        return string.IsNullOrWhiteSpace(torrent.Title) ? "playlist" : torrent.Title;
    }

    private HashSet<string> CurrentTorrentHashes()
    {
        return Torrents
            .Select(torrent => torrent.Hash)
            .Where(hash => !string.IsNullOrWhiteSpace(hash))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static TorrentItem? SelectAddedTorrent(
        IReadOnlyList<TorrentItem> torrents,
        string? preferredHash,
        IReadOnlySet<string> previousHashes)
    {
        if (!string.IsNullOrWhiteSpace(preferredHash))
        {
            var byHash = torrents.FirstOrDefault(torrent =>
                string.Equals(torrent.Hash, preferredHash, StringComparison.OrdinalIgnoreCase));
            if (byHash is not null)
            {
                return byHash;
            }
        }

        return torrents.FirstOrDefault(torrent =>
            !string.IsNullOrWhiteSpace(torrent.Hash) &&
            !previousHashes.Contains(torrent.Hash));
    }

    private static bool HasUsefulTorrentDetails(TorrentItem torrent)
    {
        return torrent.Files.Count > 0 || torrent.SizeBytes > 0;
    }

    private void ReplaceSelectedTorrent(TorrentItem updated, int? preferredFileId = null)
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

        _suppressSelectedTorrentDetailsRefresh = true;
        try
        {
            SelectedTorrent = updated;
            if (preferredFileId is not null)
            {
                var selectedFile = updated.Files.FirstOrDefault(file => file.Id == preferredFileId.Value);
                if (selectedFile is not null)
                {
                    SelectedTorrentFile = selectedFile;
                }
            }
        }
        finally
        {
            _suppressSelectedTorrentDetailsRefresh = false;
        }
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
        try
        {
            SyncSettingsFromView();
            await _settingsStore.SaveAsync(_settings).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("Settings", "Settings save failed.", exception);
            return;
        }

        if (!string.Equals(AppPaths.UserSettingsFile, AppPaths.ServiceSettingsFile, StringComparison.OrdinalIgnoreCase))
        {
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
        SelectSearchProviderOption(provider);
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

        if (!TrySetClipboardText(RuntimeSettingsJson, "RuntimeSettings"))
        {
            return;
        }

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
        if (!TrySetClipboardText(AppPaths.UserLogFile + Environment.NewLine + AppPaths.ServiceLogFile, "Logs"))
        {
            return;
        }

        StatusMessage = L["StatusLogPathsCopied"];
    }

    private void OpenLogFolders()
    {
        var directories = new[]
            {
                AppPaths.UserLogsDirectory,
                AppPaths.ProgramDataLogsDirectory
            }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var opened = 0;
        foreach (var directory in directories)
        {
            if (OpenDirectory(directory, createDirectory: true, updateStatus: false))
            {
                opened++;
            }
        }

        if (opened == directories.Length)
        {
            StatusMessage = L["StatusLogFoldersOpened"];
        }
    }

    private void OpenTorrServerExecutableFolder()
    {
        var directory = ResolveContainingDirectory(LocalServer.ExecutablePath, AppPaths.DefaultLocalServerDirectory);
        OpenDirectory(directory, createDirectory: true);
    }

    private void OpenLocalServerDataFolder()
    {
        OpenDirectory(LocalTorrServerConfigurationWriter.GetDataDirectory(LocalServer), createDirectory: true);
    }

    private void OpenLocalServerCacheFolder()
    {
        OpenDirectory(ResolveLocalServerCacheDirectory(), createDirectory: true);
    }

    private void OpenTorrServerVersionsFolder()
    {
        OpenDirectory(GetTorrServerVersionsDirectory(), createDirectory: true);
    }

    private void OpenSelectedInstalledTorrServerFolder()
    {
        if (SelectedInstalledTorrServer is null)
        {
            StatusMessage = L["StatusNoInstalledTorrServerSelected"];
            return;
        }

        var directory = ResolveContainingDirectory(SelectedInstalledTorrServer.ExecutablePath, string.Empty);
        OpenDirectory(directory, createDirectory: false);
    }

    private void OpenSettingsBackupsFolder()
    {
        OpenDirectory(AppPaths.UserSettingsBackupsDirectory, createDirectory: true);
    }

    private void OpenTorrWindUpdatesFolder()
    {
        OpenDirectory(AppPaths.UpdatesDirectory, createDirectory: true);
    }

    private void OpenDownloadedTorrWindUpdate()
    {
        if (string.IsNullOrWhiteSpace(_downloadedTorrWindUpdatePath) ||
            !File.Exists(_downloadedTorrWindUpdatePath))
        {
            StatusMessage = L["StatusNoDownloadedTorrWindUpdate"];
            _openDownloadedTorrWindUpdateCommand.RaiseCanExecuteChanged();
            return;
        }

        OpenFile(_downloadedTorrWindUpdatePath);
    }

    private bool OpenDirectory(string directoryPath, bool createDirectory, bool updateStatus = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new InvalidOperationException(L["StatusFolderPathEmpty"]);
            }

            if (createDirectory)
            {
                Directory.CreateDirectory(directoryPath);
            }
            else if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException(directoryPath);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = directoryPath,
                UseShellExecute = true
            });

            if (updateStatus)
            {
                StatusMessage = string.Format(L["StatusFolderOpened"], directoryPath);
            }

            LogInfo("Shell", "Folder opened.", directoryPath);
            return true;
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(L["StatusFolderOpenFailed"], exception.Message);
            LogError("Shell", "Failed to open folder.", exception, directoryPath);
            return false;
        }
    }

    private bool OpenFile(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new InvalidOperationException(L["StatusFilePathEmpty"]);
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(filePath);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });

            StatusMessage = string.Format(L["StatusFileOpened"], filePath);
            LogInfo("Shell", "File opened.", filePath);
            return true;
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(L["StatusFileOpenFailed"], exception.Message);
            LogError("Shell", "Failed to open file.", exception, filePath);
            return false;
        }
    }

    private static string ResolveContainingDirectory(string path, string fallbackDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return fallbackDirectory;
        }

        if (Directory.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path);
        return string.IsNullOrWhiteSpace(directory)
            ? fallbackDirectory
            : directory;
    }

    private string ResolveLocalServerCacheDirectory()
    {
        if (!string.IsNullOrWhiteSpace(LocalServer.TemporaryDataPath))
        {
            return LocalServer.TemporaryDataPath;
        }

        return Path.Combine(LocalTorrServerConfigurationWriter.GetDataDirectory(LocalServer), "cache");
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

        if (!TrySetClipboardText(BuildDiagnosticsReport(includeHeader: false), "Diagnostics"))
        {
            return;
        }

        StatusMessage = L["StatusDiagnosticsCopied"];
    }

    private string BuildDiagnosticsReport(bool includeHeader)
    {
        var lines = new List<string>();
        if (includeHeader)
        {
            lines.Add("TorrWind diagnostics");
            lines.Add("Created: " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
            lines.Add(string.Empty);
        }

        lines.AddRange(DiagnosticItems.Select(item => $"{item.Name}: {item.Value}"));
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static async Task AddTextEntryAsync(
        ZipArchive archive,
        string entryName,
        string content,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        await using var entryStream = entry.Open();
        await using var writer = new StreamWriter(entryStream);
        await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task AddFileEntryIfExistsAsync(
        ZipArchive archive,
        string filePath,
        string entryName,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        try
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
            await using var entryStream = entry.Open();
            await using var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            await fileStream.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await AddTextEntryAsync(
                    archive,
                    entryName + ".error.txt",
                    $"Failed to include {filePath}:{Environment.NewLine}{exception.Message}{Environment.NewLine}",
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private string BuildSupportBundleManifest()
    {
        return string.Join(
            Environment.NewLine,
            [
                "TorrWind support bundle",
                "Created: " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                "Sensitive values in settings.sanitized.json are redacted.",
                string.Empty,
                "Entries:",
                "- diagnostics.txt",
                "- settings.sanitized.json",
                "- logs/gui.jsonl",
                "- logs/gui.jsonl.1 when present",
                "- logs/service.jsonl",
                "- logs/service.jsonl.1 when present",
                "- logs/mpv-player.log when present"
            ]) + Environment.NewLine;
    }

    private string BuildSanitizedSettingsJson()
    {
        var sanitized = new
        {
            _settings.Language,
            _settings.ActiveServerId,
            _settings.SettingsBackupRetentionCount,
            Servers = _settings.Servers.Select(server => new
            {
                server.Id,
                server.Name,
                BaseUrl = RedactUrl(server.BaseUrl),
                HasUsername = !string.IsNullOrWhiteSpace(server.Username),
                HasPassword = !string.IsNullOrWhiteSpace(server.Password),
                server.IsLocal,
                server.IgnoreCertificateErrors,
                server.ReadOnly
            }),
            LocalServer = new
            {
                LocalServer.Enabled,
                LocalServer.RunAsWindowsService,
                LocalServer.ExecutablePath,
                LocalServer.InstalledVersion,
                LocalServer.PreviousExecutablePath,
                LocalServer.PreviousVersion,
                LocalServer.DataDirectory,
                LocalServer.TemporaryDataPath,
                LocalServer.ListenAddress,
                LocalServer.Port,
                LocalServer.UseHttpAuth,
                HasUsername = !string.IsNullOrWhiteSpace(LocalServer.Username),
                HasPassword = !string.IsNullOrWhiteSpace(LocalServer.Password),
                LocalServer.UseSsl,
                LocalServer.SslPort,
                LocalServer.ForceHttps,
                LocalServer.CertificatePath,
                LocalServer.CertificateKeyPath,
                LocalServer.ReadOnlyDatabase,
                LocalServer.AllowSearchWithoutAuth,
                WhiteListEntries = CountNonEmptyLines(LocalServer.WhiteList),
                BlackListEntries = CountNonEmptyLines(LocalServer.BlackList),
                ProxyUrl = RedactUrl(LocalServer.ProxyUrl),
                LocalServer.ProxyMode,
                LocalServer.EnableDlna,
                LocalServer.EnableWebDav,
                LocalServer.CacheMode,
                LocalServer.CacheSizeMb,
                LocalServer.PreloadCachePercent,
                LocalServer.ReaderReadAheadPercent,
                LocalServer.TorrentDisconnectTimeoutSeconds,
                LocalServer.ConnectionsLimit,
                LocalServer.DownloadSpeedLimitKb,
                LocalServer.UploadSpeedLimitKb,
                LocalServer.AllowLanAccess,
                HasTmdbApiKey = !string.IsNullOrWhiteSpace(LocalServer.TmdbApiKey),
                LocalServer.TmdbApiUrl,
                LocalServer.TmdbImageUrl,
                LocalServer.TmdbImageUrlRu
            },
            Player = new
            {
                Player.PreferredPlayer,
                Player.CustomPlayerPath,
                Player.PreferDirectStreamUrl
            },
            SearchProviders = _settings.SearchProviders.Select(provider => new
            {
                provider.Id,
                provider.Name,
                Url = RedactUrl(provider.Url),
                HasApiKey = !string.IsNullOrWhiteSpace(provider.ApiKey),
                provider.Categories,
                provider.Enabled,
                provider.IgnoreCertificateErrors,
                provider.TimeoutSeconds
            }),
            SearchHistoryCount = _settings.SearchHistory.Count
        };

        return JsonSerializer.Serialize(
            sanitized,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            });
    }

    private static int CountNonEmptyLines(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? 0
            : value
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .Count(line => !string.IsNullOrWhiteSpace(line));
    }

    private static string RedactUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('?', StringComparison.Ordinal))
        {
            return value;
        }

        var hashIndex = value.IndexOf('#', StringComparison.Ordinal);
        var withoutFragment = hashIndex >= 0 ? value[..hashIndex] : value;
        var fragment = hashIndex >= 0 ? value[hashIndex..] : string.Empty;
        var queryIndex = withoutFragment.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex < 0)
        {
            return value;
        }

        var prefix = withoutFragment[..(queryIndex + 1)];
        var query = withoutFragment[(queryIndex + 1)..];
        var parts = query.Split('&');
        for (var i = 0; i < parts.Length; i++)
        {
            var separatorIndex = parts[i].IndexOf('=', StringComparison.Ordinal);
            var key = separatorIndex >= 0 ? parts[i][..separatorIndex] : parts[i];
            if (IsSensitiveName(key))
            {
                parts[i] = separatorIndex >= 0 ? key + "=<redacted>" : key;
            }
        }

        return prefix + string.Join("&", parts) + fragment;
    }

    private static bool IsSensitiveName(string name)
    {
        return name.Contains("key", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("token", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("pass", StringComparison.OrdinalIgnoreCase);
    }

    private void AddApplicationDiagnostics()
    {
        AddDiagnostic("DiagnosticAppVersion", AppVersion);
        AddDiagnostic("DiagnosticRuntime", RuntimeInformation.FrameworkDescription);
        AddDiagnostic("DiagnosticOS", RuntimeInformation.OSDescription);
        AddDiagnostic("DiagnosticProcessArchitecture", RuntimeInformation.ProcessArchitecture.ToString());
        AddDiagnostic("DiagnosticUserSettingsFile", AppPaths.UserSettingsFile);
    }

    private static string ResolveAppVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(MainWindowViewModel).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "1.0.0";
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
            await AddWebDavDiagnosticsAsync(client).ConfigureAwait(true);
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
            AddDiagnostic("DiagnosticSettingsDlnaFriendlyName", ReadJsonValue(root, "FriendlyName"));
            AddDiagnostic("DiagnosticSettingsTmdbKey", FormatBool(!string.IsNullOrWhiteSpace(ReadNestedJsonValue(root, "TMDBSettings", "APIKey"))));
            AddDiagnostic("DiagnosticSettingsTmdbApiUrl", EmptyAsNotAvailable(ReadNestedJsonValue(root, "TMDBSettings", "APIURL")));
            AddDiagnostic("DiagnosticSettingsTmdbImageUrl", EmptyAsNotAvailable(ReadNestedJsonValue(root, "TMDBSettings", "ImageURL")));
            AddDiagnostic("DiagnosticSettingsTmdbImageUrlRu", EmptyAsNotAvailable(ReadNestedJsonValue(root, "TMDBSettings", "ImageURLRu")));
        }
        catch (Exception exception)
        {
            AddDiagnostic("DiagnosticSettings", exception.Message);
            LogError("Diagnostics", "Runtime settings diagnostics failed.", exception);
        }
    }

    private async Task AddWebDavDiagnosticsAsync(TorrServerClient client)
    {
        if (SelectedServer?.IsLocal == true)
        {
            AddDiagnostic("DiagnosticWebDavConfigured", FormatBool(LocalServer.EnableWebDav));
        }

        AddDiagnostic("DiagnosticWebDavUrl", client.WebDavUri.AbsoluteUri);
        try
        {
            var status = await client.ProbeWebDavAsync().ConfigureAwait(true);
            AddDiagnostic("DiagnosticWebDavEndpoint", FormatHttpStatus(status));
        }
        catch (Exception exception)
        {
            AddDiagnostic("DiagnosticWebDavEndpoint", exception.Message);
            LogWarning("Diagnostics", "WebDAV diagnostics failed.", exception.Message);
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
        RebuildPlayerKindOptions();
        RebuildRetrackersModeOptions();
        RebuildSearchProviderOptions();
        UpdateTorrServerReleaseText();
        await SaveSettingsAsync().ConfigureAwait(true);
        OnPropertyChanged(nameof(ActiveServerLabel));
    }

    private async Task ApplySelectedLanguageAsync()
    {
        if (_suppressLanguageApply || _isApplyingLanguage || string.IsNullOrWhiteSpace(SelectedLanguage))
        {
            return;
        }

        try
        {
            _isApplyingLanguage = true;
            await ApplyLanguageAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            HandleCommandException(exception);
        }
        finally
        {
            _isApplyingLanguage = false;
        }
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

    private void HandleCommandException(Exception exception)
    {
        StatusMessage = exception.Message;
        LogError("Command", "Unhandled command failure.", exception);
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

    private static string FormatHttpStatus(HttpStatusCode status)
    {
        return ((int)status).ToString(CultureInfo.InvariantCulture) + " " + status;
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

    private static string ReadNestedJsonValue(JsonElement root, string objectName, string name)
    {
        if (TryFindJsonProperty(root, objectName, out var container) &&
            container.ValueKind == JsonValueKind.Object &&
            TryFindJsonProperty(container, name, out var value))
        {
            return FormatJsonValue(value);
        }

        return string.Empty;
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
        var results = new List<SearchResult>();

        try
        {
            results.AddRange(await client.SearchServerTorznabAsync(query).ConfigureAwait(true));
        }
        catch (Exception exception)
        {
            _lastSearchFailedProviders++;
            LogError("Search", "Selected TorrServer Torznab search failed.", exception, SelectedServer.Name);
        }

        try
        {
            results.AddRange(await client.SearchServerRutorAsync(query).ConfigureAwait(true));
        }
        catch (Exception exception)
        {
            _lastSearchFailedProviders++;
            LogError("Search", "Selected TorrServer RuTor search failed.", exception, SelectedServer.Name);
        }

        return results
            .GroupBy(result => FirstNotEmpty(result.Magnet, result.Link, result.Title), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
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
            .Where(IsWithinPublishedDateRange)
            .Where(result => categories.Count == 0 ||
                string.IsNullOrWhiteSpace(result.Category) ||
                categories.Contains(result.Category))
            .OrderByDescending(result => result.Seeders)
            .ThenByDescending(result => result.PublishedAt);
    }

    private bool IsWithinPublishedDateRange(SearchResult result)
    {
        if (SearchPublishedFrom is null && SearchPublishedTo is null)
        {
            return true;
        }

        if (result.PublishedAt is null)
        {
            return false;
        }

        var publishedDate = result.PublishedAt.Value.LocalDateTime.Date;
        return (SearchPublishedFrom is null || publishedDate >= SearchPublishedFrom.Value) &&
            (SearchPublishedTo is null || publishedDate <= SearchPublishedTo.Value);
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
        foreach (var option in SearchProviderOptions)
        {
            option.Dispose();
        }

        SearchProviderOptions.Clear();
        SearchProviderOptions.Add(SearchProviderOption.ForSelectedServer(L["SearchModeSelectedServer"]));
        SearchProviderOptions.Add(SearchProviderOption.ForAllProviders(L["SearchModeAllProviders"]));

        foreach (var provider in SearchProviders)
        {
            SearchProviderOptions.Add(SearchProviderOption.ForProvider(provider));
        }

        var singleProviderOption = SearchProviders.Count == 1
            ? SearchProviderOptions.FirstOrDefault(option => option.Provider?.Id == SearchProviders[0].Id)
            : null;

        SelectedSearchProviderOption = SearchProviderOptions.FirstOrDefault(option =>
                selected is not null && option.Matches(selected)) ??
            singleProviderOption ??
            SearchProviderOptions.FirstOrDefault();
    }

    private void RebuildPlayerKindOptions()
    {
        var selected = Player.PreferredPlayer;
        PlayerKindOptions.Clear();
        PlayerKindOptions.Add(new PlayerKindOption(ExternalPlayerKind.BuiltInMpv, L["PlayerBuiltInMpv"]));
        PlayerKindOptions.Add(new PlayerKindOption(ExternalPlayerKind.SystemDefault, L["PlayerSystemDefault"]));
        PlayerKindOptions.Add(new PlayerKindOption(ExternalPlayerKind.Vlc, L["PlayerVlc"]));
        PlayerKindOptions.Add(new PlayerKindOption(ExternalPlayerKind.MpcHc, L["PlayerMpcHc"]));
        PlayerKindOptions.Add(new PlayerKindOption(ExternalPlayerKind.PotPlayer, L["PlayerPotPlayer"]));
        PlayerKindOptions.Add(new PlayerKindOption(ExternalPlayerKind.Custom, L["PlayerCustom"]));
        Player.PreferredPlayer = selected;
        OnPropertyChanged(nameof(Player));
    }

    private void RebuildRetrackersModeOptions()
    {
        var selected = LocalServer.RetrackersMode;
        RetrackersModeOptions.Clear();
        RetrackersModeOptions.Add(new RetrackersModeOption(0, L["RetrackersModeNone"]));
        RetrackersModeOptions.Add(new RetrackersModeOption(1, L["RetrackersModeAdd"]));
        RetrackersModeOptions.Add(new RetrackersModeOption(2, L["RetrackersModeRemove"]));
        RetrackersModeOptions.Add(new RetrackersModeOption(3, L["RetrackersModeReplace"]));
        LocalServer.RetrackersMode = Math.Clamp(selected, 0, 3);
        OnPropertyChanged(nameof(LocalServer));
    }

    private void SelectSearchProviderOption(SearchProviderSettings provider)
    {
        SelectedSearchProviderOption = SearchProviderOptions.FirstOrDefault(option =>
                option.Provider is not null && option.Provider.Id == provider.Id) ??
            SelectedSearchProviderOption;
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

    private static string GetTorrServerVersionsDirectory()
    {
        return Path.Combine(AppPaths.DefaultLocalServerDirectory, "versions");
    }

    private void RefreshInstalledTorrServers()
    {
        RefreshInstalledTorrServers(updateStatusMessage: true);
    }

    private void RefreshInstalledTorrServers(bool updateStatusMessage)
    {
        try
        {
            var selectedPath = SelectedInstalledTorrServer?.ExecutablePath;
            var versionsDirectory = GetTorrServerVersionsDirectory();
            List<InstalledTorrServerItem> items = Directory.Exists(versionsDirectory)
                ? Directory
                    .EnumerateFiles(versionsDirectory, "*.exe", SearchOption.AllDirectories)
                    .Select(path => new FileInfo(path))
                    .Where(file => file.Exists)
                    .Select(file => new InstalledTorrServerItem(file.FullName, ResolveInstalledTorrServerVersion(file.FullName), file.Length, new DateTimeOffset(file.LastWriteTime)))
                    .OrderByDescending(item => item.ModifiedAt)
                    .ToList()
                : [];

            InstalledTorrServers.Clear();
            foreach (var item in items)
            {
                InstalledTorrServers.Add(item);
            }

            SelectedInstalledTorrServer = InstalledTorrServers.FirstOrDefault(item =>
                    string.Equals(item.ExecutablePath, selectedPath, StringComparison.OrdinalIgnoreCase)) ??
                InstalledTorrServers.FirstOrDefault(item =>
                    string.Equals(item.ExecutablePath, LocalServer.ExecutablePath, StringComparison.OrdinalIgnoreCase)) ??
                InstalledTorrServers.FirstOrDefault();

            if (updateStatusMessage)
            {
                StatusMessage = string.Format(L["StatusInstalledTorrServersLoaded"], InstalledTorrServers.Count);
            }
        }
        catch (Exception exception)
        {
            if (updateStatusMessage)
            {
                StatusMessage = string.Format(L["StatusInstalledTorrServersLoadFailed"], exception.Message);
            }

            LogError("TorrServer", "Failed to refresh installed TorrServer versions.", exception);
        }
    }

    private async Task UseSelectedInstalledTorrServerAsync()
    {
        if (SelectedInstalledTorrServer is null)
        {
            StatusMessage = L["StatusNoInstalledTorrServerSelected"];
            return;
        }

        try
        {
            await SwitchLocalTorrServerExecutableAsync(
                SelectedInstalledTorrServer.ExecutablePath,
                SelectedInstalledTorrServer.Version,
                "StatusTorrServerSwitched").ConfigureAwait(true);
            LogInfo("TorrServer", "Switched to installed TorrServer version.", SelectedInstalledTorrServer.ExecutablePath);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("TorrServer", "Failed to switch installed TorrServer version.", exception);
        }
    }

    private void DeleteSelectedInstalledTorrServer()
    {
        if (SelectedInstalledTorrServer is null)
        {
            StatusMessage = L["StatusNoInstalledTorrServerSelected"];
            return;
        }

        var item = SelectedInstalledTorrServer;
        if (string.Equals(item.ExecutablePath, LocalServer.ExecutablePath, StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = L["StatusCannotDeleteActiveTorrServer"];
            return;
        }

        var result = System.Windows.MessageBox.Show(
            string.Format(L["ConfirmDeleteInstalledTorrServer"], item.Version, item.ExecutablePath),
            L["ConfirmDeleteInstalledTorrServerTitle"],
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var file = new FileInfo(item.ExecutablePath);
            if (file.Exists && file.IsReadOnly)
            {
                file.Attributes &= ~FileAttributes.ReadOnly;
            }

            file.Delete();
            RemoveEmptyVersionDirectory(item.ExecutablePath);
            RefreshInstalledTorrServers(updateStatusMessage: false);
            StatusMessage = string.Format(L["StatusInstalledTorrServerDeleted"], item.Version);
            LogInfo("TorrServer", "Installed TorrServer version deleted.", item.ExecutablePath);
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(L["StatusInstalledTorrServerDeleteFailed"], exception.Message);
            LogError("TorrServer", "Failed to delete installed TorrServer version.", exception, item.ExecutablePath);
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
        var expectedSha256 = await releases.GetExpectedSha256Async(release).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            StatusMessage = L["StatusTorrServerVerifyingChecksum"];
        }

        ValidateDownloadedTorrServer(destination, release, expectedSha256);
        await SwitchLocalTorrServerExecutableAsync(destination, release.Version, "StatusTorrServerDownloaded").ConfigureAwait(true);
        RefreshInstalledTorrServers(updateStatusMessage: false);
        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            LogInfo("TorrServer", "Downloaded TorrServer SHA256 verified.", expectedSha256);
        }

        LogInfo("TorrServer", "TorrServer downloaded.", $"{release.Version}: {destination}");
    }

    private async Task SwitchLocalTorrServerExecutableAsync(string executablePath, string version, string statusKey)
    {
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("TorrServer executable was not found.", executablePath);
        }

        if (!string.IsNullOrWhiteSpace(LocalServer.ExecutablePath) &&
            !string.Equals(LocalServer.ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase))
        {
            LocalServer.PreviousExecutablePath = LocalServer.ExecutablePath;
            LocalServer.PreviousVersion = LocalServer.InstalledVersion;
        }

        LocalServer.ExecutablePath = executablePath;
        LocalServer.InstalledVersion = version;
        OnPropertyChanged(nameof(LocalServer));
        UpdateTorrServerReleaseText();
        await SaveSettingsAsync().ConfigureAwait(true);
        StatusMessage = string.Format(L[statusKey], version);
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

    private async Task<TorrWindRelease> FetchLatestTorrWindReleaseAsync(TorrWindReleaseService releases)
    {
        StatusMessage = L["StatusCheckingTorrWindUpdate"];
        var release = await releases.GetLatestReleaseAsync().ConfigureAwait(true);
        _latestTorrWindRelease = release;
        UpdateTorrWindUpdateText();
        return release;
    }

    private void UpdateTorrWindUpdateText()
    {
        TorrWindUpdateText = _latestTorrWindRelease is null
            ? string.Format(L["TorrWindUpdateInfoUnchecked"], AppVersion)
            : string.Format(
                L["TorrWindUpdateInfo"],
                AppVersion,
                _latestTorrWindRelease.Version,
                _latestTorrWindRelease.PackageName,
                TorrentItem.FormatBytes(_latestTorrWindRelease.SizeBytes),
                FormatTorrWindPackageKind(_latestTorrWindRelease.PackageKind));
    }

    private async Task CheckTorrWindUpdateAsync()
    {
        try
        {
            var releases = new TorrWindReleaseService();
            var release = await FetchLatestTorrWindReleaseAsync(releases).ConfigureAwait(true);
            StatusMessage = IsTorrWindReleaseNewer(release.Version, AppVersion)
                ? string.Format(L["StatusTorrWindUpdateAvailable"], AppVersion, release.Version)
                : string.Format(L["StatusTorrWindUpToDate"], AppVersion);
            LogInfo("TorrWind", "TorrWind update check completed.", $"{AppVersion} -> {release.Version}");
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("TorrWind", "Failed to check TorrWind update.", exception);
        }
    }

    private async Task DownloadTorrWindUpdateAsync()
    {
        try
        {
            var releases = new TorrWindReleaseService();
            var release = await FetchLatestTorrWindReleaseAsync(releases).ConfigureAwait(true);
            await DownloadTorrWindUpdateReleaseAsync(releases, release).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            LogError("TorrWind", "Failed to download TorrWind update.", exception);
        }
    }

    private async Task DownloadTorrWindUpdateReleaseAsync(TorrWindReleaseService releases, TorrWindRelease release)
    {
        AppPaths.EnsureWorkingDirectories();
        var destination = Path.Combine(
            AppPaths.UpdatesDirectory,
            SanitizePathSegment(release.Version),
            release.PackageName);

        var nextProgressReport = 0L;
        var progress = new Progress<long>(bytes =>
        {
            if (bytes < nextProgressReport && bytes < release.SizeBytes)
            {
                return;
            }

            nextProgressReport = bytes + 1024 * 1024;
            StatusMessage = string.Format(
                L["StatusTorrWindUpdateDownloadProgress"],
                release.Version,
                TorrentItem.FormatBytes(bytes),
                TorrentItem.FormatBytes(release.SizeBytes));
        });

        await releases.DownloadAsync(release.DownloadUrl, destination, progress).ConfigureAwait(true);
        var expectedSha256 = await releases.GetExpectedSha256Async(release).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            StatusMessage = L["StatusTorrWindVerifyingChecksum"];
        }

        ValidateDownloadedTorrWindUpdate(destination, release, expectedSha256);
        _downloadedTorrWindUpdatePath = destination;
        _openDownloadedTorrWindUpdateCommand.RaiseCanExecuteChanged();
        StatusMessage = string.Format(L["StatusTorrWindUpdateDownloaded"], release.Version, destination);
        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            LogInfo("TorrWind", "Downloaded TorrWind update SHA256 verified.", expectedSha256);
        }

        LogInfo("TorrWind", "TorrWind update downloaded.", $"{release.Version}: {destination}");
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

    private static string ResolveInstalledTorrServerVersion(string executablePath)
    {
        var directory = Path.GetDirectoryName(executablePath);
        return string.IsNullOrWhiteSpace(directory)
            ? string.Empty
            : Path.GetFileName(directory);
    }

    private static void RemoveEmptyVersionDirectory(string executablePath)
    {
        var directory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        var parent = Directory.GetParent(directory);
        if (parent is null ||
            !string.Equals(parent.Name, "versions", StringComparison.OrdinalIgnoreCase) ||
            Directory.EnumerateFileSystemEntries(directory).Any())
        {
            return;
        }

        Directory.Delete(directory);
    }

    private static void ValidateDownloadedTorrServer(
        string executablePath,
        TorrServerRelease release,
        string? expectedSha256)
    {
        var file = new FileInfo(executablePath);
        if (!file.Exists || file.Length <= 0)
        {
            throw new InvalidDataException("Downloaded TorrServer executable is empty.");
        }

        if (release.SizeBytes > 0 && file.Length != release.SizeBytes)
        {
            throw new InvalidDataException(
                $"Downloaded TorrServer size mismatch. Expected {release.SizeBytes} bytes, got {file.Length} bytes.");
        }

        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            var actualSha256 = ComputeSha256(executablePath);
            if (!string.Equals(actualSha256, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Downloaded TorrServer SHA256 mismatch. Expected {expectedSha256}, got {actualSha256}.");
            }
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
    }

    private static void ValidateDownloadedTorrWindUpdate(
        string packagePath,
        TorrWindRelease release,
        string? expectedSha256)
    {
        var file = new FileInfo(packagePath);
        if (!file.Exists || file.Length <= 0)
        {
            throw new InvalidDataException("Downloaded TorrWind update package is empty.");
        }

        if (release.SizeBytes > 0 && file.Length != release.SizeBytes)
        {
            throw new InvalidDataException(
                $"Downloaded TorrWind update size mismatch. Expected {release.SizeBytes} bytes, got {file.Length} bytes.");
        }

        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            var actualSha256 = ComputeSha256(packagePath);
            if (!string.Equals(actualSha256, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Downloaded TorrWind update SHA256 mismatch. Expected {expectedSha256}, got {actualSha256}.");
            }
        }
    }

    private static bool IsTorrWindReleaseNewer(string releaseVersion, string currentVersion)
    {
        var normalizedRelease = NormalizeReleaseVersion(releaseVersion);
        var normalizedCurrent = NormalizeReleaseVersion(currentVersion);
        if (Version.TryParse(normalizedRelease, out var release) &&
            Version.TryParse(normalizedCurrent, out var current))
        {
            return release > current;
        }

        return !string.Equals(normalizedRelease, normalizedCurrent, StringComparison.OrdinalIgnoreCase);
    }

    private string FormatTorrWindPackageKind(string packageKind)
    {
        return packageKind switch
        {
            "Installer" => L["TorrWindPackageInstaller"],
            "Portable" => L["TorrWindPackagePortable"],
            _ => EmptyAsNotAvailable(packageKind)
        };
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
            var serviceExe = Path.Combine(AppContext.BaseDirectory, "TorrWind.Service.exe");
            await new WindowsServiceManager().UninstallAsync(serviceExe).ConfigureAwait(true);
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
            var serviceExe = Path.Combine(AppContext.BaseDirectory, "TorrWind.Service.exe");
            await new WindowsServiceManager().StartAsync(serviceExe).ConfigureAwait(true);
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
            var serviceExe = Path.Combine(AppContext.BaseDirectory, "TorrWind.Service.exe");
            await new WindowsServiceManager().StopAsync(serviceExe).ConfigureAwait(true);
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
        if (_selectedServer is not null)
        {
            _selectedServer.PropertyChanged -= OnSelectedServerPropertyChanged;
        }

        foreach (var option in SearchProviderOptions)
        {
            option.Dispose();
        }

        _localServerProcess.Dispose();
    }
}

public sealed class SearchProviderOption : INotifyPropertyChanged, IDisposable
{
    private readonly string _fallbackDisplayName;

    private SearchProviderOption(string displayName, SearchProviderSettings? provider, bool useSelectedServer, bool useAllProviders)
    {
        _fallbackDisplayName = displayName;
        Provider = provider;
        UseSelectedServer = useSelectedServer;
        UseAllProviders = useAllProviders;

        if (Provider is not null)
        {
            Provider.PropertyChanged += OnProviderPropertyChanged;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName => Provider is null || string.IsNullOrWhiteSpace(Provider.Name)
        ? _fallbackDisplayName
        : Provider.Name;

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

    public void Dispose()
    {
        if (Provider is not null)
        {
            Provider.PropertyChanged -= OnProviderPropertyChanged;
        }
    }

    private void OnProviderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName) ||
            string.Equals(e.PropertyName, nameof(SearchProviderSettings.Name), StringComparison.Ordinal))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        }
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

public sealed class PlayerKindOption
{
    public PlayerKindOption(ExternalPlayerKind kind, string name)
    {
        Kind = kind;
        Name = name;
    }

    public ExternalPlayerKind Kind { get; }

    public string Name { get; }
}

public sealed class RetrackersModeOption
{
    public RetrackersModeOption(int value, string name)
    {
        Value = value;
        Name = name;
    }

    public int Value { get; }

    public string Name { get; }
}

public sealed class BuiltInPlayerRequest
{
    public BuiltInPlayerRequest(Uri mediaUri, string title, ServerProfile? server)
    {
        MediaUri = mediaUri;
        Title = title;
        Server = server;
    }

    public Uri MediaUri { get; }

    public string Title { get; }

    public ServerProfile? Server { get; }
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

    public string ChecksumText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Release.Sha256))
            {
                return "SHA256";
            }

            return Release.ChecksumDownloadUrl is null ? string.Empty : "SHA256 asset";
        }
    }
}

public sealed class InstalledTorrServerItem
{
    public InstalledTorrServerItem(string executablePath, string version, long sizeBytes, DateTimeOffset modifiedAt)
    {
        ExecutablePath = executablePath;
        Version = version;
        SizeBytes = sizeBytes;
        ModifiedAt = modifiedAt;
    }

    public string Version { get; }

    public string ExecutablePath { get; }

    public long SizeBytes { get; }

    public DateTimeOffset ModifiedAt { get; }

    public string ModifiedAtText => ModifiedAt.ToString("yyyy-MM-dd HH:mm:ss");

    public string SizeText => TorrentItem.FormatBytes(SizeBytes);
}
