namespace SolarOfThings.Core.Statistics;

public sealed record PowerAggregationBucket(
    string MetricKey,
    AggregationPeriod Period,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtcExclusive,
    string LocalLabel,
    int SampleCount,
    double? MinimumWatts,
    double? MaximumWatts,
    double? AverageWatts,
    double NetEnergyKwh,
    double PositiveEnergyKwh,
    double NegativeEnergyKwh,
    double CoveredHours,
    double UncoveredHours,
    double CoveragePercent);

public sealed record PowerAggregationSeries(
    string DeviceId,
    string MetricKey,
    AggregationPeriod Period,
    string TimeZoneId,
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndUtc,
    double ObservedMedianGapMinutes,
    double ContinuityThresholdMinutes,
    IReadOnlyList<PowerAggregationBucket> Buckets);
