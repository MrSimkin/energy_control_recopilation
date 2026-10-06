using System.Globalization;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.Utility;

/// <summary>
/// Bill-specific provisional statistical completion for the urgent Enel audit.
/// This intentionally remains separate from the generic production completion
/// method and from frozen R3.
///
/// Method frozen from research:
/// bill-gap-calendar-window-empirical.v1
/// - full historical calendar windows
/// - same local clock time + actual gap duration
/// - same weekday/weekend class
/// - strictly prior dates
/// - latest 15 eligible dates, minimum 10
/// - inverse empirical q05/q50/q95
/// - exact Cartesian aggregation across distinct internal gaps
/// - short bill-edge slivers completed deterministically from nearest boundary
/// - Enel billed kWh is never used in construction/calibration
/// </summary>
public sealed class UtilityBillGapStatisticalCompletionService
{
    public const string MethodVersion =
        "bill-gap-calendar-window-empirical.v1";

    private const int DayCount = 15;
    private const int MinimumDays = 10;
    private const double EdgeToleranceMinutes = 5.5;
    private const double Alpha = 0.10;
    private const int MaximumExactCombinations = 1_000_000;

    private readonly SqliteDatabase _database;

    public UtilityBillGapStatisticalCompletionService(
        SqliteDatabase database)
    {
        _database = database;
    }

