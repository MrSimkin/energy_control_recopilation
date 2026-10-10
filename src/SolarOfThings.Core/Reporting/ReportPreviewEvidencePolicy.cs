using SolarOfThings.Core.Statistics;

namespace SolarOfThings.Core.Reporting;

/// <summary>
/// Preview uses the report's same integrator. Insufficient time-connected
/// measurements must not be displayed as a real zero-energy observation.
/// </summary>
public static class ReportPreviewEvidencePolicy
{
    public static ReportPreviewMetric Evaluate(PowerMetricStatistics metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        var usable = metric.SampleCount >= 2 &&
                     metric.CoveredHours > 0 &&
                     double.IsFinite(metric.CoveragePercent) &&
                     metric.CoveragePercent > 0 &&
                     double.IsFinite(metric.PositiveEnergyKwh) &&
                     double.IsFinite(metric.NegativeEnergyKwh);
        return new ReportPreviewMetric(
            usable ? metric.PositiveEnergyKwh : null,
            usable ? metric.NegativeEnergyKwh : null,
            usable ? metric.CoveragePercent : null,
            metric.SampleCount);
    }

    /// <summary>
    /// Counts only streams with a valid integrated interval, not a guessed
    /// energy zero. Minimum applies ONLY to the eligible subset; it is not
    /// evidence that unavailable streams or unobserved days are complete.
    /// </summary>
    public static ReportPreviewCoverageSummary Summarize(EnergyRangeSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var metrics = new[]
        {
            Evaluate(summary.PvPower),
            Evaluate(summary.HouseLoadPower),
            Evaluate(summary.GridImportPower),
            Evaluate(summary.BatteryPower)
        };
        var available = metrics
            .Where(metric => metric.CoveragePercent.HasValue)
            .ToArray();
        return new ReportPreviewCoverageSummary(
            available.Length, metrics.Length - available.Length,
            available.Length == 0
                ? null : available.Min(metric => metric.CoveragePercent!.Value));
    }

    public static bool CanApply(int requestedGeneration, int currentGeneration,
        bool visible, string? requestedDevice, string? currentDevice) =>
        requestedGeneration == currentGeneration && visible &&
        !string.IsNullOrWhiteSpace(requestedDevice) &&
        string.Equals(requestedDevice, currentDevice, StringComparison.Ordinal);
}

public sealed record ReportPreviewMetric(
    double? PositiveEnergyKwh,
    double? NegativeEnergyKwh,
    double? CoveragePercent,
    int SampleCount);

public sealed record ReportPreviewCoverageSummary(
    int EligibleStreams, int UnavailableStreams,
    double? MinimumEligibleCoveragePercent);
