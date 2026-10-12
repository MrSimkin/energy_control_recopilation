namespace SolarOfThings.Core.SqlExplorer;

/// <summary>
/// Prevents asynchronous SQL results from painting a previous statement,
/// profile, station-local period, or abandoned/closed WPF page.
/// No database reads or UI thread affinity: used by the window and smoke tests.
/// </summary>
public static class SqlExplorerResultPolicy
{
    public static bool CanShowPreview(
        string executedSql, string currentSql,
        string? requestedDeviceId, string? currentDeviceId,
        bool pageVisible, bool windowClosed, bool cancelled) =>
        pageVisible && !windowClosed && !cancelled &&
        string.Equals(executedSql, currentSql, StringComparison.Ordinal) &&
        string.Equals(requestedDeviceId, currentDeviceId, StringComparison.Ordinal);

    public static bool CanShowIntegratedEnergy(
        SqlIntegratedEnergyRequest requested,
        SqlIntegratedEnergyRequest? current,
        bool pageVisible, bool windowClosed, bool cancelled) =>
        pageVisible && !windowClosed && !cancelled &&
        requested == current;
}
