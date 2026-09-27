using SolarOfThings.Core.Data;
using SolarOfThings.Core.Installation;
using SolarOfThings.Core.Statistics;

namespace SolarOfThings.Core.Reporting;

public sealed class FamilyReportAnalysisService
{
    private const double GridUseThresholdWatts = 100;
    private const double PvLowThresholdWatts = 100;
    private const double ObservableNightCoveragePercent = 60;
    private const double HighlightCoveragePercent = 50;

    private readonly SqliteDatabase _database;
    private readonly BatteryThresholdContextService _thresholds;
    private readonly EnergyAggregationTableService _aggregation;

    public FamilyReportAnalysisService(
        SqliteDatabase database,
        BatteryThresholdContextService thresholds,
        EnergyAggregationTableService aggregation)
    {
        _database = database;
        _thresholds = thresholds;
        _aggregation = aggregation;
    }

    public FamilyReportAnalysis Analyze(EnergyReportRequest request)
    {
        var threshold = _thresholds.Get(request.DeviceId);
        var frames = LoadFrames(request);
        var zone = ResolveTimeZone(request.TimeZoneId);
        var medianGap = MedianGapMinutes(frames);
        var continuityThreshold = medianGap > 0
            ? Math.Clamp(medianGap * 3.0, 10.0, 20.0)
            : 15.0;

        var detectedEvents = DetectReserveGridEvents(
            frames,
            zone,
            threshold.NormalGridTransferSocPercent,
            continuityThreshold);

        // Only complete nights fully contained inside the selected local-date
        // range participate in family night statistics. This prevents the
        // first partial morning and last partial evening from distorting the
        // event count or observable-night denominator.
        var events = detectedEvents
            .Where(item =>
            {
                var nightDate = NightStartDate(item.StartLocal);
                return nightDate.HasValue &&
                       nightDate.Value >= request.LocalStartDate &&
                       nightDate.Value < request.LocalEndDate;
            })
            .ToArray();

        var nights = BuildNightObservations(
            request,
            frames,
            events,
            zone,
            continuityThreshold);

        var frameCoverage = CalculateFrameCoverage(
            request,
            frames,
            continuityThreshold);

        var spanDays = Math.Max(
            1,
            request.LocalEndDate.DayNumber -
            request.LocalStartDate.DayNumber + 1);

        var housePattern = BuildTypicalWindow(
            frames,
            zone,
            frame => frame.HouseWatts,
            "house",
            minimumUsefulWatts: 0,
            medianGapMinutes: medianGap,
            selectedDayCount: spanDays);

        var solarPattern = BuildTypicalWindow(
            frames,
            zone,
            frame => frame.PvWatts,
            "solar",
            minimumUsefulWatts: 50,
            medianGapMinutes: medianGap,
            selectedDayCount: spanDays);

        var gridPattern = BuildTypicalWindow(
            frames,
            zone,
            frame => frame.GridWatts,
            "grid",
            minimumUsefulWatts: 50,
            medianGapMinutes: medianGap,
            selectedDayCount: spanDays);

        var daily = _aggregation.Get(
            request.DeviceId,
            request.StartUtc,
            request.EndUtc,
            request.TimeZoneId,
            AggregationPeriod.Day);

        var highlights = BuildHighlights(daily);

        var spanDays = Math.Max(
            1,
            request.LocalEndDate.DayNumber -
            request.LocalStartDate.DayNumber + 1);

        var evolutionPeriod = spanDays <= 21
            ? AggregationPeriod.Day
            : spanDays <= 180
                ? AggregationPeriod.Week
                : AggregationPeriod.Month;

        var evolutionTable = evolutionPeriod == AggregationPeriod.Day
            ? daily
            : _aggregation.Get(
                request.DeviceId,
                request.StartUtc,
                request.EndUtc,
                request.TimeZoneId,
                evolutionPeriod);

        var evolution = evolutionTable.Rows
            .Select(row => new FamilyEvolutionRow(
                row.LocalLabel,
                row.PvEnergyDisplayKwh,
                row.HouseEnergyDisplayKwh,
                row.GridImportEnergyDisplayKwh,
                row.BatteryDischargedEnergyDisplayKwh,
                row.MinimumAvailableCoveragePercent))
            .ToArray();

        var observableNights = nights.Count(night => night.IsObservable);
        var nightsWithReserve = nights.Count(
            night => night.IsObservable && night.ReserveGridEpisodeCount > 0);

        return new FamilyReportAnalysis(
            threshold.NormalGridTransferSocPercent,
            threshold.ReturnToBatterySocPercent,
            threshold.UsesObservedSettings,
            frameCoverage,
            observableNights,
            nightsWithReserve,
            events.Length,
            events.Sum(item => item.DurationMinutes),
            TypicalNightTime(events.Select(item => item.StartLocal).ToArray()),
            housePattern,
            solarPattern,
            gridPattern,
            events,
            nights,
            highlights,
            evolutionPeriod.ToString(),
            evolution);
    }

