using SolarOfThings.Core.Normalization;

namespace SolarOfThings.Core.Installation;

public sealed record HouseholdOperatingState(
    string StateKey,
    DateTimeOffset? ObservedAtUtc);

public sealed class HouseholdOperatingStateService
{
    public HouseholdOperatingState Evaluate(
        IReadOnlyDictionary<string, NormalizedMetricValue> metrics)
    {
        metrics.TryGetValue("pv_power_w", out var pv);
        metrics.TryGetValue("house_load_power_w", out var house);
        metrics.TryGetValue("grid_import_power_w", out var grid);
        metrics.TryGetValue("grid_voltage_v", out var gridVoltage);
        metrics.TryGetValue("battery_soc_pct", out var soc);
        metrics.TryGetValue("battery_power_w", out var battery);

        var latest = new[]
            {
                pv?.RecordedAtUtc,
                house?.RecordedAtUtc,
                grid?.RecordedAtUtc,
                gridVoltage?.RecordedAtUtc,
                soc?.RecordedAtUtc,
                battery?.RecordedAtUtc
            }
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty()
            .Max();

        DateTimeOffset? observedAt = latest == default ? null : latest;

        if (soc is not null &&
            gridVoltage is not null &&
            gridVoltage.Value < 50)
        {
            if (soc.Value <= 10.5)
            {
                return new HouseholdOperatingState(
                    "OUTAGE_PROTECTED_FLOOR",
                    observedAt);
            }

            if (soc.Value < 20)
            {
                return new HouseholdOperatingState(
                    "OUTAGE_EMERGENCY_RESERVE",
                    observedAt);
            }

            if (battery is not null && battery.Value > 100)
            {
                return new HouseholdOperatingState(
                    "OUTAGE_BATTERY",
                    observedAt);
            }
        }

        if (grid is not null &&
            grid.Value > 100 &&
            soc is not null &&
            soc.Value < 50)
        {
            if (pv is not null &&
                pv.Value > 100 &&
                battery is not null &&
                battery.Value < -50)
            {
                return new HouseholdOperatingState(
                    "GRID_RECOVERY",
                    observedAt);
            }

            return new HouseholdOperatingState(
                "GRID_LOW_SOC",
                observedAt);
        }

        if (battery is not null &&
            battery.Value > 100 &&
            (grid is null || grid.Value < 100))
        {
            return new HouseholdOperatingState(
                "BATTERY_SUPPLY",
                observedAt);
        }

        if (pv is not null &&
            pv.Value > 100 &&
            battery is not null &&
            battery.Value < -100 &&
            (grid is null || grid.Value < 100))
        {
            return new HouseholdOperatingState(
                "SOLAR_AND_CHARGING",
                observedAt);
        }

        if (pv is not null &&
            pv.Value > 100 &&
            house is not null &&
            pv.Value >= house.Value * 0.7 &&
            (grid is null || grid.Value < 100))
        {
            return new HouseholdOperatingState(
                "SOLAR_PRIMARY",
                observedAt);
        }

        if (grid is not null && grid.Value > 100)
        {
            return new HouseholdOperatingState(
                "GRID_SUPPLY",
                observedAt);
        }

        if (battery is not null && battery.Value < -100)
        {
            return new HouseholdOperatingState(
                "BATTERY_CHARGING",
                observedAt);
        }

        return new HouseholdOperatingState(
            observedAt.HasValue ? "MIXED" : "NO_DATA",
            observedAt);
    }
}
