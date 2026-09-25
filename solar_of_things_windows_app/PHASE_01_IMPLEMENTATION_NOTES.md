# Phase 1 — Technical Skeleton / Proof of Architecture

Date started: 2026-09-24

Status: PAUSED BY USER — CORE ARCHITECTURE VALIDATED

## Implemented in initial skeleton

- .NET 10 / C# repository structure.
- WPF Windows desktop application targeting x64.
- Core library separated from UI.
- SQLite via Microsoft.Data.Sqlite.
- SQLite WAL mode and initial schema-migration table.
- Initial application metadata/synchronization tables.
- Deterministic data-path abstraction.
- Portable-mode marker support.
- Installed-mode data root based on Windows common application data.
- Dependency-injection/host foundation.
- AdminLTE-inspired compact desktop dashboard shell.
- Update Data placeholder clearly marked as Phase 2 work.
- SQLite smoke-test console project.
- GitHub Actions Windows build + smoke test.

## Dependency versions

At implementation start, current stable .NET 10 package servicing versions were verified as:
- Microsoft.Data.Sqlite 10.0.12
- Microsoft.Extensions.Hosting 10.0.12

Versions are pinned centrally in Directory.Packages.props.

## Phase 1 items still to prove/complete

- GitHub Windows Release build: PASS (0 warnings, 0 errors).
- Structured local JSONL diagnostics foundation: implemented.
- SQL-backed non-secret application settings repository: implemented.
- Windows DPAPI protected secret-store abstraction: implemented and smoke-tested.
- Confirm installed/portable folder behavior on an actual Windows 11 x64 machine.
- Database schema starter documentation: implemented.
- Finalize Phase 1 acceptance receipt before Phase 2.

## Important scope rule

Phase 1 does not implement Solar of Things authentication or telemetry.

The UI intentionally contains no fabricated energy data. Values remain unavailable until Phase 2/3 real-device work.


## CI validation

GitHub Actions Windows run `36085579691` passed on the Phase 1 infrastructure code.

Validated:
- .NET 10 restore;
- Release build;
- WPF compilation;
- SQLite database creation;
- schema migration to v1;
- settings round-trip;
- JSONL diagnostics creation;
- Windows DPAPI secret save/read/delete;
- clean smoke-test database teardown.

A later workflow revision adds a self-contained win-x64 portable test artifact for manual Windows 11 UI inspection.


## Pause / resume marker

Work was paused by explicit user request on 2026-09-24.

Resume from the remaining Phase 1 acceptance tasks only. Do not restart the validated architecture foundation.

Canonical continuation note: `CONTINUITY_STATUS.md`.