    public UtilityBillGapStatisticalAnalysis Analyze(
        string deviceId,
        DateOnly startLocalDate,
        DateOnly endLocalDateInclusive,
        string timeZoneId)
    {
        if (endLocalDateInclusive < startLocalDate)
        {
            (startLocalDate, endLocalDateInclusive) =
                (endLocalDateInclusive, startLocalDate);
        }

        var all = LoadAllSamples(deviceId);
        if (all.Count < 20)
        {
            return Insufficient(
                startLocalDate,
                endLocalDateInclusive,
                "INSUFFICIENT_GRID_HISTORY");
        }

        var threshold = ContinuityThresholdMinutes(all);
        var startUtc =
            LocalInstant(startLocalDate, TimeOnly.MinValue, timeZoneId);
        var endUtcExclusive =
            LocalInstant(
                endLocalDateInclusive.AddDays(1),
                TimeOnly.MinValue,
                timeZoneId);

        var interval = BuildIntervalTruth(
            all,
            startUtc,
            endUtcExclusive,
            threshold,
            timeZoneId);

        if (interval.Samples.Count < 2)
        {
            return Insufficient(
                startLocalDate,
                endLocalDateInclusive,
                "INSUFFICIENT_INTERVAL_SAMPLES");
        }

        var firstHistoryLocalDate =
            SolarApiTime.GetLocalDate(
                all[0].TimestampUtc,
                timeZoneId)
            .AddDays(1);

        var gapEvidence =
            new List<UtilityBillGapEvidence>();
        var distributions =
            new List<double[]>();
        var deterministicBoundaryKwh = 0.0;

        for (var index = 0;
             index < interval.Gaps.Count;
             index++)
        {
            var gap = interval.Gaps[index];
            var gapIndex = index + 1;

            if (!string.Equals(
                    gap.Kind,
                    "INTERNAL",
                    StringComparison.Ordinal))
            {
                if (gap.DurationMinutes >
                    EdgeToleranceMinutes)
                {
                    return Insufficient(
                        startLocalDate,
                        endLocalDateInclusive,
                        $"BOUNDARY_GAP_{gapIndex}_EXCEEDS_EDGE_TOLERANCE",
                        interval);
                }

                var watts =
                    string.Equals(
                        gap.Kind,
                        "BOUNDARY_START",
                        StringComparison.Ordinal)
                        ? gap.EndWatts
                        : gap.StartWatts;

                deterministicBoundaryKwh +=
                    Math.Max(0, watts ?? 0) *
                    gap.DurationMinutes /
                    60.0 /
                    1000.0;

                gapEvidence.Add(
                    new UtilityBillGapEvidence(
                        gapIndex,
                        gap.Kind,
                        SolarApiTime.ConvertToLocalTime(
                            gap.StartUtc,
                            timeZoneId),
                        SolarApiTime.ConvertToLocalTime(
                            gap.EndUtc,
                            timeZoneId),
                        gap.DurationMinutes,
                        "N/A",
                        0,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        0,
                        null,
                        null,
                        null,
                        null,
                        null,
                        gap.StartWatts,
                        gap.EndWatts,
                        null,
                        null));
                continue;
            }

            var targetLocal =
                SolarApiTime.ConvertToLocalTime(
                    gap.StartUtc,
                    timeZoneId);
            var targetDayType =
                DayType(targetLocal.DayOfWeek);

            var historical =
                HistoricalSeries(
                    all,
                    firstHistoryLocalDate,
                    DateOnly.FromDateTime(
                        targetLocal.DateTime),
                    TimeOnly.FromDateTime(
                        targetLocal.DateTime),
                    gap.DurationMinutes,
                    targetDayType,
                    threshold,
                    timeZoneId);

            if (historical.Count < MinimumDays)
            {
                return Insufficient(
                    startLocalDate,
                    endLocalDateInclusive,
                    $"GAP_{gapIndex}_INSUFFICIENT_COMPARABLE_DATES",
                    interval);
            }

            var calibration =
                historical
                    .TakeLast(DayCount)
                    .ToArray();

            if (calibration.Length < MinimumDays)
            {
                return Insufficient(
                    startLocalDate,
                    endLocalDateInclusive,
                    $"GAP_{gapIndex}_INSUFFICIENT_CALIBRATION_DATES",
                    interval);
            }

            var values =
                calibration
                    .Select(item => item.EnergyKwh)
                    .ToArray();

            var q05 = InverseEmpiricalQuantile(values, 0.05);
            var q50 = InverseEmpiricalQuantile(values, 0.50);
            var q95 = InverseEmpiricalQuantile(values, 0.95);

            var backtest =
                Backtest(
                    historical);

            var coverage =
                backtest.Count > 0
                    ? backtest.Count(item => item.Covered) /
                      (double)backtest.Count
                    : (double?)null;
            var bias =
                backtest.Count > 0
                    ? backtest.Average(
                        item => item.P50ErrorKwh)
                    : (double?)null;
            var mae =
                backtest.Count > 0
                    ? backtest.Average(
                        item => Math.Abs(item.P50ErrorKwh))
                    : (double?)null;
            var meanWidth =
                backtest.Count > 0
                    ? backtest.Average(
                        item => item.Q95Kwh - item.Q05Kwh)
                    : (double?)null;
            var meanScore =
                backtest.Count > 0
                    ? backtest.Average(
                        item => item.IntervalScore)
                    : (double?)null;

            var targetState =
                (gap.StartWatts ?? 0) > 100
                    ? "ACTIVE"
                    : "INACTIVE";

            var sameStartState =
                historical
                    .Where(item =>
                        ((item.StartWatts > 100)
                            ? "ACTIVE"
                            : "INACTIVE") ==
                        targetState)
                    .ToArray();

            gapEvidence.Add(
                new UtilityBillGapEvidence(
                    gapIndex,
                    gap.Kind,
                    targetLocal,
                    SolarApiTime.ConvertToLocalTime(
                        gap.EndUtc,
                        timeZoneId),
                    gap.DurationMinutes,
                    targetDayType,
                    calibration.Length,
                    calibration[0].Day,
                    calibration[^1].Day,
                    q05,
                    q50,
                    q95,
                    values.Max(),
                    backtest.Count,
                    coverage,
                    bias,
                    mae,
                    meanWidth,
                    meanScore,
                    gap.StartWatts,
                    gap.EndWatts,
                    sameStartState.Length,
                    sameStartState.Count(
                        item => item.EnergyKwh > 1e-12)));

            distributions.Add(values);
        }

        var totals =
            new List<double>
            {
                interval.ObservedKwh +
                deterministicBoundaryKwh
            };

        foreach (var distribution in distributions)
        {
            if ((long)totals.Count *
                distribution.Length >
                MaximumExactCombinations)
            {
                return Insufficient(
                    startLocalDate,
                    endLocalDateInclusive,
                    "EXACT_AGGREGATION_TOO_LARGE",
                    interval);
            }

            totals =
                totals
                    .SelectMany(
                        total =>
                            distribution.Select(
                                value =>
                                    total + value))
                    .ToList();
        }

        totals.Sort();

        var p05 =
            InverseEmpiricalQuantile(
                totals,
                0.05);
        var p50 =
            InverseEmpiricalQuantile(
                totals,
                0.50);
        var p95 =
            InverseEmpiricalQuantile(
                totals,
                0.95);
        var mean =
            totals.Average();
        var variance =
            totals.Count > 1
                ? totals.Sum(
                      value =>
                          Math.Pow(
                              value - mean,
                              2)) /
                  (totals.Count - 1)
                : 0;

        var denominator =
            interval.CoveredHours +
            interval.UncoveredHours;
        var coveragePercent =
            denominator > 0
                ? interval.CoveredHours /
                  denominator *
                  100.0
                : 0;

        var completion =
            new UtilityGridImportStatisticalCompletion(
                interval.ObservedKwh,
                p05,
                p50,
                p95,
                mean,
                Math.Sqrt(Math.Max(0, variance)),
                coveragePercent,
                interval.UncoveredHours,
                gapEvidence.Sum(
                    item =>
                        item.CalibrationDays),
                totals.Count,
                MethodVersion,
                "PROVISIONAL_REPORT_METHOD_RESEARCH_ONLY",
                0.05,
                0.95);

        return new UtilityBillGapStatisticalAnalysis(
            completion,
            startLocalDate,
            endLocalDateInclusive,
            startUtc,
            endUtcExclusive,
            threshold,
            interval.CoveredHours,
            interval.UncoveredHours,
            deterministicBoundaryKwh,
            gapEvidence,
            totals.Count,
            mean,
            totals.Max(),
            "PROVISIONAL_REPORT_METHOD_RESEARCH_ONLY",
            false);
    }

