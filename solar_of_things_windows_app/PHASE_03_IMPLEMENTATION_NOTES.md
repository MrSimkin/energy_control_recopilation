# Phase 3 — Raw Data Ingestion and Historical Backfill

Date started: 2026-09-25

Status: **IN PROGRESS**

## Canonical ingestion model

The trustworthy local corpus is built from Solar of Things raw history **one station-local calendar day at a time**.

Raw device telemetry is commonly around five-minute cadence, but it is not an exact five-minute protocol grid.

Therefore the collector must:

- keep the exact timestamps returned by Solar of Things;
- never synthesize missing 5-minute rows;
- preserve null/missing observations;
- preserve long gaps as gaps;
- use actual timestamp intervals later when integrating power to energy;
- never calculate energy as `sum(power) × 5 minutes` merely because cadence is usually near five minutes.

## Backfill windowing

For each local calendar day:

1. generate timezone-aware start/end boundaries from the station IANA timezone;
2. send explicit UTC offset on `fromTime` / `toTime`;
3. query selected-key raw history;
4. paginate with `count=300`;
5. stop on a short page or, when a positive `total` behaves as page count, when `page >= total`;
6. if selected-key history is refused or becomes partial on any page, query `deviceState/attribute/record/list` for that same day and merge/upsert;
7. persist raw API responses and normalized raw attribute samples;
8. record daily completeness evidence.

A high safety page cap prevents a malformed server reply from looping forever.

## Initial range

When the local corpus is empty, use the commissioned device's real `installedAt` metadata as the initial practical lower bound when available.

The current real device metadata contains a valid installation timestamp, so the upcoming first target-machine backfill will start from the actual installation date rather than an arbitrary retention guess.

If installation metadata is absent, the implementation currently uses a conservative fallback range; later live evidence can refine automatic oldest-available discovery.

## Local storage — schema v4

`history_sample`

Grain:
`device_id + attribute_key + recorded_at_utc`

Stores:
- raw JSON value;
- explicit missing flag;
- source endpoint;
- retrieval timestamp.

Upsert semantics:
- repeated downloads are idempotent;
- a later missing/null observation does not overwrite an already-known non-missing value at the same device/key/timestamp.

`history_day_status`

Stores:
- device;
- station-local date;
- timezone;
- source;
- COMPLETE / EMPTY / PARTIAL;
- real frame count;
- pages received;
- first/last real timestamp;
- detail JSON;
- update timestamp.

Detail evidence now includes:
- median actual timestamp gap;
- p90 gap;
- maximum gap;
- fallback/error state;
- upsert count.

No requirement for exactly 288 frames exists.

`raw_api_capture`

Preserves the raw historical request/response page evidence for later debugging/reconciliation.

`sync_run`

Audits synchronization start/end/status/detail.

## Update behavior

Fresh local corpus:
- full daily backfill from installation-date lower bound through today.

Existing corpus:
- restart from one local day before the newest stored timestamp to capture late/corrected cloud uploads;
- upsert idempotently.

The Core ingestion service supports progress reporting and cancellation.

The current desktop shell has `Actualizar datos` wired to the ingestion engine. Richer progress/cancel presentation can be refined after the first real multi-month backfill proves the cloud behavior.

## Current validation boundary

Automated Windows builds have already validated the schema-v4 application state before the most recent pagination/completeness refinements.

The next meaningful target-machine test should not be another isolated smoke pass.

It should verify in one run:

1. account/session connection;
2. timestamp-corrected Phase 2 history/alarm probes;
3. first real Phase 3 daily backfill;
4. pagination behavior;
5. raw response shape;
6. recovered date depth;
7. actual frame cadence/gaps;
8. SQLite persistence/idempotency;
9. subsequent incremental update.

Use the existing sanitized diagnostics plus the SQLite corpus/sync audit as development evidence.

Do not begin normalization/aggregation assumptions until the raw target-device corpus is proven.


## Backfill safety / user escape controls

The first real multi-month backfill must never depend on an unbounded loop or force the user to kill the process.

Implemented safety rules:

- visible progress bar during historical synchronization;
- explicit **Stop / Detener** control;
- cooperative cancellation through the ingestion cancellation token;
- already committed raw pages/samples remain in SQLite when stopped;
- a later Update Data resumes from the existing corpus and retries partial historical days rather than discarding prior work;
- minimum application-level request spacing: 500 ms;
- hard per-sync history request budget: 750 requests;
- HTTP 429 immediately stops history synchronization and is not automatically retried by the HTTP client;
- unresolved HTTP 401/403 stops history synchronization;
- repeated server-side 5xx responses trip a bounded circuit breaker;
- three consecutive PARTIAL historical days trip a safety stop;
- each endpoint also has a finite per-day page cap;
- all safety stops are recorded in diagnostics/sync audit.

These limits are intentionally conservative for the personal Solar of Things / SiSeLi account. The goal is complete recoverable data, not maximum request throughput.


## Capture start selection

The desktop UI exposes two explicit start modes:

### Automatic (recommended)

Automatic mode is bounded and evidence-based. It does **not** probe arbitrary old years.

On an empty local corpus:
- use the commissioned device's real `installedAt` date when available;
- if installation metadata is unavailable, use only the conservative fallback already defined by the ingestion engine.

On an existing local corpus:
- resume from one local day before the newest stored timestamp;
- also retry any historical days still marked `PARTIAL`.

The UI shows the calculated automatic start date before synchronization begins.

For the current real inverter, installation metadata places the automatic lower bound in April 2026, so automatic mode must not attempt dates such as 2020.

### Manual

Manual mode enables a date picker.

The chosen date becomes the lower bound for that synchronization without deleting or replacing already captured data.

Safety rules:
- a requested future date is clamped to the current station-local date;
- when `installedAt` is known, a requested date earlier than installation is clamped to the installation date;
- the same pagination, pacing, request-budget, circuit-breaker and safe-stop rules still apply.

The purpose of manual mode is user control, not bypassing the anti-loop safeguards.
