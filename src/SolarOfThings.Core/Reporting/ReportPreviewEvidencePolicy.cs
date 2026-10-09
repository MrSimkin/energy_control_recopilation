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