    private UtilityBillGapStatisticalAnalysis Insufficient(
        DateOnly start,
        DateOnly end,
        string status,
        IntervalTruth? interval = null)
    {
        var observed =
            interval?.ObservedKwh ?? 0;
        var covered =
            interval?.CoveredHours ?? 0;
        var uncovered =
            interval?.UncoveredHours ?? 0;
        var denominator =
            covered + uncovered;
        var coverage =
            denominator > 0
                ? covered / denominator * 100.0
                : 0;

        return new UtilityBillGapStatisticalAnalysis(
            UtilityGridImportStatisticalCompletion.Insufficient(
                observed,
                coverage,
                uncovered,
                0,
                status) with
            {
                MethodVersion = MethodVersion
            },
            start,
            end,
            default,
            default,
            interval?.ContinuityThresholdMinutes ?? 0,
            covered,
            uncovered,
            0,
            [],
            0,
            null,
            null,
            status,
            false);
    }

    private IReadOnlyList<StatSample> LoadAllSamples(
        string deviceId)
    {
        using var connection =
            _database.OpenConnection();
        using var command =
            connection.CreateCommand();

        command.CommandText = """
            SELECT recorded_at_utc,
                   normalized_value
            FROM normalized_metric_sample
            WHERE device_id = $deviceId
              AND metric_key = 'grid_import_power_w'
              AND normalized_value IS NOT NULL
              AND confidence <> 'UNRESOLVED'
            ORDER BY recorded_at_utc;
            """;
        command.Parameters.AddWithValue(
            "$deviceId",
            deviceId);

        using var reader =
            command.ExecuteReader();

        var rows =
            new List<StatSample>();

        DateTimeOffset? previous = null;
        while (reader.Read())
        {
            if (!DateTimeOffset.TryParse(
                    reader.GetString(0),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var timestamp))
            {
                continue;
            }

            if (!double.TryParse(
                    Convert.ToString(
                        reader.GetValue(1),
                        CultureInfo.InvariantCulture),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var watts) ||
                !double.IsFinite(watts))
            {
                continue;
            }

            timestamp =
                timestamp.ToUniversalTime();

            if (previous.HasValue &&
                timestamp == previous.Value &&
                rows.Count > 0)
            {
                rows[^1] =
                    new StatSample(
                        timestamp,
                        watts);
            }
            else
            {
                rows.Add(
                    new StatSample(
                        timestamp,
                        watts));
            }

            previous = timestamp;
        }

        return rows;
    }

