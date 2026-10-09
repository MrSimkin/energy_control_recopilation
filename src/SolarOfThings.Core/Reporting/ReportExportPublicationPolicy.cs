namespace SolarOfThings.Core.Reporting;

/// <summary>
/// Fail closed before publishing a completed export if its source selection,
/// window, device or application state changed while it was being computed.
/// This guard does not control filename authorization; that belongs to the
/// separate same-directory staging/publication policy.
/// </summary>
public static class ReportExportPublicationPolicy
{
    public static bool CanPublish(
        EnergyReportRequest original,
        EnergyReportRequest? current,
        bool windowOpen,
        bool reportPageVisible,
        bool cancellationRequested)
    {
        ArgumentNullException.ThrowIfNull(original);
        return windowOpen && reportPageVisible && !cancellationRequested &&
            current is not null && original == current;
    }
}
