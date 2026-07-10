using System.Text.Json;
using TorrWind.Core.Models;

namespace TorrWind.Core.Tests;

public sealed class TorrentFileModelTests
{
    [Theory]
    [InlineData("Movie.Name.(2024).WEB-DL.1080p.mkv", "Movie.Name.(2024).WEB-DL.1080p")]
    [InlineData("The Gentlemen [S01] (2024)/Episode 01 - Refined.mkv", "Episode 01 - Refined")]
    [InlineData("folder\\Show.S01E02.avi", "Show.S01E02")]
    [InlineData("Documentary.2020", "Documentary.2020")]
    public void DisplayName_PreservesApiFileNameWithoutVideoExtension(string path, string expected)
    {
        var file = new TorrentFile { Path = path };

        Assert.Equal(expected, file.DisplayName);
    }

    [Theory]
    [InlineData("Show.S01E02.mkv", "1", "2")]
    [InlineData("Show.S01E02E03.mkv", "1", "2")]
    [InlineData("Show.S01.E02.mkv", "1", "2")]
    [InlineData("Show.S01-Е02.mkv", "1", "2")]
    [InlineData("Show.С01Е02.mkv", "1", "2")]
    [InlineData("Show.1x02.mkv", "1", "2")]
    public void SeasonAndEpisodeText_ParseCommonEpisodeMarkers(string path, string expectedSeason, string expectedEpisode)
    {
        var file = new TorrentFile { Path = path };

        Assert.Equal(expectedSeason, file.SeasonText);
        Assert.Equal(expectedEpisode, file.EpisodeText);
    }

    [Theory]
    [InlineData("movie.4320p.mkv", "4320p")]
    [InlineData("movie.4K.mkv", "2160p")]
    [InlineData("movie.UHD.mkv", "2160p")]
    [InlineData("movie.FHD.mkv", "1080p")]
    [InlineData("movie.1280x720.avi", "1280x720")]
    public void FromJson_ResolvesResolutionFromFileName(string path, string expectedResolution)
    {
        var file = TorrentFile.FromJson(Json($$"""
            {
              "path": "{{path}}",
              "size": 100
            }
            """));

        Assert.Equal(expectedResolution, file.Resolution);
    }

    [Fact]
    public void FromJson_PrefersExplicitResolutionOverFileNameAndDimensions()
    {
        var file = TorrentFile.FromJson(Json("""
            {
              "path": "movie.720p.mkv",
              "resolution": "HDRip",
              "width": 1920,
              "height": 1080
            }
            """));

        Assert.Equal("HDRip", file.Resolution);
    }

    [Fact]
    public void FromJson_UsesDimensionsWhenExplicitResolutionIsMissing()
    {
        var file = TorrentFile.FromJson(Json("""
            {
              "path": "movie.mkv",
              "width": 1920,
              "height": 1080
            }
            """));

        Assert.Equal("1920x1080", file.Resolution);
    }

    [Theory]
    [InlineData("movie.avi")]
    [InlineData("movie.mkv")]
    [InlineData("movie.mp4")]
    [InlineData("movie.m2ts")]
    [InlineData("movie.ts")]
    [InlineData("movie.webm")]
    [InlineData("movie.wmv")]
    [InlineData("movie.mov")]
    [InlineData("movie.vob")]
    [InlineData("movie.iso")]
    public void IsVideoFilePath_RecognizesCommonVideoExtensions(string path)
    {
        Assert.True(TorrentFile.IsVideoFilePath(path));
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