    private static double ContinuityThresholdMinutes(
        IReadOnlyList<StatSample> samples)
    {
        var gaps =
            new List<double>();

        for (var i = 0;
             i < samples.Count - 1;
             i++)
        {
            var minutes =
                (samples[i + 1].TimestampUtc -
                 samples[i].TimestampUtc)
                .TotalMinutes;
            if (minutes > 0)
                gaps.Add(minutes);
        }

        var median =
            Median(gaps);
        if (median <= 0)
            median = 5.0;

        return Math.Min(
            20.0,
            Math.Max(
                10.0,
                median * 3.0));
    }

    private static IntervalTruth BuildIntervalTruth(
        IReadOnlyList<StatSample> all,
        DateTimeOffset startUtc,
        DateTimeOffset endUtcExclusive,
        double thresholdMinutes,
        string timeZoneId)
    {
        var samples =
            all
                .Where(item =>
                    item.TimestampUtc >= startUtc &&
                    item.TimestampUtc <= endUtcExclusive)
                .ToArray();

        if (samples.Length == 0)
        {
            return new IntervalTruth(
                [],
                0,
                0,
                (endUtcExclusive - startUtc)
                    .TotalHours,
                [],
                thresholdMinutes);
        }

        var observed = 0.0;
        var covered = 0.0;
        var uncovered = 0.0;
        var gaps =
            new List<Gap>();

        if (samples[0].TimestampUtc >
            startUtc)
        {
            var hours =
                (samples[0].TimestampUtc -
                 startUtc)
                .TotalHours;

            uncovered += hours;
            gaps.Add(
                new Gap(
                    startUtc,
                    samples[0].TimestampUtc,
                    hours * 60.0,
                    "BOUNDARY_START",
                    null,
                    samples[0].Watts));
        }

        for (var i = 0;
             i < samples.Length - 1;
             i++)
        {
            var left = samples[i];
            var right = samples[i + 1];
            var hours =
                (right.TimestampUtc -
                 left.TimestampUtc)
                .TotalHours;

            if (hours <= 0)
                continue;

            if (hours * 60.0 >
                thresholdMinutes)
            {
                uncovered += hours;
                gaps.Add(
                    new Gap(
                        left.TimestampUtc,
                        right.TimestampUtc,
                        hours * 60.0,
                        "INTERNAL",
                        left.Watts,
                        right.Watts));
                continue;
            }

            covered += hours;
            observed +=
                (Math.Max(0, left.Watts) +
                 Math.Max(0, right.Watts)) /
                2.0 *
                hours /
                1000.0;
        }

        if (samples[^1].TimestampUtc <
            endUtcExclusive)
        {
            var hours =
                (endUtcExclusive -
                 samples[^1].TimestampUtc)
                .TotalHours;

            uncovered += hours;
            gaps.Add(
                new Gap(
                    samples[^1].TimestampUtc,
                    endUtcExclusive,
                    hours * 60.0,
                    "BOUNDARY_END",
                    samples[^1].Watts,
                    null));
        }

        return new IntervalTruth(
            samples,
            observed,
            covered,
            uncovered,
            gaps,
            thresholdMinutes);
    }

