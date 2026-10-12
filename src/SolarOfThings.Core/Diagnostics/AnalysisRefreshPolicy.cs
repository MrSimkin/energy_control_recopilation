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
    /// <summary>
    /// A chart series checkbox may reuse the last successfully rendered
    /// immutable aggregation only when its source generation and device
    /// still match the active, visible Analysis page.
    /// </summary>
    /// <summary>
    /// The WPF Analysis reader is single-flight. A newer selection queued
    /// while the old reader is unwinding must be serviced exactly once on
    /// return, but never for a hidden/closed page or an un-applied calendar
    /// edit. Pure decision to keep the UI handoff regression-testable.
    /// </summary>
    public static bool ShouldContinueQueuedRead(
        bool hasQueuedRequest,
        bool analysisVisible,
        bool windowClosed,
        bool presetLoading,
        bool customRangePendingApply) =>
        hasQueuedRequest && analysisVisible && !windowClosed &&
        !presetLoading && !customRangePendingApply;

    public static bool CanReuseChart(
        int displayedGeneration,
        int requestedGeneration,
        bool analysisVisible,
        bool dataLoading,
        string? displayedDeviceId,
        string? currentDeviceId) =>
        displayedGeneration >= 0 &&
        displayedGeneration == requestedGeneration &&
        analysisVisible &&
        !dataLoading &&
        !string.IsNullOrWhiteSpace(displayedDeviceId) &&
        string.Equals(displayedDeviceId, currentDeviceId,
            StringComparison.Ordinal);
}
