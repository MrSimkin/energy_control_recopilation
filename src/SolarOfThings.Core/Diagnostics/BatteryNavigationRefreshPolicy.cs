namespace SolarOfThings.Core.Diagnostics;

/// <summary>
/// A delayed read of the last battery snapshot must not trigger a new
/// remote current-state request if navigation, device, session or freshness
/// changed during the read. Pure predicate; no persistence or I/O.
/// </summary>
public static class BatteryNavigationRefreshPolicy
{
    public static bool ShouldRefresh(
        int observedGeneration,
        int latestGeneration,
        bool batteryVisible,
        bool windowClosed,
        string observedDeviceId,
        string? currentDeviceId,
        bool hasSession,
        bool snapshotIsFresh) =>
        hasSession && !snapshotIsFresh &&
        BatteryRefreshPolicy.CanApply(
            observedGeneration, latestGeneration, batteryVisible,
            windowClosed, observedDeviceId, currentDeviceId);
}
