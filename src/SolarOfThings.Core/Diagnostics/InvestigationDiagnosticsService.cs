using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.History;
using SolarOfThings.Core.Infrastructure;
using SolarOfThings.Core.Installation;
using SolarOfThings.Core.Reporting;
using SolarOfThings.Core.SolarOfThings;
using SolarOfThings.Core.Statistics;

namespace SolarOfThings.Core.Diagnostics;

public sealed record InvestigationActionResult(
    string Action,
    string Outcome,
    string Detail);

public sealed record InvestigationRunResult(
    IReadOnlyList<InvestigationActionResult> Actions,
    string BundlePath);

public sealed class InvestigationDiagnosticsService
{
    private static readonly string[] PriorityFields =
    [
        "workingMode",
        "chargingPriorityOrder",
        "pvEnergyFeedingPriority",
        "mode",
        "outputModel",
        "powerSupplyFromPVToLoadInACState",
        "acChargingSwitch",
        "solarChargingSwitch",
        "chargingMainSwitch",
        "bmsReturnsToBatteryModeSOC",
        "bmsReturnsToMainsModeSOC",
        "bmsLowPowerSOC",
        "returnToBatteryModeVoltage",
        "returnToMainsModeVoltage"
    ];

    private readonly SqliteDatabase _database;
    private readonly CommissioningProfileRepository _profiles;
    private readonly SolarOfThingsSessionManager _session;
    private readonly HistoryRepository _history;
    private readonly CurrentStateSnapshotService _currentState;
    private readonly ApiDiagnosticsStore _apiDiagnostics;
    private readonly SourceAttributionService _sourceAttribution;
    private readonly AppPaths _paths;

    public InvestigationDiagnosticsService(
        SqliteDatabase database,
        CommissioningProfileRepository profiles,
        SolarOfThingsSessionManager session,
        HistoryRepository history,
        CurrentStateSnapshotService currentState,
        ApiDiagnosticsStore apiDiagnostics,
        SourceAttributionService sourceAttribution,
        AppPaths paths)
    {
        _database = database;
        _profiles = profiles;
        _session = session;
        _history = history;
        _currentState = currentState;
        _apiDiagnostics = apiDiagnostics;
        _sourceAttribution = sourceAttribution;
        _paths = paths;
    }

    public async Task<InvestigationActionResult> CaptureLatestStateAsync(
        CancellationToken cancellationToken = default)
    {
        var profile = RequireProfile();
        try
        {
            var ok = await _currentState.RefreshAsync(profile, cancellationToken);
            return new(
                "LatestState",
                ok ? "SUCCESS" : "WARN",
                ok
                    ? "Current state refreshed and stored in raw_api_capture."
                    : "Current state request did not produce a successful snapshot. See sanitized API diagnostics.");
        }
        catch (Exception ex)
        {
            return Failure("LatestState", ex);
        }
    }

    public async Task<InvestigationActionResult> CaptureEnergyFlowAsync(
        CancellationToken cancellationToken = default)
    {
        var profile = RequireProfile();
        if (string.IsNullOrWhiteSpace(profile.DataSource))
        {
            return new("EnergyFlow", "SKIPPED", "No validated dataSource is available.");
        }

        try
        {
            var response = await _session.GetAsync(
                "InvestigationDiagnostics",
                "EnergyFlowSnapshot",
                $"deviceState/simple/energy/flow/v1?deviceId={Uri.EscapeDataString(profile.DeviceId)}&dataSource={Uri.EscapeDataString(profile.DataSource)}",
                TimeZone(profile),
                cancellationToken);

            CaptureResponse(
                "EnergyFlowSnapshot",
                profile.DeviceId,
                "energy/flow/v1",
                response);

            return ApiResult("EnergyFlow", response);
        }
        catch (Exception ex)
        {
            return Failure("EnergyFlow", ex);
        }
    }

    public async Task<InvestigationActionResult> CaptureConfigCacheAsync(
        CancellationToken cancellationToken = default)
    {
        var profile = RequireProfile();

        try
        {
            var response = await _session.PostAsync(
                "InvestigationDiagnostics",
                "ConfigCacheSnapshot",
                $"remote/device/configs/cache/get?deviceId={Uri.EscapeDataString(profile.DeviceId)}",
                body: new { },
                TimeZone(profile),
                cancellationToken);

            CaptureResponse(
                "ConfigCacheSnapshot",
                profile.DeviceId,
                "remote/configs/cache/get",
                response);

            return ApiResult("ConfigCache", response);
        }
        catch (Exception ex)
        {
            return Failure("ConfigCache", ex);
        }
    }

