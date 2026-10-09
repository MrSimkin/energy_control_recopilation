using SolarOfThings.Core.Statistics;

namespace SolarOfThings.Core.Reporting;

/// <summary>
/// An aggregated time bucket is "usable" only if it has measured
/// time-integrable evidence. A missing bucket is NOT a measured zero.
/// The report's period grouping and station timezone are not changed.
/// </summary>
public static class ReportPeriodQualityPolicy
{
    public static ReportPeriodQuality Summarize(EnergyAggregationTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var any = 0;
        var all = 0;
        var missing = 0;
        foreach (var row in table.Rows)
        {
            var signals = new[]
            {
                row.PvCoveragePercent,
                row.HouseCoveragePercent,
                row.GridCoveragePercent,
                row.BatteryCoveragePercent
            };
            var available = signals.Count(x => double.IsFinite(x) && x > 0);
            if (available == 0) missing++;
            else any++;
            if (available == 4) all++;
        }
        return new ReportPeriodQuality(
            table.Rows.Count, any, all, missing, table.Period);
    }
}

public sealed record ReportPeriodQuality(
    int AggregatedPeriods,
    int PeriodsWithAnyEnergyEvidence,
    int PeriodsWithAllFourEnergySignals,
    int PeriodsWithoutUsableEnergyEvidence,
    AggregationPeriod Aggregation);
