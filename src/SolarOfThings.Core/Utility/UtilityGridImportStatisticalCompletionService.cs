using System.Globalization;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.SolarOfThings;
using SolarOfThings.Core.Statistics;

namespace SolarOfThings.Core.Utility;

/// <summary>
/// Completes uncovered grid-import intervals with an empirical predictive model.
/// The utility-meter/bill value is intentionally not used to build the distribution.
/// </summary>
public sealed class UtilityGridImportStatisticalCompletionService
{
    public const string MethodVersion = "grid-import-empirical-bootstrap.v1";
    private const int SimulationCount = 2000;
    private const double SegmentMinutes = 5.0;

    private readonly SqliteDatabase _database;
    private readonly EnergyRangeStatisticsService _statistics;

    public UtilityGridImportStatisticalCompletionService(
        SqliteDatabase database,
        EnergyRangeStatisticsService statistics)
    {
        _database = database;
        _statistics = statistics;
    }

    public UtilityGridImportStatisticalCompletion Analyze(
        string deviceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string timeZoneId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (toUtc < fromUtc)
            (fromUtc, toUtc) = (toUtc, fromUtc);

        fromUtc = fromUtc.ToUniversalTime();
        toUtc = toUtc.ToUniversalTime();

        var stats = _statistics.GetMetric(
            deviceId,
            "grid_import_power_w",
            fromUtc,
            toUtc,
            cancellationToken);

        var samples = LoadSamples(
            deviceId,
            fromUtc,
            toUtc,
            timeZoneId,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        var gaps = BuildGaps(
            samples,
            fromUtc,
            toUtc,
            stats.ContinuityThresholdMinutes);

        if (samples.Count < 20)
        {
            return UtilityGridImportStatisticalCompletion.Insufficient(
                stats.PositiveEnergyKwh,
                stats.CoveragePercent,
                stats.UncoveredHours,
                samples.Count,
                "INSUFFICIENT_DONOR_DATA");
        }

        if (gaps.Count == 0 ||
            stats.UncoveredHours <= 0.000001)
        {
            return new UtilityGridImportStatisticalCompletion(
                stats.PositiveEnergyKwh,
                stats.PositiveEnergyKwh,
                stats.PositiveEnergyKwh,
                stats.PositiveEnergyKwh,
                stats.PositiveEnergyKwh,
                0,
                stats.CoveragePercent,
                0,
                samples.Count,
                0,
                MethodVersion,
                "COMPLETE_OBSERVATION",
                0.05,
                0.95);
        }

        var seed = StableSeed(
            deviceId,
            fromUtc,
            toUtc);
        var random = new Random(seed);
        var outcomes = new double[SimulationCount];

        for (var simulation = 0;
             simulation < SimulationCount;
             simulation++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var missingKwh = 0.0;

            foreach (var gap in gaps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var cursor = gap.StartUtc;
                var segments = 0;
                while (cursor < gap.EndUtc)
                {
                    if ((++segments & 127) == 0)
                        cancellationToken.ThrowIfCancellationRequested();
                    var next = cursor.AddMinutes(SegmentMinutes);
                    if (next > gap.EndUtc)
                        next = gap.EndUtc;

                    var midpoint = cursor +
                        TimeSpan.FromTicks(
                            (next - cursor).Ticks / 2);
                    var local = SolarApiTime.ConvertToLocalTime(
                        midpoint,
                        timeZoneId);

                    var pool = CandidatePool(
                        samples,
                        local);

                    if (pool.Count == 0)
                    {
                        cursor = next;
                        continue;
                    }

                    var watts = pool[random.Next(pool.Count)];
                    missingKwh +=
                        Math.Max(0, watts) *
                        (next - cursor).TotalHours /
                        1000.0;
                    cursor = next;
                }
            }

            outcomes[simulation] =
                stats.PositiveEnergyKwh + missingKwh;
        }

        cancellationToken.ThrowIfCancellationRequested();
        Array.Sort(outcomes);

        var mean = outcomes.Average();
        var variance = outcomes.Length > 1
            ? outcomes.Sum(value =>
                Math.Pow(value - mean, 2)) /
              (outcomes.Length - 1)
            : 0;

        return new UtilityGridImportStatisticalCompletion(
            stats.PositiveEnergyKwh,
            Percentile(outcomes, 0.05),
            Percentile(outcomes, 0.50),
            Percentile(outcomes, 0.95),
            mean,
            Math.Sqrt(Math.Max(0, variance)),
            stats.CoveragePercent,
            gaps.Sum(item =>
                (item.EndUtc - item.StartUtc).TotalHours),
            samples.Count,
            SimulationCount,
            MethodVersion,
            "EMPIRICAL_PREDICTIVE_INTERVAL",
            0.05,
            0.95);
    }

    private IReadOnlyList<StatSample> LoadSamples(
        string deviceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT recorded_at_utc, normalized_value
            FROM normalized_metric_sample
            WHERE device_id = $deviceId
              AND metric_key = 'grid_import_power_w'
              AND normalized_value IS NOT NULL
              AND confidence <> 'UNRESOLVED'
              AND recorded_at_utc >= $fromUtc
              AND recorded_at_utc <= $toUtc
            ORDER BY recorded_at_utc;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);
        command.Parameters.AddWithValue(
            "$fromUtc",
            fromUtc.ToString("O"));
        command.Parameters.AddWithValue(
            "$toUtc",
            toUtc.ToString("O"));

        using var reader = command.ExecuteReader();
        var result = new List<StatSample>();
        var scanned = 0;

        while (reader.Read())
        {
            if ((++scanned & 255) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            if (!DateTimeOffset.TryParse(
                    reader.GetString(0),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var timestamp))
            {
                continue;
            }

            timestamp = timestamp.ToUniversalTime();
            var local = SolarApiTime.ConvertToLocalTime(
                timestamp,
                timeZoneId);

            result.Add(new StatSample(
                timestamp,
                Math.Max(0, reader.GetDouble(1)),
                local.Hour,
                IsWeekend(local)));
        }

        return result;
    }

    private static IReadOnlyList<Gap> BuildGaps(
        IReadOnlyList<StatSample> samples,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        double continuityThresholdMinutes)
    {
        var result = new List<Gap>();
        var threshold = TimeSpan.FromMinutes(
            Math.Max(
                1,
                continuityThresholdMinutes));

        if (samples.Count == 0)
        {
            result.Add(new Gap(fromUtc, toUtc));
            return result;
        }

        if (samples[0].TimestampUtc > fromUtc)
        {
            result.Add(new Gap(
                fromUtc,
                samples[0].TimestampUtc));
        }

        for (var index = 0;
             index + 1 < samples.Count;
             index++)
        {
            var current = samples[index];
            var next = samples[index + 1];
            var duration =
                next.TimestampUtc - current.TimestampUtc;

            if (duration > threshold)
            {
                result.Add(new Gap(
                    current.TimestampUtc,
                    next.TimestampUtc));
            }
        }

        if (samples[^1].TimestampUtc < toUtc)
        {
            result.Add(new Gap(
                samples[^1].TimestampUtc,
                toUtc));
        }

        return result
            .Where(item => item.EndUtc > item.StartUtc)
            .ToArray();
    }

    private static IReadOnlyList<double> CandidatePool(
        IReadOnlyList<StatSample> samples,
        DateTimeOffset local)
    {
        var weekend = IsWeekend(local);

        var strict = samples
            .Where(item =>
                item.LocalHour == local.Hour &&
                item.IsWeekend == weekend)
            .Select(item => item.Watts)
            .ToArray();

        if (strict.Length >= 12)
            return strict;

        var nearby = samples
            .Where(item =>
                HourDistance(
                    item.LocalHour,
                    local.Hour) <= 1 &&
                item.IsWeekend == weekend)
            .Select(item => item.Watts)
            .ToArray();

        if (nearby.Length >= 12)
            return nearby;

        var sameHour = samples
            .Where(item =>
                item.LocalHour == local.Hour)
            .Select(item => item.Watts)
            .ToArray();

        if (sameHour.Length >= 8)
            return sameHour;

        return samples
            .Select(item => item.Watts)
            .ToArray();
    }

    private static int HourDistance(
        int left,
        int right)
    {
        var difference = Math.Abs(left - right);
        return Math.Min(
            difference,
            24 - difference);
    }

    private static bool IsWeekend(
        DateTimeOffset local) =>
        local.DayOfWeek is
            DayOfWeek.Saturday or
            DayOfWeek.Sunday;

    private static double Percentile(
        IReadOnlyList<double> sorted,
        double percentile)
    {
        if (sorted.Count == 0)
            return 0;

        var position =
            Math.Clamp(percentile, 0, 1) *
            (sorted.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);

        if (lower == upper)
            return sorted[lower];

        var fraction = position - lower;
        return sorted[lower] +
               (sorted[upper] - sorted[lower]) *
               fraction;
    }

    private static int StableSeed(
        string deviceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        unchecked
        {
            uint hash = 2166136261;
            var text =
                $"{deviceId}|{fromUtc:O}|{toUtc:O}|{MethodVersion}";

            foreach (var character in text)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return (int)(hash & 0x7fffffff);
        }
    }

    private sealed record StatSample(
        DateTimeOffset TimestampUtc,
        double Watts,
        int LocalHour,
        bool IsWeekend);

    private sealed record Gap(
        DateTimeOffset StartUtc,
        DateTimeOffset EndUtc);
}

public sealed record UtilityGridImportStatisticalCompletion(
    double ObservedKwh,
    double? LowerKwh,
    double? MedianKwh,
    double? UpperKwh,
    double? MeanKwh,
    double? StandardDeviationKwh,
    double CoveragePercent,
    double MissingHours,
    int DonorSampleCount,
    int SimulationCount,
    string MethodVersion,
    string Status,
    double LowerPercentile,
    double UpperPercentile)
{
    public bool HasPredictiveInterval =>
        LowerKwh.HasValue &&
        MedianKwh.HasValue &&
        UpperKwh.HasValue;

    public static UtilityGridImportStatisticalCompletion Insufficient(
        double observedKwh,
        double coveragePercent,
        double missingHours,
        int donorSampleCount,
        string status) =>
        new(
            observedKwh,
            null,
            null,
            null,
            null,
            null,
            coveragePercent,
            missingHours,
            donorSampleCount,
            0,
            UtilityGridImportStatisticalCompletionService.MethodVersion,
            status,
            0.05,
            0.95);
}
