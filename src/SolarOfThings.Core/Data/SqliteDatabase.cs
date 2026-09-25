using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.Core.Data;

public sealed class SqliteDatabase
{
    public const int CurrentSchemaVersion = 1;

    private readonly AppPaths _paths;

    public SqliteDatabase(AppPaths paths)
    {
        _paths = paths;
    }

    public string DatabasePath => _paths.DatabasePath;

    public void Initialize()
    {
        using var connection = OpenConnection();

        Execute(connection, "PRAGMA foreign_keys = ON;");
        Execute(connection, "PRAGMA journal_mode = WAL;");
        Execute(connection, "PRAGMA synchronous = NORMAL;");
        Execute(connection, "PRAGMA busy_timeout = 5000;");

        Execute(connection, """
            CREATE TABLE IF NOT EXISTS schema_migration (
                version INTEGER PRIMARY KEY,
                applied_utc TEXT NOT NULL,
                description TEXT NOT NULL
            );
            """);

        var current = GetSchemaVersion(connection);
        if (current < 1)
        {
            ApplyMigration1(connection);
        }

        var finalVersion = GetSchemaVersion(connection);
        if (finalVersion != CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported database schema version {finalVersion}; expected {CurrentSchemaVersion}.");
        }
    }

    public int GetSchemaVersion()
    {
        using var connection = OpenConnection();
        return GetSchemaVersion(connection);
    }

    private SqliteConnection OpenConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static int GetSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migration;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void ApplyMigration1(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(connection, """
            CREATE TABLE app_setting (
                key TEXT PRIMARY KEY,
                value TEXT NULL,
                updated_utc TEXT NOT NULL
            );

            CREATE TABLE sync_run (
                sync_run_id INTEGER PRIMARY KEY AUTOINCREMENT,
                started_utc TEXT NOT NULL,
                completed_utc TEXT NULL,
                status TEXT NOT NULL,
                detail TEXT NULL
            );

            CREATE INDEX ix_sync_run_started_utc
                ON sync_run(started_utc DESC);
            """, transaction);

        using var migration = connection.CreateCommand();
        migration.Transaction = transaction;
        migration.CommandText = """
            INSERT INTO schema_migration(version, applied_utc, description)
            VALUES ($version, $appliedUtc, $description);
            """;
        migration.Parameters.AddWithValue("$version", 1);
        migration.Parameters.AddWithValue("$appliedUtc", DateTimeOffset.UtcNow.ToString("O"));
        migration.Parameters.AddWithValue("$description", "Initial application metadata and synchronization tables.");
        migration.ExecuteNonQuery();

        transaction.Commit();
    }

    private static void Execute(
        SqliteConnection connection,
        string sql,
        SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
