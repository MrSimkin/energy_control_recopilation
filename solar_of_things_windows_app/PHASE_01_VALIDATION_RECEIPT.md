# Phase 1 — Technical Skeleton Validation Receipt

Date: 2026-09-24

Status: REFINED BUILD CI PASS / FINAL WINDOWS 11 USER REVALIDATION PENDING

## Validated commit/run

GitHub Actions workflow:
- Name: Windows Build
- Run ID: 36085763308
- Source commit: 778abaab896d6e211fdc658aa56e80bb6947e7bd
- Conclusion: SUCCESS

## Automated checks passed

- .NET 10 SDK setup
- NuGet restore
- Windows x64 WPF Release build
- Build completed with no compilation failure
- SQLite database creation
- SQLite schema migration v1
- SQLite WAL-capable database foundation
- application-settings SQL round-trip
- structured JSONL diagnostics creation
- Windows DPAPI secret save/read/delete round-trip
- cleanup of pooled SQLite connections
- self-contained win-x64 publish
- portable-mode marker creation
- GitHub artifact upload

## Portable development artifact

Artifact:
- Name: SolarEnergyMonitor-win-x64-dev
- Artifact ID: 10843334570
- Approximate archive size: 66.7 MB
- Workflow run: 36085763308

This is a development/test artifact, not a v1 installer or release.

Expected portable behavior:
- executable and dependencies are contained in the extracted artifact;
- `portable.mode` causes writable data to remain with the portable copy;
- first launch should create local `Data\energy.db`, `Backups\`, and `Logs\` directories as needed;
- the UI intentionally contains no fabricated telemetry;
- Update Data currently explains that Solar of Things synchronization begins in Phase 2.

## Remaining Phase 1 manual checkpoint

A human launch on the user's actual Windows 11 x64 laptop should confirm:

1. application starts normally;
2. AdminLTE-inspired shell renders acceptably;
3. no unexpected Windows permission prompt is required;
4. `Data\energy.db` is created beside the portable app as designed;
5. database path shown in the status bar is correct;
6. the app closes cleanly.

This is the only Phase 1 acceptance item that CI cannot prove because GitHub Actions does not provide an interactive user desktop session.

## Decision

Repository/architecture proof is validated.

Phase 2 implementation may begin without changing Phase 1 architecture, but the manual UI checkpoint should be completed before treating the desktop shell as visually accepted.


## Manual Windows 11 x64 checkpoint — completed 2026-09-25

User validation of the original portable development artifact:

1. application starts normally: PASS;
2. AdminLTE-inspired shell renders acceptably: PASS;
3. normal execution requires no UAC/elevation: PASS;
4. portable `Data\\energy.db`, `Backups\\`, and `Logs\\`: PASS;
5. displayed database path points to portable `...\\Data\\energy.db`: PASS;
6. clean shutdown: PASS.

The first execution produced an expected Windows SmartScreen unknown-publisher warning because the development artifact is unsigned.

## Subsequent narrow refinement

After this checkpoint, the user explicitly changed the language requirement from historical English-only to Spanish-default with English selectable. Minimal functional placeholder sidebar navigation was also requested so the shell navigation can be tested rather than remaining visually static.

These are refinements, not failures of the validated Phase 1 architecture.

Formal Phase 1 closure is deferred until the refined portable build passes CI and receives one final short user revalidation.


## Refined Phase 1 CI validation — 2026-09-25

GitHub Actions workflow:
- Name: Windows Build
- Run ID: `36171030035`
- Source commit: `ee8e8200f132be9aef42875c6ae92eb1f10adc66`
- Conclusion: **SUCCESS**

Automated checks passed:
- .NET 10 restore;
- Release WPF x64 build including localization resources/navigation code;
- existing SQLite smoke test;
- self-contained win-x64 publish;
- portable-mode marker;
- artifact upload.

Fresh portable artifact:
- Name: `SolarEnergyMonitor-win-x64-dev`
- Artifact ID: `10881030047`
- SHA-256: `4389db735ab205698c014bc97e505191b0601ad2075988f926ef441642281cde`

Remaining acceptance item:
final user check of Spanish default, English switching/persistence, sidebar navigation, normal non-admin launch, portable path, and clean shutdown.
