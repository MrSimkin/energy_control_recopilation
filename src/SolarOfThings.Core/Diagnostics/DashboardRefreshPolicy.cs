namespace SolarOfThings.Core.Diagnostics;

/// <summary>
/// Pure, conservative guard for painting asynchronous Dashboard read results.
/// UI-thread single-flight coordination is owned by MainWindow.
/// </summary>
public static class DashboardRefreshPolicy
{
    public static bool CanApply(
        int requestedGeneration,
        int latestGeneration,
        bool dashboardVisible,
        bool windowClosed,
        string requestedDeviceId,
        string? currentDeviceId) =>
        requestedGeneration == latestGeneration &&
        dashboardVisible &&
        !windowClosed &&
        string.Equals(requestedDeviceId, currentDeviceId,
            StringComparison.Ordinal);
}