    private static IReadOnlyList<HistoricalWindow>
        HistoricalSeries(
            IReadOnlyList<StatSample> all,
            DateOnly firstDay,
            DateOnly targetDay,
            TimeOnly startClock,
            double durationMinutes,
            string targetDayType,
            double thresholdMinutes,
            string timeZoneId)
    {
        var rows =
            new List<HistoricalWindow>();

        for (var day = firstDay;
             day < targetDay;
             day = day.AddDays(1))
        {
            if (!string.Equals(
                    DayType(
                        day.ToDateTime(
                            TimeOnly.MinValue)
                        .DayOfWeek),
                    targetDayType,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var row =
                IntegrateCalendarWindow(
                    all,
                    day,
                    startClock,
                    durationMinutes,
                    thresholdMinutes,
                    timeZoneId);

            if (row is not null)
                rows.Add(row);
        }

        return rows;
    }

    private static HistoricalWindow?
        IntegrateCalendarWindow(
            IReadOnlyList<StatSample> all,
            DateOnly day,
            TimeOnly startClock,
            double durationMinutes,
            double thresholdMinutes,
            string timeZoneId)
    {
        var start =
            LocalInstant(
                day,
                startClock,
                timeZoneId);
        var end =
            start.AddMinutes(
                durationMinutes);

        var samples =
            all
                .Where(item =>
                    item.TimestampUtc >= start &&
                    item.TimestampUtc <= end)
                .ToArray();

        if (samples.Length < 2)
            return null;

        var pre =
            (samples[0].TimestampUtc -
             start)
            .TotalMinutes;
        var post =
            (end -
             samples[^1].TimestampUtc)
            .TotalMinutes;

        if (pre < 0 ||
            post < 0 ||
            pre > EdgeToleranceMinutes ||
            post > EdgeToleranceMinutes)
        {
            return null;
        }

        var energy = 0.0;

        for (var i = 0;
             i < samples.Length - 1;
             i++)
        {
            var hours =
                (samples[i + 1].TimestampUtc -
                 samples[i].TimestampUtc)
                .TotalHours;

            if (hours <= 0 ||
                hours * 60.0 >
                thresholdMinutes)
            {
                return null;
            }

            energy +=
                (Math.Max(0, samples[i].Watts) +
                 Math.Max(0, samples[i + 1].Watts)) /
                2.0 *
                hours /
                1000.0;
        }

        energy +=
            Math.Max(0, samples[0].Watts) *
            pre /
            60.0 /
            1000.0;

        energy +=
            Math.Max(0, samples[^1].Watts) *
            post /
            60.0 /
            1000.0;

        return new HistoricalWindow(
            day,
            energy,
            samples[0].Watts,
            samples[^1].Watts);
    }

    private static IReadOnlyList<BacktestRow>
        Backtest(
            IReadOnlyList<HistoricalWindow> rows)
    {
        var output =
            new List<BacktestRow>();

        for (var index = 0;
             index < rows.Count;
             index++)
        {
            var calibration =
                rows
                    .Take(index)
                    .TakeLast(DayCount)
                    .ToArray();

            if (calibration.Length <
                MinimumDays)
            {
                continue;
            }

            var values =
                calibration
                    .Select(item =>
                        item.EnergyKwh)
                    .ToArray();

            var q05 =
                InverseEmpiricalQuantile(
                    values,
                    0.05);
            var q50 =
                InverseEmpiricalQuantile(
                    values,
                    0.50);
            var q95 =
                InverseEmpiricalQuantile(
                    values,
                    0.95);
            var truth =
                rows[index].EnergyKwh;

            var score =
                q95 - q05;
            if (truth < q05)
            {
                score +=
                    (2.0 / Alpha) *
                    (q05 - truth);
            }
            else if (truth > q95)
            {
                score +=
                    (2.0 / Alpha) *
                    (truth - q95);
            }

            output.Add(
                new BacktestRow(
                    truth >= q05 &&
                    truth <= q95,
                    q05,
                    q50,
                    q95,
                    q50 - truth,
                    score));
        }

        return output;
    }

    private static DateTimeOffset LocalInstant(
        DateOnly day,
        TimeOnly clock,
        string timeZoneId)
    {
        var zone =
            SolarApiTime.GetTimeZoneInfo(
                timeZoneId);
        var local =
            DateTime.SpecifyKind(
                day.ToDateTime(clock),
                DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            local =
                local.AddHours(1);
        }

        return new DateTimeOffset(
                local,
                zone.GetUtcOffset(local))
            .ToUniversalTime();
    }

    private static string DayType(
        DayOfWeek day) =>
        day is DayOfWeek.Saturday or
            DayOfWeek.Sunday
            ? "WEEKEND"
            : "WEEKDAY";

    private static double InverseEmpiricalQuantile(
        IReadOnlyList<double> values,
        double q)
    {
        var ordered =
            values
                .OrderBy(value => value)
                .ToArray();

        if (ordered.Length == 0)
            return 0;

        var index =
            Math.Clamp(
                (int)Math.Ceiling(
                    q * ordered.Length) -
                1,
                0,
                ordered.Length - 1);

        return ordered[index];
    }

    private static double Median(
        IReadOnlyList<double> values)
    {
        if (values.Count == 0)
            return 0;

        var ordered =
            values
                .OrderBy(value => value)
                .ToArray();
        var middle =
            ordered.Length / 2;

        return ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] +
               ordered[middle]) /
              2.0;
    }

