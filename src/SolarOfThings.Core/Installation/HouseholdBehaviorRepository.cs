using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Installation;

public sealed class HouseholdBehaviorRepository
{
    private readonly SqliteDatabase _database;

    public HouseholdBehaviorRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public bool HasContextVersion(string deviceId, string contextVersion)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(
                SELECT 1
                FROM household_behavior_sample
                WHERE device_id = $deviceId
                  AND context_version = $contextVersion
                LIMIT 1
            );
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);
        command.Parameters.AddWithValue("$contextVersion", contextVersion);
        return Convert.ToInt32(command.ExecuteScalar()) != 0;
    }

    public int GetSampleCount(string deviceId, string contextVersion)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM household_behavior_sample
            WHERE device_id = $deviceId
              AND context_version = $contextVersion;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);
        command.Parameters.AddWithValue("$contextVersion", contextVersion);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public IReadOnlyDictionary<string, int> GetStateCounts(
        string deviceId,
        string contextVersion)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT state_key, COUNT(*)
            FROM household_behavior_sample
            WHERE device_id = $deviceId
              AND context_version = $contextVersion
            GROUP BY state_key
            ORDER BY state_key;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);
        command.Parameters.AddWithValue("$contextVersion", contextVersion);

        using var reader = command.ExecuteReader();
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        while (reader.Read())
        {
            result[reader.GetString(0)] = reader.GetInt32(1);
        }

        return result;
    }
}
