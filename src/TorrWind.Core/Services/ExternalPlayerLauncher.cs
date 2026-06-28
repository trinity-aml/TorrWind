using System.Diagnostics;
using TorrWind.Core.Models;

namespace TorrWind.Core.Services;

public sealed class ExternalPlayerLauncher
{
    public void Play(Uri mediaUri, PlayerSettings settings)
    {
        var playerPath = ResolvePlayerPath(settings);

        if (string.IsNullOrWhiteSpace(playerPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = mediaUri.ToString(),
                UseShellExecute = true
            });
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = playerPath,
            UseShellExecute = false,
            ArgumentList = { mediaUri.ToString() }
        });
    }

    private static string ResolvePlayerPath(PlayerSettings settings)
    {
        return settings.PreferredPlayer switch
        {
            ExternalPlayerKind.Custom => settings.CustomPlayerPath,
            ExternalPlayerKind.Vlc => FirstExisting(
                @"C:\Program Files\VideoLAN\VLC\vlc.exe",
                @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe"),
            ExternalPlayerKind.MpcHc => FirstExisting(
                @"C:\Program Files\MPC-HC\mpc-hc64.exe",
                @"C:\Program Files (x86)\MPC-HC\mpc-hc.exe"),
            ExternalPlayerKind.PotPlayer => FirstExisting(
                @"C:\Program Files\DAUM\PotPlayer\PotPlayerMini64.exe",
                @"C:\Program Files (x86)\DAUM\PotPlayer\PotPlayerMini.exe"),
            _ => string.Empty
        };
    }

    private static string FirstExisting(params string[] paths)
    {
        return paths.FirstOrDefault(File.Exists) ?? string.Empty;
    }
}