    private sealed record StatSample(
        DateTimeOffset TimestampUtc,
        double Watts);

    private sealed record Gap(
        DateTimeOffset StartUtc,
        DateTimeOffset EndUtc,
        double DurationMinutes,
        string Kind,
        double? StartWatts,
        double? EndWatts);

    private sealed record HistoricalWindow(
        DateOnly Day,
        double EnergyKwh,
        double StartWatts,
        double EndWatts);

    private sealed record BacktestRow(
        bool Covered,
        double Q05Kwh,
        double Q50Kwh,
        double Q95Kwh,
        double P50ErrorKwh,
        double IntervalScore);

    private sealed record IntervalTruth(
        IReadOnlyList<StatSample> Samples,
        double ObservedKwh,
        double CoveredHours,
        double UncoveredHours,
        IReadOnlyList<Gap> Gaps,
        double ContinuityThresholdMinutes);
}

public sealed record UtilityBillGapStatisticalAnalysis(
    UtilityGridImportStatisticalCompletion Completion,
    DateOnly StartLocalDate,
    DateOnly EndLocalDateInclusive,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtcExclusive,
    double ContinuityThresholdMinutes,
    double CoveredHours,
    double UncoveredHours,
    double DeterministicBoundaryCompletionKwh,
    IReadOnlyList<UtilityBillGapEvidence> Gaps,
    int ExactCombinationCount,
    double? MeanKwh,
    double? MaximumKwh,
    string Status,
    bool EnelValueUsedInConstruction);

public sealed record UtilityBillGapEvidence(
    int GapIndex,
    string Kind,
    DateTimeOffset StartLocal,
    DateTimeOffset EndLocal,
    double DurationMinutes,
    string DayType,
    int CalibrationDays,
    DateOnly? CalibrationFirstDate,
    DateOnly? CalibrationLastDate,
    double? Q05Kwh,
    double? Q50Kwh,
    double? Q95Kwh,
    double? CalibrationMaxKwh,
    int BacktestCases,
    double? BacktestCoverage,
    double? BacktestP50BiasKwh,
    double? BacktestP50MaeKwh,
    double? BacktestMeanWidthKwh,
    double? BacktestMeanIntervalScore,
    double? TargetStartWatts,
    double? TargetEndWatts,
    int? SupportingSameStartStatePriorDays,
    int? SupportingSameStartStateNonzeroDays);
