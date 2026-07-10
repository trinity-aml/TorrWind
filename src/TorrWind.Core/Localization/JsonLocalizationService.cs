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
        if (!Directory.Exists(_localesDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(_localesDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Cast<string>()
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task LoadSystemLanguageAsync(CancellationToken cancellationToken = default)
    {
        var cultureName = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var selected = GetAvailableLanguages().Contains(cultureName, StringComparer.OrdinalIgnoreCase)
            ? cultureName
            : "en";

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
        var selectedFilePath = Path.Combine(_localesDirectory, requestedLanguage + ".json");
        var languageExists = File.Exists(selectedFilePath);
        if (!languageExists)
        {
            requestedLanguage = "en";
            selectedFilePath = Path.Combine(_localesDirectory, "en.json");
        }

        _strings.Clear();

        var fallbackFilePath = Path.Combine(_localesDirectory, "en.json");
        await TryLoadFileIntoAsync(fallbackFilePath, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(requestedLanguage, "en", StringComparison.OrdinalIgnoreCase))
        {
            var selectedLoaded = await TryLoadFileIntoAsync(selectedFilePath, cancellationToken).ConfigureAwait(false);
            if (!selectedLoaded)
            {
                requestedLanguage = "en";
            }
        }

        CurrentLanguage = requestedLanguage;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
    }

    private async Task<bool> TryLoadFileIntoAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        Dictionary<string, string>? values;
        try
        {
            await using var stream = File.OpenRead(filePath);
            values = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: cancellationToken)
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
            _strings[key] = value;
        }

        return true;
    }
}
