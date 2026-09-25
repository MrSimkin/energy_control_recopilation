namespace SolarOfThings.Core.Statistics;

public sealed record HouseholdBehaviorStatistics(
    string DeviceId,
    string ContextVersion,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int SampleCount,
    int TransitionCount,
    double ObservedMedianGapMinutes,
    double ContinuityThresholdMinutes,
    double CoveredMinutes,
    double UncoveredGapMinutes,
    double CoveragePercent,
    IReadOnlyDictionary<string, double> StateDurationMinutes,
    IReadOnlyDictionary<string, int> StateSampleCounts);
