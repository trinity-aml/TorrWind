using TorrWind.Core.Localization;

namespace TorrWind.Core.Tests;

public sealed class JsonLocalizationServiceTests
{
    [Fact]
    public async Task LoadAsync_MergesEnglishFallbackWithSelectedLanguage()
    {
        using var directory = TemporaryDirectory.Create();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "en.json"), """
            {
              "Shared": "English",
              "OnlyEnglish": "Fallback"
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "ru.json"), """
            {
              "Shared": "Russian"
            }
            """);
        var localization = new JsonLocalizationService(directory.Path);

        await localization.LoadAsync("ru");

        Assert.Equal("ru", localization.CurrentLanguage);
        Assert.Equal("Russian", localization["Shared"]);
        Assert.Equal("Fallback", localization["OnlyEnglish"]);
        Assert.Equal("MissingKey", localization["MissingKey"]);
    }

    [Fact]
    public async Task LoadAsync_FallsBackToEnglishWhenLanguageFileIsMissing()
    {
        using var directory = TemporaryDirectory.Create();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "en.json"), """
            {
              "Shared": "English"
            }
            """);
        var localization = new JsonLocalizationService(directory.Path);

        await localization.LoadAsync("de");

        Assert.Equal("en", localization.CurrentLanguage);
        Assert.Equal("English", localization["Shared"]);
    }

    [Fact]
    public async Task LoadAsync_LoadsSelectedLanguageWhenEnglishFallbackIsMissing()
    {
        using var directory = TemporaryDirectory.Create();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "ru.json"), """
            {
              "Shared": "Russian"
            }
            """);
        var localization = new JsonLocalizationService(directory.Path);

        await localization.LoadAsync("ru");

        Assert.Equal("ru", localization.CurrentLanguage);
        Assert.Equal("Russian", localization["Shared"]);
    }

    [Fact]
    public async Task LoadAsync_IgnoresInvalidSelectedLanguageFileAndKeepsEnglishFallback()
    {
        using var directory = TemporaryDirectory.Create();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "en.json"), """
            {
              "Shared": "English"
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "ru.json"), "{ not-json");
        var localization = new JsonLocalizationService(directory.Path);

        await localization.LoadAsync("ru");

        Assert.Equal("en", localization.CurrentLanguage);
        Assert.Equal("English", localization["Shared"]);
    }

    [Fact]
    public async Task LoadAsync_DoesNotResolveLanguageOutsideLocalesDirectory()
    {
        using var directory = TemporaryDirectory.Create();
        var localesDirectory = Path.Combine(directory.Path, "locales");
        Directory.CreateDirectory(localesDirectory);
        await File.WriteAllTextAsync(Path.Combine(localesDirectory, "en.json"), """
            {
              "Shared": "English"
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "outside.json"), """
            {
              "Shared": "Outside"
            }
            """);
        var localization = new JsonLocalizationService(localesDirectory);

        await localization.LoadAsync("../outside");

        Assert.Equal("en", localization.CurrentLanguage);
        Assert.Equal("English", localization["Shared"]);
    }

    [Fact]
    public async Task LoadAsync_UsesActualFilePathForCaseInsensitiveLanguageMatch()
    {
        using var directory = TemporaryDirectory.Create();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "RU.json"), """
            {
              "Shared": "Russian"
            }
            """);
        var localization = new JsonLocalizationService(directory.Path);

        await localization.LoadAsync("ru");

        Assert.Equal("RU", localization.CurrentLanguage);
        Assert.Equal("Russian", localization["Shared"]);
    }

    [Fact]
    public async Task LoadAsync_KeepsCurrentStringsWhenAllLocaleFilesBecomeInvalid()
    {
        using var directory = TemporaryDirectory.Create();
        var englishPath = Path.Combine(directory.Path, "en.json");
        await File.WriteAllTextAsync(englishPath, """
            {
              "Shared": "English"
            }
            """);
        var localization = new JsonLocalizationService(directory.Path);
        await localization.LoadAsync("en");
        await File.WriteAllTextAsync(englishPath, "{ not-json");

        await localization.LoadAsync("en");

        Assert.Equal("en", localization.CurrentLanguage);
        Assert.Equal("English", localization["Shared"]);
    }

    [Fact]
    public async Task LoadAsync_IgnoresBlankKeysAndNullValues()
    {
        using var directory = TemporaryDirectory.Create();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "en.json"), """
            {
              "": "Blank key",
              "NullValue": null,
              "Shared": "English"
            }
            """);
        var localization = new JsonLocalizationService(directory.Path);

        await localization.LoadAsync("en");

        Assert.Equal("English", localization["Shared"]);
        Assert.Equal("NullValue", localization["NullValue"]);
    }

    [Fact]
    public void GetAvailableLanguages_ReturnsSortedJsonFileNamesOnly()
    {
        using var directory = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(directory.Path, "ru.json"), "{}");
        File.WriteAllText(Path.Combine(directory.Path, "en.json"), "{}");
        File.WriteAllText(Path.Combine(directory.Path, "readme.txt"), "");
        var localization = new JsonLocalizationService(directory.Path);

        Assert.Equal(["en", "ru"], localization.GetAvailableLanguages());
    }

    [Fact]
    public async Task BundledLocales_HaveMatchingKeys()
    {
        var localesDirectory = Path.Combine(AppContext.BaseDirectory, "locales");
        var en = await ReadLocaleAsync(Path.Combine(localesDirectory, "en.json"));
        var ru = await ReadLocaleAsync(Path.Combine(localesDirectory, "ru.json"));

        Assert.NotEmpty(en);
        Assert.Empty(en.Keys.Except(ru.Keys, StringComparer.OrdinalIgnoreCase));
        Assert.Empty(ru.Keys.Except(en.Keys, StringComparer.OrdinalIgnoreCase));
    }

    private static async Task<Dictionary<string, string>> ReadLocaleAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return await System.Text.Json.JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream) ??
            [];
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "torrwind-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
