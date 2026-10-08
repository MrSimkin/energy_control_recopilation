namespace SolarOfThings.Core.Diagnostics;

/// <summary>
/// Pure conservative predicate for whether an Analysis result from a
/// background data reader may paint WPF charts/text. Generations advance
/// on refresh, page exit and device changes. No external state is touched.
/// </summary>
public static class AnalysisRefreshPolicy
{
    public static bool CanApply(
        int requestedGeneration,
        int latestGeneration,
        bool analysisVisible,
        bool windowClosed,
        string requestedDeviceId,
        string? currentDeviceId) =>
        requestedGeneration == latestGeneration &&
        analysisVisible && !windowClosed &&
        string.Equals(
            requestedDeviceId, currentDeviceId, StringComparison.Ordinal);
}
