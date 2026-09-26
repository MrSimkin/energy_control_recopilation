using SolarOfThings.Core.Statistics;

namespace SolarOfThings.Core.Reporting;

public enum ReportKind
{
    SimpleEnergy,
    DetailedEnergy,
    Battery
}

public sealed record ReportPreset(
    string Name,
    ReportKind Kind,
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
    AggregationPeriod Aggregation,
    ReportKind Kind,
    string LanguageCode);

public sealed record EnergyReportData(
    EnergyReportRequest Request,
    EnergyRangeSummary Summary,
    EnergyAggregationTable Table,
    FamilyReportAnalysis Family,
    DateTimeOffset GeneratedUtc);
