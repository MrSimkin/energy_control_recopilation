using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Statistics;

public sealed class EnergyRangeStatisticsService
{
    private static readonly string[] PowerMetricKeys =
    [
        "pv_power_w",
        "house_load_power_w",
        "grid_import_power_w",
        "battery_power_w"
    ];

    private readonly SqliteDatabase _database;

    public EnergyRangeStatisticsService(SqliteDatabase database)
    {
        _database = database;
    }

    public EnergyRangeSummary Get(
        string deviceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        if (toUtc < fromUtc)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        return new EnergyRangeSummary(
            deviceId,
            fromUtc.ToUniversalTime(),
            toUtc.ToUniversalTime(),
            GetMetric(deviceId, "pv_power_w", fromUtc, toUtc),
            GetMetric(deviceId, "house_load_power_w", fromUtc, toUtc),
            GetMetric(deviceId, "grid_import_power_w", fromUtc, toUtc),
            GetMetric(deviceId, "battery_power_w", fromUtc, toUtc));
    }

    public PowerMetricStatistics GetMetric(
        string deviceId,
        string metricKey,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        if (!PowerMetricKeys.Contains(metricKey, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(
                nameof(metricKey),
                metricKey,
                "Metric is not a supported normalized power metric.");
        }

        if (toUtc < fromUtc)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        fromUtc = fromUtc.ToUniversalTime();
        toUtc = toUtc.ToUniversalTime();

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT recorded_at_utc, normalized_value
            FROM normalized_metric_sample
            WHERE device_id = $deviceId
              AND metric_key = $metricKey
              AND normalized_value IS NOT NULL
              AND confidence <> 'UNRESOLVED'
              AND recorded_at_utc >= $fromUtc
              AND recorded_at_utc <= $toUtc
            ORDER BY recorded_at_utc;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);
        command.Parameters.AddWithValue("$metricKey", metricKey);
        command.Parameters.AddWithValue("$fromUtc", fromUtc.ToString("O"));
        command.Parameters.AddWithValue("$toUtc", toUtc.ToString("O"));

        using var reader = command.ExecuteReader();
        var samples = new List<PowerSample>();

        while (reader.Read())
        {
            if (!DateTimeOffset.TryParse(reader.GetString(0), out var timestamp))
            {
                continue;
            }

            samples.Add(new PowerSample(
                timestamp.ToUniversalTime(),
                reader.GetDouble(1)));
        }

        if (samples.Count == 0)
        {
            return Empty(metricKey);
        }

        var gapsMinutes = new List<double>();
        for (var i = 0; i < samples.Count - 1; i++)
        {
            var gap = (samples[i + 1].TimestampUtc - samples[i].TimestampUtc).TotalMinutes;
            if (gap > 0)
            {
                gapsMinutes.Add(gap);
            }
        }

        var medianGapMinutes = Median(gapsMinutes);
        var continuityThresholdMinutes = medianGapMinutes > 0
            ? Math.Clamp(medianGapMinutes * 3.0, 10.0, 20.0)
            : 15.0;

        var positiveWh = 0.0;
        var negativeWh = 0.0;
        var coveredHours = 0.0;
        var uncoveredHours = 0.0;

        if (samples[0].TimestampUtc > fromUtc)
        {
            uncoveredHours += (samples[0].TimestampUtc - fromUtc).TotalHours;
        }

        for (var i = 0; i < samples.Count - 1; i++)
        {
            var current = samples[i];
            var next = samples[i + 1];
            var gap = next.TimestampUtc - current.TimestampUtc;

            if (gap <= TimeSpan.Zero)
            {
                continue;
            }

            if (gap.TotalMinutes > continuityThresholdMinutes)
            {
                uncoveredHours += gap.TotalHours;
                continue;
            }

            coveredHours += gap.TotalHours;
            AccumulateSignedTrapezoid(
                current.ValueWatts,
                next.ValueWatts,
                gap.TotalHours,
                ref positiveWh,
                ref negativeWh);
        }

        if (samples[^1].TimestampUtc < toUtc)
        {
            uncoveredHours += (toUtc - samples[^1].TimestampUtc).TotalHours;
        }

        var netWh = positiveWh - negativeWh;
        var averageWatts = coveredHours > 0
            ? netWh / coveredHours
            : (double?)null;

        var denominator = coveredHours + uncoveredHours;
        var coveragePercent = denominator > 0
            ? coveredHours / denominator * 100.0
            : 0;

        return new PowerMetricStatistics(
            metricKey,
            samples[0].TimestampUtc,
            samples[^1].TimestampUtc,
            samples.Count,
            samples.Min(sample => sample.ValueWatts),
            samples.Max(sample => sample.ValueWatts),
            averageWatts,
            netWh / 1000.0,
            positiveWh / 1000.0,
            negativeWh / 1000.0,
            coveredHours,
            uncoveredHours,
            coveragePercent,
            medianGapMinutes,
            continuityThresholdMinutes);
    }

    private static PowerMetricStatistics Empty(string metricKey)
    {
        return new PowerMetricStatistics(
            metricKey,
            null,
            null,
            0,
            null,
            null,
            null,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);
    }

    private static void AccumulateSignedTrapezoid(
        double startWatts,
        double endWatts,
        double hours,
        ref double positiveWh,
        ref double negativeWh)
    {
        if (startWatts >= 0 && endWatts >= 0)
        {
            positiveWh += (startWatts + endWatts) / 2.0 * hours;
            return;
        }

        if (startWatts <= 0 && endWatts <= 0)
        {
            negativeWh += (Math.Abs(startWatts) + Math.Abs(endWatts)) / 2.0 * hours;
            return;
        }

        var magnitude = Math.Abs(startWatts) + Math.Abs(endWatts);
        if (magnitude <= 0)
        {
            return;
        }

        var fractionToZero = Math.Abs(startWatts) / magnitude;
        var firstHours = hours * fractionToZero;
        var secondHours = hours - firstHours;

        if (startWatts > 0)
        {
            positiveWh += startWatts * firstHours / 2.0;
            negativeWh += Math.Abs(endWatts) * secondHours / 2.0;
        }
        else
        {
            negativeWh += Math.Abs(startWatts) * firstHours / 2.0;
            positiveWh += endWatts * secondHours / 2.0;
        }
    }

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var ordered = values.OrderBy(value => value).ToArray();
        var middle = ordered.Length / 2;

        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2.0
            : ordered[middle];
    }

    private sealed record PowerSample(
        DateTimeOffset TimestampUtc,
        double ValueWatts);
}
