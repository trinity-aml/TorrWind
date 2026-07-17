using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace TorrWind.Core.Localization;

public sealed class JsonLocalizationService : INotifyPropertyChanged
{
    private readonly string _localesDirectory;
    private readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);

    public JsonLocalizationService(string localesDirectory)
    {
        _localesDirectory = localesDirectory;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentLanguage { get; private set; } = "en";

    public string this[string key] => _strings.TryGetValue(key, out var value) ? value : key;

    public IReadOnlyList<string> GetAvailableLanguages()
    {
        return GetAvailableLanguageFiles()
            .Keys
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task LoadSystemLanguageAsync(CancellationToken cancellationToken = default)
    {
        var languages = GetAvailableLanguages();
        var culture = CultureInfo.CurrentUICulture;
        var selected = languages.FirstOrDefault(language =>
                string.Equals(language, culture.Name, StringComparison.OrdinalIgnoreCase)) ??
            languages.FirstOrDefault(language =>
                string.Equals(language, culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase)) ??
            "en";

        await LoadAsync(selected, cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadAsync(string language, CancellationToken cancellationToken = default)
    {
        if (string.Equals(language, "system", StringComparison.OrdinalIgnoreCase))
        {
            await LoadSystemLanguageAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var requestedLanguage = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim();
        var languageFiles = GetAvailableLanguageFiles();
        languageFiles.TryGetValue("en", out var fallbackFilePath);
        if (!languageFiles.TryGetValue(requestedLanguage, out var selectedFilePath))
        {
            requestedLanguage = "en";
            selectedFilePath = fallbackFilePath;
        }
        else
        {
            requestedLanguage = Path.GetFileNameWithoutExtension(selectedFilePath);
        }

        var loadedStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fallbackLoaded = await TryLoadFileIntoAsync(fallbackFilePath, loadedStrings, cancellationToken)
            .ConfigureAwait(false);

        var selectedLoaded = fallbackLoaded && string.Equals(
            selectedFilePath,
            fallbackFilePath,
            StringComparison.OrdinalIgnoreCase);
        if (!selectedLoaded && !string.IsNullOrWhiteSpace(selectedFilePath))
        {
            selectedLoaded = await TryLoadFileIntoAsync(selectedFilePath, loadedStrings, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!selectedLoaded && fallbackLoaded)
        {
            requestedLanguage = "en";
        }
        else if (!selectedLoaded)
        {
            return;
        }

        _strings.Clear();
        foreach (var (key, value) in loadedStrings)
        {
            _strings[key] = value;
        }

        CurrentLanguage = requestedLanguage;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
    }

    private Dictionary<string, string> GetAvailableLanguageFiles()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(_localesDirectory))
        {
            return files;
        }

        try
        {
            foreach (var filePath in Directory
                         .EnumerateFiles(_localesDirectory, "*.json", SearchOption.TopDirectoryOnly)
                         .Order(StringComparer.OrdinalIgnoreCase))
            {
                var language = Path.GetFileNameWithoutExtension(filePath);
                if (!string.IsNullOrWhiteSpace(language))
                {
                    files.TryAdd(language, filePath);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        return files;
    }

    private static async Task<bool> TryLoadFileIntoAsync(
        string? filePath,
        Dictionary<string, string> target,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        Dictionary<string, string?>? values;
        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            values = await JsonSerializer.DeserializeAsync<Dictionary<string, string?>>(
                    stream,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }

        if (values is null)
        {
            return false;
        }

        foreach (var (key, value) in values)
        {
            if (!string.IsNullOrWhiteSpace(key) && value is not null)
            {
                target[key] = value;
            }
        }

        return true;
    }
}
