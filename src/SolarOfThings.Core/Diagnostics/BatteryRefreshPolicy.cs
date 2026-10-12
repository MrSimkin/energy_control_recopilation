namespace SolarOfThings.Core.Diagnostics;

/// <summary>
/// Pure policy preventing Battery background reads from painting stale
/// results after another request, a page/device switch, or window closure.
/// </summary>
public static class BatteryRefreshPolicy
{
    public static bool CanApply(
        int requestedGeneration, int latestGeneration,
        bool batteryVisible, bool windowClosed,
        string requestedDeviceId, string? currentDeviceId) =>
        requestedGeneration == latestGeneration && batteryVisible &&
        !windowClosed && string.Equals(
            requestedDeviceId, currentDeviceId, StringComparison.Ordinal);
}
