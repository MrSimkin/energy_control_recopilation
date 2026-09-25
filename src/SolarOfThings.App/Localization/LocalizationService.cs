using System.Globalization;
using System.Windows;
using SolarOfThings.Core.Settings;

namespace SolarOfThings.App.Localization;

public sealed class LocalizationService
{
    private const string LanguageSettingKey = "ui.language";
    private const string DefaultLanguage = "es";
    private readonly AppSettingsRepository _settings;

    public LocalizationService(AppSettingsRepository settings)
    {
        _settings = settings;
    }

    public string CurrentLanguage { get; private set; } = DefaultLanguage;

    public void Initialize()
    {
        var storedLanguage = NormalizeLanguage(_settings.Get(LanguageSettingKey));
        ApplyLanguage(storedLanguage, persist: false);
    }

    public void SetLanguage(string? language)
    {
        ApplyLanguage(NormalizeLanguage(language), persist: true);
    }

    public string GetString(string key)
    {
        return Application.Current.TryFindResource(key)?.ToString() ?? key;
    }

    private void ApplyLanguage(string language, bool persist)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var currentDictionary = dictionaries.FirstOrDefault(
            dictionary => dictionary.Source?.OriginalString.Contains("Resources/Strings.", StringComparison.OrdinalIgnoreCase) == true);

        var newDictionary = new ResourceDictionary
        {
            Source = new Uri($"Resources/Strings.{language}.xaml", UriKind.Relative)
        };

        if (currentDictionary is not null)
        {
            var index = dictionaries.IndexOf(currentDictionary);
            dictionaries[index] = newDictionary;
        }
        else
        {
            dictionaries.Insert(0, newDictionary);
        }

        CurrentLanguage = language;

        var culture = language == "en"
            ? CultureInfo.GetCultureInfo("en-US")
            : CultureInfo.GetCultureInfo("es-CL");

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        if (persist)
        {
            _settings.Set(LanguageSettingKey, language);
        }
    }

    private static string NormalizeLanguage(string? language)
    {
        return string.Equals(language?.Trim(), "en", StringComparison.OrdinalIgnoreCase)
            ? "en"
            : DefaultLanguage;
    }
}
