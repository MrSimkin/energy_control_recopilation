using SolarOfThings.Core.Statistics;

namespace SolarOfThings.Core.Reporting;

public sealed record ReportPreset(
    string Name,
    string RangePreset,
    DateOnly LocalStartDate,
    DateOnly LocalEndDate,
    AggregationPeriod Aggregation,
    DateTimeOffset UpdatedUtc);

public sealed record EnergyReportRequest(
    string Title,
    string DeviceId,
    DateOnly LocalStartDate,
    DateOnly LocalEndDate,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string TimeZoneId,
    AggregationPeriod Aggregation);

public sealed record EnergyReportData(
    EnergyReportRequest Request,
    EnergyRangeSummary Summary,
    EnergyAggregationTable Table,
    DateTimeOffset GeneratedUtc);
