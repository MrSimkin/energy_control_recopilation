namespace SolarOfThings.Core.Normalization;

public sealed record NormalizedMetricValue(
    string MetricKey,
    DateTimeOffset RecordedAtUtc,
    double Value,
    string Unit,
    string SourceAttributeKey,
    string RuleVersion,
    string Confidence,
    string Quality);

public sealed record NormalizationResult(
    string DeviceId,
    string RuleVersion,
    string Status,
    int InputFrameCount,
    int OutputMetricCount,
    IReadOnlyDictionary<string, int> MetricCounts,
    string? Detail);
