# Solar Energy Monitor — Database Schema Starter

Date: 2026-09-24

Status: PHASE 1 STARTER — schema will expand in later phases

Database engine: SQLite

Default database filename:

`energy.db`

## Access policy

The database is intentionally accessible with ordinary SQLite clients.

Recommended external access while the app is running:

- read-only queries;
- use documented reporting views once they are introduced;
- do not directly modify application-owned tables.

SQLite WAL mode is enabled so the application can write while an external client performs normal reads.

Credentials, access tokens, refresh tokens, cookies and reusable signing secrets must **never** be stored in this database.

## Current schema version

`1`

## Tables

### schema_migration

Tracks database schema migrations.

| Column | Type | Meaning |
|---|---|---|
| version | INTEGER PK | Schema migration version |
| applied_utc | TEXT | ISO-8601 UTC time when migration was applied |
| description | TEXT | Human-readable migration description |

### app_setting

Stores non-secret application settings.

| Column | Type | Meaning |
|---|---|---|
| key | TEXT PK | Setting key |
| value | TEXT NULL | Setting value |
| updated_utc | TEXT | Last update time in UTC |

Do not use this table for passwords/tokens.

### sync_run

Starter audit table for future Solar of Things synchronization operations.

| Column | Type | Meaning |
|---|---|---|
| sync_run_id | INTEGER PK AUTOINCREMENT | Synchronization run ID |
| started_utc | TEXT | Start time |
| completed_utc | TEXT NULL | Completion time |
| status | TEXT | Run status |
| detail | TEXT NULL | Non-secret diagnostic detail |

Index:

`ix_sync_run_started_utc`

## Planned logical layers

Later migrations will implement the canonical specification layers:

1. Raw source data
2. Normalized telemetry
3. Calculated/reporting data
4. Device/station capability profile
5. Utility meter/bills
6. Tariff schedules/provenance
7. Saved report presets
8. Synchronization/data-quality audit

Stable human-facing SQL views will be introduced after the telemetry schema is validated against the real inverter.

## Reprocessing rule

Raw observations will remain authoritative source evidence.

Normalized and calculated layers may be rebuilt after interpretation/calculation rule changes without requiring a cloud redownload when the necessary raw evidence is already stored.
