using SolarOfThings.Core.Reporting;
using SolarOfThings.Core.SolarOfThings;
using SolarOfThings.Core.Statistics;

namespace SolarOfThings.Core.Reporting;

/// <summary>
/// A user-requested draft report context, never an implicit export.
/// Report aggregation currently supports Day/Week/Month/Year, not Hour.
/// An hourly Analysis row therefore selects its full calendar day and
/// exposes that conversion to the UI instead of silently implying 1 hour.
/// </summary>
public static class ReportContextNavigationPolicy
{
    public static ReportContextSelection FromAnalysis(
        DateOnly start, DateOnly end, AggregationPeriod analysisAggregation,
        ReportKind reportKind)
    {
        if (start > end)
            throw new ArgumentOutOfRangeException(nameof(end),
                "A historical report must have an ordered date range.");
        var reportAggregation = analysisAggregation == AggregationPeriod.Hour
            ? AggregationPeriod.Day : analysisAggregation;
        return new ReportContextSelection(start, end, reportAggregation,
            reportKind, analysisAggregation == AggregationPeriod.Hour,
            "ANALYSIS_RANGE");
    }

    /// <summary>
    /// Convert actual first/last stored sample timestamps into the installation's
    /// inclusive local calendar range. Coverage counts do not establish that
    /// every intervening day is complete or that a sample is energy in kWh.
    /// </summary>
    public static ReportContextSelection FromStoredCoverage(
        DateTimeOffset firstStoredUtc, DateTimeOffset lastStoredUtc,
        string timeZoneId, ReportKind kind)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new ArgumentException("Installation time zone is required.", nameof(timeZoneId));
        if (lastStoredUtc < firstStoredUtc)
            throw new ArgumentOutOfRangeException(nameof(lastStoredUtc),
                "Last stored sample precedes first stored sample.");

        var from = SolarApiTime.GetLocalDate(firstStoredUtc, timeZoneId);
        var to = SolarApiTime.GetLocalDate(lastStoredUtc, timeZoneId);
        if (to < from)
            throw new InvalidOperationException("Stored samples resolved an inverted local range.");
        return new ReportContextSelection(from, to, AggregationPeriod.Day,
            kind, false, "DATA_COVERAGE");
    }

    public static ReportContextSelection FromSelectedRow(
        EnergyAggregationRow row, string timeZoneId, ReportKind kind)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new ArgumentException("Time zone is required.", nameof(timeZoneId));
        if (row.EndUtcExclusive <= row.StartUtc)
            throw new ArgumentException("Selected analysis row has an invalid UTC window.");

        // The last instant before exclusive end belongs to the selected row;
        // time-zone conversion preserves DST changes at day boundaries.
        var from = SolarApiTime.GetLocalDate(row.StartUtc, timeZoneId);
        var to = SolarApiTime.GetLocalDate(row.EndUtcExclusive.AddTicks(-1), timeZoneId);
        if (to < from)
            throw new InvalidOperationException("Selected row resolved an inverted local range.");
        return new ReportContextSelection(from, to, AggregationPeriod.Day,
            kind, row.EndUtcExclusive - row.StartUtc < TimeSpan.FromHours(23),
            "ANALYSIS_ROW");
    }
}

public sealed record ReportContextSelection(
    DateOnly From, DateOnly To, AggregationPeriod Aggregation,
    ReportKind Kind, bool ExpandedToCalendarDay, string Source);
