using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.Core.Data;

public sealed class SqliteDatabase
{
    public const int CurrentSchemaVersion = 15;

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
            current = 5;
        }

        if (current < 6)
        {
            ApplyMigration6(connection);
            current = 6;
        }

        if (current < 7)
        {
            ApplyMigration7(connection);
            current = 7;
        }

        if (current < 8)
        {
            ApplyMigration8(connection);
            current = 8;
        }

        if (current < 9)
        {
            ApplyMigration9(connection);
            current = 9;
        }

        if (current < 10)
        {
            ApplyMigration10(connection);
            current = 10;
        }

        if (current < 11)
        {
            ApplyMigration11(connection);
            current = 11;
        }

        if (current < 12)
        {
            ApplyMigration12(connection);
            current = 12;
        }

        if (current < 13)
        {
            ApplyMigration13(connection);
            current = 13;
        }

        if (current < 14)
        {
            ApplyMigration14(connection);
            current = 14;
        }

        if (current < 15)
        {
            ApplyMigration15(connection);
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

    private static void ApplyMigration6(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(connection, """
            CREATE TABLE normalized_metric_sample (
                device_id TEXT NOT NULL,
                metric_key TEXT NOT NULL,
                recorded_at_utc TEXT NOT NULL,
                normalized_value REAL NULL,
                normalized_unit TEXT NOT NULL,
                source_attribute_key TEXT NOT NULL,
                source_value_json TEXT NULL,
                normalization_rule_version TEXT NOT NULL,
                confidence TEXT NOT NULL,
                quality TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                PRIMARY KEY(device_id, metric_key, recorded_at_utc)
            );

            CREATE INDEX ix_normalized_metric_device_metric_time
                ON normalized_metric_sample(device_id, metric_key, recorded_at_utc DESC);

            CREATE TABLE normalization_run (
                normalization_run_id INTEGER PRIMARY KEY AUTOINCREMENT,
                device_id TEXT NOT NULL,
                rule_version TEXT NOT NULL,
                started_utc TEXT NOT NULL,
                completed_utc TEXT NULL,
                status TEXT NOT NULL,
                input_frame_count INTEGER NOT NULL DEFAULT 0,
                output_metric_count INTEGER NOT NULL DEFAULT 0,
                detail_json TEXT NULL
            );

            CREATE INDEX ix_normalization_run_device_started
                ON normalization_run(device_id, started_utc DESC);
            """, transaction);

        RecordMigration(
            connection,
            transaction,
            6,
            "Phase 4 versioned normalized metric layer and normalization audit.");

        transaction.Commit();
    }

    private static void ApplyMigration7(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(connection, """
            CREATE TABLE installation_config_check (
                device_id TEXT NOT NULL,
                check_key TEXT NOT NULL,
                source_attribute_key TEXT NOT NULL,
                status TEXT NOT NULL,
                observed_at_utc TEXT NULL,
                observed_value_json TEXT NULL,
                expected_display TEXT NOT NULL,
                detail TEXT NOT NULL,
                evaluated_utc TEXT NOT NULL,
                PRIMARY KEY(device_id, check_key)
            );

            CREATE INDEX ix_installation_config_check_device_status
                ON installation_config_check(device_id, status);
            """, transaction);

        RecordMigration(
            connection,
            transaction,
            7,
            "Phase 4 read-only installation behavior contract health snapshot.");

        transaction.Commit();
    }

    private static void ApplyMigration8(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(connection, """
            CREATE TABLE household_behavior_sample (
                device_id TEXT NOT NULL,
                recorded_at_utc TEXT NOT NULL,
                context_version TEXT NOT NULL,
                state_key TEXT NOT NULL,
                confidence TEXT NOT NULL,
                evidence_json TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                PRIMARY KEY(device_id, recorded_at_utc, context_version)
            );

            CREATE INDEX ix_household_behavior_device_time
                ON household_behavior_sample(device_id, recorded_at_utc DESC);

            CREATE INDEX ix_household_behavior_device_state
                ON household_behavior_sample(device_id, state_key, recorded_at_utc);
            """, transaction);

        RecordMigration(
            connection,
            transaction,
            8,
            "Phase 4/5 contextual household behavior samples derived from normalized telemetry.");

        transaction.Commit();
    }

    private static void ApplyMigration9(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(connection, """
            CREATE TABLE utility_meter_reading (
                reading_id INTEGER PRIMARY KEY AUTOINCREMENT,
                reading_at_utc TEXT NOT NULL,
                reading_kwh REAL NOT NULL,
                reference TEXT NULL,
                notes TEXT NULL,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );

            CREATE UNIQUE INDEX ux_utility_meter_reading_time
                ON utility_meter_reading(reading_at_utc);

            CREATE INDEX ix_utility_meter_reading_time
                ON utility_meter_reading(reading_at_utc DESC);

            CREATE TABLE utility_bill (
                bill_id INTEGER PRIMARY KEY AUTOINCREMENT,
                period_start_utc TEXT NOT NULL,
                period_end_utc TEXT NOT NULL,
                billed_consumption_kwh REAL NULL,
                amount_clp REAL NULL,
                invoice_reference TEXT NULL,
                notes TEXT NULL,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );

            CREATE INDEX ix_utility_bill_period
                ON utility_bill(period_start_utc DESC, period_end_utc DESC);
            """, transaction);

        RecordMigration(
            connection,
            transaction,
            9,
            "Phase 8 utility meter readings, optional bill records and reconciliation foundation.");

        transaction.Commit();
    }

    private static void ApplyMigration10(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(connection, """
            ALTER TABLE utility_meter_reading ADD COLUMN source_kind TEXT NOT NULL DEFAULT 'UNSPECIFIED';
            ALTER TABLE utility_meter_reading ADD COLUMN time_precision TEXT NOT NULL DEFAULT 'EXACT';
            ALTER TABLE utility_meter_reading ADD COLUMN time_assumption TEXT NOT NULL DEFAULT 'EXACT';

            UPDATE utility_meter_reading
            SET source_kind = CASE
                    WHEN lower(COALESCE(reference,'')) LIKE '%enel%' THEN 'UTILITY_OFFICIAL'
                    WHEN lower(COALESCE(reference,'')) LIKE '%propia%'
                      OR lower(COALESCE(reference,'')) LIKE '%personal%' THEN 'PERSONAL'
                    ELSE 'UNSPECIFIED'
                END,
                time_precision = CASE
                    WHEN lower(COALESCE(reference,'')) LIKE '%enel%' THEN 'DATE_ONLY'
                    ELSE 'EXACT'
                END,
                time_assumption = CASE
                    WHEN lower(COALESCE(reference,'')) LIKE '%enel%' THEN 'START_OF_DAY_ASSUMED'
                    ELSE 'EXACT'
                END;

            ALTER TABLE utility_bill ADD COLUMN from_reading_id INTEGER NULL;
            ALTER TABLE utility_bill ADD COLUMN to_reading_id INTEGER NULL;
            ALTER TABLE utility_bill ADD COLUMN meter_start_kwh REAL NULL;
            ALTER TABLE utility_bill ADD COLUMN meter_end_kwh REAL NULL;
            ALTER TABLE utility_bill ADD COLUMN tariff_plan TEXT NULL;
            ALTER TABLE utility_bill ADD COLUMN taxable_amount_clp REAL NULL;
            ALTER TABLE utility_bill ADD COLUMN iva_clp REAL NULL;
            ALTER TABLE utility_bill ADD COLUMN exempt_amount_clp REAL NULL;
            ALTER TABLE utility_bill ADD COLUMN gross_bill_amount_clp REAL NULL;
            ALTER TABLE utility_bill ADD COLUMN other_charges_clp REAL NULL;
            ALTER TABLE utility_bill ADD COLUMN total_due_clp REAL NULL;
            ALTER TABLE utility_bill ADD COLUMN period_precision TEXT NOT NULL DEFAULT 'EXACT';

            CREATE TABLE utility_bill_line (
                bill_line_id INTEGER PRIMARY KEY AUTOINCREMENT,
                bill_id INTEGER NOT NULL,
                section_key TEXT NOT NULL,
                category_key TEXT NULL,
                description TEXT NOT NULL,
                quantity REAL NULL,
                unit TEXT NULL,
                unit_rate_clp REAL NULL,
                amount_clp REAL NOT NULL,
                tax_treatment TEXT NULL,
                sort_order INTEGER NOT NULL DEFAULT 0,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                FOREIGN KEY(bill_id) REFERENCES utility_bill(bill_id) ON DELETE CASCADE
            );

            CREATE INDEX ix_utility_bill_line_bill
                ON utility_bill_line(bill_id, sort_order, bill_line_id);
            """, transaction);

        RecordMigration(
            connection,
            transaction,
            10,
            "Phase 8 reading provenance/time precision, arbitrary bill linkage and flexible bill line items.");

        transaction.Commit();
    }

    private static void ApplyMigration11(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(connection, """
            CREATE TABLE tariff_publication (
                publication_id INTEGER PRIMARY KEY AUTOINCREMENT,
                provider TEXT NOT NULL,
                category TEXT NOT NULL,
                title TEXT NOT NULL,
                source_url TEXT NOT NULL UNIQUE,
                effective_from TEXT NULL,
                is_retroactive INTEGER NOT NULL DEFAULT 0,
                local_pdf_path TEXT NULL,
                content_sha256 TEXT NULL,
                content_length INTEGER NULL,
                page_count INTEGER NULL,
                capture_status TEXT NOT NULL,
                captured_utc TEXT NULL,
                updated_utc TEXT NOT NULL
            );

            CREATE INDEX ix_tariff_publication_effective
                ON tariff_publication(provider, category, effective_from DESC);

            CREATE TABLE tariff_publication_page_text (
                publication_id INTEGER NOT NULL,
                page_number INTEGER NOT NULL,
                page_text TEXT NOT NULL,
                PRIMARY KEY(publication_id, page_number),
                FOREIGN KEY(publication_id)
                    REFERENCES tariff_publication(publication_id)
                    ON DELETE CASCADE
            );
            """, transaction);

        RecordMigration(
            connection,
            transaction,
            11,
            "Phase 9 official tariff publication capture with source PDF hash and extracted page text.");

        transaction.Commit();
    }

    private static void ApplyMigration12(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(connection, """
            ALTER TABLE tariff_publication
                ADD COLUMN normalization_status TEXT NOT NULL DEFAULT 'NOT_NORMALIZED';
            ALTER TABLE tariff_publication
                ADD COLUMN normalization_parser_version TEXT NULL;
            ALTER TABLE tariff_publication
                ADD COLUMN normalized_utc TEXT NULL;

            CREATE TABLE tariff_rate_candidate (
                rate_candidate_id INTEGER PRIMARY KEY AUTOINCREMENT,
                publication_id INTEGER NOT NULL,
                page_number INTEGER NOT NULL,
                tariff_plan TEXT NOT NULL,
                component_key TEXT NOT NULL,
                printed_description TEXT NOT NULL,
                unit TEXT NULL,
                network_type TEXT NULL,
                etr_band TEXT NULL,
                candidate_index INTEGER NOT NULL,
                net_rate_clp REAL NULL,
                published_iva_column_clp REAL NULL,
                source_text TEXT NOT NULL,
                parser_version TEXT NOT NULL,
                validation_state TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                FOREIGN KEY(publication_id)
                    REFERENCES tariff_publication(publication_id)
                    ON DELETE CASCADE
            );

            CREATE INDEX ix_tariff_rate_candidate_publication
                ON tariff_rate_candidate(publication_id, page_number, component_key);

            CREATE INDEX ix_tariff_rate_candidate_lookup
                ON tariff_rate_candidate(
                    tariff_plan,
                    component_key,
                    network_type,
                    etr_band,
                    publication_id
                );
            """, transaction);

        RecordMigration(
            connection,
            transaction,
            12,
            "Phase 9 normalized tariff candidate evidence without service applicability claims.");

        transaction.Commit();
    }

    private static void ApplyMigration13(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(connection, """
            DROP INDEX IF EXISTS ux_utility_meter_reading_time;

            CREATE INDEX IF NOT EXISTS ix_utility_meter_reading_source_time
                ON utility_meter_reading(source_kind, reading_at_utc DESC);
            """, transaction);

        RecordMigration(
            connection,
            transaction,
            13,
            "Allow distinct utility-reading evidence sources to share the same timestamp; identity remains reading_id/source based.");

        transaction.Commit();
    }

    private static void ApplyMigration14(
        SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(connection, """
            ALTER TABLE tariff_publication
                ADD COLUMN official_document_number TEXT NULL;
            ALTER TABLE tariff_publication
                ADD COLUMN official_publication_date TEXT NULL;
            ALTER TABLE tariff_publication
                ADD COLUMN corrects_official_document_number TEXT NULL;
            ALTER TABLE tariff_publication
                ADD COLUMN regulatory_metadata_source TEXT NULL;

            CREATE INDEX ix_tariff_publication_official_document
                ON tariff_publication(
                    provider,
                    category,
                    official_document_number
                );

            CREATE TABLE tariff_publication_relation (
                relation_id INTEGER PRIMARY KEY AUTOINCREMENT,
                source_publication_id INTEGER NOT NULL,
                relation_type TEXT NOT NULL,
                target_provider TEXT NOT NULL,
                target_category TEXT NOT NULL,
                target_official_document_number TEXT NOT NULL,
                target_publication_id INTEGER NULL,
                evidence_source_url TEXT NULL,
                evidence_text TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                FOREIGN KEY(source_publication_id)
                    REFERENCES tariff_publication(publication_id)
                    ON DELETE CASCADE,
                FOREIGN KEY(target_publication_id)
                    REFERENCES tariff_publication(publication_id)
                    ON DELETE SET NULL,
                UNIQUE(
                    source_publication_id,
                    relation_type,
                    target_provider,
                    target_category,
                    target_official_document_number
                )
            );

            CREATE INDEX ix_tariff_publication_relation_target
                ON tariff_publication_relation(
                    target_provider,
                    target_category,
                    target_official_document_number
                );
            """, transaction);

        RecordMigration(
            connection,
            transaction,
            14,
            "Official tariff document identity and explicit correction/supersession relation graph.");

        transaction.Commit();
    }

    private static void ApplyMigration15(
        SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        Execute(connection, """
            CREATE TABLE utility_bill_document (
                document_id INTEGER PRIMARY KEY AUTOINCREMENT,
                provider TEXT NOT NULL,
                original_file_name TEXT NOT NULL,
                local_pdf_path TEXT NOT NULL,
                content_sha256 TEXT NOT NULL UNIQUE,
                content_length INTEGER NOT NULL,
                page_count INTEGER NOT NULL,
                parser_version TEXT NOT NULL,
                extracted_text TEXT NULL,
                imported_utc TEXT NOT NULL
            );

            CREATE INDEX ix_utility_bill_document_sha
                ON utility_bill_document(content_sha256);

            CREATE TABLE utility_bill_field_evidence (
                evidence_id INTEGER PRIMARY KEY AUTOINCREMENT,
                bill_id INTEGER NOT NULL,
                field_key TEXT NOT NULL,
                source_kind TEXT NOT NULL,
                evidence_state TEXT NOT NULL,
                printed_value_text TEXT NULL,
                normalized_value_text TEXT NULL,
                source_page INTEGER NULL,
                source_text TEXT NULL,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                FOREIGN KEY(bill_id) REFERENCES utility_bill(bill_id)
                    ON DELETE CASCADE,
                UNIQUE(bill_id, field_key)
            );

            ALTER TABLE utility_bill
                ADD COLUMN source_kind TEXT NOT NULL DEFAULT 'LEGACY_MANUAL';
            ALTER TABLE utility_bill
                ADD COLUMN source_document_id INTEGER NULL;
            ALTER TABLE utility_bill
                ADD COLUMN review_state TEXT NOT NULL DEFAULT 'LEGACY_UNREVIEWED';
            ALTER TABLE utility_bill
                ADD COLUMN iva_rate REAL NULL;

            ALTER TABLE utility_bill_line
                ADD COLUMN source_kind TEXT NOT NULL DEFAULT 'LEGACY_MANUAL';
            ALTER TABLE utility_bill_line
                ADD COLUMN evidence_state TEXT NOT NULL DEFAULT 'LEGACY_UNREVIEWED';
            ALTER TABLE utility_bill_line
                ADD COLUMN source_page INTEGER NULL;
            ALTER TABLE utility_bill_line
                ADD COLUMN source_text TEXT NULL;
            """, transaction);

        RecordMigration(
            connection,
            transaction,
            15,
            "Phase 10 bill-ingestion v2: source documents, field provenance, review state, VAT rate and line evidence.");
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
