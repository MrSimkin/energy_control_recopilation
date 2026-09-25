using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.Core.Data;

public sealed class SqliteDatabase
{
    public const int CurrentSchemaVersion = 5;

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
            current = 1;
        }

        if (current < 2)
        {
            ApplyMigration2(connection);
            current = 2;
        }

        if (current < 3)
        {
            ApplyMigration3(connection);
            current = 3;
        }

        if (current < 4)
        {
            ApplyMigration4(connection);
            current = 4;
        }

        if (current < 5)
        {
            ApplyMigration5(connection);
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

    public SqliteConnection OpenConnection()
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

        RecordMigration(
            connection,
            transaction,
            1,
            "Initial application metadata and synchronization tables.");

        transaction.Commit();
    }

    private static void ApplyMigration2(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(connection, """
            CREATE TABLE commissioning_profile (
                profile_id INTEGER PRIMARY KEY CHECK(profile_id = 1),
                station_id TEXT NOT NULL,
                station_name TEXT NULL,
                station_timezone TEXT NULL,
                device_id TEXT NOT NULL,
                device_name TEXT NULL,
                serial_number TEXT NULL,
                model TEXT NULL,
                manufacturer TEXT NULL,
                dtu_id TEXT NULL,
                gather_protocol_number TEXT NULL,
                software_version TEXT NULL,
                data_source TEXT NULL,
                gather_attributes_status TEXT NOT NULL,
                gather_attribute_count INTEGER NOT NULL,
                latest_state_status TEXT NOT NULL,
                energy_flow_status TEXT NOT NULL,
                history_status TEXT NOT NULL,
                aggregate_status TEXT NOT NULL,
                alarm_status TEXT NOT NULL,
                capabilities_json TEXT NOT NULL,
                attribute_catalog_json TEXT NOT NULL,
                station_json TEXT NOT NULL,
                device_json TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );
            """, transaction);

        RecordMigration(
            connection,
            transaction,
            2,
            "Phase 2 read-only commissioning capability profile.");

        transaction.Commit();
    }

    private static void ApplyMigration3(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(connection, """
            ALTER TABLE commissioning_profile ADD COLUMN device_sort_key TEXT NULL;
            ALTER TABLE commissioning_profile ADD COLUMN device_type_number TEXT NULL;
            ALTER TABLE commissioning_profile ADD COLUMN rated_power REAL NULL;
            ALTER TABLE commissioning_profile ADD COLUMN is_online INTEGER NULL;
            ALTER TABLE commissioning_profile ADD COLUMN last_data_at TEXT NULL;
            """, transaction);

        RecordMigration(
            connection,
            transaction,
            3,
            "Promote device identity, rated power, online state and source freshness metadata.");

        transaction.Commit();
    }

    private static void ApplyMigration4(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(connection, """
            CREATE TABLE history_sample (
                device_id TEXT NOT NULL,
                attribute_key TEXT NOT NULL,
                recorded_at_utc TEXT NOT NULL,
                value_json TEXT NULL,
                is_missing INTEGER NOT NULL DEFAULT 0,
                source TEXT NOT NULL,
                retrieved_utc TEXT NOT NULL,
                PRIMARY KEY(device_id, attribute_key, recorded_at_utc)
            );

            CREATE INDEX ix_history_sample_device_time
                ON history_sample(device_id, recorded_at_utc);

            CREATE TABLE history_day_status (
                device_id TEXT NOT NULL,
                local_date TEXT NOT NULL,
                timezone TEXT NOT NULL,
                source TEXT NOT NULL,
                status TEXT NOT NULL,
                frame_count INTEGER NOT NULL,
                page_count INTEGER NOT NULL,
                first_at_utc TEXT NULL,
                last_at_utc TEXT NULL,
                detail_json TEXT NULL,
                updated_utc TEXT NOT NULL,
                PRIMARY KEY(device_id, local_date)
            );

            CREATE TABLE raw_api_capture (
                capture_id INTEGER PRIMARY KEY AUTOINCREMENT,
                operation TEXT NOT NULL,
                device_id TEXT NULL,
                local_date TEXT NULL,
                source TEXT NOT NULL,
                page INTEGER NULL,
                request_json TEXT NULL,
                response_json TEXT NOT NULL,
                retrieved_utc TEXT NOT NULL
            );

            CREATE INDEX ix_raw_api_capture_device_date
                ON raw_api_capture(device_id, local_date, operation);
            """, transaction);

        RecordMigration(
            connection,
            transaction,
            4,
            "Phase 3 raw history corpus, daily completeness and raw API capture.");

        transaction.Commit();
    }

    private static void ApplyMigration5(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(connection, """
            ALTER TABLE history_day_status
                ADD COLUMN retry_count INTEGER NOT NULL DEFAULT 0;
            """, transaction);

        RecordMigration(
            connection,
            transaction,
            5,
            "Track bounded automatic retries for unresolved historical days.");

        transaction.Commit();
    }

    private static void RecordMigration(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int version,
        string description)
    {
        using var migration = connection.CreateCommand();
        migration.Transaction = transaction;
        migration.CommandText = """
            INSERT INTO schema_migration(version, applied_utc, description)
            VALUES ($version, $appliedUtc, $description);
            """;
        migration.Parameters.AddWithValue("$version", version);
        migration.Parameters.AddWithValue("$appliedUtc", DateTimeOffset.UtcNow.ToString("O"));
        migration.Parameters.AddWithValue("$description", description);
        migration.ExecuteNonQuery();
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
