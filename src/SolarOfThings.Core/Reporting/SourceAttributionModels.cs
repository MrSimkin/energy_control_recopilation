using SolarOfThings.Core.Statistics;

namespace SolarOfThings.Core.Reporting;

public sealed record SourceAttributionBucket(
    string LocalLabel,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtcExclusive,
    double SolarToHouseKwh,
    double BatteryToHouseKwh,
    double GridToHouseKwh,
    double UnattributedHouseKwh,
    double ObservedHouseKwh,
    double AttributedHouseKwh,
    double ObservedCoveragePercent,
    double AttributionCoverageOfObservedPercent,
    double? BatteryStoredEndingKwh,
    double? SocEndingPercent,
    int ObservedFrameCount,
    int AttributedFrameCount,
    double MeanAbsoluteBalanceResidualPercent,
    double MaximumAbsoluteBalanceResidualPercent);

public sealed record SourceAttributionReport(
    string DeviceId,
    AggregationPeriod Aggregation,
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndUtc,
    string TimeZoneId,
    string RuleVersion,
    double SolarToHouseKwh,
    double BatteryToHouseKwh,
    double GridToHouseKwh,
    double UnattributedHouseKwh,
    double ObservedHouseKwh,
    double AttributionCoverageOfObservedPercent,
    double ObservedTimeCoveragePercent,
    int ExplicitModeSnapshotCount,
    int HistoricalConfigurationChangeCount,
    IReadOnlyList<SourceAttributionBucket> Buckets);