    public async Task<InvestigationActionResult> CaptureDirectConfigReadAsync(
        CancellationToken cancellationToken = default)
    {
        var profile = RequireProfile();

        try
        {
            var start = await _session.PostAsync(
                "InvestigationDiagnostics",
                "ConfigDirectReadStart",
                $"remote/device/configs/read?deviceId={Uri.EscapeDataString(profile.DeviceId)}",
                body: new { },
                TimeZone(profile),
                cancellationToken);

            CaptureResponse(
                "ConfigDirectReadStart",
                profile.DeviceId,
                "remote/configs/read",
                start);

            if (!start.IsSuccess)
            {
                return ApiResult("ConfigDirectRead", start);
            }

            var batchReadId = FindStringRecursive(
                start.Data.ValueKind == JsonValueKind.Undefined ? start.Root : start.Data,
                "batchReadId", "id");

            if (string.IsNullOrWhiteSpace(batchReadId))
            {
                return new(
                    "ConfigDirectRead",
                    "WARN",
                    "The read request succeeded but no batchReadId was found. The raw response was preserved.");
            }

            SolarApiResponse? last = null;
            var completed = false;

            for (var attempt = 1; attempt <= 60; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                last = await _session.GetAsync(
                    "InvestigationDiagnostics",
                    $"ConfigDirectReadDetails{attempt}",
                    $"remote/device/configs/read/details?batchReadId={Uri.EscapeDataString(batchReadId)}",
                    TimeZone(profile),
                    cancellationToken);

                CaptureResponse(
                    "ConfigDirectReadDetails",
                    profile.DeviceId,
                    "remote/configs/read/details",
                    last);

                if (!last.IsSuccess)
                {
                    break;
                }

                completed = LooksComplete(last.Data);
                if (completed)
                {
                    break;
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(1),
                    cancellationToken);
            }

            if (last is null)
            {
                return new(
                    "ConfigDirectRead",
                    "WARN",
                    "Batch read started but no details response was obtained.");
            }

            if (!last.IsSuccess)
            {
                return ApiResult("ConfigDirectRead", last);
            }

            if (!completed)
            {
                return new(
                    "ConfigDirectRead",
                    "WARN",
                    "Batch read remained unfinished after 60 seconds. The latest details response was preserved.");
            }

            return new(
                "ConfigDirectRead",
                "SUCCESS",
                "Direct configuration batch read completed and the final details response was preserved.");
        }
        catch (Exception ex)
        {
            return Failure("ConfigDirectRead", ex);
        }
    }

