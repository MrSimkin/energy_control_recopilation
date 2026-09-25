namespace SolarOfThings.Core.Statistics;

public sealed record SocAggregationBucket(
    AggregationPeriod Period,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtcExclusive,
    string LocalLabel,
    int SampleCount,
    double? MinimumPercent,
    double? MaximumPercent,
    double? AveragePercent,
    double? EndingPercent,
    DateTimeOffset? EndingSampleUtc,
    double CoveredHours,
    double UncoveredHours,
    double CoveragePercent);

public sealed record SocAggregationSeries(
    string DeviceId,
    AggregationPeriod Period,
    string TimeZoneId,
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndUtc,
    double ObservedMedianGapMinutes,
    double ContinuityThresholdMinutes,
    IReadOnlyList<SocAggregationBucket> Buckets);