    private List<MetricFrame> LoadFrames(EnergyReportRequest request)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT recorded_at_utc, metric_key, normalized_value
            FROM normalized_metric_sample
            WHERE device_id = $deviceId
              AND recorded_at_utc >= $fromUtc
              AND recorded_at_utc <= $toUtc
              AND normalized_value IS NOT NULL
              AND confidence <> 'UNRESOLVED'
              AND metric_key IN (
                  'pv_power_w',
                  'house_load_power_w',
                  'grid_import_power_w',
                  'battery_power_w',
                  'battery_soc_pct'
              )
            ORDER BY recorded_at_utc, metric_key;
            """;
        command.Parameters.AddWithValue("$deviceId", request.DeviceId);
        command.Parameters.AddWithValue(
            "$fromUtc",
            request.StartUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue(
            "$toUtc",
            request.EndUtc.ToUniversalTime().ToString("O"));

        using var reader = command.ExecuteReader();
        var byTimestamp = new SortedDictionary<DateTimeOffset, MetricFrame>();

        while (reader.Read())
        {
            if (!DateTimeOffset.TryParse(reader.GetString(0), out var timestamp))
            {
                continue;
            }

            timestamp = timestamp.ToUniversalTime();
            if (!byTimestamp.TryGetValue(timestamp, out var frame))
            {
                frame = new MetricFrame(timestamp);
                byTimestamp[timestamp] = frame;
            }

            var value = reader.GetDouble(2);
            switch (reader.GetString(1))
            {
                case "pv_power_w":
                    frame.PvWatts = value;
                    break;
                case "house_load_power_w":
                    frame.HouseWatts = value;
                    break;
                case "grid_import_power_w":
                    frame.GridWatts = value;
                    break;
                case "battery_power_w":
                    frame.BatteryWatts = value;
                    break;
                case "battery_soc_pct":
                    frame.SocPercent = value;
                    break;
            }
        }

        return byTimestamp.Values.ToList();
    }

    private static IReadOnlyList<FamilyEnergyEvent> DetectReserveGridEvents(
        IReadOnlyList<MetricFrame> frames,
        TimeZoneInfo zone,
        double transferThreshold,
        double continuityThresholdMinutes)
    {
        var result = new List<FamilyEnergyEvent>();
        EventAccumulator? current = null;
        MetricFrame? previous = null;

        foreach (var frame in frames)
        {
            if (current is not null &&
                previous is not null &&
                (frame.TimestampUtc - previous.TimestampUtc).TotalMinutes >
                    continuityThresholdMinutes)
            {
                FinalizeEvent(current, zone, result);
                current = null;
            }

            var local = TimeZoneInfo.ConvertTime(frame.TimestampUtc, zone);
            var gridAndSolarShortfall =
                IsNight(local) &&
                frame.GridWatts.HasValue &&
                frame.PvWatts.HasValue &&
                frame.GridWatts.Value >= GridUseThresholdWatts &&
                IsPvInsufficient(frame);

            var startsReserveEpisode =
                current is null &&
                gridAndSolarShortfall &&
                frame.SocPercent.HasValue &&
                frame.SocPercent.Value <= transferThreshold + 0.5;

            var continuesReserveEpisode =
                current is not null &&
                gridAndSolarShortfall;

            if (startsReserveEpisode)
            {
                current = new EventAccumulator(
                    frame.TimestampUtc,
                    frame.TimestampUtc,
                    frame.SocPercent!.Value,
                    frame.SocPercent.Value,
                    frame.GridWatts!.Value,
                    frame.PvWatts!.Value,
                    transferThreshold,
                    1);
            }
            else if (continuesReserveEpisode)
            {
                current!.EndUtc = frame.TimestampUtc;
                if (frame.SocPercent.HasValue)
                {
                    current.MinimumSocPercent = Math.Min(
                        current.MinimumSocPercent,
                        frame.SocPercent.Value);
                }
                current.MaximumGridWatts = Math.Max(
                    current.MaximumGridWatts,
                    frame.GridWatts!.Value);
                current.MaximumPvWatts = Math.Max(
                    current.MaximumPvWatts,
                    frame.PvWatts!.Value);
                current.SampleCount++;
            }
            else if (current is not null)
            {
                FinalizeEvent(current, zone, result);
                current = null;
            }

            previous = frame;
        }

        if (current is not null)
        {
            FinalizeEvent(current, zone, result);
        }

        return result;
    }

    private static bool IsPvInsufficient(MetricFrame frame)
    {
        if (!frame.PvWatts.HasValue)
        {
            return false;
        }

        if (frame.PvWatts.Value <= PvLowThresholdWatts)
        {
            return true;
        }

        return frame.HouseWatts.HasValue &&
               frame.PvWatts.Value + 50 < frame.HouseWatts.Value;
    }

    private static void FinalizeEvent(
        EventAccumulator current,
        TimeZoneInfo zone,
        ICollection<FamilyEnergyEvent> target)
    {
        if (current.SampleCount < 2 || current.EndUtc <= current.StartUtc)
        {
            return;
        }

        var startLocal = TimeZoneInfo.ConvertTime(current.StartUtc, zone);
        var endLocal = TimeZoneInfo.ConvertTime(current.EndUtc, zone);

        target.Add(new FamilyEnergyEvent(
            "BATTERY_RESERVE_GRID_EPISODE",
            current.StartUtc,
            current.EndUtc,
            startLocal,
            endLocal,
            (current.EndUtc - current.StartUtc).TotalMinutes,
            current.StartSocPercent,
            current.MinimumSocPercent,
            current.TransferThresholdPercent,
            current.MaximumGridWatts,
            current.MaximumPvWatts));
    }

    private static IReadOnlyList<FamilyNightObservation> BuildNightObservations(
        EnergyReportRequest request,
        IReadOnlyList<MetricFrame> frames,
        IReadOnlyList<FamilyEnergyEvent> events,
        TimeZoneInfo zone,
        double continuityThresholdMinutes)
    {
        var nights = new List<FamilyNightObservation>();

        for (var date = request.LocalStartDate;
             date < request.LocalEndDate;
             date = date.AddDays(1))
        {
            var nightStart = LocalToUtc(date, new TimeOnly(18, 0), zone);
            var nightEnd = LocalToUtc(date.AddDays(1), new TimeOnly(9, 0), zone);
            var totalMinutes = Math.Max(
                1,
                (nightEnd - nightStart).TotalMinutes);

            var coveredMinutes = 0.0;

            for (var index = 0; index < frames.Count - 1; index++)
            {
                var current = frames[index];
                var next = frames[index + 1];

                if (!HasNightEvidence(current) || !HasNightEvidence(next))
                {
                    continue;
                }

                var gap = (next.TimestampUtc - current.TimestampUtc).TotalMinutes;
                if (gap <= 0 || gap > continuityThresholdMinutes)
                {
                    continue;
                }

                var start = current.TimestampUtc < nightStart
                    ? nightStart
                    : current.TimestampUtc;
                var end = next.TimestampUtc > nightEnd
                    ? nightEnd
                    : next.TimestampUtc;

                if (end > start)
                {
                    coveredMinutes += (end - start).TotalMinutes;
                }
            }

            var coverage = Math.Clamp(
                coveredMinutes / totalMinutes * 100.0,
                0,
                100);

            var nightEvents = events
                .Where(item => NightStartDate(item.StartLocal) == date)
                .ToArray();

            nights.Add(new FamilyNightObservation(
                date,
                coverage,
                coverage >= ObservableNightCoveragePercent,
                nightEvents.Length,
                nightEvents.Sum(item => item.DurationMinutes)));
        }

        return nights;
    }

    private static double CalculateFrameCoverage(
        EnergyReportRequest request,
        IReadOnlyList<MetricFrame> frames,
        double continuityThresholdMinutes)
    {
        if (frames.Count < 2)
        {
            return 0;
        }

        var covered = 0.0;
        var rangeMinutes = Math.Max(
            1,
            (request.EndUtc - request.StartUtc).TotalMinutes);

        for (var index = 0; index < frames.Count - 1; index++)
        {
            var current = frames[index];
            var next = frames[index + 1];
            var gap = (next.TimestampUtc - current.TimestampUtc).TotalMinutes;

            if (gap > 0 && gap <= continuityThresholdMinutes)
            {
                covered += gap;
            }
        }

        return Math.Clamp(covered / rangeMinutes * 100.0, 0, 100);
    }

    private static bool HasNightEvidence(MetricFrame frame) =>
        frame.SocPercent.HasValue &&
        frame.GridWatts.HasValue &&
        frame.PvWatts.HasValue;

    private static FamilyHourlyPattern? BuildTypicalWindow(
        IReadOnlyList<MetricFrame> frames,
        TimeZoneInfo zone,
        Func<MetricFrame, double?> selector,
        string metricKey,
        double minimumUsefulWatts,
        double medianGapMinutes,
        int selectedDayCount)
    {
        var expectedSamplesPerHour = medianGapMinutes > 0
            ? 60.0 / medianGapMinutes
            : 4.0;
        var minimumSamplesPerHour = Math.Max(
            2,
            (int)Math.Ceiling(expectedSamplesPerHour * 0.50));

        var cells = frames
            .Select(frame => new
            {
                Frame = frame,
                Local = TimeZoneInfo.ConvertTime(frame.TimestampUtc, zone),
                Value = selector(frame)
            })
            .Where(item => item.Value.HasValue)
            .GroupBy(item => new
            {
                Date = DateOnly.FromDateTime(item.Local.DateTime),
                item.Local.Hour
            })
            .Where(group => group.Count() >= minimumSamplesPerHour)
            .Select(group => new HourCell(
                group.Key.Date,
                group.Key.Hour,
                group.Average(item => item.Value!.Value)))
            .ToArray();

        var opportunityDays = cells
            .Select(cell => cell.Date)
            .Distinct()
            .Count();

        var minimumObservedDays = Math.Max(
            3,
            (int)Math.Ceiling(selectedDayCount * 0.60));

        if (opportunityDays < minimumObservedDays)
        {
            return null;
        }

        var requiredDays = Math.Max(
            3,
            (int)Math.Ceiling(opportunityDays * 0.40));

        var byHour = cells
            .GroupBy(cell => cell.Hour)
            .ToDictionary(
                group => group.Key,
                group => new HourSummary(
                    Median(group.Select(item => item.AverageWatts).ToArray()),
                    group.Select(item => item.Date).Distinct().Count()));

        FamilyHourlyPattern? best = null;
        var bestScore = double.MinValue;

        for (var startHour = 0; startHour < 24; startHour++)
        {
            var hours = new[]
            {
                startHour,
                (startHour + 1) % 24,
                (startHour + 2) % 24
            };

            if (hours.Any(hour => !byHour.ContainsKey(hour)))
            {
                continue;
            }

            var observedDays = hours
                .Select(hour => byHour[hour].ObservedDays)
                .Min();

            if (observedDays < requiredDays)
            {
                continue;
            }

            var score = hours
                .Select(hour => byHour[hour].MedianWatts)
                .Average();

            if (score <= minimumUsefulWatts || score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            best = new FamilyHourlyPattern(
                metricKey,
                startHour,
                (startHour + 3) % 24,
                score,
                observedDays,
                opportunityDays);
        }

        return best;
    }

    private static IReadOnlyList<FamilyPeriodHighlight> BuildHighlights(
        EnergyAggregationTable daily)
    {
        var result = new List<FamilyPeriodHighlight>();

        AddMaximumHighlights(
            result,
            daily.Rows,
            "solar-day-max",
            row => row.PvEnergyDisplayKwh,
            row => row.PvCoveragePercent,
            "kWh");

        AddMaximumHighlights(
            result,
            daily.Rows,
            "house-day-max",
            row => row.HouseEnergyDisplayKwh,
            row => row.HouseCoveragePercent,
            "kWh");

        AddMaximumHighlights(
            result,
            daily.Rows,
            "grid-day-max",
            row => row.GridImportEnergyDisplayKwh,
            row => row.GridCoveragePercent,
            "kWh");

        return result;
    }

    private static void AddMaximumHighlights(
        ICollection<FamilyPeriodHighlight> target,
        IReadOnlyList<EnergyAggregationRow> rows,
        string metricKey,
        Func<EnergyAggregationRow, double?> valueSelector,
        Func<EnergyAggregationRow, double> coverageSelector,
        string unit)
    {
        var eligible = rows
            .Select(row => new
            {
                Row = row,
                Value = valueSelector(row),
                Coverage = coverageSelector(row)
            })
            .Where(item =>
                item.Value.HasValue &&
                item.Coverage >= HighlightCoveragePercent)
            .ToArray();

        if (eligible.Length == 0)
        {
            return;
        }

        var maximum = eligible.Max(item => item.Value!.Value);
        var tolerance = Math.Max(0.01, Math.Abs(maximum) * 0.001);

        foreach (var item in eligible.Where(
                     item => Math.Abs(item.Value!.Value - maximum) <= tolerance))
        {
            target.Add(new FamilyPeriodHighlight(
                metricKey,
                item.Row.LocalLabel,
                item.Value!.Value,
                unit,
                item.Coverage));
        }
    }

    private static TimeOnly? TypicalNightTime(
        IReadOnlyList<DateTimeOffset> occurrences)
    {
        if (occurrences.Count < 3)
        {
            return null;
        }

        var anchoredMinutes = occurrences
            .Select(value =>
            {
                var minutes = value.Hour * 60 + value.Minute;
                return minutes >= 18 * 60
                    ? minutes - 18 * 60
                    : minutes + 6 * 60;
            })
            .Select(value => (double)value)
            .ToArray();

        var median = (int)Math.Round(Median(anchoredMinutes));
        var clockMinutes = (median + 18 * 60) % (24 * 60);
        return new TimeOnly(clockMinutes / 60, clockMinutes % 60);
    }

    private static double MedianGapMinutes(IReadOnlyList<MetricFrame> frames)
    {
        if (frames.Count < 2)
        {
            return 0;
        }

        var gaps = new List<double>(frames.Count - 1);
        for (var index = 0; index < frames.Count - 1; index++)
        {
            var gap =
                (frames[index + 1].TimestampUtc - frames[index].TimestampUtc)
                .TotalMinutes;

            if (gap > 0)
            {
                gaps.Add(gap);
            }
        }

        return Median(gaps);
    }

    private static double Median(IEnumerable<double> values) =>
        Median(values.ToArray());

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

    private static bool IsNight(DateTimeOffset local) =>
        local.Hour >= 18 || local.Hour < 9;

    private static DateOnly? NightStartDate(DateTimeOffset local)
    {
        var date = DateOnly.FromDateTime(local.DateTime);
        if (local.Hour >= 18)
        {
            return date;
        }

        if (local.Hour < 9)
        {
            return date.AddDays(-1);
        }

        return null;
    }

    private static DateTimeOffset LocalToUtc(
        DateOnly date,
        TimeOnly time,
        TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(
            date.ToDateTime(time),
            DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        var offset = zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                if (OperatingSystem.IsWindows() &&
                    TimeZoneInfo.TryConvertIanaIdToWindowsId(
                        timeZoneId,
                        out var windowsId))
                {
                    try
                    {
                        return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                    }
                    catch (TimeZoneNotFoundException)
                    {
                    }
                }
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private sealed class MetricFrame
    {
        public MetricFrame(DateTimeOffset timestampUtc)
        {
            TimestampUtc = timestampUtc;
        }

        public DateTimeOffset TimestampUtc { get; }
        public double? PvWatts { get; set; }
        public double? HouseWatts { get; set; }
        public double? GridWatts { get; set; }
        public double? BatteryWatts { get; set; }
        public double? SocPercent { get; set; }
    }

    private sealed class EventAccumulator
    {
        public EventAccumulator(
            DateTimeOffset startUtc,
            DateTimeOffset endUtc,
            double startSocPercent,
            double minimumSocPercent,
            double maximumGridWatts,
            double maximumPvWatts,
            double transferThresholdPercent,
            int sampleCount)
        {
            StartUtc = startUtc;
            EndUtc = endUtc;
            StartSocPercent = startSocPercent;
            MinimumSocPercent = minimumSocPercent;
            MaximumGridWatts = maximumGridWatts;
            MaximumPvWatts = maximumPvWatts;
            TransferThresholdPercent = transferThresholdPercent;
            SampleCount = sampleCount;
        }

        public DateTimeOffset StartUtc { get; }
        public DateTimeOffset EndUtc { get; set; }
        public double StartSocPercent { get; }
        public double MinimumSocPercent { get; set; }
        public double MaximumGridWatts { get; set; }
        public double MaximumPvWatts { get; set; }
        public double TransferThresholdPercent { get; }
        public int SampleCount { get; set; }
    }

    private sealed record HourCell(
        DateOnly Date,
        int Hour,
        double AverageWatts);

    private sealed record HourSummary(
        double MedianWatts,
        int ObservedDays);
}
