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
                CacheSizeMb = 512,
                DataDirectory = AppPaths.DefaultLocalServerDirectory,
                TemporaryDataPath = Path.Combine(AppPaths.DefaultLocalServerDirectory, "cache")
            }
        };
    }
}

public sealed class ServerProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "TorrServer";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8090";

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool IsLocal { get; set; }

    public bool IgnoreCertificateErrors { get; set; }

    public bool ReadOnly { get; set; }

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

    public int CacheSizeMb { get; set; } = 512;

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
    Custom
}

public sealed class SearchProviderSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Torznab";

    public string Url { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Categories { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public bool IgnoreCertificateErrors { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}
