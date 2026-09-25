using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Installation;

public sealed class InstallationHealthRepository
{
    private readonly SqliteDatabase _database;

    public InstallationHealthRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public InstallationStateSnapshot? GetLatestStateSnapshot(string deviceId)
    {
        using var connection = _database.OpenConnection();
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
        if (!reader.Read() ||
            !DateTimeOffset.TryParse(reader.GetString(1), out var retrievedAt))
        {
            return null;
        }

        return new InstallationStateSnapshot(retrievedAt, reader.GetString(0));
    }

    public void SaveSummary(InstallationConfigSummary summary)
    {
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO installation_config_check (
                device_id,
                check_key,
                source_attribute_key,
                status,
                observed_at_utc,
                observed_value_json,
                expected_display,
                detail,
                evaluated_utc
            )
            VALUES (
                $deviceId,
                $checkKey,
                $sourceAttributeKey,
                $status,
                $observedAtUtc,
                $observedValueJson,
                $expectedDisplay,
                $detail,
                $evaluatedUtc
            )
            ON CONFLICT(device_id, check_key) DO UPDATE SET
                source_attribute_key = excluded.source_attribute_key,
                status = excluded.status,
                observed_at_utc = excluded.observed_at_utc,
                observed_value_json = excluded.observed_value_json,
                expected_display = excluded.expected_display,
                detail = excluded.detail,
                evaluated_utc = excluded.evaluated_utc;
            """;

        command.Parameters.Add("$deviceId", SqliteType.Text);
        command.Parameters.Add("$checkKey", SqliteType.Text);
        command.Parameters.Add("$sourceAttributeKey", SqliteType.Text);
        command.Parameters.Add("$status", SqliteType.Text);
        command.Parameters.Add("$observedAtUtc", SqliteType.Text);
        command.Parameters.Add("$observedValueJson", SqliteType.Text);
        command.Parameters.Add("$expectedDisplay", SqliteType.Text);
        command.Parameters.Add("$detail", SqliteType.Text);
        command.Parameters.Add("$evaluatedUtc", SqliteType.Text);

        foreach (var check in summary.Checks)
        {
            command.Parameters["$deviceId"].Value = summary.DeviceId;
            command.Parameters["$checkKey"].Value = check.CheckKey;
            command.Parameters["$sourceAttributeKey"].Value = check.SourceAttributeKey;
            command.Parameters["$status"].Value = check.Status;
            command.Parameters["$observedAtUtc"].Value =
                check.ObservedAtUtc.HasValue
                    ? check.ObservedAtUtc.Value.ToString("O")
                    : DBNull.Value;
            command.Parameters["$observedValueJson"].Value =
                string.IsNullOrWhiteSpace(check.ObservedValueJson)
                    ? DBNull.Value
                    : check.ObservedValueJson;
            command.Parameters["$expectedDisplay"].Value = check.ExpectedDisplay;
            command.Parameters["$detail"].Value = check.Detail;
            command.Parameters["$evaluatedUtc"].Value = check.EvaluatedUtc.ToString("O");
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public InstallationConfigSummary? GetSummary(string deviceId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                check_key,
                source_attribute_key,
                status,
                observed_at_utc,
                observed_value_json,
                expected_display,
                detail,
                evaluated_utc
            FROM installation_config_check
            WHERE device_id = $deviceId
            ORDER BY check_key;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);

        using var reader = command.ExecuteReader();
        var checks = new List<InstallationConfigCheck>();
        DateTimeOffset? evaluatedAt = null;

        while (reader.Read())
        {
            DateTimeOffset? observedAt = null;
            if (!reader.IsDBNull(3) &&
                DateTimeOffset.TryParse(reader.GetString(3), out var parsedObservedAt))
            {
                observedAt = parsedObservedAt;
            }

            if (DateTimeOffset.TryParse(reader.GetString(7), out var parsedEvaluatedAt))
            {
                evaluatedAt = !evaluatedAt.HasValue || parsedEvaluatedAt > evaluatedAt.Value
                    ? parsedEvaluatedAt
                    : evaluatedAt;
            }

            checks.Add(new InstallationConfigCheck(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                observedAt,
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                parsedEvaluatedAt));
        }

        if (checks.Count == 0)
        {
            return null;
        }

        var drift = checks.Count(check => check.Status == "CONFIG_DRIFT");
        var unresolved = checks.Count(check => check.Status == "CONFIG_UNRESOLVED");
        var confirmed = checks.Count(check => check.Status == "CONFIG_CONFIRMED");

        var overall = drift > 0
            ? "CONFIG_DRIFT"
            : unresolved > 0
                ? "CONFIG_UNRESOLVED"
                : "CONFIG_CONFIRMED";

        return new InstallationConfigSummary(
            deviceId,
            overall,
            confirmed,
            drift,
            unresolved,
            evaluatedAt ?? DateTimeOffset.MinValue,
            checks);
    }
}
