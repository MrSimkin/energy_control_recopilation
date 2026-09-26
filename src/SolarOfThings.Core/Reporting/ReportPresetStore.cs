using System.Text.Json;
using SolarOfThings.Core.Settings;

namespace SolarOfThings.Core.Reporting;

public sealed class ReportPresetStore
{
    private const string SettingKey = "report.presets.v1";
    private readonly AppSettingsRepository _settings;

    public ReportPresetStore(AppSettingsRepository settings)
    {
        _settings = settings;
    }

    public IReadOnlyList<ReportPreset> GetAll()
    {
        var json = _settings.Get(SettingKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<ReportPreset>();
        }

        try
        {
            return (JsonSerializer.Deserialize<List<ReportPreset>>(json) ?? [])
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<ReportPreset>();
        }
    }

    public void Save(ReportPreset preset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preset.Name);

        var items = GetAll()
            .Where(item => !string.Equals(
                item.Name,
                preset.Name,
                StringComparison.OrdinalIgnoreCase))
            .Append(preset)
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        _settings.Set(
            SettingKey,
            JsonSerializer.Serialize(items));
    }

    public void Delete(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var items = GetAll()
            .Where(item => !string.Equals(
                item.Name,
                name,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (items.Length == 0)
        {
            _settings.Delete(SettingKey);
        }
        else
        {
            _settings.Set(
                SettingKey,
                JsonSerializer.Serialize(items));
        }
    }
}
