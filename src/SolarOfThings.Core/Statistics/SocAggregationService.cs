using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Statistics;

public sealed class SocAggregationService
{
    private readonly SqliteDatabase _database;

    public SocAggregationService(SqliteDatabase database)
    {
        _database = database;
    }

    public SocAggregationSeries GetSeries(
        string deviceId,
        DateTimeOffset rangeStartUtc,
        DateTimeOffset rangeEndUtc,
        string timeZoneId,
        AggregationPeriod period)
    {
        if (rangeEndUtc < rangeStartUtc)
        {
            (rangeStartUtc, rangeEndUtc) = (rangeEndUtc, rangeStartUtc);
        }

        rangeStartUtc = rangeStartUtc.ToUniversalTime();
        rangeEndUtc = rangeEndUtc.ToUniversalTime();

        var rangeEndExclusive = rangeEndUtc == DateTimeOffset.MaxValue
            ? rangeEndUtc
            : rangeEndUtc.AddTicks(1);

        var buckets = AggregationBucketPlanner
            .Build(
                period,
                rangeStartUtc,
                rangeEndExclusive,
                timeZoneId)
            .Select(window => new SocBucketAccumulator(
                window.Period,
                window.StartUtc,
                window.EndUtcExclusive,
                window.LocalLabel))
            .ToList();

        if (buckets.Count == 0)
        {
            return new SocAggregationSeries(
                deviceId,
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

        return new SocAggregationSeries(
            deviceId,
            period,
            timeZoneId,
            rangeStartUtc,
            rangeEndUtc,
            medianGap,
            continuityThreshold,
            buckets.Select(bucket => bucket.ToRecord()).ToArray());
    }

    private IReadOnlyList<SocSample> LoadSamplesWithContext(
        string deviceId,
        DateTimeOffset rangeStartUtc,
        DateTimeOffset rangeEndExclusive)
    {
        using var connection = _database.OpenConnection();
        var samples = new SortedDictionary<DateTimeOffset, double>();

        void AddBoundarySample(
            string comparisonSql,
            string orderSql,
            DateTimeOffset boundary)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT recorded_at_utc, normalized_value
                FROM normalized_metric_sample
                WHERE device_id = $deviceId
                  AND metric_key = 'battery_soc_pct'
                  AND normalized_value IS NOT NULL
                  AND confidence <> 'UNRESOLVED'
                  AND recorded_at_utc {comparisonSql} $boundary
                ORDER BY recorded_at_utc {orderSql}
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$deviceId", deviceId);
            command.Parameters.AddWithValue("$boundary", boundary.ToString("O"));

            using var reader = command.ExecuteReader();
            if (reader.Read() &&
                DateTimeOffset.TryParse(
                    reader.GetString(0),
                    out var timestamp))
            {
                samples[timestamp.ToUniversalTime()] =
                    reader.GetDouble(1);
            }
        }

        AddBoundarySample("<", "DESC", rangeStartUtc);

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT recorded_at_utc, normalized_value
                FROM normalized_metric_sample
                WHERE device_id = $deviceId
                  AND metric_key = 'battery_soc_pct'
                  AND normalized_value IS NOT NULL
                  AND confidence <> 'UNRESOLVED'
                  AND recorded_at_utc >= $fromUtc
                  AND recorded_at_utc < $toUtc
                ORDER BY recorded_at_utc;
                """;
            command.Parameters.AddWithValue("$deviceId", deviceId);
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

        AddBoundarySample(">=", "ASC", rangeEndExclusive);

        return samples
            .Select(pair => new SocSample(pair.Key, pair.Value))
            .ToArray();
    }

    private static void CountSamples(
        IReadOnlyList<SocSample> samples,
        IReadOnlyList<SocBucketAccumulator> buckets,
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
                bucket.AddSample(
                    sample.TimestampUtc,
                    sample.ValuePercent);
            }
        }
    }

    private static double CalculateMedianGapMinutes(
        IReadOnlyList<SocSample> samples)
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
        SocSample first,
        SocSample second,
        DateTimeOffset instant)
    {
        if (instant <= first.TimestampUtc)
        {
            return first.ValuePercent;
        }

        if (instant >= second.TimestampUtc)
        {
            return second.ValuePercent;
        }

        var totalTicks =
            (second.TimestampUtc -
             first.TimestampUtc).Ticks;

        if (totalTicks <= 0)
        {
            return first.ValuePercent;
        }

        var fraction =
            (double)(instant - first.TimestampUtc).Ticks /
            totalTicks;

        return first.ValuePercent +
               (second.ValuePercent - first.ValuePercent) *
               fraction;
    }

    private sealed record SocSample(
        DateTimeOffset TimestampUtc,
        double ValuePercent);

    private sealed class SocBucketAccumulator
    {
        private double _weightedPercentHours;
        private double _coveredHours;
        private double? _minimumPercent;
        private double? _maximumPercent;
        private double? _endingPercent;
        private DateTimeOffset? _endingSampleUtc;

        public SocBucketAccumulator(
            AggregationPeriod period,
            DateTimeOffset startUtc,
            DateTimeOffset endUtcExclusive,
            string localLabel)
        {
            Period = period;
            StartUtc = startUtc;
            EndUtcExclusive = endUtcExclusive;
            LocalLabel = localLabel;
        }

        public AggregationPeriod Period { get; }
        public DateTimeOffset StartUtc { get; }
        public DateTimeOffset EndUtcExclusive { get; }
        public string LocalLabel { get; }
        public int SampleCount { get; private set; }

        public void AddSample(
            DateTimeOffset timestampUtc,
            double valuePercent)
        {
            SampleCount++;
            IncludeValue(valuePercent);

            if (!_endingSampleUtc.HasValue ||
                timestampUtc >= _endingSampleUtc.Value)
            {
                _endingSampleUtc = timestampUtc;
                _endingPercent = valuePercent;
            }
        }

        public void AddSegment(
            DateTimeOffset startUtc,
            DateTimeOffset endUtc,
            double startPercent,
            double endPercent)
        {
            var hours =
                (endUtc - startUtc).TotalHours;

            if (hours <= 0)
            {
                return;
            }

            IncludeValue(startPercent);
            IncludeValue(endPercent);

            _coveredHours += hours;
            _weightedPercentHours +=
                (startPercent + endPercent) /
                2.0 *
                hours;
        }

        public SocAggregationBucket ToRecord()
        {
            var totalHours =
                (EndUtcExclusive - StartUtc).TotalHours;
            var uncoveredHours =
                Math.Max(0, totalHours - _coveredHours);
            var average =
                _coveredHours > 0
                    ? _weightedPercentHours /
                      _coveredHours
                    : (double?)null;
            var coverage =
                totalHours > 0
                    ? Math.Clamp(
                        _coveredHours / totalHours * 100.0,
                        0,
                        100)
                    : 0;

            return new SocAggregationBucket(
                Period,
                StartUtc,
                EndUtcExclusive,
                LocalLabel,
                SampleCount,
                _minimumPercent,
                _maximumPercent,
                average,
                _endingPercent,
                _endingSampleUtc,
                _coveredHours,
                uncoveredHours,
                coverage);
        }

        private void IncludeValue(double valuePercent)
        {
            _minimumPercent =
                !_minimumPercent.HasValue ||
                valuePercent < _minimumPercent.Value
                    ? valuePercent
                    : _minimumPercent;

            _maximumPercent =
                !_maximumPercent.HasValue ||
                valuePercent > _maximumPercent.Value
                    ? valuePercent
                    : _maximumPercent;
        }
    }
}
