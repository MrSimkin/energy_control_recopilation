using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Normalization;

public sealed class NormalizationRepository
{
    private readonly SqliteDatabase _database;

    public NormalizationRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public IReadOnlyDictionary<string, NormalizedMetricValue> GetLatestMetrics(string deviceId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            WITH ranked AS (
                SELECT
                    metric_key,
                    recorded_at_utc,
                    normalized_value,
                    normalized_unit,
                    source_attribute_key,
                    normalization_rule_version,
                    confidence,
                    quality,
                    ROW_NUMBER() OVER (
                        PARTITION BY metric_key
                        ORDER BY recorded_at_utc DESC
                    ) AS row_number
                FROM normalized_metric_sample
                WHERE device_id = $deviceId
                  AND normalized_value IS NOT NULL
                  AND confidence <> 'UNRESOLVED'
            )
            SELECT
                metric_key,
                recorded_at_utc,
                normalized_value,
                normalized_unit,
                source_attribute_key,
                normalization_rule_version,
                confidence,
                quality
            FROM ranked
            WHERE row_number = 1;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);

        using var reader = command.ExecuteReader();
        var result = new Dictionary<string, NormalizedMetricValue>(StringComparer.Ordinal);

        while (reader.Read())
        {
            if (!DateTimeOffset.TryParse(reader.GetString(1), out var recordedAt))
            {
                continue;
            }

            result[reader.GetString(0)] = new NormalizedMetricValue(
                reader.GetString(0),
                recordedAt,
                reader.GetDouble(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7));
        }

        return result;
    }

    public int GetNormalizedSampleCount(string deviceId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM normalized_metric_sample
            WHERE device_id = $deviceId;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
