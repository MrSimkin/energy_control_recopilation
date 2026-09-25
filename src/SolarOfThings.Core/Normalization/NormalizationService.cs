using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Diagnostics;

namespace SolarOfThings.Core.Normalization;

public sealed class NormalizationService
{
    public const string RuleVersion = "hpvinv02.v3";

    private static readonly string[] RelevantKeys =
    [
        "generationPower",
        "pvPower",
        "outputActivePower",
        "mainsPower",
        "acInputVoltage",
        "batteryCapacity",
        "bmsCurrentSOC",
        "batteryVoltage",
        "bmsChargingCurrent",
        "batteryChargingCurrent",
        "batteryDischargeCurrent",
        "pvGeneratedEnergyOfDay",
        "pvGeneratedEnergyOfTotal"
    ];

    private readonly SqliteDatabase _database;
    private readonly ApiDiagnosticsStore _diagnostics;

    public NormalizationService(
        SqliteDatabase database,
        ApiDiagnosticsStore diagnostics)
    {
        _database = database;
        _diagnostics = diagnostics;
    }

    public Task<NormalizationResult> RebuildAsync(
        CommissioningProfile profile,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => Rebuild(profile, cancellationToken),
            cancellationToken);
    }

    private NormalizationResult Rebuild(
        CommissioningProfile profile,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(profile.Model, "HPVINV02", StringComparison.OrdinalIgnoreCase))
        {
            var unsupported = new NormalizationResult(
                profile.DeviceId,
                RuleVersion,
                "UNSUPPORTED",
                0,
                0,
                new Dictionary<string, int>(),
                $"No safe normalization rule exists yet for model '{profile.Model ?? "(unknown)"}'.");

            _diagnostics.RecordLocal(
                "Normalization",
                "UnsupportedModel",
                "UNRESOLVED",
                unsupported.Detail ?? "Unsupported model.");

            return unsupported;
        }

        var units = ReadAttributeUnits(profile.AttributeCatalogJson);
        var runId = StartRun(profile.DeviceId);
        var started = DateTimeOffset.UtcNow;
        var metricCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var inputFrames = 0;
        var outputMetrics = 0;

        try
        {
            using var readConnection = _database.OpenConnection();
            using var readCommand = readConnection.CreateCommand();
            readCommand.CommandText = $"""
                SELECT
                    recorded_at_utc,
                    attribute_key,
                    value_json,
                    is_missing
                FROM history_sample
                WHERE device_id = $deviceId
                  AND attribute_key IN ({string.Join(",", RelevantKeys.Select((_, index) => $"$key{index}"))})
                ORDER BY recorded_at_utc, attribute_key;
                """;
            readCommand.Parameters.AddWithValue("$deviceId", profile.DeviceId);
            for (var i = 0; i < RelevantKeys.Length; i++)
            {
                readCommand.Parameters.AddWithValue($"$key{i}", RelevantKeys[i]);
            }

            using var reader = readCommand.ExecuteReader();

            using var writeConnection = _database.OpenConnection();
            using var transaction = writeConnection.BeginTransaction();

            using (var delete = writeConnection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = """
                    DELETE FROM normalized_metric_sample
                    WHERE device_id = $deviceId;
                    """;
                delete.Parameters.AddWithValue("$deviceId", profile.DeviceId);
                delete.ExecuteNonQuery();
            }

            using var insert = CreateInsertCommand(writeConnection, transaction);

            string? currentTimestampRaw = null;
            DateTimeOffset currentTimestamp = default;
            var frame = new Dictionary<string, RawReading>(StringComparer.Ordinal);

            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var timestampRaw = reader.GetString(0);
                if (!string.Equals(currentTimestampRaw, timestampRaw, StringComparison.Ordinal))
                {
                    if (currentTimestampRaw is not null)
                    {
                        outputMetrics += WriteFrame(
                            profile,
                            units,
                            currentTimestamp,
                            frame,
                            insert,
                            metricCounts);
                        inputFrames++;
                    }

                    frame.Clear();
                    currentTimestampRaw = timestampRaw;

                    if (!DateTimeOffset.TryParse(timestampRaw, out currentTimestamp))
                    {
                        currentTimestampRaw = null;
                        continue;
                    }
                }

                var key = reader.GetString(1);
                var isMissing = reader.GetInt32(3) != 0;
                var valueJson = reader.IsDBNull(2) ? null : reader.GetString(2);
                frame[key] = new RawReading(
                    isMissing ? null : ParseDouble(valueJson),
                    valueJson,
                    isMissing);
            }

            if (currentTimestampRaw is not null)
            {
                outputMetrics += WriteFrame(
                    profile,
                    units,
                    currentTimestamp,
                    frame,
                    insert,
                    metricCounts);
                inputFrames++;
            }

            transaction.Commit();

            var result = new NormalizationResult(
                profile.DeviceId,
                RuleVersion,
                "SUCCESS",
                inputFrames,
                outputMetrics,
                metricCounts,
                $"Rebuilt normalized metrics locally in {(DateTimeOffset.UtcNow - started).TotalSeconds:F1}s.");

            CompleteRun(runId, "SUCCESS", result);
            _diagnostics.RecordLocal(
                "Normalization",
                "RebuildComplete",
                "SUCCESS",
                $"Normalized {outputMetrics} metric samples from {inputFrames} raw frames.",
                JsonSerializer.Serialize(result));

            return result;
        }
        catch (OperationCanceledException)
        {
            var result = new NormalizationResult(
                profile.DeviceId,
                RuleVersion,
                "CANCELLED",
                inputFrames,
                outputMetrics,
                metricCounts,
                "Normalization cancelled; the previous committed normalized corpus remains intact.");

            CompleteRun(runId, "CANCELLED", result);
            return result;
        }
        catch (Exception ex)
        {
            var failed = new NormalizationResult(
                profile.DeviceId,
                RuleVersion,
                "FAIL",
                inputFrames,
                outputMetrics,
                metricCounts,
                ex.Message);

            CompleteRun(runId, "FAIL", failed);
            _diagnostics.RecordLocal(
                "Normalization",
                "RebuildFailed",
                "FAIL",
                ex.Message);

            throw;
        }
    }

    private int WriteFrame(
        CommissioningProfile profile,
        IReadOnlyDictionary<string, string?> units,
        DateTimeOffset timestamp,
        IReadOnlyDictionary<string, RawReading> frame,
        SqliteCommand insert,
        IDictionary<string, int> metricCounts)
    {
        var rows = new List<NormalizedRow>(10);
        var ratedPowerW = NormalizeRatedPower(profile.RatedPower);

        AddTargetPvPower(
            rows,
            frame,
            units,
            ratedPowerW);

        AddDirectPower(
            rows,
            frame,
            units,
            "outputActivePower",
            "kW",
            "house_load_power_w",
            "CONFIRMED",
            "OFFICIAL_ENERGY_FLOW_LOAD",
            ratedPowerW,
            2.0);

        AddGridImport(rows, frame, units, ratedPowerW);
        AddGridVoltage(rows, frame, units);
        AddBatterySoc(rows, frame, units);
        AddBatteryVoltage(rows, frame, units);
        AddBatteryCurrents(rows, frame, units);
        AddBatteryPower(rows, frame, units, ratedPowerW);
        AddCounter(rows, frame, units, "pvGeneratedEnergyOfDay", "pv_energy_day_counter_kwh");
        AddCounter(rows, frame, units, "pvGeneratedEnergyOfTotal", "pv_energy_total_counter_kwh");

        foreach (var row in rows)
        {
            BindAndExecute(insert, profile.DeviceId, timestamp, row);

            metricCounts[row.MetricKey] =
                metricCounts.TryGetValue(row.MetricKey, out var count)
                    ? count + 1
                    : 1;
        }

        return rows.Count;
    }

    private static void AddTargetPvPower(
        ICollection<NormalizedRow> rows,
        IReadOnlyDictionary<string, RawReading> frame,
        IReadOnlyDictionary<string, string?> units,
        double? ratedPowerW)
    {
        var primaryKey = TryRead(frame, units, "generationPower", "kW", out var generation)
            ? "generationPower"
            : TryRead(frame, units, "pvPower", "kW", out generation)
                ? "pvPower"
                : null;

        if (primaryKey is null)
        {
            return;
        }

        var watts = generation.Value!.Value * 1000.0;
        var valid = watts >= 0 &&
                    (!ratedPowerW.HasValue || watts <= ratedPowerW.Value * 2.0);

        rows.Add(new NormalizedRow(
            "pv_power_w",
            valid ? watts : null,
            "W",
            primaryKey,
            generation.Json,
            valid ? "CONFIRMED" : "UNRESOLVED",
            valid
                ? primaryKey == "generationPower"
                    ? "OFFICIAL_ENERGY_FLOW_PRIMARY"
                    : "FALLBACK_PV_ALIAS"
                : "PLAUSIBILITY_REVIEW"));
    }

    private static void AddDirectPower(
        ICollection<NormalizedRow> rows,
        IReadOnlyDictionary<string, RawReading> frame,
        IReadOnlyDictionary<string, string?> units,
        string rawKey,
        string expectedUnit,
        string metricKey,
        string confidence,
        string quality,
        double? ratedPowerW,
        double ratedMultiplier)
    {
        if (!TryRead(frame, units, rawKey, expectedUnit, out var raw))
        {
            return;
        }

        var watts = raw.Value!.Value * 1000.0;
        var finalConfidence = confidence;
        var finalQuality = quality;

        if (watts < 0 ||
            (ratedPowerW.HasValue && watts > ratedPowerW.Value * ratedMultiplier))
        {
            finalConfidence = "UNRESOLVED";
            finalQuality = "PLAUSIBILITY_REVIEW";
        }

        rows.Add(new NormalizedRow(
            metricKey,
            finalConfidence == "UNRESOLVED" ? null : watts,
            "W",
            rawKey,
            raw.Json,
            finalConfidence,
            finalQuality));
    }

    private static void AddGridImport(
        ICollection<NormalizedRow> rows,
        IReadOnlyDictionary<string, RawReading> frame,
        IReadOnlyDictionary<string, string?> units,
        double? ratedPowerW)
    {
        if (!TryRead(frame, units, "mainsPower", "kW", out var raw))
        {
            return;
        }

        var watts = raw.Value!.Value * 1000.0;
        var confidence = "CONFIRMED";
        var quality = "OFFICIAL_ENERGY_FLOW_GRID_IMPORT";
        double? normalized = watts;

        if (watts < 0)
        {
            normalized = null;
            confidence = "UNRESOLVED";
            quality = "NEGATIVE_GRID_SIGN_REQUIRES_REVIEW";
        }
        else if (ratedPowerW.HasValue && watts > ratedPowerW.Value * 3.0)
        {
            normalized = null;
            confidence = "UNRESOLVED";
            quality = "PLAUSIBILITY_REVIEW";
        }

        rows.Add(new NormalizedRow(
            "grid_import_power_w",
            normalized,
            "W",
            "mainsPower",
            raw.Json,
            confidence,
            quality));
    }

    private static void AddGridVoltage(
        ICollection<NormalizedRow> rows,
        IReadOnlyDictionary<string, RawReading> frame,
        IReadOnlyDictionary<string, string?> units)
    {
        if (!TryRead(frame, units, "acInputVoltage", "V", out var raw))
        {
            return;
        }

        var value = raw.Value!.Value;
        var valid = value is >= 0 and < 350;

        rows.Add(new NormalizedRow(
            "grid_voltage_v",
            valid ? value : null,
            "V",
            "acInputVoltage",
            raw.Json,
            valid ? "CONFIRMED" : "UNRESOLVED",
            valid ? "GRID_INPUT_VOLTAGE" : "PLAUSIBILITY_REVIEW"));
    }

    private static void AddBatterySoc(
        ICollection<NormalizedRow> rows,
        IReadOnlyDictionary<string, RawReading> frame,
        IReadOnlyDictionary<string, string?> units)
    {
        RawReading raw;
        string sourceKey;

        if (TryRead(frame, units, "bmsCurrentSOC", "%", out raw))
        {
            sourceKey = "bmsCurrentSOC";
        }
        else if (TryRead(frame, units, "batteryCapacity", "%", out raw))
        {
            sourceKey = "batteryCapacity";
        }
        else
        {
            return;
        }

        var value = raw.Value!.Value;
        var valid = value is >= 0 and <= 100;

        rows.Add(new NormalizedRow(
            "battery_soc_pct",
            valid ? value : null,
            "%",
            sourceKey,
            raw.Json,
            valid ? "CONFIRMED" : "UNRESOLVED",
            valid ? "OK" : "OUT_OF_RANGE"));
    }

    private static void AddBatteryVoltage(
        ICollection<NormalizedRow> rows,
        IReadOnlyDictionary<string, RawReading> frame,
        IReadOnlyDictionary<string, string?> units)
    {
        if (!TryRead(frame, units, "batteryVoltage", "V", out var raw))
        {
            return;
        }

        var value = raw.Value!.Value;
        var valid = value is > 0 and < 100;

        rows.Add(new NormalizedRow(
            "battery_voltage_v",
            valid ? value : null,
            "V",
            "batteryVoltage",
            raw.Json,
            valid ? "CONFIRMED" : "UNRESOLVED",
            valid ? "OK" : "PLAUSIBILITY_REVIEW"));
    }

    private static void AddBatteryCurrents(
        ICollection<NormalizedRow> rows,
        IReadOnlyDictionary<string, RawReading> frame,
        IReadOnlyDictionary<string, string?> units)
    {
        RawReading charge;
        string? chargeKey = null;

        if (TryRead(frame, units, "bmsChargingCurrent", "A", out charge))
        {
            chargeKey = "bmsChargingCurrent";
        }
        else if (TryRead(frame, units, "batteryChargingCurrent", "A", out charge))
        {
            chargeKey = "batteryChargingCurrent";
        }

        if (chargeKey is not null)
        {
            var value = charge.Value!.Value;
            var valid = value is >= 0 and < 500;

            rows.Add(new NormalizedRow(
                "battery_charge_current_a",
                valid ? value : null,
                "A",
                chargeKey,
                charge.Json,
                valid ? "CONFIRMED" : "UNRESOLVED",
                valid ? "MEASURED_DIRECTIONAL_CURRENT" : "PLAUSIBILITY_REVIEW"));
        }

        if (TryRead(frame, units, "batteryDischargeCurrent", "A", out var discharge))
        {
            var value = discharge.Value!.Value;
            var valid = value is >= 0 and < 500;

            rows.Add(new NormalizedRow(
                "battery_discharge_current_a",
                valid ? value : null,
                "A",
                "batteryDischargeCurrent",
                discharge.Json,
                valid ? "CONFIRMED" : "UNRESOLVED",
                valid ? "MEASURED_DIRECTIONAL_CURRENT" : "PLAUSIBILITY_REVIEW"));
        }
    }

    private static void AddBatteryPower(
        ICollection<NormalizedRow> rows,
        IReadOnlyDictionary<string, RawReading> frame,
        IReadOnlyDictionary<string, string?> units,
        double? ratedPowerW)
    {
        if (!TryRead(frame, units, "batteryVoltage", "V", out var voltage) ||
            !TryRead(frame, units, "batteryDischargeCurrent", "A", out var discharge))
        {
            return;
        }

        RawReading charge;
        string chargeKey;

        if (TryRead(frame, units, "bmsChargingCurrent", "A", out charge))
        {
            chargeKey = "bmsChargingCurrent";
        }
        else if (TryRead(frame, units, "batteryChargingCurrent", "A", out charge))
        {
            chargeKey = "batteryChargingCurrent";
        }
        else
        {
            return;
        }

        var watts =
            voltage.Value!.Value *
            (discharge.Value!.Value - charge.Value!.Value);

        var valid = !ratedPowerW.HasValue ||
                    Math.Abs(watts) <= ratedPowerW.Value * 2.0;

        rows.Add(new NormalizedRow(
            "battery_power_w",
            valid ? watts : null,
            "W",
            $"batteryVoltage+{chargeKey}+batteryDischargeCurrent",
            JsonSerializer.Serialize(new
            {
                batteryVoltage = voltage.Value,
                chargingCurrent = charge.Value,
                dischargeCurrent = discharge.Value,
                chargingSource = chargeKey
            }),
            valid ? "PROBABLE" : "UNRESOLVED",
            valid
                ? "DERIVED_VOLTAGE_X_DIRECTIONAL_CURRENTS"
                : "PLAUSIBILITY_REVIEW"));
    }

    private static void AddCounter(
        ICollection<NormalizedRow> rows,
        IReadOnlyDictionary<string, RawReading> frame,
        IReadOnlyDictionary<string, string?> units,
        string rawKey,
        string metricKey)
    {
        if (!TryRead(frame, units, rawKey, "kWh", out var raw))
        {
            return;
        }

        var value = raw.Value!.Value;
        var valid = value >= 0;

        rows.Add(new NormalizedRow(
            metricKey,
            valid ? value : null,
            "kWh",
            rawKey,
            raw.Json,
            valid ? "CONFIRMED" : "UNRESOLVED",
            valid ? "OK" : "PLAUSIBILITY_REVIEW"));
    }

    private static bool TryRead(
        IReadOnlyDictionary<string, RawReading> frame,
        IReadOnlyDictionary<string, string?> units,
        string key,
        string expectedUnit,
        out RawReading reading)
    {
        if (!frame.TryGetValue(key, out var found) ||
            found.Missing ||
            !found.Value.HasValue)
        {
            reading = new RawReading(null, null, true);
            return false;
        }

        reading = found;
        return units.TryGetValue(key, out var actualUnit) &&
               string.Equals(actualUnit, expectedUnit, StringComparison.OrdinalIgnoreCase);
    }

    private static double? NormalizeRatedPower(decimal? ratedPower)
    {
        if (!ratedPower.HasValue || ratedPower.Value <= 0)
        {
            return null;
        }

        var value = (double)ratedPower.Value;
        return value <= 50
            ? value * 1000.0
            : value;
    }

    private static IReadOnlyDictionary<string, string?> ReadAttributeUnits(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new Dictionary<string, string?>();
            }

            var result = new Dictionary<string, string?>(StringComparer.Ordinal);

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("key", out var keyElement) ||
                    keyElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var key = keyElement.GetString();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                string? unit = null;
                if (item.TryGetProperty("unit", out var unitElement) &&
                    unitElement.ValueKind == JsonValueKind.String)
                {
                    unit = unitElement.GetString();
                }

                result[key] = unit;
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, string?>();
        }
    }

    private static double? ParseDouble(string? valueJson)
    {
        if (string.IsNullOrWhiteSpace(valueJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(valueJson);
            var value = document.RootElement;

            if (value.ValueKind == JsonValueKind.Number &&
                value.TryGetDouble(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String &&
                double.TryParse(
                    value.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out number))
            {
                return number;
            }
        }
        catch
        {
        }

        return null;
    }

    private long StartRun(string deviceId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO normalization_run (
                device_id,
                rule_version,
                started_utc,
                status
            )
            VALUES (
                $deviceId,
                $ruleVersion,
                $startedUtc,
                'RUNNING'
            );
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);
        command.Parameters.AddWithValue("$ruleVersion", RuleVersion);
        command.Parameters.AddWithValue("$startedUtc", DateTimeOffset.UtcNow.ToString("O"));
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private void CompleteRun(
        long runId,
        string status,
        NormalizationResult result)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE normalization_run
            SET completed_utc = $completedUtc,
                status = $status,
                input_frame_count = $inputFrames,
                output_metric_count = $outputMetrics,
                detail_json = $detailJson
            WHERE normalization_run_id = $runId;
            """;
        command.Parameters.AddWithValue("$completedUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$inputFrames", result.InputFrameCount);
        command.Parameters.AddWithValue("$outputMetrics", result.OutputMetricCount);
        command.Parameters.AddWithValue("$detailJson", JsonSerializer.Serialize(result));
        command.Parameters.AddWithValue("$runId", runId);
        command.ExecuteNonQuery();
    }

    private static SqliteCommand CreateInsertCommand(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO normalized_metric_sample (
                device_id,
                metric_key,
                recorded_at_utc,
                normalized_value,
                normalized_unit,
                source_attribute_key,
                source_value_json,
                normalization_rule_version,
                confidence,
                quality,
                updated_utc
            )
            VALUES (
                $deviceId,
                $metricKey,
                $recordedAtUtc,
                $normalizedValue,
                $normalizedUnit,
                $sourceAttributeKey,
                $sourceValueJson,
                $ruleVersion,
                $confidence,
                $quality,
                $updatedUtc
            )
            ON CONFLICT(device_id, metric_key, recorded_at_utc) DO UPDATE SET
                normalized_value = excluded.normalized_value,
                normalized_unit = excluded.normalized_unit,
                source_attribute_key = excluded.source_attribute_key,
                source_value_json = excluded.source_value_json,
                normalization_rule_version = excluded.normalization_rule_version,
                confidence = excluded.confidence,
                quality = excluded.quality,
                updated_utc = excluded.updated_utc;
            """;

        command.Parameters.Add("$deviceId", SqliteType.Text);
        command.Parameters.Add("$metricKey", SqliteType.Text);
        command.Parameters.Add("$recordedAtUtc", SqliteType.Text);
        command.Parameters.Add("$normalizedValue", SqliteType.Real);
        command.Parameters.Add("$normalizedUnit", SqliteType.Text);
        command.Parameters.Add("$sourceAttributeKey", SqliteType.Text);
        command.Parameters.Add("$sourceValueJson", SqliteType.Text);
        command.Parameters.Add("$ruleVersion", SqliteType.Text);
        command.Parameters.Add("$confidence", SqliteType.Text);
        command.Parameters.Add("$quality", SqliteType.Text);
        command.Parameters.Add("$updatedUtc", SqliteType.Text);

        command.Prepare();
        return command;
    }

    private static void BindAndExecute(
        SqliteCommand command,
        string deviceId,
        DateTimeOffset timestamp,
        NormalizedRow row)
    {
        command.Parameters["$deviceId"].Value = deviceId;
        command.Parameters["$metricKey"].Value = row.MetricKey;
        command.Parameters["$recordedAtUtc"].Value = timestamp.ToUniversalTime().ToString("O");
        command.Parameters["$normalizedValue"].Value =
            row.Value.HasValue ? row.Value.Value : DBNull.Value;
        command.Parameters["$normalizedUnit"].Value = row.Unit;
        command.Parameters["$sourceAttributeKey"].Value = row.SourceAttributeKey;
        command.Parameters["$sourceValueJson"].Value =
            row.SourceValueJson ?? (object)DBNull.Value;
        command.Parameters["$ruleVersion"].Value = RuleVersion;
        command.Parameters["$confidence"].Value = row.Confidence;
        command.Parameters["$quality"].Value = row.Quality;
        command.Parameters["$updatedUtc"].Value = DateTimeOffset.UtcNow.ToString("O");
        command.ExecuteNonQuery();
    }

    private sealed record RawReading(
        double? Value,
        string? Json,
        bool Missing);

    private sealed record NormalizedRow(
        string MetricKey,
        double? Value,
        string Unit,
        string SourceAttributeKey,
        string? SourceValueJson,
        string Confidence,
        string Quality);
}
