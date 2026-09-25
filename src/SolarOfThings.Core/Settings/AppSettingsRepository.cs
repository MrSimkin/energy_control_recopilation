using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Settings;

public sealed class AppSettingsRepository
{
    private readonly SqliteDatabase _database;

    public AppSettingsRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public string? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_setting WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);

        return command.ExecuteScalar() as string;
    }

    public void Set(string key, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO app_setting(key, value, updated_utc)
            VALUES ($key, $value, $updatedUtc)
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void Delete(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM app_setting WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        command.ExecuteNonQuery();
    }
}
