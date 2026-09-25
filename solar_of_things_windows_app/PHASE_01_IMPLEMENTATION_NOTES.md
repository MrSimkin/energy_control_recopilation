# Phase 1 — Technical Skeleton / Proof of Architecture

Date started: 2026-09-24

Status: REFINED BUILD CI PASS — FINAL USER REVALIDATION PENDING

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
- Database schema starter documentation: implemented.
- Formal CI validation receipt: implemented (`PHASE_01_VALIDATION_RECEIPT.md`).
- Remaining: manual installed/portable UI/path behavior check on the user's actual Windows 11 x64 laptop.

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


## Latest validation authority

Use `PHASE_01_VALIDATION_RECEIPT.md` as the latest formal CI validation record.

Latest successful validation run: `36085763308`, source commit `778abaab896d6e211fdc658aa56e80bb6947e7bd`.

The only unresolved Phase 1 acceptance item at pause is the manual Windows 11 launch/visual/portable-path checkpoint.


## User Windows 11 manual checkpoint — 2026-09-25

The original Phase 1 portable artifact was tested on the user's actual Windows 11 x64 machine.

Observed:
- application launched normally;
- a normal launch did **not** request UAC/elevation;
- clean shutdown: PASS;
- shell rendering: PASS;
- `Data\\energy.db`: created;
- `Backups\\`: created;
- `Logs\\`: created;
- JSONL diagnostics file: created;
- database path: portable `...\\Data\\energy.db`;
- first-run SmartScreen unknown-publisher warning: expected for unsigned development artifact.

The user initially invoked one run as Administrator manually; that UAC prompt was therefore user-initiated, not an application requirement. A later normal launch confirmed no UAC requirement.

## Approved requirement change / Phase 1 refinement — 2026-09-25

The previous English-only UI requirement was correct historical documentation of the earlier user instruction. The user has now changed the requirement:

- Spanish is the default UI language;
- English must be selectable;
- language preference must persist;
- current shell text must be localized;
- sidebar placeholders must visibly navigate so the shell itself can be validated;
- no Phase 2 authentication/telemetry work is authorized as part of this refinement;
- current product name is provisional; final product name = TBD.

After CI passes for this refinement, Phase 1 remains pending one final user revalidation of the refined portable build.


## Refined build CI receipt — 2026-09-25

Implementation commit:
`ee8e8200f132be9aef42875c6ae92eb1f10adc66`

GitHub Actions run:
`36171030035`

Conclusion:
**SUCCESS**

Fresh portable artifact:
- `SolarEnergyMonitor-win-x64-dev`
- artifact ID `10881030047`
- SHA-256 `4389db735ab205698c014bc97e505191b0601ad2075988f926ef441642281cde`

The refined shell now contains:
- reusable resource-dictionary localization;
- Spanish default;
- English selector;
- persisted language preference through `app_setting`;
- localized current shell and Phase 1 informational dialog;
- distinct functional placeholder navigation for every sidebar section;
- no fabricated telemetry.

Phase 1 is waiting only for final user revalidation of this artifact.
