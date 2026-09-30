# Solar of Things — Research Exporter R1

Purpose:
- create the minimal, pseudonymized research package required for Phase 3 real-data statistical backtesting;
- include enough whitelisted telemetry/context to avoid a second extraction for Phase 4 when possible.

Safety:
- opens the selected SQLite database with `Mode=ReadOnly`;
- never writes to `energy.db`;
- never exports original device/station identifiers, serials, credentials, Enel bills, meter readings, tariff data, or raw API payloads;
- raw `LatestStateSnapshot` payloads may be read only to extract explicitly whitelisted operating-context fields; the raw payload is never written to the package.

Package:
- `normalized_metrics.csv`
- `mode_context.csv`
- `historical_soc_config.csv`
- `history_day_status.csv`
- `research_settings.json`
- `manifest.json`
- `README.txt`

UX follows the canonical long-operation pattern:
- progress under the action;
- `Paso n/4`;
- completion ping;
- exact output path.
