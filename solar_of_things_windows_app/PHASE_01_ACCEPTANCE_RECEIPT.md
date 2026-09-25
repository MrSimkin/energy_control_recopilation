# Phase 1 — Final Acceptance Receipt

Date: 2026-09-25

Status: **COMPLETE / ACCEPTED**

Repository:
`MrSimkin/energy_control_recopilation`

Canonical branch:
`main`

## Scope accepted

Phase 1 — Technical Skeleton / Proof of Architecture.

Accepted foundation includes:

- .NET 10 / C# / WPF Windows 11 x64 application;
- separate Core library;
- dependency injection / host foundation;
- SQLite database with WAL and schema migration foundation;
- SQL-backed application settings;
- Windows DPAPI secret-storage abstraction;
- deterministic installed/portable data paths;
- JSONL diagnostics;
- AdminLTE-inspired desktop shell;
- Spanish-default / English-selectable localization;
- persisted language preference;
- functional distinct placeholder sidebar navigation;
- Windows CI build/smoke/publish pipeline;
- self-contained portable win-x64 artifact.

## Automated validation

Original architecture CI:
- run `36085763308`
- source commit `778abaab896d6e211fdc658aa56e80bb6947e7bd`
- result: **SUCCESS**

Refined localization/navigation CI:
- run `36171030035`
- source commit `ee8e8200f132be9aef42875c6ae92eb1f10adc66`
- result: **SUCCESS**

Refined portable artifact:
- `SolarEnergyMonitor-win-x64-dev`
- artifact ID `10881030047`
- SHA-256 reported by GitHub:
  `4389db735ab205698c014bc97e505191b0601ad2075988f926ef441642281cde`

## Target-machine acceptance

The user tested the refined artifact on the actual Windows 11 x64 target computer and reported all requested checks passed:

- normal launch without UAC: PASS;
- Spanish default on a fresh portable data set: PASS;
- English switching: PASS;
- English persisted after restart: PASS;
- Spanish persisted after switching back: PASS;
- all sidebar navigation entries visibly changed to their intended distinct placeholders: PASS;
- portable database remained under `Data\\energy.db`: PASS;
- clean shutdown: PASS.

Earlier original-build checks also established:

- acceptable shell rendering;
- `Data\\energy.db`, `Backups\\`, and `Logs\\` creation;
- JSONL diagnostics creation;
- expected SmartScreen unknown-publisher warning for unsigned development builds.

## Requirement-change note

The historical English-only UI requirement remains valid as historical evidence of the original user instruction.

On 2026-09-25 the user explicitly changed the canonical requirement to:

**Spanish default; English selectable.**

This change has been implemented and accepted.

The current name `Solar Energy Monitor / SolarEnergyMonitor` remains a development name.

**Final product name = TBD.**

## Scope boundary

Phase 1 does not include production Solar of Things authentication or telemetry.

No fabricated telemetry was accepted into the shell.

## Closure decision

**PHASE 1 IS FORMALLY COMPLETE.**

Next authorized development phase:

**Phase 2 — Solar of Things Authentication and Commissioning**

Phase 2 should begin from the validated Phase 1 foundation and the existing completed API research. Do not restart Phase 0 or Phase 1 absent concrete regression evidence.
