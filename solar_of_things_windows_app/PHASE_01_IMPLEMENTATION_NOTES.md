# Phase 1 — Technical Skeleton / Proof of Architecture

Date started: 2026-09-24

Status: IN PROGRESS

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

- Confirm GitHub Windows build passes.
- Decide/finalize logging provider and structured local log file design.
- Add explicit application configuration persistence.
- Add secure Windows credential-storage abstraction (without real Solar of Things login yet).
- Confirm installed/portable folder behavior on an actual Windows 11 x64 machine.
- Add schema/database documentation starter.
- Finalize Phase 1 acceptance receipt before Phase 2.

## Important scope rule

Phase 1 does not implement Solar of Things authentication or telemetry.

The UI intentionally contains no fabricated energy data. Values remain unavailable until Phase 2/3 real-device work.
