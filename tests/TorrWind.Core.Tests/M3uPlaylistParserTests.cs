using TorrWind.Core.Services;

namespace TorrWind.Core.Tests;

public sealed class M3uPlaylistParserTests
{
    [Fact]
    public void LooksLikePlaylist_DetectsExtensionsAndQueryMarker()
    {
        Assert.True(M3uPlaylistParser.LooksLikePlaylist(new Uri("http://127.0.0.1:8090/stream/show.m3u")));
        Assert.True(M3uPlaylistParser.LooksLikePlaylist(new Uri("http://127.0.0.1:8090/stream/show.m3u8")));
        Assert.True(M3uPlaylistParser.LooksLikePlaylist(new Uri("http://127.0.0.1:8090/stream/movie.mkv?link=hash&m3u")));
        Assert.False(M3uPlaylistParser.LooksLikePlaylist(new Uri("http://127.0.0.1:8090/stream/movie.mkv?link=hash&play")));
    }

    [Fact]
    public void Parse_UsesExtinfTitlesAndResolvesRelativeUrls()
    {
        const string playlist = """
            #EXTM3U
            #EXTINF:0,Episode One
            episode1.mkv
            #EXTINF:-1 tvg-id="two",Episode Two
            http://media.local/video%202.mkv
            #EXT-X-VERSION:3
            folder/episode3.mp4
            """;

        var entries = M3uPlaylistParser.Parse(
            playlist,
            new Uri("http://127.0.0.1:8090/playlists/show.m3u"));

        Assert.Equal(3, entries.Count);
        Assert.Equal(1, entries[0].Number);
        Assert.Equal("Episode One", entries[0].Title);
        Assert.Equal("http://127.0.0.1:8090/playlists/episode1.mkv", entries[0].Uri.AbsoluteUri);
        Assert.Equal("Episode Two", entries[1].Title);
        Assert.Equal("http://media.local/video%202.mkv", entries[1].Uri.AbsoluteUri);
        Assert.Equal(3, entries[2].Number);
        Assert.Equal("episode3.mp4", entries[2].Title);
        Assert.Equal("http://127.0.0.1:8090/playlists/folder/episode3.mp4", entries[2].Uri.AbsoluteUri);
    }

    [Fact]
    public void Parse_FallsBackToEpisodeTitleWhenUriHasNoFileName()
    {
        var entries = M3uPlaylistParser.Parse(
            "http://127.0.0.1:8090/stream/?link=hash&index=1&play",
            new Uri("http://127.0.0.1:8090/list.m3u"));

        var entry = Assert.Single(entries);
        Assert.Equal("Episode 1", entry.Title);
    }

    [Fact]
    public void Parse_IgnoresCommasInsideExtinfAttributes()
    {
        const string playlist = """
            #EXTM3U
            #EXTINF:-1 tvg-name="Title, From Attribute" group-title="Shows, HD",Episode, With Comma
            episode1.mkv
            """;

        var entry = Assert.Single(M3uPlaylistParser.Parse(
            playlist,
            new Uri("http://127.0.0.1:8090/playlists/show.m3u")));

        Assert.Equal("Episode, With Comma", entry.Title);
        Assert.Equal("http://127.0.0.1:8090/playlists/episode1.mkv", entry.Uri.AbsoluteUri);
    }

    [Fact]
    public void Parse_IgnoresUtf8BomAtStartOfPlaylist()
    {
        var entries = M3uPlaylistParser.Parse(
            "\uFEFF#EXTM3U" + Environment.NewLine + "#EXTINF:0,Episode One" + Environment.NewLine + "episode1.mkv",
            new Uri("http://127.0.0.1:8090/list.m3u"));

        var entry = Assert.Single(entries);
        Assert.Equal("Episode One", entry.Title);
        Assert.Equal("http://127.0.0.1:8090/episode1.mkv", entry.Uri.AbsoluteUri);
    }
}
