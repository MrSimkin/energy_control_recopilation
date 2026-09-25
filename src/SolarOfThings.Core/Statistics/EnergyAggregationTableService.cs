namespace SolarOfThings.Core.Statistics;

public sealed class EnergyAggregationTableService
{
    private readonly PowerAggregationService _power;
    private readonly SocAggregationService _soc;

    public EnergyAggregationTableService(
        PowerAggregationService power,
        SocAggregationService soc)
    {
        _power = power;
        _soc = soc;
    }

    public EnergyAggregationTable Get(
        string deviceId,
        DateTimeOffset rangeStartUtc,
        DateTimeOffset rangeEndUtc,
        string timeZoneId,
        AggregationPeriod period)
    {
        var pv = _power.GetSeries(
            deviceId,
            "pv_power_w",
            rangeStartUtc,
            rangeEndUtc,
            timeZoneId,
            period);

        var house = _power.GetSeries(
            deviceId,
            "house_load_power_w",
            rangeStartUtc,
            rangeEndUtc,
            timeZoneId,
            period);

        var grid = _power.GetSeries(
            deviceId,
            "grid_import_power_w",
            rangeStartUtc,
            rangeEndUtc,
            timeZoneId,
            period);

        var battery = _power.GetSeries(
            deviceId,
            "battery_power_w",
            rangeStartUtc,
            rangeEndUtc,
            timeZoneId,
            period);

        var soc = _soc.GetSeries(
            deviceId,
            rangeStartUtc,
            rangeEndUtc,
            timeZoneId,
            period);

        var pvByStart = pv.Buckets.ToDictionary(
            bucket => bucket.StartUtc);
        var houseByStart = house.Buckets.ToDictionary(
            bucket => bucket.StartUtc);
        var gridByStart = grid.Buckets.ToDictionary(
            bucket => bucket.StartUtc);
        var batteryByStart = battery.Buckets.ToDictionary(
            bucket => bucket.StartUtc);
        var socByStart = soc.Buckets.ToDictionary(
            bucket => bucket.StartUtc);

        var allStarts = pv.Buckets
            .Select(bucket => bucket.StartUtc)
            .Concat(house.Buckets.Select(bucket => bucket.StartUtc))
            .Concat(grid.Buckets.Select(bucket => bucket.StartUtc))
            .Concat(battery.Buckets.Select(bucket => bucket.StartUtc))
            .Concat(soc.Buckets.Select(bucket => bucket.StartUtc))
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

        var metricPresent = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["pv"] = pv.Buckets.Any(bucket => bucket.SampleCount > 0),
            ["house"] = house.Buckets.Any(bucket => bucket.SampleCount > 0),
            ["grid"] = grid.Buckets.Any(bucket => bucket.SampleCount > 0),
            ["battery"] = battery.Buckets.Any(bucket => bucket.SampleCount > 0),
            ["soc"] = soc.Buckets.Any(bucket => bucket.SampleCount > 0)
        };

        var rows = new List<EnergyAggregationRow>(allStarts.Length);

        foreach (var start in allStarts)
        {
            pvByStart.TryGetValue(start, out var pvBucket);
            houseByStart.TryGetValue(start, out var houseBucket);
            gridByStart.TryGetValue(start, out var gridBucket);
            batteryByStart.TryGetValue(start, out var batteryBucket);
            socByStart.TryGetValue(start, out var socBucket);

            var label =
                pvBucket?.LocalLabel ??
                houseBucket?.LocalLabel ??
                gridBucket?.LocalLabel ??
                batteryBucket?.LocalLabel ??
                socBucket?.LocalLabel ??
                start.ToString("O");

            var end =
                pvBucket?.EndUtcExclusive ??
                houseBucket?.EndUtcExclusive ??
                gridBucket?.EndUtcExclusive ??
                batteryBucket?.EndUtcExclusive ??
                socBucket?.EndUtcExclusive ??
                start;

            var availableCoverage = new List<double>(5);

            if (metricPresent["pv"] && pvBucket is not null)
            {
                availableCoverage.Add(pvBucket.CoveragePercent);
            }

            if (metricPresent["house"] && houseBucket is not null)
            {
                availableCoverage.Add(houseBucket.CoveragePercent);
            }

            if (metricPresent["grid"] && gridBucket is not null)
            {
                availableCoverage.Add(gridBucket.CoveragePercent);
            }

            if (metricPresent["battery"] && batteryBucket is not null)
            {
                availableCoverage.Add(batteryBucket.CoveragePercent);
            }

            if (metricPresent["soc"] && socBucket is not null)
            {
                availableCoverage.Add(socBucket.CoveragePercent);
            }

            var minimumCoverage = availableCoverage.Count > 0
                ? availableCoverage.Min()
                : 0;

            rows.Add(new EnergyAggregationRow(
                label,
                start,
                end,
                pvBucket?.PositiveEnergyKwh ?? 0,
                houseBucket?.PositiveEnergyKwh ?? 0,
                gridBucket?.PositiveEnergyKwh ?? 0,
                batteryBucket?.PositiveEnergyKwh ?? 0,
                batteryBucket?.NegativeEnergyKwh ?? 0,
                socBucket?.AveragePercent,
                socBucket?.MinimumPercent,
                socBucket?.MaximumPercent,
                socBucket?.EndingPercent,
                minimumCoverage));
        }

        return new EnergyAggregationTable(
            deviceId,
            period,
            timeZoneId,
            rangeStartUtc.ToUniversalTime(),
            rangeEndUtc.ToUniversalTime(),
            rows);
    }
}
