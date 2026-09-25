using System.Text.Json;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Diagnostics;
using SolarOfThings.Core.Normalization;

namespace SolarOfThings.Core.Installation;

public sealed class HouseholdBehaviorService
{
    private static readonly string[] MetricKeys =
    [
        "pv_power_w",
        "house_load_power_w",
        "grid_import_power_w",
        "grid_voltage_v",
        "battery_soc_pct",
        "battery_power_w"
    ];

    private readonly SqliteDatabase _database;
    private readonly HouseholdOperatingStateService _classifier;
    private readonly ApiDiagnosticsStore _diagnostics;

    public HouseholdBehaviorService(
        SqliteDatabase database,
        HouseholdOperatingStateService classifier,
        ApiDiagnosticsStore diagnostics)
    {
        _database = database;
        _classifier = classifier;
        _diagnostics = diagnostics;
    }

    public Task<HouseholdBehaviorResult> RebuildAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => Rebuild(deviceId, cancellationToken),
            cancellationToken);
    }

    private HouseholdBehaviorResult Rebuild(
        string deviceId,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var contextVersion = InstallationContextPolicyService.ContextVersion;
        var inputFrames = 0;
        var outputSamples = 0;
        var stateCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        try
        {
            using var readConnection = _database.OpenConnection();
            using var readCommand = readConnection.CreateCommand();

            readCommand.CommandText = $"""
                SELECT
                    recorded_at_utc,
                    metric_key,
                    normalized_value,
                    normalized_unit,
                    source_attribute_key,
                    normalization_rule_version,
                    confidence,
                    quality
                FROM normalized_metric_sample
                WHERE device_id = $deviceId
                  AND normalized_value IS NOT NULL
                  AND confidence <> 'UNRESOLVED'
                  AND metric_key IN ({string.Join(",", MetricKeys.Select((_, index) => $"$key{index}"))})
                ORDER BY recorded_at_utc, metric_key;
                """;

            readCommand.Parameters.AddWithValue("$deviceId", deviceId);
            for (var i = 0; i < MetricKeys.Length; i++)
            {
                readCommand.Parameters.AddWithValue($"$key{i}", MetricKeys[i]);
            }

            using var reader = readCommand.ExecuteReader();

            using var writeConnection = _database.OpenConnection();
            using var transaction = writeConnection.BeginTransaction();

            using (var delete = writeConnection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = """
                    DELETE FROM household_behavior_sample
                    WHERE device_id = $deviceId
                      AND context_version = $contextVersion;
                    """;
                delete.Parameters.AddWithValue("$deviceId", deviceId);
                delete.Parameters.AddWithValue("$contextVersion", contextVersion);
                delete.ExecuteNonQuery();
            }

            using var insert = writeConnection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO household_behavior_sample (
                    device_id,
                    recorded_at_utc,
                    context_version,
                    state_key,
                    confidence,
                    evidence_json,
                    updated_utc
                )
                VALUES (
                    $deviceId,
                    $recordedAtUtc,
                    $contextVersion,
                    $stateKey,
                    $confidence,
                    $evidenceJson,
                    $updatedUtc
                );
                """;

            insert.Parameters.Add("$deviceId", SqliteType.Text);
            insert.Parameters.Add("$recordedAtUtc", SqliteType.Text);
            insert.Parameters.Add("$contextVersion", SqliteType.Text);
            insert.Parameters.Add("$stateKey", SqliteType.Text);
            insert.Parameters.Add("$confidence", SqliteType.Text);
            insert.Parameters.Add("$evidenceJson", SqliteType.Text);
            insert.Parameters.Add("$updatedUtc", SqliteType.Text);

            string? currentTimestampRaw = null;
            DateTimeOffset currentTimestamp = default;
            var frame = new Dictionary<string, NormalizedMetricValue>(StringComparer.Ordinal);

            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var timestampRaw = reader.GetString(0);
                if (!string.Equals(currentTimestampRaw, timestampRaw, StringComparison.Ordinal))
                {
                    if (currentTimestampRaw is not null)
                    {
                        outputSamples += WriteFrame(
                            deviceId,
                            contextVersion,
                            currentTimestamp,
                            frame,
                            insert,
                            stateCounts);
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

                var metricKey = reader.GetString(1);
                frame[metricKey] = new NormalizedMetricValue(
                    metricKey,
                    currentTimestamp,
                    reader.GetDouble(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetString(7));
            }

            if (currentTimestampRaw is not null)
            {
                outputSamples += WriteFrame(
                    deviceId,
                    contextVersion,
                    currentTimestamp,
                    frame,
                    insert,
                    stateCounts);
                inputFrames++;
            }

            transaction.Commit();

            var result = new HouseholdBehaviorResult(
                deviceId,
                contextVersion,
                "SUCCESS",
                inputFrames,
                outputSamples,
                stateCounts,
                $"Rebuilt contextual behavior samples locally in {(DateTimeOffset.UtcNow - started).TotalSeconds:F1}s.");

            _diagnostics.RecordLocal(
                "HouseholdBehavior",
                "RebuildComplete",
                "SUCCESS",
                $"Classified {outputSamples} contextual behavior samples from {inputFrames} normalized frames.",
                JsonSerializer.Serialize(result));

            return result;
        }
        catch (OperationCanceledException)
        {
            return new HouseholdBehaviorResult(
                deviceId,
                contextVersion,
                "CANCELLED",
                inputFrames,
                outputSamples,
                stateCounts,
                "Contextual behavior rebuild cancelled; the previously committed contextual corpus remains intact.");
        }
        catch (Exception ex)
        {
            _diagnostics.RecordLocal(
                "HouseholdBehavior",
                "RebuildFailed",
                "FAIL",
                ex.Message);

            throw;
        }
    }

    private int WriteFrame(
        string deviceId,
        string contextVersion,
        DateTimeOffset timestamp,
        IReadOnlyDictionary<string, NormalizedMetricValue> frame,
        SqliteCommand insert,
        IDictionary<string, int> stateCounts)
    {
        if (frame.Count == 0)
        {
            return 0;
        }

        var state = _classifier.Evaluate(frame);
        if (state.StateKey == "NO_DATA")
        {
            return 0;
        }

        var confidence = frame.Values.All(metric => metric.Confidence == "CONFIRMED")
            ? "CONFIRMED"
            : "PROBABLE";

        var evidenceJson = JsonSerializer.Serialize(
            frame.ToDictionary(
                pair => pair.Key,
                pair => new
                {
                    pair.Value.Value,
                    pair.Value.Unit,
                    pair.Value.Confidence,
                    pair.Value.SourceAttributeKey,
                    pair.Value.RuleVersion
                },
                StringComparer.Ordinal));

        insert.Parameters["$deviceId"].Value = deviceId;
        insert.Parameters["$recordedAtUtc"].Value = timestamp.ToUniversalTime().ToString("O");
        insert.Parameters["$contextVersion"].Value = contextVersion;
        insert.Parameters["$stateKey"].Value = state.StateKey;
        insert.Parameters["$confidence"].Value = confidence;
        insert.Parameters["$evidenceJson"].Value = evidenceJson;
        insert.Parameters["$updatedUtc"].Value = DateTimeOffset.UtcNow.ToString("O");
        insert.ExecuteNonQuery();

        stateCounts[state.StateKey] =
            stateCounts.TryGetValue(state.StateKey, out var count)
                ? count + 1
                : 1;

        return 1;
    }
}
