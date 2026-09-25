# Solar of Things Windows App — Continuity / Resume Status

Date: 2026-09-24
Status: PAUSED BY USER — SAFE RESUME POINT RECORDED

This file is the canonical continuity note for resuming the project after the user-requested pause.

## Repository / branch

- Repository: `MrSimkin/energy_control_recopilation`
- Canonical branch: `main`
- State validated immediately before this consolidation against commit:
  `b8f3ced4e9f60784c9c2fa1b9ab020600e732c60`
- That commit records: **Phase 1 CI validation and completed infrastructure**.

Any commits after that point made solely for this pause/consolidation are documentation/status commits and do not advance implementation.

## Completed before pause

### Public/API research

The Solar of Things / SiSeLi cloud research under:

`solar_of_things_api_research/`

is complete for the current project scope through Round 14.

Key outcome:
- cloud/API path sufficiently reconstructed;
- broad public research closed;
- target account/device-specific validation deferred to the future local app commissioning flow.

### Product requirements

Requirements and clarification records 01–05 are stored under:

`solar_of_things_windows_app/`

They cover:
- Windows 11 x64;
- C#/.NET/WPF direction;
- AdminLTE-like compact dashboard UI;
- SQLite/SQL accessibility;
- raw / normalized / calculated layers;
- on-demand Update Data synchronization;
- historical backfill;
- Excel + PDF exports;
- utility meter readings;
- Chile tariff acquisition and bill estimation/reconciliation;
- battery details;
- reporting, presets, glossary/tooltips;
- installer + portable;
- backup/security/update policies.

### Functional specification

`PRODUCT_FUNCTIONAL_SPEC_V1.md`

Status:
**CANONICAL V1 — APPROVED / FROZEN**

User approved the specification on 2026-09-24.

Phase 0 is complete.

### Development roadmap

`DEVELOPMENT_ROADMAP_V1.md`

Status:
**ACTIVE DEVELOPMENT ROADMAP**

Phase 0 is complete.

## Phase 1 state at pause

Phase 1 has **started** and is **partially complete**.

Canonical implementation note:

`PHASE_01_IMPLEMENTATION_NOTES.md`

Current Phase 1 status:
**CORE ARCHITECTURE VALIDATED**

Implemented/validated:

- .NET 10 / C# solution structure;
- WPF Windows desktop app targeting x64;
- separate Core library;
- SQLite via Microsoft.Data.Sqlite;
- SQLite WAL mode;
- schema migration table / schema v1 starter;
- non-secret SQL-backed application settings;
- deterministic data-path abstraction;
- portable-mode marker support;
- installed-mode data-root logic;
- dependency injection / host foundation;
- structured local JSONL diagnostics;
- Windows DPAPI protected secret-store abstraction;
- AdminLTE-inspired compact desktop shell;
- Update Data placeholder only;
- smoke-test console project;
- GitHub Actions Windows build/smoke workflow;
- database schema starter documentation.

CI proof:

GitHub Actions Windows run:

`36085579691`

passed.

Validated by CI:
- .NET 10 restore;
- Release build;
- WPF compilation;
- SQLite DB creation;
- migration to schema v1;
- settings round-trip;
- JSONL diagnostics;
- DPAPI secret save/read/delete;
- clean smoke-test DB teardown.

Source structure currently present:

- `SolarOfThings.sln`
- `src/SolarOfThings.App/`
- `src/SolarOfThings.Core/`
- `tools/SolarOfThings.SmokeTest/`
- `.github/workflows/windows-build.yml`

Supporting project files:
- `global.json`
- `Directory.Build.props`
- `Directory.Packages.props`
- `.gitignore`

## Phase 1 work still pending

Do **not** repeat already validated architecture work unless evidence shows it is broken.

Remaining Phase 1 tasks:

1. confirm installed/portable folder behavior on an actual Windows 11 x64 machine;
2. perform manual UI/portable artifact inspection as needed;
3. finalize the Phase 1 acceptance receipt;
4. only then move to Phase 2.

## What has NOT started

Phase 2 has **not** started.

In particular there is no implemented production Solar of Things:

- login/authentication;
- token/session handling;
- station discovery;
- device discovery;
- telemetry download;
- historical backfill;
- target-device commissioning.

The UI must continue to avoid fabricated energy values until real Solar of Things data is available.

## Resume instruction

When the project resumes:

1. read this file;
2. read `PRODUCT_FUNCTIONAL_SPEC_V1.md`;
3. read the Phase 1 section of `DEVELOPMENT_ROADMAP_V1.md`;
4. read `PHASE_01_IMPLEMENTATION_NOTES.md`;
5. inspect current `main` only as needed to verify no external changes;
6. continue from the **remaining Phase 1 validation/acceptance tasks**;
7. do not restart Phase 0 or rebuild the already CI-validated Phase 1 foundation from scratch.

## Scope guard

Do not begin Phase 2 Solar of Things authentication/telemetry until Phase 1 has a formal acceptance receipt.

Do not reopen completed API research unless a concrete implementation-time gap requires a narrow targeted investigation.

## Pause reason

The user explicitly requested that work stop and that all progress be consolidated into the repository so the project can later resume from repository state alone.
