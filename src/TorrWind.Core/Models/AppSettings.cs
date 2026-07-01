using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TorrWind.Core;

namespace TorrWind.Core.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "system";

    public Guid? ActiveServerId { get; set; }

    public List<ServerProfile> Servers { get; set; } = [];

    public LocalServerSettings LocalServer { get; set; } = new();

    public PlayerSettings Player { get; set; } = new();

    public List<SearchProviderSettings> SearchProviders { get; set; } = [];

    public List<string> SearchHistory { get; set; } = [];

    public int SettingsBackupRetentionCount { get; set; } = 20;

    public static AppSettings CreateDefault()
    {
        var localServer = ServerProfile.CreateLocal();

        return new AppSettings
        {
            ActiveServerId = localServer.Id,
            Servers = [localServer],
            LocalServer = new LocalServerSettings
            {
                Port = 8090,
                ListenAddress = "127.0.0.1",
                CacheMode = CacheMode.Memory,
                CacheSizeMb = 64,
                PreloadCachePercent = 50,
                ReaderReadAheadPercent = 95,
                TorrentDisconnectTimeoutSeconds = 30,
                ConnectionsLimit = 25,
                DataDirectory = AppPaths.DefaultLocalServerDirectory,
                TemporaryDataPath = Path.Combine(AppPaths.DefaultLocalServerDirectory, "cache")
            }
        };
    }
}

public sealed class ServerProfile : INotifyPropertyChanged
{
    private Guid _id = Guid.NewGuid();
    private string _name = "TorrServer";
    private string _baseUrl = "http://127.0.0.1:8090";
    private string? _username;
    private string? _password;
    private bool _isLocal;
    private bool _ignoreCertificateErrors;
    private bool _readOnly;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string BaseUrl
    {
        get => _baseUrl;
        set => SetProperty(ref _baseUrl, value);
    }

    public string? Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string? Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool IsLocal
    {
        get => _isLocal;
        set => SetProperty(ref _isLocal, value);
    }

    public bool IgnoreCertificateErrors
    {
        get => _ignoreCertificateErrors;
        set => SetProperty(ref _ignoreCertificateErrors, value);
    }

    public bool ReadOnly
    {
        get => _readOnly;
        set => SetProperty(ref _readOnly, value);
    }

    [JsonIgnore]
    public Uri BaseUri => NormalizeBaseUri(BaseUrl);

    public static ServerProfile CreateLocal()
    {
        return new ServerProfile
        {
            Name = "Local TorrServer",
            BaseUrl = "http://127.0.0.1:8090",
            IsLocal = true
        };
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (string.Equals(propertyName, nameof(BaseUrl), StringComparison.Ordinal))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BaseUri)));
        }

        return true;
    }

    private static Uri NormalizeBaseUri(string baseUrl)
    {
        var value = string.IsNullOrWhiteSpace(baseUrl) ? "http://127.0.0.1:8090" : baseUrl.Trim();
        if (!value.EndsWith("/", StringComparison.Ordinal))
        {
            value += "/";
        }

        return new Uri(value, UriKind.Absolute);
    }
}

public sealed class LocalServerSettings
{
    public bool Enabled { get; set; } = true;

    public bool RunAsWindowsService { get; set; }

    public string ExecutablePath { get; set; } = string.Empty;

    public string InstalledVersion { get; set; } = string.Empty;

    public string PreviousExecutablePath { get; set; } = string.Empty;

    public string PreviousVersion { get; set; } = string.Empty;

    public string DataDirectory { get; set; } = string.Empty;

    public string TemporaryDataPath { get; set; } = string.Empty;

    public string ListenAddress { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 8090;

    public bool UseHttpAuth { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool UseSsl { get; set; }

    public int SslPort { get; set; } = 8091;

    public bool ForceHttps { get; set; }

    public string CertificatePath { get; set; } = string.Empty;

    public string CertificateKeyPath { get; set; } = string.Empty;

    public bool ReadOnlyDatabase { get; set; }

    public bool AllowSearchWithoutAuth { get; set; }

    public string WhiteList { get; set; } = string.Empty;

    public string BlackList { get; set; } = string.Empty;

    public int MaxStreamSizeMb { get; set; }

    public string ProxyUrl { get; set; } = string.Empty;

    public string ProxyMode { get; set; } = string.Empty;

    public bool EnableDlna { get; set; }

    public bool EnableWebDav { get; set; }

    public CacheMode CacheMode { get; set; } = CacheMode.Memory;

    public int CacheSizeMb { get; set; } = 64;

    public int PreloadCachePercent { get; set; } = 50;

    public int ReaderReadAheadPercent { get; set; } = 95;

    public int TorrentDisconnectTimeoutSeconds { get; set; } = 30;

    public int ConnectionsLimit { get; set; } = 25;

    public int DownloadSpeedLimitKb { get; set; }

    public int UploadSpeedLimitKb { get; set; }

    public bool AllowLanAccess { get; set; }
}

public enum CacheMode
{
    Memory,
    Disk
}

public sealed class PlayerSettings
{
    public ExternalPlayerKind PreferredPlayer { get; set; } = ExternalPlayerKind.SystemDefault;

    public string CustomPlayerPath { get; set; } = string.Empty;

    public bool PreferDirectStreamUrl { get; set; } = true;
}

public enum ExternalPlayerKind
{
    SystemDefault,
    Vlc,
    MpcHc,
    PotPlayer,
    Custom,
    BuiltInLibVlc
}

public sealed class SearchProviderSettings : INotifyPropertyChanged
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Torznab";
    private string _url = string.Empty;
    private string _apiKey = string.Empty;
    private string _categories = string.Empty;
    private bool _enabled = true;
    private bool _ignoreCertificateErrors;
    private int _timeoutSeconds = 30;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    public string Categories
    {
        get => _categories;
        set => SetProperty(ref _categories, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public bool IgnoreCertificateErrors
    {
        get => _ignoreCertificateErrors;
        set => SetProperty(ref _ignoreCertificateErrors, value);
    }

    public int TimeoutSeconds
    {
        get => _timeoutSeconds;
        set => SetProperty(ref _timeoutSeconds, value);
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
