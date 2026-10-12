namespace SolarOfThings.Core.Statistics;

/// <summary>
/// Prevent stale background coverage reads from replacing newer page/device state.
/// </summary>
public static class DataCoverageRefreshPolicy
{
    public static bool CanApply(int requestGeneration, int currentGeneration,
        bool isVisible, string? requestedDeviceId, string? currentDeviceId) =>
        requestGeneration == currentGeneration &&
        isVisible &&
        !string.IsNullOrWhiteSpace(requestedDeviceId) &&
        string.Equals(requestedDeviceId, currentDeviceId, StringComparison.Ordinal);
}
