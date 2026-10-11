using SolarOfThings.Core.SolarOfThings;
using SolarOfThings.Core.Statistics;

namespace SolarOfThings.Core.Reporting;

/// <summary>
/// Lists observed grouped periods with missing physical-energy evidence,
/// using the exact bucket set already calculated for report preview.
/// No synthetic missing calendar buckets, guessed zeros or second SQL query.
/// </summary>
public static class ReportPeriodInspectionPolicy
{
    public static ReportPeriodInspectionSummary Inspect(
        EnergyAggregationTable table, int maximumRows = 100)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (maximumRows is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(maximumRows));

        var latest = new List<ReportPeriodIssue>(Math.Min(maximumRows, table.Rows.Count));
        var affected = 0;
        foreach (var row in table.Rows)
        {
            var solar = CoverageOrMissing(row.PvCoveragePercent);
            var house = CoverageOrMissing(row.HouseCoveragePercent);
            var grid = CoverageOrMissing(row.GridCoveragePercent);
            var battery = CoverageOrMissing(row.BatteryCoveragePercent);
            var count = (solar.HasValue ? 1 : 0) +
                        (house.HasValue ? 1 : 0) +
                        (grid.HasValue ? 1 : 0) +
                        (battery.HasValue ? 1 : 0);
            if (count == 4) continue;
            affected++;
            var issue = new ReportPeriodIssue(row, count,
                solar, house, grid, battery);
            if (latest.Count == maximumRows)
                latest.RemoveAt(0);
            latest.Add(issue);
        }

        latest.Reverse();
        return new ReportPeriodInspectionSummary(
            table.Rows.Count, affected, latest.ToArray());
    }

    /// <summary>
    /// Navigate to the actual intersected interval of the user-selected
    /// report and measured bucket, in the station timezone. The analysis
    /// range can never grow past the selected report's source window.
    /// </summary>
    public static ReportPeriodInspectionSelection SelectForAnalysis(
        ReportPeriodIssue issue, EnergyReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.TimeZoneId))
            throw new ArgumentException("Station timezone required.", nameof(request));

        var start = issue.SourceRow.StartUtc > request.StartUtc
            ? issue.SourceRow.StartUtc : request.StartUtc;
        var end = issue.SourceRow.EndUtcExclusive < request.EndUtc
            ? issue.SourceRow.EndUtcExclusive : request.EndUtc;
        if (end <= start)
            throw new InvalidOperationException(
                "Selected period is outside the current report date window.");

        var from = SolarApiTime.GetLocalDate(start, request.TimeZoneId);
        var to = SolarApiTime.GetLocalDate(end.AddTicks(-1), request.TimeZoneId);
        if (from > to)
            throw new InvalidOperationException("Invalid local report period.");
        var aggregation = request.Aggregation == AggregationPeriod.Hour
            ? AggregationPeriod.Day : request.Aggregation;
        return new ReportPeriodInspectionSelection(from, to, aggregation);
    }

    private static double? CoverageOrMissing(double coverage) =>
        double.IsFinite(coverage) && coverage > 0 && coverage <= 100
            ? coverage : null;
}

public sealed record ReportPeriodIssue(
    EnergyAggregationRow SourceRow,
    int AvailableEnergySignals,
    double? PvCoveragePercent,
    double? HouseCoveragePercent,
    double? GridCoveragePercent,
    double? BatteryCoveragePercent);

public sealed record ReportPeriodInspectionSummary(
    int ObservedBuckets, int AffectedBuckets,
    IReadOnlyList<ReportPeriodIssue> LatestAffected);

public sealed record ReportPeriodInspectionSelection(
    DateOnly From, DateOnly To, AggregationPeriod Aggregation);
