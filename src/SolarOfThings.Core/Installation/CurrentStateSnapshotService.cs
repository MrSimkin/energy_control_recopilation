using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Diagnostics;
using SolarOfThings.Core.History;
using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.Installation;

public sealed class CurrentStateSnapshotService
{
    private readonly SolarOfThingsSessionManager _session;
    private readonly HistoryRepository _history;
    private readonly ApiDiagnosticsStore _diagnostics;

    public CurrentStateSnapshotService(
        SolarOfThingsSessionManager session,
        HistoryRepository history,
        ApiDiagnosticsStore diagnostics)
    {
        _session = session;
        _history = history;
        _diagnostics = diagnostics;
    }

    public async Task<bool> RefreshAsync(
        CommissioningProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profile.DataSource))
        {
            _diagnostics.RecordLocal(
                "InstallationContext",
                "LatestStateSnapshot",
                "UNRESOLVED",
                "Current-state snapshot skipped because no validated dataSource is available.");
            return false;
        }

        var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
            ? "America/Santiago"
            : profile.StationTimeZone;

        var response = await _session.GetAsync(
            "InstallationContext",
            "LatestStateSnapshot",
            $"deviceState/simple/state/latest/v1?deviceId={Uri.EscapeDataString(profile.DeviceId)}&dataSource={Uri.EscapeDataString(profile.DataSource)}",
            timeZone,
            cancellationToken);

        if (!response.IsSuccess)
        {
            _diagnostics.RecordLocal(
                "InstallationContext",
                "LatestStateSnapshot",
                "WARN",
                $"Current-state snapshot was not refreshed ({response.Code ?? response.HttpStatus.ToString()}).");
            return false;
        }

        _history.CaptureRaw(
            "LatestStateSnapshot",
            profile.DeviceId,
            null,
            "state/latest/v1",
            null,
            null,
            response.Data.GetRawText(),
            DateTimeOffset.UtcNow);

        _diagnostics.RecordLocal(
            "InstallationContext",
            "LatestStateSnapshot",
            "SUCCESS",
            "Current read-only inverter state snapshot stored for contextual interpretation.");

        return true;
    }
}
