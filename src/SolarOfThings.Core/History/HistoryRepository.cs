using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.History;

public sealed class HistoryRepository
{
    private readonly SqliteDatabase _database;

    public HistoryRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public int UpsertSamples(IEnumerable<HistorySample> samples)
    {
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO history_sample (
                device_id,
                attribute_key,
                recorded_at_utc,
                value_json,
                is_missing,
                source,
                retrieved_utc
            )
            VALUES (
                $deviceId,
                $attributeKey,
                $recordedAtUtc,
                $valueJson,
                $isMissing,
                $source,
                $retrievedUtc
            )
            ON CONFLICT(device_id, attribute_key, recorded_at_utc) DO UPDATE SET
                value_json = CASE
                    WHEN excluded.is_missing = 1 AND history_sample.is_missing = 0
                        THEN history_sample.value_json
                    ELSE excluded.value_json
                END,
                is_missing = CASE
                    WHEN excluded.is_missing = 1 AND history_sample.is_missing = 0
                        THEN history_sample.is_missing
                    ELSE excluded.is_missing
                END,
                source = CASE
                    WHEN excluded.is_missing = 1 AND history_sample.is_missing = 0
                        THEN history_sample.source
                    ELSE excluded.source
                END,
                retrieved_utc = excluded.retrieved_utc;
            """;

        var deviceId = command.Parameters.Add("$deviceId", SqliteType.Text);
        var attributeKey = command.Parameters.Add("$attributeKey", SqliteType.Text);
        var recordedAtUtc = command.Parameters.Add("$recordedAtUtc", SqliteType.Text);
        var valueJson = command.Parameters.Add("$valueJson", SqliteType.Text);
        var isMissing = command.Parameters.Add("$isMissing", SqliteType.Integer);
        var source = command.Parameters.Add("$source", SqliteType.Text);
        var retrievedUtc = command.Parameters.Add("$retrievedUtc", SqliteType.Text);

        command.Prepare();

        var count = 0;
        foreach (var sample in samples)
        {
            deviceId.Value = sample.DeviceId;
            attributeKey.Value = sample.AttributeKey;
            recordedAtUtc.Value = sample.RecordedAtUtc.ToUniversalTime().ToString("O");
            valueJson.Value = sample.ValueJson is null ? DBNull.Value : sample.ValueJson;
            isMissing.Value = sample.IsMissing ? 1 : 0;
            source.Value = sample.Source;
            retrievedUtc.Value = sample.RetrievedUtc.ToUniversalTime().ToString("O");
            count += command.ExecuteNonQuery();
        }

        transaction.Commit();
        return count;
    }

    public void SaveDayStatus(HistoryDayStatus status)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO history_day_status (
                device_id,
                local_date,
                timezone,
                source,
                status,
                frame_count,
                page_count,
                first_at_utc,
                last_at_utc,
                detail_json,
                updated_utc
            )
            VALUES (
                $deviceId,
                $localDate,
                $timezone,
                $source,
                $status,
                $frameCount,
                $pageCount,
                $firstAtUtc,
                $lastAtUtc,
                $detailJson,
                $updatedUtc
            )
            ON CONFLICT(device_id, local_date) DO UPDATE SET
                timezone = excluded.timezone,
                source = excluded.source,
                status = excluded.status,
                frame_count = excluded.frame_count,
                page_count = excluded.page_count,
                first_at_utc = excluded.first_at_utc,
                last_at_utc = excluded.last_at_utc,
                detail_json = excluded.detail_json,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$deviceId", status.DeviceId);
        command.Parameters.AddWithValue("$localDate", status.LocalDate.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$timezone", status.TimeZone);
        command.Parameters.AddWithValue("$source", status.Source);
        command.Parameters.AddWithValue("$status", status.Status);
        command.Parameters.AddWithValue("$frameCount", status.FrameCount);
        command.Parameters.AddWithValue("$pageCount", status.PageCount);
        command.Parameters.AddWithValue("$firstAtUtc", status.FirstAtUtc?.ToUniversalTime().ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$lastAtUtc", status.LastAtUtc?.ToUniversalTime().ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$detailJson", status.DetailJson ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$updatedUtc", status.UpdatedUtc.ToUniversalTime().ToString("O"));
        command.ExecuteNonQuery();
    }

    public void CaptureRaw(
        string operation,
        string? deviceId,
        DateOnly? localDate,
        string source,
        int? page,
        string? requestJson,
        string responseJson,
        DateTimeOffset retrievedUtc)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO raw_api_capture (
                operation,
                device_id,
                local_date,
                source,
                page,
                request_json,
                response_json,
                retrieved_utc
            )
            VALUES (
                $operation,
                $deviceId,
                $localDate,
                $source,
                $page,
                $requestJson,
                $responseJson,
                $retrievedUtc
            );
            """;
        command.Parameters.AddWithValue("$operation", operation);
        command.Parameters.AddWithValue("$deviceId", deviceId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$localDate", localDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$page", page ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$requestJson", requestJson ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$responseJson", responseJson);
        command.Parameters.AddWithValue("$retrievedUtc", retrievedUtc.ToUniversalTime().ToString("O"));
        command.ExecuteNonQuery();
    }

    public long StartSyncRun(string detailJson)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO sync_run(started_utc, status, detail)
            VALUES ($startedUtc, 'RUNNING', $detail);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$startedUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$detail", detailJson);
        return Convert.ToInt64(command.ExecuteScalar());
    }

    public void CompleteSyncRun(long syncRunId, string status, string detailJson)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE sync_run
            SET completed_utc = $completedUtc,
                status = $status,
                detail = $detail
            WHERE sync_run_id = $syncRunId;
            """;
        command.Parameters.AddWithValue("$completedUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$detail", detailJson);
        command.Parameters.AddWithValue("$syncRunId", syncRunId);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<DateOnly> GetPartialDates(string deviceId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT local_date
            FROM history_day_status
            WHERE device_id = $deviceId
              AND status = 'PARTIAL'
            ORDER BY local_date;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);

        using var reader = command.ExecuteReader();
        var result = new List<DateOnly>();

        while (reader.Read())
        {
            if (DateOnly.TryParse(reader.GetString(0), out var date))
            {
                result.Add(date);
            }
        }

        return result;
    }

    public DateTimeOffset? GetNewestTimestamp(string deviceId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT MAX(recorded_at_utc)
            FROM history_sample
            WHERE device_id = $deviceId;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);
        var raw = command.ExecuteScalar()?.ToString();
        return DateTimeOffset.TryParse(raw, out var parsed) ? parsed : null;
    }

    public DateTimeOffset? GetLastSuccessfulSyncUtc()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT completed_utc
            FROM sync_run
            WHERE status IN ('SUCCESS', 'PARTIAL')
              AND completed_utc IS NOT NULL
            ORDER BY sync_run_id DESC
            LIMIT 1;
            """;
        var raw = command.ExecuteScalar()?.ToString();
        return DateTimeOffset.TryParse(raw, out var parsed) ? parsed : null;
    }

    public int GetSampleCount(string deviceId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM history_sample
            WHERE device_id = $deviceId;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
