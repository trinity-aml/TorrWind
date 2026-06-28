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

        var filePath = Path.Combine(_localesDirectory, language + ".json");
        if (!File.Exists(filePath))
        {
            filePath = Path.Combine(_localesDirectory, "en.json");
            language = "en";
        }

        _strings.Clear();

        if (File.Exists(filePath))
        {
            await using var stream = File.OpenRead(filePath);
            var values = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (values is not null)
            {
                foreach (var (key, value) in values)
                {
                    _strings[key] = value;
                }
            }
        }

        CurrentLanguage = language;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
    }
}