    public string BuildOverview()
    {
        var profile = _profiles.Get();
        var sb = new StringBuilder();

        sb.AppendLine("SOURCE ATTRIBUTION / CLEAN-RUN INVESTIGATION");
        sb.AppendLine("===========================================");
        sb.AppendLine($"Generated UTC: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine($"Database schema: {_database.GetSchemaVersion()}");
        sb.AppendLine($"Database size: {new FileInfo(_database.DatabasePath).Length / 1024d / 1024d:F1} MB");

        if (profile is null)
        {
            sb.AppendLine("Commissioning profile: MISSING");
            return sb.ToString();
        }

        sb.AppendLine($"Model: {profile.Model ?? "-"}");
        sb.AppendLine($"Gather protocol: {profile.GatherProtocolNumber ?? "-"}");
        sb.AppendLine($"Data source: {profile.DataSource ?? "-"}");
        sb.AppendLine($"Latest state: {profile.LatestStateStatus}");
        sb.AppendLine($"Energy flow: {profile.EnergyFlowStatus}");
        sb.AppendLine($"History: {profile.HistoryStatus}");
        sb.AppendLine();

        using var connection = _database.OpenConnection();

        AppendScalar(sb, connection, "Historical raw rows",
            "SELECT COUNT(*) FROM history_sample WHERE device_id = $deviceId;", profile.DeviceId);
        AppendScalar(sb, connection, "Normalized rows",
            "SELECT COUNT(*) FROM normalized_metric_sample WHERE device_id = $deviceId;", profile.DeviceId);
        AppendScalar(sb, connection, "Behavior rows",
            "SELECT COUNT(*) FROM household_behavior_sample WHERE device_id = $deviceId;", profile.DeviceId);
        AppendScalar(sb, connection, "Raw API captures",
            "SELECT COUNT(*) FROM raw_api_capture WHERE device_id = $deviceId;", profile.DeviceId);
        AppendScalar(sb, connection, "EnergyFlow snapshots",
            "SELECT COUNT(*) FROM raw_api_capture WHERE device_id = $deviceId AND operation = 'EnergyFlowSnapshot';", profile.DeviceId);
        AppendScalar(sb, connection, "Config cache snapshots",
            "SELECT COUNT(*) FROM raw_api_capture WHERE device_id = $deviceId AND operation = 'ConfigCacheSnapshot';", profile.DeviceId);
        AppendScalar(sb, connection, "Config direct-read responses",
            "SELECT COUNT(*) FROM raw_api_capture WHERE device_id = $deviceId AND operation IN ('ConfigDirectReadStart','ConfigDirectReadDetails');", profile.DeviceId);

        sb.AppendLine();
        sb.AppendLine("Priority/mode values from latest saved state:");
        foreach (var row in GetLatestStatePriorityValues(connection, profile.DeviceId))
        {
            sb.AppendLine($"- {row.Field}: {row.Value ?? "-"} [{row.Display ?? "-"}] @ {row.RetrievedUtc}");
        }

        sb.AppendLine();
        sb.AppendLine("Use 'Export investigation bundle' for CSV/raw evidence and balance validation.");
        return sb.ToString();
    }

    public async Task<InvestigationRunResult> RunCompleteAsync(
        CancellationToken cancellationToken = default)
    {
        var actions = new List<InvestigationActionResult>
        {
            await CaptureLatestStateAsync(cancellationToken),
            await CaptureEnergyFlowAsync(cancellationToken),
            await CaptureDirectConfigReadAsync(cancellationToken),
            await CaptureConfigCacheAsync(cancellationToken)
        };

        var path = SaveInvestigationBundle();
        return new InvestigationRunResult(actions, path);
    }

    public string SaveInvestigationBundle()
    {
        var profile = RequireProfile();
        var directory = Path.Combine(_paths.LogDirectory, "Exports");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(
            directory,
            $"investigation-bundle-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.zip");

        using var file = File.Create(path);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);

        WriteText(zip, "00-README.txt", BuildBundleReadme(profile));
        WriteText(zip, "01-overview.txt", BuildOverview());
        WriteText(zip, "02-sanitized-api-diagnostics.txt", _apiDiagnostics.BuildSanitizedReport(500));

        using var connection = _database.OpenConnection();

        WriteQueryCsv(zip, connection, "10-table-inventory.csv", """
            SELECT name AS table_name,
                   (SELECT COUNT(*) FROM sqlite_master AS ignored WHERE 0) AS placeholder
            FROM sqlite_master
            WHERE type='table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """, transform: rows => ExpandTableCounts(connection, rows));

        WriteQueryCsv(zip, connection, "11-history-attribute-inventory.csv", """
            SELECT attribute_key,
                   COUNT(*) AS samples,
                   SUM(CASE WHEN is_missing <> 0 THEN 1 ELSE 0 END) AS missing,
                   COUNT(DISTINCT CASE WHEN is_missing = 0 THEN value_json END) AS distinct_non_missing,
                   MIN(recorded_at_utc) AS first_utc,
                   MAX(recorded_at_utc) AS last_utc
            FROM history_sample
            WHERE device_id = $deviceId
            GROUP BY attribute_key
            ORDER BY attribute_key;
            """, profile.DeviceId);

        WriteQueryCsv(zip, connection, "12-history-low-cardinality.csv", """
            SELECT attribute_key,
                   COUNT(*) AS samples,
                   COUNT(DISTINCT value_json) AS distinct_values,
                   GROUP_CONCAT(DISTINCT value_json) AS values_seen,
                   MIN(recorded_at_utc) AS first_utc,
                   MAX(recorded_at_utc) AS last_utc
            FROM history_sample
            WHERE device_id = $deviceId
              AND is_missing = 0
            GROUP BY attribute_key
            HAVING COUNT(DISTINCT value_json) <= 20
            ORDER BY distinct_values DESC, attribute_key;
            """, profile.DeviceId);

        WriteHistoricalChangesCsv(zip, connection, profile.DeviceId);

        WriteQueryCsv(zip, connection, "14-history-day-status.csv", """
            SELECT local_date, timezone, source, status, frame_count, page_count,
                   first_at_utc, last_at_utc, retry_count, updated_utc
            FROM history_day_status
            WHERE device_id = $deviceId
            ORDER BY local_date;
            """, profile.DeviceId);

        WriteQueryCsv(zip, connection, "15-normalized-metric-inventory.csv", """
            SELECT metric_key,
                   COUNT(*) AS samples,
                   COUNT(CASE WHEN normalized_value IS NOT NULL THEN 1 END) AS non_null,
                   MIN(recorded_at_utc) AS first_utc,
                   MAX(recorded_at_utc) AS last_utc,
                   MIN(normalized_value) AS minimum,
                   MAX(normalized_value) AS maximum,
                   GROUP_CONCAT(DISTINCT confidence) AS confidence_values,
                   GROUP_CONCAT(DISTINCT quality) AS quality_values
            FROM normalized_metric_sample
            WHERE device_id = $deviceId
            GROUP BY metric_key
            ORDER BY metric_key;
            """, profile.DeviceId);

        WriteQueryCsv(zip, connection, "16-raw-capture-inventory.csv", """
            SELECT operation, source, COUNT(*) AS captures,
                   MIN(retrieved_utc) AS first_retrieved_utc,
                   MAX(retrieved_utc) AS last_retrieved_utc
            FROM raw_api_capture
            WHERE device_id = $deviceId
            GROUP BY operation, source
            ORDER BY operation, source;
            """, profile.DeviceId);

        WriteLatestStateCsv(zip, connection, profile.DeviceId, false);
        WriteLatestStateCsv(zip, connection, profile.DeviceId, true);

        WriteQueryCsv(zip, connection, "19-installation-config-check.csv", """
            SELECT check_key, source_attribute_key, status, observed_at_utc,
                   observed_value_json, expected_display, detail, evaluated_utc
            FROM installation_config_check
            WHERE device_id = $deviceId
            ORDER BY check_key;
            """, profile.DeviceId);

        WriteQueryCsv(zip, connection, "20-behavior-state-counts.csv", """
            SELECT context_version, state_key, confidence, COUNT(*) AS samples,
                   MIN(recorded_at_utc) AS first_utc,
                   MAX(recorded_at_utc) AS last_utc
            FROM household_behavior_sample
            WHERE device_id = $deviceId
            GROUP BY context_version, state_key, confidence
            ORDER BY context_version, state_key, confidence;
            """, profile.DeviceId);

        WritePowerBalanceCsv(zip, connection, profile.DeviceId);
        WriteGridChargingCandidates(zip, connection, profile.DeviceId);
        WriteEnergyFlowInterpretation(zip, connection, profile.DeviceId);
        WriteSourceAttributionEvidence(zip, connection, profile);
        WriteSelectedRawCaptures(zip, connection, profile.DeviceId);
        WriteProfileEvidence(zip, profile);

        return path;
    }

    private CommissioningProfile RequireProfile() =>
        _profiles.Get() ?? throw new InvalidOperationException(
            "No commissioned Solar of Things device is available.");

    private static string TimeZone(CommissioningProfile profile) =>
        string.IsNullOrWhiteSpace(profile.StationTimeZone)
            ? "America/Santiago"
            : profile.StationTimeZone;

    private void CaptureResponse(
        string operation,
        string deviceId,
        string source,
        SolarApiResponse response)
    {
        var safe = DiagnosticSanitizer.SanitizeJson(response.RawJson, 4_000_000)
                   ?? response.RawJson;

        _history.CaptureRaw(
            operation,
            deviceId,
            null,
            source,
            null,
            null,
            safe,
            DateTimeOffset.UtcNow);
    }

