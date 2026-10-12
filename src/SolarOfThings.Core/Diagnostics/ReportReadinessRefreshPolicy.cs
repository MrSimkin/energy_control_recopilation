namespace SolarOfThings.Core.Diagnostics;

/// <summary>
/// Only the latest Reports coverage read for the visible, same-device
/// session can enable exports or populate a report range. No WPF dependency.
/// </summary>
public static class ReportReadinessRefreshPolicy
{
    public static bool CanApply(
        int requestedGeneration, int currentGeneration,
        bool reportVisible, bool windowClosed,
        string requestedDeviceId, string? activeDeviceId) =>
        requestedGeneration == currentGeneration &&
        reportVisible && !windowClosed &&
        !string.IsNullOrWhiteSpace(requestedDeviceId) &&
        string.Equals(requestedDeviceId, activeDeviceId,
            StringComparison.Ordinal);
}
