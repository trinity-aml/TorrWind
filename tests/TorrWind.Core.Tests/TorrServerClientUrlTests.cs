using TorrWind.Core.Models;
using TorrWind.Core.Services;

namespace TorrWind.Core.Tests;

public sealed class TorrServerClientUrlTests
{
    [Fact]
    public void GetPlaylistUri_UsesStreamEndpointWithM3uFileNameAndLinkQuery()
    {
        using var client = new TorrServerClient(new ServerProfile
        {
            BaseUrl = "http://127.0.0.1:8090"
        });

        var uri = client.GetPlaylistUri(
            "c708fd241fb88b781b9bd1f691dd9a9ab307b824",
            "Venom (2018) UHD BDRip 1080p [HEVC] 10 bit.mkv");

        Assert.Equal(
            "http://127.0.0.1:8090/stream/Venom%20(2018)%20UHD%20BDRip%201080p%20%5BHEVC%5D%2010%20bit.mkv.m3u?link=c708fd241fb88b781b9bd1f691dd9a9ab307b824&m3u",
            uri.AbsoluteUri);
    }

    [Fact]
    public void GetPlaylistUri_DoesNotDuplicateExistingM3uExtension()
    {
        using var client = new TorrServerClient(new ServerProfile
        {
            BaseUrl = "http://127.0.0.1:8090/"
        });

        var uri = client.GetPlaylistUri("hash", "Show.S01.m3u");

        Assert.Equal("http://127.0.0.1:8090/stream/Show.S01.m3u?link=hash&m3u", uri.AbsoluteUri);
    }

    [Fact]
    public void GetStreamUri_UsesFileNameOnlyAndKeepsPlaybackQuery()
    {
        using var client = new TorrServerClient(new ServerProfile
        {
            BaseUrl = "http://127.0.0.1:8090"
        });

        var uri = client.GetStreamUri(
            "8a8364fd34b0b876ae69063c8c1dfae6eedae03c",
            4,
            "The.Gentlemen.S01/episode 04.mkv",
            "session-1");

        Assert.Equal(
            "http://127.0.0.1:8090/stream/episode%2004.mkv?link=8a8364fd34b0b876ae69063c8c1dfae6eedae03c&index=4&play&ss=session-1",
            uri.AbsoluteUri);
    }
}
