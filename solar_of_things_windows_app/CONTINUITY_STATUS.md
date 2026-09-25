# Solar of Things Windows App — Continuity / Resume Status

Date: 2026-09-25
Status: PHASE 1 COMPLETE — PHASE 2 IMPLEMENTATION IN PROGRESS

This file is the canonical continuity note.

## Repository / branch

- Repository: `MrSimkin/energy_control_recopilation`
- Canonical branch: `main`
- Pre-refinement HEAD: `fe8e319fbe4470a5eae260a44706dd20325d4bc7`
- Original formal CI validation run: `36085763308`
- Original validated source commit: `778abaab896d6e211fdc658aa56e80bb6947e7bd`

## Completed and still authoritative

- Solar of Things / SiSeLi public/API research through Round 14.
- Product/functional specification v1, subject to the approved 2026-09-25 language change note.
- .NET 10 / C# / WPF x64 architecture.
- separate Core library.
- SQLite + WAL + migration foundation.
- SQL-backed non-secret settings.
- portable/installed path strategy.
- JSONL diagnostics.
- Windows DPAPI secret-store abstraction.
- GitHub Actions Windows build/smoke/publish workflow.

Do not repeat this foundation unless evidence shows a regression.

## Original Windows 11 manual checkpoint — PASS

On 2026-09-25 the user tested the original portable development artifact on the actual Windows 11 x64 target machine.

Confirmed:
- shell launched and rendered correctly;
- normal launch did not require UAC/elevation;
- application closed normally;
- `Data\\energy.db` was created;
- `Backups\\` was created;
- `Logs\\` was created;
- JSONL diagnostics were created;
- displayed database path was the portable `...\\Data\\energy.db` path;
- SmartScreen unknown-publisher warning was expected for the unsigned development build.

The user had manually selected Run as administrator on an earlier launch; its UAC dialog was user-initiated and is not evidence of an application elevation requirement.

## Approved requirement change — 2026-09-25

Earlier files correctly record the former instruction that the UI be English.

The user has now explicitly changed the canonical requirement:

**Spanish is the default UI language. English is selectable as an alternative.**

The preference must persist locally.

Historical requirement files remain historical and should not be rewritten to pretend this was always the requirement.

Current product name `Solar Energy Monitor / SolarEnergyMonitor` is provisional.

**Final product name = TBD.**

## Refined Phase 1 build — CI PASS

Implementation commit:
- `ee8e8200f132be9aef42875c6ae92eb1f10adc66`

GitHub Actions:
- workflow: Windows Build
- run ID: `36171030035`
- conclusion: **SUCCESS**

Fresh portable artifact:
- name: `SolarEnergyMonitor-win-x64-dev`
- artifact ID: `10881030047`
- SHA-256 digest reported by GitHub: `4389db735ab205698c014bc97e505191b0601ad2075988f926ef441642281cde`
- source commit: `ee8e8200f132be9aef42875c6ae92eb1f10adc66`

Validated by CI:
- .NET 10 restore;
- WPF Release build;
- existing SQLite smoke test;
- self-contained win-x64 publish;
- portable-mode marker;
- artifact upload.

## Final refined-build user revalidation — PASS

On 2026-09-25 the user revalidated artifact ID `10881030047` on the target Windows 11 x64 machine and reported **all checks passed**:

- normal launch without UAC;
- Spanish shown by default on a fresh portable database;
- English switching works;
- English preference persists after restart;
- switching back to Spanish persists after restart;
- every sidebar entry visibly navigates to its distinct placeholder;
- portable database remains at `...\\Data\\energy.db`;
- clean shutdown.

**Phase 1 is formally COMPLETE.**

Canonical closure receipt:
`PHASE_01_ACCEPTANCE_RECEIPT.md`.

## Current development frontier

Phase 1 is complete.

Phase 2 implementation is now in progress.

Current implementation tranche includes:
- production REST client foundation;
- IoT Open signing implementation;
- account/password protocol login;
- protected local session/token storage;
- access+refresh token rotation;
- advanced existing-token-pair bootstrap;
- station/device discovery;
- station/device detail reads;
- gather-attribute discovery;
- dataSource probing;
- read-only energy-flow/history/aggregate/alarm capability probes;
- SQLite schema v3 commissioning capability profile;
- first-class sanitized development diagnostics and copy/save report.

The development diagnostic stream records endpoint/method, sanitized request/response, HTTP status, API code/message, timing, retries, commissioning step, selected station/device metadata and capability outcomes. Passwords/password hashes, tokens, cookies, reusable client secrets and request signatures are redacted before diagnostic JSONL is written.

Normal account/password login now uses the public production IoT Open client profile used by the Solar of Things web-client ecosystem. A DPAPI-protected local override remains available for future upstream changes. Existing access/refresh tokens remain an optional advanced bootstrap, not a normal requirement.

Latest Phase 2 implementation state passed Windows CI in run `36180523465` from source commit `7fc2d5701f34948e8181c3e555c2d3cdaadcde9b`.

Portable artifact:
- name: `SolarEnergyMonitor-win-x64-dev`
- artifact ID: `10883867535`
- SHA-256: `f315669a111af582e390c50289ba38b2a04e96d1a3e38bbb0d811fa686dfc8c1`

CI validated:
- compile/publish;
- SQLite schema v3;
- production client-profile decryption/default/override behavior;
- IoT Open signing vector;
- commissioning-profile persistence including device type/sort, rated power, online state and `lastDataAt`;
- DPAPI;
- diagnostic body/header redaction;
- proactive refresh/session logic;
- evidence-backed server logout lifecycle and preservation of Solar user IDs as strings.

First real-account checkpoint on 2026-09-25:
- existing-token local session bootstrap: PASS;
- station discovery against production: PASS;
- device discovery against production: PASS;
- one station and one device found;
- exact large platform IDs remained lossless strings;
- credential/token redaction: PASS.

The first exported report revealed that raw platform responses also contain unnecessary personal account/location metadata. Diagnostic privacy hardening was therefore added before requesting the next report.

Phase 2 is not complete until the remaining read-only capability probes and safe reconnect/session behavior are validated against the user's real account/device.

Phase 2 scope:
- Solar of Things authentication;
- token/session handling;
- station discovery;
- device discovery;
- telemetry download;
- historical backfill;
- target-device commissioning.

## Resume rule

Resume inside **Phase 2 — Solar of Things Authentication and Commissioning**.

Immediate checkpoint: use artifact `10883867535` with normal Solar of Things account/password login, run the full read-only commissioning sequence, verify session restore after restart, then test server logout and copy the diagnostic report so any remaining account/device-specific mismatches can be corrected.

Do not restart completed API research, Phase 0 specification, or Phase 1 architecture/localization/navigation work unless a concrete regression or implementation-time evidence requires a narrow correction.
