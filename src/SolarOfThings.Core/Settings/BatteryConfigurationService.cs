using System.Globalization;

namespace SolarOfThings.Core.Settings;

public sealed record BatteryConfiguration(double UsableCapacityKwh);

public sealed class BatteryConfigurationService
{
    private const string UsableCapacityKey = "battery.usable-capacity-kwh";
    public const double DefaultUsableCapacityKwh = 11.776;

    private readonly AppSettingsRepository _settings;

    public BatteryConfigurationService(AppSettingsRepository settings)
    {
        _settings = settings;
    }

    public BatteryConfiguration Get()
    {
        var raw = _settings.Get(UsableCapacityKey);

        if (double.TryParse(
                raw,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) &&
            parsed is >= 1 and <= 100)
        {
            return new BatteryConfiguration(parsed);
        }

        return new BatteryConfiguration(DefaultUsableCapacityKwh);
    }

    public void SaveUsableCapacity(double usableCapacityKwh)
    {
        if (usableCapacityKwh is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(usableCapacityKwh),
                "Usable battery capacity must be between 1 and 100 kWh.");
        }

        _settings.Set(
            UsableCapacityKey,
            usableCapacityKwh.ToString("0.###", CultureInfo.InvariantCulture));
    }
}