    private static InvestigationActionResult ApiResult(
        string action,
        SolarApiResponse response) =>
        new(
            action,
            response.IsSuccess ? "SUCCESS" : "API_ERROR",
            response.IsSuccess
                ? "Read-only response captured successfully."
                : $"HTTP {response.HttpStatus}; code {response.Code ?? "-"}; {response.Message ?? "no message"}");

    private static InvestigationActionResult Failure(string action, Exception ex) =>
        new(action, "ERROR", DiagnosticSanitizer.SanitizeText(ex.Message));

    private static bool LooksComplete(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        if (data.TryGetProperty("isFinished", out var finished))
        {
            if (finished.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (finished.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            var finishedText = finished.ToString();
            if (bool.TryParse(finishedText, out var finishedBool))
            {
                return finishedBool;
            }
        }

        foreach (var name in new[] { "state", "status", "readState" })
        {
            if (!data.TryGetProperty(name, out var value))
            {
                continue;
            }

            var text = value.ToString();
            if (text.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("COMPLETE", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("FINISHED", StringComparison.OrdinalIgnoreCase) ||
                text == "2")
            {
                return true;
            }

            if (text.Equals("RUNNING", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("PENDING", StringComparison.OrdinalIgnoreCase) ||
                text == "0" || text == "1")
            {
                return false;
            }
        }

        return true;
    }

    private static string? FindStringRecursive(JsonElement element, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (names.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    var value = property.Value.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                var nested = FindStringRecursive(property.Value, names);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindStringRecursive(item, names);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static void AppendScalar(
        StringBuilder sb,
        SqliteConnection connection,
        string label,
        string sql,
        string deviceId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$deviceId", deviceId);
        sb.AppendLine($"{label}: {command.ExecuteScalar() ?? 0}");
    }

    private static IReadOnlyList<(string Field, string? Value, string? Display, string RetrievedUtc)>
        GetLatestStatePriorityValues(SqliteConnection connection, string deviceId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT response_json, retrieved_utc
            FROM raw_api_capture
            WHERE device_id = $deviceId
              AND operation = 'LatestStateSnapshot'
            ORDER BY capture_id DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return Array.Empty<(string, string?, string?, string)>();
        }

        return ParseLatestFields(reader.GetString(0), reader.GetString(1))
            .Where(row => PriorityFields.Contains(row.Field, StringComparer.Ordinal))
            .Select(row => (row.Field, row.Value, row.Display, row.RetrievedUtc))
            .ToArray();
    }

    private static string BuildBundleReadme(CommissioningProfile profile) =>
        $"""
        SOLAR ENERGY MONITOR — INVESTIGATION BUNDLE
        ===========================================
        Generated UTC: {DateTimeOffset.UtcNow:O}

        This ZIP is intended for development/debug review.
        It contains read-only database inventories, historical configuration changes,
        latest-state field mappings, raw API evidence (sanitized), and power-balance diagnostics.

        Target model: {profile.Model ?? "-"}
        Gather protocol: {profile.GatherProtocolNumber ?? "-"}
        DataSource: {profile.DataSource ?? "-"}

        SECURITY:
        - DPAPI secret files are never included.
        - Passwords, tokens, cookies, signatures and known personal account fields are sanitized.
        - Device/station technical identifiers may remain because they are diagnostic evidence.

        ONLINE ACTIONS:
        - LatestState: read-only.
        - EnergyFlow: read-only.
        - Config cache: read-only.
        - Direct config batch read: ACTIVE_DEVICE_READ but does not write/change configuration.
        - No config write, cache clear, passthrough, restart, fast-report start/stop, or other mutation is used.
        """;

    private static void WriteProfileEvidence(ZipArchive zip, CommissioningProfile profile)
    {
        WriteText(zip, "30-attribute-catalog.json",
            DiagnosticSanitizer.SanitizeJson(profile.AttributeCatalogJson, 4_000_000) ?? "[]");
        WriteText(zip, "31-capabilities.json",
            DiagnosticSanitizer.SanitizeJson(profile.CapabilitiesJson, 4_000_000) ?? "{}");
        WriteText(zip, "32-device.json",
            DiagnosticSanitizer.SanitizeJson(profile.DeviceJson, 4_000_000) ?? "{}");
        WriteText(zip, "33-station.json",
            DiagnosticSanitizer.SanitizeJson(profile.StationJson, 4_000_000) ?? "{}");
    }

    private static void WriteHistoricalChangesCsv(
        ZipArchive zip,
        SqliteConnection connection,
        string deviceId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            WITH low_cardinality AS (
                SELECT attribute_key
                FROM history_sample
                WHERE device_id = $deviceId AND is_missing = 0
                GROUP BY attribute_key
                HAVING COUNT(DISTINCT value_json) <= 20
            )
            SELECT h.attribute_key, h.recorded_at_utc, h.value_json
            FROM history_sample h
            JOIN low_cardinality l ON l.attribute_key = h.attribute_key
            WHERE h.device_id = $deviceId
              AND h.is_missing = 0
            ORDER BY h.attribute_key, h.recorded_at_utc;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);

        using var reader = command.ExecuteReader();
        var rows = new List<string[]>();
        string? currentKey = null;
        string? previous = null;

        while (reader.Read())
        {
            var key = reader.GetString(0);
            var timestamp = reader.GetString(1);
            var value = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);

            if (!string.Equals(currentKey, key, StringComparison.Ordinal))
            {
                currentKey = key;
                previous = null;
            }

            if (previous is null || !string.Equals(previous, value, StringComparison.Ordinal))
            {
                rows.Add([key, timestamp, previous ?? string.Empty, value]);
                previous = value;
            }
        }

        WriteCsv(zip, "13-historical-low-cardinality-changes.csv",
            ["attribute_key", "recorded_at_utc", "previous_value", "new_value"], rows);
    }

    private static void WriteLatestStateCsv(
        ZipArchive zip,
        SqliteConnection connection,
        string deviceId,
        bool priorityOnly)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT response_json, retrieved_utc
            FROM raw_api_capture
            WHERE device_id = $deviceId
              AND operation = 'LatestStateSnapshot'
            ORDER BY retrieved_utc;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);

        using var reader = command.ExecuteReader();
        var rows = new List<string[]>();

        while (reader.Read())
        {
            var json = reader.GetString(0);
            var retrieved = reader.GetString(1);

            foreach (var row in ParseLatestFields(json, retrieved))
            {
                if (priorityOnly &&
                    !PriorityFields.Contains(row.Field, StringComparer.Ordinal))
                {
                    continue;
                }

                rows.Add([
                    row.RetrievedUtc,
                    row.Field,
                    row.Value ?? string.Empty,
                    row.Display ?? string.Empty,
                    row.Unit ?? string.Empty,
                    row.NameDisplay ?? string.Empty
                ]);
            }
        }

        WriteCsv(zip,
            priorityOnly ? "18-latest-state-priority-fields.csv" : "17-latest-state-all-fields.csv",
            ["retrieved_utc", "field_key", "value", "value_display", "unit", "name_display"],
            rows);
    }

    private static IReadOnlyList<(string RetrievedUtc, string Field, string? Value, string? Display, string? Unit, string? NameDisplay)>
        ParseLatestFields(string json, string retrievedUtc)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("data", out var data))
            {
                root = data;
            }

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("fields", out var fields) ||
                fields.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<(string, string, string?, string?, string?, string?)>();
            }

            var rows = new List<(string, string, string?, string?, string?, string?)>();
            foreach (var field in fields.EnumerateObject())
            {
                var value = ReadJsonProperty(field.Value, "value");
                var display = ReadJsonProperty(field.Value, "valueDisplay");
                var unit = ReadJsonProperty(field.Value, "unit");
                var name = ReadJsonProperty(field.Value, "nameDisplay");
                rows.Add((retrievedUtc, field.Name, value, display, unit, name));
            }

            return rows;
        }
        catch
        {
            return Array.Empty<(string, string, string?, string?, string?, string?)>();
        }
    }

    private static string? ReadJsonProperty(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
    }

    private static void WritePowerBalanceCsv(
        ZipArchive zip,
        SqliteConnection connection,
        string deviceId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT recorded_at_utc,
                   MAX(CASE WHEN metric_key='pv_power_w' THEN normalized_value END) AS pv_w,
                   MAX(CASE WHEN metric_key='house_load_power_w' THEN normalized_value END) AS house_w,
                   MAX(CASE WHEN metric_key='grid_import_power_w' THEN normalized_value END) AS grid_w,
                   MAX(CASE WHEN metric_key='battery_power_w' THEN normalized_value END) AS battery_w
            FROM normalized_metric_sample
            WHERE device_id = $deviceId
              AND metric_key IN ('pv_power_w','house_load_power_w','grid_import_power_w','battery_power_w')
              AND normalized_value IS NOT NULL
              AND confidence <> 'UNRESOLVED'
            GROUP BY recorded_at_utc
            ORDER BY recorded_at_utc;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);

        using var reader = command.ExecuteReader();
        var rows = new List<string[]>();

        while (reader.Read())
        {
            if (reader.IsDBNull(1) || reader.IsDBNull(2) ||
                reader.IsDBNull(3) || reader.IsDBNull(4))
            {
                continue;
            }

            var pv = reader.GetDouble(1);
            var house = reader.GetDouble(2);
            var grid = reader.GetDouble(3);
            var battery = reader.GetDouble(4);
            var batteryDischarge = Math.Max(battery, 0);
            var batteryCharge = Math.Max(-battery, 0);
            var inputs = pv + grid + batteryDischarge;
            var outputs = house + batteryCharge;
            var residual = inputs - outputs;
            var scale = Math.Max(Math.Max(inputs, outputs), 100.0);
            var residualPct = residual / scale * 100.0;

            rows.Add([
                reader.GetString(0),
                F(pv), F(house), F(grid), F(battery),
                F(batteryDischarge), F(batteryCharge),
                F(inputs), F(outputs), F(residual), F(residualPct)
            ]);
        }

        WriteCsv(zip, "21-power-balance-all-observable-frames.csv",
            ["recorded_at_utc","pv_w","house_w","grid_import_w","battery_power_w",
             "battery_discharge_w","battery_charge_w","input_side_w","output_side_w",
             "residual_w","residual_pct_of_scale"],
            rows);

        var worst = rows
            .OrderByDescending(row =>
                double.TryParse(row[9], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    ? Math.Abs(value)
                    : 0)
            .Take(500)
            .ToArray();

        WriteCsv(zip, "22-power-balance-worst-500.csv",
            ["recorded_at_utc","pv_w","house_w","grid_import_w","battery_power_w",
             "battery_discharge_w","battery_charge_w","input_side_w","output_side_w",
             "residual_w","residual_pct_of_scale"],
            worst);
    }

    private void WriteSourceAttributionEvidence(
        ZipArchive zip,
        SqliteConnection connection,
        CommissioningProfile profile)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    MIN(recorded_at_utc),
                    MAX(recorded_at_utc)
                FROM normalized_metric_sample
                WHERE device_id = $deviceId
                  AND metric_key = 'house_load_power_w'
                  AND normalized_value IS NOT NULL
                  AND confidence <> 'UNRESOLVED';
                """;
            command.Parameters.AddWithValue("$deviceId", profile.DeviceId);

            using var reader = command.ExecuteReader();
            if (!reader.Read() ||
                reader.IsDBNull(0) ||
                reader.IsDBNull(1) ||
                !DateTimeOffset.TryParse(reader.GetString(0), out var fromUtc) ||
                !DateTimeOffset.TryParse(reader.GetString(1), out var toUtc))
            {
                WriteText(
                    zip,
                    "23-source-attribution-unavailable.txt",
                    "No usable household-load range was available for attribution.");
                return;
            }

            var report = _sourceAttribution.Get(
                profile.DeviceId,
                fromUtc,
                toUtc,
                string.IsNullOrWhiteSpace(profile.StationTimeZone)
                    ? "America/Santiago"
                    : profile.StationTimeZone,
                AggregationPeriod.Day);

            var summary = $"""
                SOURCE ATTRIBUTION — FULL AVAILABLE HISTORY
                ==========================================
                Rule version: {report.RuleVersion}
                Range UTC: {report.RangeStartUtc:O} — {report.RangeEndUtc:O}
                Aggregation: {report.Aggregation}
                Solar → House: {report.SolarToHouseKwh:F6} kWh
                Battery → House: {report.BatteryToHouseKwh:F6} kWh
                Grid/Utility → House: {report.GridToHouseKwh:F6} kWh
                Unattributed observed house: {report.UnattributedHouseKwh:F6} kWh
                Observed house: {report.ObservedHouseKwh:F6} kWh
                Attribution coverage of observed: {report.AttributionCoverageOfObservedPercent:F3}%
                Observed time coverage: {report.ObservedTimeCoveragePercent:F3}%
                Explicit mode snapshots: {report.ExplicitModeSnapshotCount}
                Historical config changes: {report.HistoricalConfigurationChangeCount}
                """;
            WriteText(zip, "23-source-attribution-summary.txt", summary);

            var rows = report.Buckets.Select(item => new[]
            {
                item.LocalLabel,
                item.StartUtc.ToString("O"),
                item.EndUtcExclusive.ToString("O"),
                F(item.SolarToHouseKwh),
                F(item.BatteryToHouseKwh),
                F(item.GridToHouseKwh),
                F(item.UnattributedHouseKwh),
                F(item.ObservedHouseKwh),
                F(item.AttributedHouseKwh),
                F(item.ObservedCoveragePercent),
                F(item.AttributionCoverageOfObservedPercent),
                item.BatteryStoredEndingKwh.HasValue ? F(item.BatteryStoredEndingKwh.Value) : string.Empty,
                item.SocEndingPercent.HasValue ? F(item.SocEndingPercent.Value) : string.Empty,
                item.ObservedFrameCount.ToString(CultureInfo.InvariantCulture),
                item.AttributedFrameCount.ToString(CultureInfo.InvariantCulture),
                F(item.MeanAbsoluteBalanceResidualPercent),
                F(item.MaximumAbsoluteBalanceResidualPercent)
            });

            WriteCsv(
                zip,
                "24-source-attribution-daily.csv",
                [
                    "local_label","start_utc","end_utc",
                    "solar_to_house_kwh","battery_to_house_kwh","grid_to_house_kwh",
                    "unattributed_house_kwh","observed_house_kwh","attributed_house_kwh",
                    "observed_coverage_pct","attribution_coverage_observed_pct",
                    "battery_stored_end_kwh_estimate","soc_end_pct",
                    "observed_frames","attributed_frames",
                    "mean_abs_balance_residual_pct","max_abs_balance_residual_pct"
                ],
                rows);
        }
        catch (Exception ex)
        {
            WriteText(
                zip,
                "23-source-attribution-error.txt",
                DiagnosticSanitizer.SanitizeText(ex.ToString()));
        }
    }

    private static void WriteGridChargingCandidates(
        ZipArchive zip,
        SqliteConnection connection,
        string deviceId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT recorded_at_utc,
                   MAX(CASE WHEN metric_key='pv_power_w' THEN normalized_value END) AS pv_w,
                   MAX(CASE WHEN metric_key='house_load_power_w' THEN normalized_value END) AS house_w,
                   MAX(CASE WHEN metric_key='grid_import_power_w' THEN normalized_value END) AS grid_w,
                   MAX(CASE WHEN metric_key='battery_power_w' THEN normalized_value END) AS battery_w
            FROM normalized_metric_sample
            WHERE device_id = $deviceId
              AND metric_key IN (
                  'pv_power_w',
                  'house_load_power_w',
                  'grid_import_power_w',
                  'battery_power_w')
              AND normalized_value IS NOT NULL
              AND confidence <> 'UNRESOLVED'
            GROUP BY recorded_at_utc
            ORDER BY recorded_at_utc;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);

        using var reader = command.ExecuteReader();
        var candidates = new List<GridChargeCandidate>();

        while (reader.Read())
        {
            if (reader.IsDBNull(1) ||
                reader.IsDBNull(2) ||
                reader.IsDBNull(3) ||
                reader.IsDBNull(4) ||
                !DateTimeOffset.TryParse(
                    reader.GetString(0),
                    out var timestamp))
            {
                continue;
            }

            var pv = reader.GetDouble(1);
            var house = reader.GetDouble(2);
            var grid = reader.GetDouble(3);
            var battery = reader.GetDouble(4);
            var batteryCharge = Math.Max(-battery, 0);
            var gridSurplus = Math.Max(grid - house, 0);

            if (pv > 50 ||
                grid < 100 ||
                batteryCharge < 50 ||
                gridSurplus < 25)
            {
                continue;
            }

            candidates.Add(new GridChargeCandidate(
                timestamp.ToUniversalTime(),
                pv,
                house,
                grid,
                battery,
                batteryCharge,
                gridSurplus));
        }

        var rows = candidates.Select(item => new[]
        {
            item.TimestampUtc.ToString("O"),
            F(item.PvWatts),
            F(item.HouseWatts),
            F(item.GridWatts),
            F(item.BatteryWatts),
            F(item.BatteryChargeWatts),
            F(item.GridSurplusWatts)
        });

        WriteCsv(
            zip,
            "25-grid-charging-candidate-frames.csv",
            [
                "recorded_at_utc",
                "pv_w",
                "house_w",
                "grid_import_w",
                "battery_power_w",
                "battery_charge_w",
                "grid_surplus_over_house_w"
            ],
            rows);

        double observedHours = 0;
        double batteryChargeKwh = 0;
        double gridSurplusKwh = 0;

        for (var index = 0; index < candidates.Count - 1; index++)
        {
            var current = candidates[index];
            var next = candidates[index + 1];
            var hours =
                (next.TimestampUtc - current.TimestampUtc)
                .TotalHours;

            if (hours <= 0 ||
                hours > 20.0 / 60.0)
            {
                continue;
            }

            observedHours += hours;
            batteryChargeKwh +=
                (current.BatteryChargeWatts +
                 next.BatteryChargeWatts) /
                2.0 * hours / 1000.0;
            gridSurplusKwh +=
                (current.GridSurplusWatts +
                 next.GridSurplusWatts) /
                2.0 * hours / 1000.0;
        }

        var summary = $"""
            POSSIBLE GRID -> BATTERY BEHAVIOR
            =================================
            This is a diagnostic candidate detector, not a final attribution rule.

            Candidate definition:
            - PV <= 50 W;
            - grid import >= 100 W;
            - derived battery charging >= 50 W;
            - grid import exceeds household load by >= 25 W.

            Candidate frames: {candidates.Count}
            Gap-aware candidate duration: {observedHours:F3} h
            Integrated derived battery charge over contiguous candidates: {batteryChargeKwh:F3} kWh
            Integrated grid surplus over house over contiguous candidates: {gridSurplusKwh:F3} kWh

            Interpretation:
            Repeated candidates are physically consistent with utility-supported
            battery charging/maintenance, but inverter conversion losses and the
            derived nature of battery power mean they must not be promoted to a
            billing/source fact without corroboration.
            """;
        WriteText(zip, "25-grid-charging-candidate-summary.txt", summary);
    }

    private static void WriteEnergyFlowInterpretation(
        ZipArchive zip,
        SqliteConnection connection,
        string deviceId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT response_json, retrieved_utc
            FROM raw_api_capture
            WHERE device_id = $deviceId
              AND operation = 'EnergyFlowSnapshot'
            ORDER BY capture_id DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            WriteText(
                zip,
                "26-energy-flow-interpretation.txt",
                "No EnergyFlow snapshot was available.");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(reader.GetString(0));
            var root = document.RootElement;
            if (root.TryGetProperty("data", out var data))
            {
                root = data;
            }

            var state = root.TryGetProperty(
                "deviceAttributeState",
                out var stateElement)
                ? stateElement
                : default;
            var fields =
                state.ValueKind == JsonValueKind.Object &&
                state.TryGetProperty("fields", out var fieldElement)
                    ? fieldElement
                    : default;

            var pvKw = ReadNestedDouble(root, "pvPanelFlow", "value", "value");
            var gridKw = ReadNestedDouble(root, "gridFlow", "value", "value");
            var loadKw = ReadNestedDouble(root, "loadFlow", "value", "value");
            var gridDirection = ReadNestedString(root, "gridFlow", "flowDirection");
            var batteryDirection = ReadNestedString(root, "batteryFlow", "flowDirection");
            var loadDirection = ReadNestedString(root, "loadFlow", "flowDirection");

            var batteryVoltage = ReadFieldDouble(fields, "batteryVoltage");
            var bmsChargeCurrent = ReadFieldDouble(fields, "bmsChargingCurrent");
            var bmsDischargeCurrent = ReadFieldDouble(fields, "bmsDischargeCurrent");
            var estimatedBatteryWatts =
                batteryVoltage.HasValue &&
                bmsChargeCurrent.HasValue &&
                bmsDischargeCurrent.HasValue
                    ? batteryVoltage.Value *
                      (bmsDischargeCurrent.Value -
                       bmsChargeCurrent.Value)
                    : (double?)null;

            var gridMinusHouse =
                gridKw.HasValue && loadKw.HasValue
                    ? ((gridKw.Value - loadKw.Value) * 1000.0)
                        .ToString("F1", CultureInfo.InvariantCulture) + " W"
                    : "-";

            var summary = $"""
                LATEST STRUCTURED ENERGY FLOW
                =============================
                Retrieved UTC: {reader.GetString(1)}
                Device-state frame UTC: {ReadNestedString(state, "time") ?? "-"}

                PV flow: {FormatMaybe(pvKw, "kW")}
                Grid flow: {FormatMaybe(gridKw, "kW")} ; direction={gridDirection ?? "-"}
                Load flow: {FormatMaybe(loadKw, "kW")} ; direction={loadDirection ?? "-"}
                Battery flow direction: {batteryDirection ?? "-"}

                Current mode: {ReadFieldDisplay(fields, "mode") ?? "-"}
                Working mode: {ReadFieldDisplay(fields, "workingMode") ?? "-"}
                Charging priority: {ReadFieldDisplay(fields, "chargingPriorityOrder") ?? "-"}
                PV feeding priority: {ReadFieldDisplay(fields, "pvEnergyFeedingPriority") ?? "-"}

                Battery voltage: {FormatMaybe(batteryVoltage, "V")}
                BMS charging current: {FormatMaybe(bmsChargeCurrent, "A")}
                BMS discharge current: {FormatMaybe(bmsDischargeCurrent, "A")}
                Derived battery power: {FormatMaybe(estimatedBatteryWatts, "W")}
                (positive = discharge, negative = charge)

                Grid minus house at snapshot: {gridMinusHouse}

                A positive grid->inverter flow together with PV near zero,
                battery charging current and grid power above household load is
                consistent with grid-supported battery charging/maintenance.
                Preserve as diagnostic evidence; do not silently override OSO
                configuration semantics or historical source attribution.
                """;

            WriteText(zip, "26-energy-flow-interpretation.txt", summary);
        }
        catch (Exception ex)
        {
            WriteText(
                zip,
                "26-energy-flow-interpretation-error.txt",
                DiagnosticSanitizer.SanitizeText(ex.ToString()));
        }
    }

    private static double? ReadNestedDouble(
        JsonElement element,
        params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        if (current.ValueKind == JsonValueKind.Number &&
            current.TryGetDouble(out var value))
        {
            return value;
        }

        return double.TryParse(
            current.ToString(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value)
            ? value
            : null;
    }

    private static string? ReadNestedString(
        JsonElement element,
        params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return current.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? null
            : current.ToString();
    }

    private static double? ReadFieldDouble(
        JsonElement fields,
        string key)
    {
        if (fields.ValueKind != JsonValueKind.Object ||
            !fields.TryGetProperty(key, out var field) ||
            field.ValueKind != JsonValueKind.Object ||
            !field.TryGetProperty("value", out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetDouble(out var numeric))
        {
            return numeric;
        }

        return double.TryParse(
            value.ToString(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out numeric)
            ? numeric
            : null;
    }

    private static string? ReadFieldDisplay(
        JsonElement fields,
        string key)
    {
        if (fields.ValueKind != JsonValueKind.Object ||
            !fields.TryGetProperty(key, out var field) ||
            field.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (field.TryGetProperty(
                "valueDisplay",
                out var display))
        {
            return display.ToString();
        }

        return field.TryGetProperty("value", out var value)
            ? value.ToString()
            : null;
    }

    private static string FormatMaybe(
        double? value,
        string unit) =>
        value.HasValue
            ? $"{value.Value.ToString("F3", CultureInfo.InvariantCulture)} {unit}"
            : "-";

    private sealed record GridChargeCandidate(
        DateTimeOffset TimestampUtc,
        double PvWatts,
        double HouseWatts,
        double GridWatts,
        double BatteryWatts,
        double BatteryChargeWatts,
        double GridSurplusWatts);

    private static string F(double value) =>
        value.ToString("0.########", CultureInfo.InvariantCulture);

    private static void WriteSelectedRawCaptures(
        ZipArchive zip,
        SqliteConnection connection,
        string deviceId)
    {
        var operations = new[]
        {
            "LatestStateSnapshot",
            "EnergyFlowSnapshot",
            "ConfigCacheSnapshot",
            "ConfigDirectReadStart",
            "ConfigDirectReadDetails"
        };

        foreach (var operation in operations)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT response_json, retrieved_utc
                FROM raw_api_capture
                WHERE device_id = $deviceId
                  AND operation = $operation
                ORDER BY capture_id DESC
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$deviceId", deviceId);
            command.Parameters.AddWithValue("$operation", operation);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                continue;
            }

            var safe = DiagnosticSanitizer.SanitizeJson(reader.GetString(0), 4_000_000)
                       ?? "{}";
            var prefix = operation switch
            {
                "LatestStateSnapshot" => "40-latest-state",
                "EnergyFlowSnapshot" => "41-energy-flow",
                "ConfigCacheSnapshot" => "42-config-cache",
                "ConfigDirectReadStart" => "43-config-read-start",
                "ConfigDirectReadDetails" => "44-config-read-details",
                _ => "49-raw"
            };

            WriteText(zip, $"{prefix}-{SafeTimestamp(reader.GetString(1))}.json", safe);
        }
    }

    private static string SafeTimestamp(string raw) =>
        DateTimeOffset.TryParse(raw, out var value)
            ? value.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            : "unknown";

    private static IEnumerable<string[]> ExpandTableCounts(
        SqliteConnection connection,
        IEnumerable<string[]> input)
    {
        foreach (var row in input)
        {
            var name = row[0];
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM \"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\";";
            yield return [name, Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "0"];
        }
    }

    private static void WriteQueryCsv(
        ZipArchive zip,
        SqliteConnection connection,
        string entryName,
        string sql,
        string? deviceId = null,
        Func<IEnumerable<string[]>, IEnumerable<string[]>>? transform = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (deviceId is not null)
        {
            command.Parameters.AddWithValue("$deviceId", deviceId);
        }

        using var reader = command.ExecuteReader();
        var headers = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();
        var rows = new List<string[]>();

        while (reader.Read())
        {
            rows.Add(Enumerable.Range(0, reader.FieldCount)
                .Select(index => reader.IsDBNull(index)
                    ? string.Empty
                    : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? string.Empty)
                .ToArray());
        }

        if (transform is not null)
        {
            rows = transform(rows).ToList();
        }

        WriteCsv(zip, entryName, headers, rows);
    }

    private static void WriteCsv(
        ZipArchive zip,
        string entryName,
        IReadOnlyList<string> headers,
        IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(Csv)));

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row.Select(Csv)));
        }

        WriteText(zip, entryName, sb.ToString());
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }

    private static void WriteText(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }
}
