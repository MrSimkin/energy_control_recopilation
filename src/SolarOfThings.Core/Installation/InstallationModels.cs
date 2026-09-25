namespace SolarOfThings.Core.Installation;

public sealed record InstallationConfigCheck(
    string CheckKey,
    string SourceAttributeKey,
    string Status,
    DateTimeOffset? ObservedAtUtc,
    string? ObservedValueJson,
    string ExpectedDisplay,
    string Detail,
    DateTimeOffset EvaluatedUtc);

public sealed record InstallationConfigSummary(
    string DeviceId,
    string OverallStatus,
    int ConfirmedCount,
    int DriftCount,
    int UnresolvedCount,
    DateTimeOffset EvaluatedUtc,
    IReadOnlyList<InstallationConfigCheck> Checks);

public sealed record RawInstallationValue(
    string AttributeKey,
    DateTimeOffset RecordedAtUtc,
    string? ValueJson);
