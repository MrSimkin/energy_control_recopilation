using SolarOfThings.Core.Data;
using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.Statistics;

public sealed class PowerAggregationService
{
    private static readonly string[] SupportedMetricKeys =
    [
        "pv_power_w",
        "house_load_power_w",
        "grid_import_power_w",
        "battery_power_w"
    ];

    private readonly SqliteDatabase _database;

    public PowerAggregationService(SqliteDatabase database)
    {
        _database = database;
    }

    public PowerAggregationSeries GetSeries(
        string deviceId,
        string metricKey,
        DateTimeOffset rangeStartUtc,
        DateTimeOffset rangeEndUtc,
        string timeZoneId,
        AggregationPeriod period)
    {
        if (!SupportedMetricKeys.Contains(metricKey, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(
                nameof(metricKey),
                metricKey,
                "Metric is not a supported normalized power metric.");
        }

        if (rangeEndUtc < rangeStartUtc)
        {
            (rangeStartUtc, rangeEndUtc) = (rangeEndUtc, rangeStartUtc);
        }

        rangeStartUtc = rangeStartUtc.ToUniversalTime();
        rangeEndUtc = rangeEndUtc.ToUniversalTime();

        var rangeEndExclusive = rangeEndUtc == DateTimeOffset.MaxValue
            ? rangeEndUtc
            : rangeEndUtc.AddTicks(1);

        var buckets = BuildBuckets(
            metricKey,
            period,
            rangeStartUtc,
            rangeEndExclusive,
            timeZoneId);

        if (buckets.Count == 0)
        {
            return new PowerAggregationSeries(
                deviceId,
                metricKey,
                period,
                timeZoneId,
                rangeStartUtc,
                rangeEndUtc,
                0,
                15,
                []);
        }

        var samples = LoadSamplesWithContext(
            deviceId,
            metricKey,
            rangeStartUtc,
            rangeEndExclusive);

        var medianGap = CalculateMedianGapMinutes(samples);
        var continuityThreshold = medianGap > 0
            ? Math.Clamp(medianGap * 3.0, 10.0, 20.0)
            : 15.0;

        CountSamples(
            samples,
            buckets,
            rangeStartUtc,
            rangeEndExclusive);

        if (samples.Count >= 2)
        {
            var bucketCursor = 0;

            for (var i = 0; i < samples.Count - 1; i++)
            {
                var first = samples[i];
                var second = samples[i + 1];
                var gap = second.TimestampUtc - first.TimestampUtc;

                if (gap <= TimeSpan.Zero ||
                    gap.TotalMinutes > continuityThreshold)
                {
                    continue;
                }

                var segmentStart = first.TimestampUtc < rangeStartUtc
                    ? rangeStartUtc
                    : first.TimestampUtc;
                var segmentEnd = second.TimestampUtc > rangeEndExclusive
                    ? rangeEndExclusive
                    : second.TimestampUtc;

                if (segmentEnd <= segmentStart)
                {
                    continue;
                }

                while (bucketCursor < buckets.Count &&
                       buckets[bucketCursor].EndUtcExclusive <= segmentStart)
                {
                    bucketCursor++;
                }

                var bucketIndex = bucketCursor;

                while (bucketIndex < buckets.Count &&
                       buckets[bucketIndex].StartUtc < segmentEnd)
                {
                    var bucket = buckets[bucketIndex];
                    var pieceStart = segmentStart > bucket.StartUtc
                        ? segmentStart
                        : bucket.StartUtc;
                    var pieceEnd = segmentEnd < bucket.EndUtcExclusive
                        ? segmentEnd
                        : bucket.EndUtcExclusive;

                    if (pieceEnd > pieceStart)
                    {
                        var startValue = Interpolate(
                            first,
                            second,
                            pieceStart);
                        var endValue = Interpolate(
                            first,
                            second,
                            pieceEnd);

                        bucket.AddSegment(
                            pieceStart,
                            pieceEnd,
                            startValue,
                            endValue);
                    }

                    if (bucket.EndUtcExclusive >= segmentEnd)
                    {
                        break;
                    }

                    bucketIndex++;
                }
            }
        }

        return new PowerAggregationSeries(
            deviceId,
            metricKey,
            period,
            timeZoneId,
            rangeStartUtc,
            rangeEndUtc,
            medianGap,
            continuityThreshold,
            buckets.Select(bucket => bucket.ToRecord()).ToArray());
    }

    private IReadOnlyList<PowerSample> LoadSamplesWithContext(
        string deviceId,
        string metricKey,
        DateTimeOffset rangeStartUtc,
        DateTimeOffset rangeEndExclusive)
    {
        using var connection = _database.OpenConnection();
        var samples = new SortedDictionary<DateTimeOffset, double>();

        void AddFromQuery(
            string comparisonSql,
            string orderSql,
            int? limit,
            DateTimeOffset boundary)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT recorded_at_utc, normalized_value
                FROM normalized_metric_sample
                WHERE device_id = $deviceId
                  AND metric_key = $metricKey
                  AND normalized_value IS NOT NULL
                  AND confidence <> 'UNRESOLVED'
                  AND recorded_at_utc {comparisonSql} $boundary
                ORDER BY recorded_at_utc {orderSql}
                {(limit.HasValue ? "LIMIT 1" : string.Empty)};
                """;
            command.Parameters.AddWithValue("$deviceId", deviceId);
            command.Parameters.AddWithValue("$metricKey", metricKey);
            command.Parameters.AddWithValue("$boundary", boundary.ToString("O"));

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!DateTimeOffset.TryParse(
                        reader.GetString(0),
                        out var timestamp))
                {
                    continue;
                }

                samples[timestamp.ToUniversalTime()] =
                    reader.GetDouble(1);
            }
        }

        AddFromQuery("<", "DESC", 1, rangeStartUtc);

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT recorded_at_utc, normalized_value
                FROM normalized_metric_sample
                WHERE device_id = $deviceId
                  AND metric_key = $metricKey
                  AND normalized_value IS NOT NULL
                  AND confidence <> 'UNRESOLVED'
                  AND recorded_at_utc >= $fromUtc
                  AND recorded_at_utc < $toUtc
                ORDER BY recorded_at_utc;
                """;
            command.Parameters.AddWithValue("$deviceId", deviceId);
            command.Parameters.AddWithValue("$metricKey", metricKey);
            command.Parameters.AddWithValue("$fromUtc", rangeStartUtc.ToString("O"));
            command.Parameters.AddWithValue("$toUtc", rangeEndExclusive.ToString("O"));

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!DateTimeOffset.TryParse(
                        reader.GetString(0),
                        out var timestamp))
                {
                    continue;
                }

                samples[timestamp.ToUniversalTime()] =
                    reader.GetDouble(1);
            }
        }

        AddFromQuery(">=", "ASC", 1, rangeEndExclusive);

        return samples
            .Select(pair => new PowerSample(pair.Key, pair.Value))
            .ToArray();
    }

    private static List<BucketAccumulator> BuildBuckets(
        string metricKey,
        AggregationPeriod period,
        DateTimeOffset rangeStartUtc,
        DateTimeOffset rangeEndExclusive,
        string timeZoneId)
    {
        var buckets = new List<BucketAccumulator>();

        if (rangeEndExclusive <= rangeStartUtc)
        {
            return buckets;
        }

        if (period == AggregationPeriod.Hour)
        {
            var localStart =
                SolarApiTime.ConvertToLocalTime(
                    rangeStartUtc,
                    timeZoneId);

            var localHour = new DateTimeOffset(
                localStart.Year,
                localStart.Month,
                localStart.Day,
                localStart.Hour,
                0,
                0,
                localStart.Offset);

            var naturalStartUtc =
                localHour.ToUniversalTime();

            while (naturalStartUtc < rangeEndExclusive)
            {
                var naturalEndUtc = naturalStartUtc.AddHours(1);
                var clippedStart = naturalStartUtc < rangeStartUtc
                    ? rangeStartUtc
                    : naturalStartUtc;
                var clippedEnd = naturalEndUtc > rangeEndExclusive
                    ? rangeEndExclusive
                    : naturalEndUtc;

                if (clippedEnd > clippedStart)
                {
                    var labelLocal =
                        SolarApiTime.ConvertToLocalTime(
                            naturalStartUtc,
                            timeZoneId);

                    buckets.Add(new BucketAccumulator(
                        metricKey,
                        period,
                        clippedStart,
                        clippedEnd,
                        $"{labelLocal:yyyy-MM-dd HH:mm} {labelLocal:zzz}"));
                }

                naturalStartUtc = naturalEndUtc;
            }

            return buckets;
        }

        var localDate =
            SolarApiTime.GetLocalDate(
                rangeStartUtc,
                timeZoneId);

        var bucketStartDate = period switch
        {
            AggregationPeriod.Day => localDate,
            AggregationPeriod.Week => StartOfWeek(localDate),
            AggregationPeriod.Month => new DateOnly(
                localDate.Year,
                localDate.Month,
                1),
            AggregationPeriod.Year => new DateOnly(
                localDate.Year,
                1,
                1),
            _ => localDate
        };

        while (true)
        {
            var nextStartDate = period switch
            {
                AggregationPeriod.Day =>
                    bucketStartDate.AddDays(1),
                AggregationPeriod.Week =>
                    bucketStartDate.AddDays(7),
                AggregationPeriod.Month =>
                    bucketStartDate.AddMonths(1),
                AggregationPeriod.Year =>
                    bucketStartDate.AddYears(1),
                _ =>
                    bucketStartDate.AddDays(1)
            };

            var naturalStartUtc =
                SolarApiTime.GetLocalDayWindow(
                    bucketStartDate,
                    timeZoneId).Start.ToUniversalTime();

            var naturalEndUtc =
                SolarApiTime.GetLocalDayWindow(
                    nextStartDate,
                    timeZoneId).Start.ToUniversalTime();

            if (naturalStartUtc >= rangeEndExclusive)
            {
                break;
            }

            var clippedStart = naturalStartUtc < rangeStartUtc
                ? rangeStartUtc
                : naturalStartUtc;
            var clippedEnd = naturalEndUtc > rangeEndExclusive
                ? rangeEndExclusive
                : naturalEndUtc;

            if (clippedEnd > clippedStart)
            {
                buckets.Add(new BucketAccumulator(
                    metricKey,
                    period,
                    clippedStart,
                    clippedEnd,
                    BuildLabel(
                        period,
                        bucketStartDate,
                        nextStartDate.AddDays(-1))));
            }

            bucketStartDate = nextStartDate;
        }

        return buckets;
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private static string BuildLabel(
        AggregationPeriod period,
        DateOnly startDate,
        DateOnly endDate)
    {
        return period switch
        {
            AggregationPeriod.Day =>
                startDate.ToString("yyyy-MM-dd"),
            AggregationPeriod.Week =>
                $"{startDate:yyyy-MM-dd}–{endDate:yyyy-MM-dd}",
            AggregationPeriod.Month =>
                startDate.ToString("yyyy-MM"),
            AggregationPeriod.Year =>
                startDate.ToString("yyyy"),
            _ =>
                startDate.ToString("yyyy-MM-dd")
        };
    }

    private static void CountSamples(
        IReadOnlyList<PowerSample> samples,
        IReadOnlyList<BucketAccumulator> buckets,
        DateTimeOffset rangeStartUtc,
        DateTimeOffset rangeEndExclusive)
    {
        var bucketIndex = 0;

        foreach (var sample in samples)
        {
            if (sample.TimestampUtc < rangeStartUtc ||
                sample.TimestampUtc >= rangeEndExclusive)
            {
                continue;
            }

            while (bucketIndex < buckets.Count &&
                   buckets[bucketIndex].EndUtcExclusive <= sample.TimestampUtc)
            {
                bucketIndex++;
            }

            if (bucketIndex >= buckets.Count)
            {
                break;
            }

            var bucket = buckets[bucketIndex];

            if (sample.TimestampUtc >= bucket.StartUtc &&
                sample.TimestampUtc < bucket.EndUtcExclusive)
            {
                bucket.AddSample(sample.ValueWatts);
            }
        }
    }

    private static double CalculateMedianGapMinutes(
        IReadOnlyList<PowerSample> samples)
    {
        if (samples.Count < 2)
        {
            return 0;
        }

        var gaps = new List<double>(samples.Count - 1);

        for (var i = 0; i < samples.Count - 1; i++)
        {
            var gap =
                (samples[i + 1].TimestampUtc -
                 samples[i].TimestampUtc).TotalMinutes;

            if (gap > 0)
            {
                gaps.Add(gap);
            }
        }

        if (gaps.Count == 0)
        {
            return 0;
        }

        gaps.Sort();
        var middle = gaps.Count / 2;

        return gaps.Count % 2 == 0
            ? (gaps[middle - 1] + gaps[middle]) / 2.0
            : gaps[middle];
    }

    private static double Interpolate(
        PowerSample first,
        PowerSample second,
        DateTimeOffset instant)
    {
        if (instant <= first.TimestampUtc)
        {
            return first.ValueWatts;
        }

        if (instant >= second.TimestampUtc)
        {
            return second.ValueWatts;
        }

        var totalTicks =
            (second.TimestampUtc -
             first.TimestampUtc).Ticks;

        if (totalTicks <= 0)
        {
            return first.ValueWatts;
        }

        var elapsedTicks =
            (instant - first.TimestampUtc).Ticks;

        var fraction =
            (double)elapsedTicks / totalTicks;

        return first.ValueWatts +
               (second.ValueWatts - first.ValueWatts) *
               fraction;
    }

    private sealed record PowerSample(
        DateTimeOffset TimestampUtc,
        double ValueWatts);

    private sealed class BucketAccumulator
    {
        private double _positiveWh;
        private double _negativeWh;
        private double _coveredHours;
        private double? _minimumWatts;
        private double? _maximumWatts;

        public BucketAccumulator(
            string metricKey,
            AggregationPeriod period,
            DateTimeOffset startUtc,
            DateTimeOffset endUtcExclusive,
            string localLabel)
        {
            MetricKey = metricKey;
            Period = period;
            StartUtc = startUtc;
            EndUtcExclusive = endUtcExclusive;
            LocalLabel = localLabel;
        }

        public string MetricKey { get; }
        public AggregationPeriod Period { get; }
        public DateTimeOffset StartUtc { get; }
        public DateTimeOffset EndUtcExclusive { get; }
        public string LocalLabel { get; }
        public int SampleCount { get; private set; }

        public void AddSample(double valueWatts)
        {
            SampleCount++;
            IncludeValue(valueWatts);
        }

        public void AddSegment(
            DateTimeOffset startUtc,
            DateTimeOffset endUtc,
            double startWatts,
            double endWatts)
        {
            var hours =
                (endUtc - startUtc).TotalHours;

            if (hours <= 0)
            {
                return;
            }

            IncludeValue(startWatts);
            IncludeValue(endWatts);

            _coveredHours += hours;

            AccumulateSignedTrapezoid(
                startWatts,
                endWatts,
                hours,
                ref _positiveWh,
                ref _negativeWh);
        }

        public PowerAggregationBucket ToRecord()
        {
            var totalHours =
                (EndUtcExclusive - StartUtc).TotalHours;
            var uncoveredHours =
                Math.Max(0, totalHours - _coveredHours);
            var netWh =
                _positiveWh - _negativeWh;
            var averageWatts =
                _coveredHours > 0
                    ? netWh / _coveredHours
                    : (double?)null;
            var coverage =
                totalHours > 0
                    ? Math.Clamp(
                        _coveredHours / totalHours * 100.0,
                        0,
                        100)
                    : 0;

            return new PowerAggregationBucket(
                MetricKey,
                Period,
                StartUtc,
                EndUtcExclusive,
                LocalLabel,
                SampleCount,
                _minimumWatts,
                _maximumWatts,
                averageWatts,
                netWh / 1000.0,
                _positiveWh / 1000.0,
                _negativeWh / 1000.0,
                _coveredHours,
                uncoveredHours,
                coverage);
        }

        private void IncludeValue(double valueWatts)
        {
            _minimumWatts =
                !_minimumWatts.HasValue ||
                valueWatts < _minimumWatts.Value
                    ? valueWatts
                    : _minimumWatts;

            _maximumWatts =
                !_maximumWatts.HasValue ||
                valueWatts > _maximumWatts.Value
                    ? valueWatts
                    : _maximumWatts;
        }

        private static void AccumulateSignedTrapezoid(
            double startWatts,
            double endWatts,
            double hours,
            ref double positiveWh,
            ref double negativeWh)
        {
            if (startWatts >= 0 &&
                endWatts >= 0)
            {
                positiveWh +=
                    (startWatts + endWatts) /
                    2.0 *
                    hours;
                return;
            }

            if (startWatts <= 0 &&
                endWatts <= 0)
            {
                negativeWh +=
                    (Math.Abs(startWatts) +
                     Math.Abs(endWatts)) /
                    2.0 *
                    hours;
                return;
            }

            var magnitude =
                Math.Abs(startWatts) +
                Math.Abs(endWatts);

            if (magnitude <= 0)
            {
                return;
            }

            var fractionToZero =
                Math.Abs(startWatts) /
                magnitude;
            var firstHours =
                hours * fractionToZero;
            var secondHours =
                hours - firstHours;

            if (startWatts > 0)
            {
                positiveWh +=
                    startWatts *
                    firstHours /
                    2.0;
                negativeWh +=
                    Math.Abs(endWatts) *
                    secondHours /
                    2.0;
            }
            else
            {
                negativeWh +=
                    Math.Abs(startWatts) *
                    firstHours /
                    2.0;
                positiveWh +=
                    endWatts *
                    secondHours /
                    2.0;
            }
        }
    }
}
